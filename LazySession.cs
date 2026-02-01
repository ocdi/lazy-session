using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CustomSession
{
    internal class LazySession : ISession
    {
        private readonly IDistributedCache _cache;
        private readonly ILogger _logger;
        private readonly SessionOptions _options;
        private readonly string _sessionKeyPrefix = "Session:";

        private Dictionary<string, byte[]>? _store;
        private bool _loaded;
        private bool _modified;
        private readonly bool _hadCookieOnRequest;
        private string? _sessionId;

        public LazySession(IDistributedCache cache, ILogger logger, IOptions<SessionOptions> options, string? sessionIdFromCookie)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
            _hadCookieOnRequest = !string.IsNullOrEmpty(sessionIdFromCookie);
            _sessionId = sessionIdFromCookie;
            _loaded = false;
            _modified = false;
        }

        public bool IsAvailable => true;

        public string Id
        {
            get
            {
                if (string.IsNullOrEmpty(_sessionId))
                {
                    _sessionId = Guid.NewGuid().ToString("N");
                }
                return _sessionId!;
            }
        }

        public IEnumerable<string> Keys
        {
            get
            {
                EnsureLoadedIfNeededAsync().GetAwaiter().GetResult();
                return _store != null ? (IEnumerable<string>)_store.Keys : Array.Empty<string>();
            }
        }

        public void Clear()
        {
            EnsureStore();
            _store!.Clear();
            _modified = true;
        }

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            return CommitAsyncCore(cancellationToken).AsTask();
        }

        public Task LoadAsync(CancellationToken cancellationToken = default)
        {
            return EnsureLoadedIfNeededAsync(cancellationToken).AsTask();
        }

        public void Remove(string key)
        {
            EnsureStore();
            if (_store!.Remove(key))
            {
                _modified = true;
            }
        }

        public void Set(string key, byte[] value)
        {
            EnsureStore();
            _store![key] = value;
            _modified = true;
        }

        public bool TryGetValue(string key, out byte[] value)
        {
            if (!_loaded)
            {
                if (_hadCookieOnRequest)
                {
                    EnsureLoadedIfNeededAsync().GetAwaiter().GetResult();
                }
                else
                {
                    value = Array.Empty<byte>();
                    return false;
                }
            }

            if (_store != null && _store.TryGetValue(key, out var v))
            {
                value = v;
                return true;
            }

            value = Array.Empty<byte>();
            return false;
        }

        private void EnsureStore()
        {
            if (!_loaded)
            {
                // If there was no cookie we intentionally don't call the distributed store; start empty.
                _store = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                _loaded = true;
                return;
            }

            if (_store == null) _store = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        }

        private async ValueTask EnsureLoadedIfNeededAsync(CancellationToken cancellationToken = default)
        {
            if (_loaded) return;

            if (!_hadCookieOnRequest)
            {
                _store = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                _loaded = true;
                return;
            }

            try
            {
                var cacheKey = _sessionKeyPrefix + Id;
                var bytes = await _cache.GetAsync(cacheKey, cancellationToken).ConfigureAwait(false);
                if (bytes != null && bytes.Length > 0)
                {
                    _store = JsonSerializer.Deserialize<Dictionary<string, byte[]>>(bytes) ?? new Dictionary<string, byte[]>();
                }
                else
                {
                    _store = new Dictionary<string, byte[]>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load session from distributed cache");
                _store = new Dictionary<string, byte[]>();
            }

            _loaded = true;
        }

        private async ValueTask CommitAsyncCore(CancellationToken cancellationToken = default)
        {
            if (!_modified)
            {
                return;
            }

            // If there are no keys, ensure the session is removed and cookie cleared if it existed
            var hasData = _store != null && _store.Count > 0;
            var cacheKey = _sessionKeyPrefix + Id;

            if (hasData)
            {
                var bytes = JsonSerializer.SerializeToUtf8Bytes(_store!);
                await _cache.SetAsync(cacheKey, bytes, new DistributedCacheEntryOptions { SlidingExpiration = _options.IdleTimeout }, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                if (_hadCookieOnRequest)
                {
                    try
                    {
                        await _cache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to remove session from distributed cache");
                    }
                }
            }
        }

        public bool WasModified => _modified;

        public bool HasData => _store != null && _store.Count > 0;

        public bool HadCookieOnRequest => _hadCookieOnRequest;
    }
}
