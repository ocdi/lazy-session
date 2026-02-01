using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Session;
using CustomSession;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CustomSession.Tests
{
    public class SessionTests
    {
        [Fact]
        public async Task NoCookie_NoCacheCalls()
        {
            var cacheMock = new Mock<IDistributedCache>(MockBehavior.Strict);
            var loggerMock = new Mock<ILogger<LazySessionMiddleware>>();
            var options = Options.Create(new Microsoft.AspNetCore.Builder.SessionOptions());

            var middleware = new LazySessionMiddleware((innerHttp) => Task.CompletedTask, cacheMock.Object, loggerMock.Object, options);
            var context = new DefaultHttpContext();

            await middleware.InvokeAsync(context);

            cacheMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task SettingValue_CallsSetAsyncAndSetsCookie()
        {
            var cacheMock = new Mock<IDistributedCache>();
            cacheMock.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default)).Returns(Task.CompletedTask).Verifiable();

            var loggerMock = new Mock<ILogger<LazySessionMiddleware>>();
            var options = Options.Create(new Microsoft.AspNetCore.Builder.SessionOptions());

            RequestDelegate next = async ctx =>
            {
                ctx.Session.Set("k", Encoding.UTF8.GetBytes("v"));
                await Task.CompletedTask;
            };

            var middleware = new LazySessionMiddleware(next, cacheMock.Object, loggerMock.Object, options);
            var context = new DefaultHttpContext();

            await middleware.InvokeAsync(context);

            cacheMock.Verify(c => c.SetAsync(It.Is<string>(s => s.StartsWith("Session:")), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), default), Times.Once);
            Assert.True(context.Response.Headers.ContainsKey("Set-Cookie"));
        }

        [Fact]
        public async Task HadCookie_Cleared_RemovesAndDeletesCookie()
        {
            var cacheMock = new Mock<IDistributedCache>();
            cacheMock.Setup(c => c.RemoveAsync(It.IsAny<string>(), default)).Returns(Task.CompletedTask).Verifiable();

            var loggerMock = new Mock<ILogger<LazySessionMiddleware>>();
            var options = Options.Create(new Microsoft.AspNetCore.Builder.SessionOptions());

            RequestDelegate next = async ctx =>
            {
                // Accessing session to ensure it's created from cookie
                var id = ctx.Request.Cookies[options.Value.Cookie.Name ?? ".AspNetCore.Session"];
                ctx.Session.Clear();
                await Task.CompletedTask;
            };

            var middleware = new LazySessionMiddleware(next, cacheMock.Object, loggerMock.Object, options);
            var context = new DefaultHttpContext();
            // simulate incoming session cookie
            context.Request.Headers["Cookie"] = (options.Value.Cookie.Name ?? ".AspNetCore.Session") + "=sid123";

            await middleware.InvokeAsync(context);

            cacheMock.Verify(c => c.RemoveAsync(It.Is<string>(s => s.StartsWith("Session:")), default), Times.Once);
            Assert.True(context.Response.Headers.ContainsKey("Set-Cookie"));
        }
    }
}
