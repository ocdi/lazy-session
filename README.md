# CustomSession (Lazy distributed session)

This repository contains a minimal ASP.NET Core example implementing a "lazy" distributed session middleware that avoids contacting the remote distributed cache unless session data actually exists.

## Goals

- Only create/store a session id and remote data when values are written.
- Do not query the distributed cache for reads if there was no session cookie on the incoming request.
- Remove session cookie and remote entry when session data is cleared.
- Reuse existing ASP.NET Core primitives where possible (`ISession`, `ISessionFeature`, `SessionOptions.Cookie.Build`).
- Reduce allocations where straightforward by using `ValueTask` for internal helpers.

## Key files

- `src/CustomSession/LazySession.cs` — `ISession` implementation that defers loading from `IDistributedCache` when no cookie exists and only writes to cache when modified.
- `src/CustomSession/LazySessionMiddleware.cs` — middleware that installs an `ISessionFeature` backed by `LazySession`, commits changes at the end of the request, and manages the session cookie using `SessionOptions.Cookie.Build(HttpContext)`.
- `src/CustomSession/SessionExtensions.cs` — `UseLazyDistributedSession()` extension to register the middleware.
- `src/CustomSession/ValueTaskExtensions.cs` — small helper utilities for working with `ValueTask` results.
- `src/CustomSession/Program.cs` — minimal example app showing how to register the distributed cache and configure `SessionOptions`.
- `src/CustomSession.Tests` — xUnit tests that mock `IDistributedCache` and verify the middleware behavior (no cache calls when no cookie and no access; store when setting values; remove and delete cookie when clearing a session that had a cookie).

## Design notes

- Lazy load behavior: when the request has no session cookie, the session starts empty and never queries the distributed cache. If the application writes to the session, a session id is generated and `IDistributedCache.SetAsync` is called on commit.
- Cookie management: when the session ends with data, we append a cookie produced by `SessionOptions.Cookie.Build(HttpContext)`. If the request started with a cookie but the session ends empty (cleared or no values), the middleware removes the remote entry and deletes the cookie using the same built options (so path/SameSite/etc. match).
- ValueTask usage: internal helpers use `ValueTask` to reduce allocations when operations complete synchronously. Public `ISession` methods still expose `Task` per interface and convert via `AsTask()`.

## Usage

1. In `Program.cs` configure a distributed cache and session options:

```csharp
builder.Services.AddDistributedMemoryCache(); // or Redis, SQL, etc.
builder.Services.Configure<SessionOptions>(opts => {
    opts.Cookie.Name = ".AspNetCore.Session";
    opts.IdleTimeout = TimeSpan.FromMinutes(20);
});

app.UseLazyDistributedSession();
```

2. Use `HttpContext.Session` as usual inside request handlers. The middleware will ensure the session is only materialized in the remote store if you write values.

## Tests

Run tests from the repository root:

```bash
dotnet test src/CustomSession/CustomSession.sln
```

The tests mock `IDistributedCache` to verify the following scenarios:

- No cookie and no session access -> no cache calls.
- Setting a session value -> `SetAsync` called and `Set-Cookie` header produced.
- Request with incoming cookie, then clearing session -> `RemoveAsync` called and cookie deleted.

## Switching to a real distributed cache

- Replace `AddDistributedMemoryCache()` with `AddStackExchangeRedisCache(...)` or a SQL implementation as appropriate.
- The middleware uses `IDistributedCache` so any registered implementation will be used.

## Next steps and improvements

- Add more tests verifying expiry and cookie attributes.
- Support `ISession` concurrency semantics more closely to the reference implementation (locking/refresh semantics) if needed.
- Consider exposing configuration options for cookie naming/creation behavior via options on the middleware.
