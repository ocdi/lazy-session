using Microsoft.AspNetCore.Builder;

namespace CustomSession
{
    public static class SessionExtensions
    {
        public static IApplicationBuilder UseLazyDistributedSession(this IApplicationBuilder app)
        {
            return app.UseMiddleware<LazySessionMiddleware>();
        }
    }
}
