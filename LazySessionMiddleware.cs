using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CustomSession
{
    internal class LazySessionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IDistributedCache _cache;
        private readonly ILogger<LazySessionMiddleware> _logger;
        private readonly IOptions<SessionOptions> _options;

        public LazySessionMiddleware(RequestDelegate next, IDistributedCache cache, ILogger<LazySessionMiddleware> logger, IOptions<SessionOptions> options)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var cookieName = _options.Value.Cookie.Name ?? ".AspNetCore.Session";
            context.Request.Cookies.TryGetValue(cookieName, out var sessionIdFromCookie);

            var session = new LazySession(_cache, _logger, _options, sessionIdFromCookie);

            // simple ISessionFeature implementation
            var feature = new SessionFeatureImpl { Session = session };
            context.Features.Set<ISessionFeature>(feature);

            await _next(context).ConfigureAwait(false);

            try
            {
                await session.CommitAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error committing session");
            }

            // Manage cookie: set when data exists; delete when there was a cookie but now empty
            if (session.HasData)
            {
                var cookieOptions = new CookieOptions
                {
                    HttpOnly = true,
                    Secure = context.Request.IsHttps,
                    Path = _options.Value.Cookie.Path ?? "/",
                    SameSite = _options.Value.Cookie.SameSite
                };

                context.Response.Cookies.Append(cookieName, session.Id, cookieOptions);
            }
            else if (session.HadCookieOnRequest && !session.HasData)
            {
                // remove cookie
                context.Response.Cookies.Delete(cookieName);
            }
        }

        private class SessionFeatureImpl : ISessionFeature
        {
            public ISession Session { get; set; } = default!;
        }
    }
}
