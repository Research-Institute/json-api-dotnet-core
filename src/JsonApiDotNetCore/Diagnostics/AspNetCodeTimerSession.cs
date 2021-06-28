using System;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;

namespace JsonApiDotNetCore.Diagnostics
{
    /// <summary>
    /// Code timing session management intended for use in ASP.NET Web Applications. Uses <see cref="HttpContext.Items" /> to isolate concurrent requests.
    /// Can be used with async/wait, but it cannot distinguish between concurrently running threads within a single HTTP request, so you'll need to pass an
    /// <see cref="CascadingCodeTimer" /> instance through the entire call chain in that case.
    /// </summary>
    [PublicAPI]
    public sealed class AspNetCodeTimerSession : ICodeTimerSession
    {
        private const string HttpContextItemKey = "CascadingCodeTimer:Session";

        private readonly HttpContext _httpContext;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ICodeTimer CodeTimer
        {
            get
            {
                HttpContext httpContext = GetHttpContext();
                var codeTimer = (ICodeTimer)httpContext.Items[HttpContextItemKey];

                if (codeTimer == null)
                {
                    codeTimer = new CascadingCodeTimer();
                    httpContext.Items[HttpContextItemKey] = codeTimer;
                }

                return codeTimer;
            }
        }

        public event EventHandler Disposed;

        public AspNetCodeTimerSession(IHttpContextAccessor httpContextAccessor)
        {
            ArgumentGuard.NotNull(httpContextAccessor, nameof(httpContextAccessor));

            _httpContextAccessor = httpContextAccessor;
        }

        public AspNetCodeTimerSession(HttpContext httpContext)
        {
            ArgumentGuard.NotNull(httpContext, nameof(httpContext));

            _httpContext = httpContext;
        }

        public void Dispose()
        {
            HttpContext httpContext = TryGetHttpContext();
            var codeTimer = (ICodeTimer)httpContext?.Items[HttpContextItemKey];

            if (codeTimer != null)
            {
                codeTimer.Dispose();
                httpContext.Items[HttpContextItemKey] = null;
            }

            OnDisposed();
        }

        private void OnDisposed()
        {
            Disposed?.Invoke(this, EventArgs.Empty);
        }

        private HttpContext GetHttpContext()
        {
            HttpContext httpContext = TryGetHttpContext();
            return httpContext ?? throw new InvalidOperationException("An active HTTP request is required.");
        }

        private HttpContext TryGetHttpContext()
        {
            return _httpContext ?? _httpContextAccessor?.HttpContext;
        }
    }
}
