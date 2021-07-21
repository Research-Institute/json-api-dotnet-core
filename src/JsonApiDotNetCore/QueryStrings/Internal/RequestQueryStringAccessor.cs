using Microsoft.AspNetCore.Http;

namespace JsonApiDotNetCore.QueryStrings.Internal
{
    /// <inheritdoc />
    internal sealed class RequestQueryStringAccessor : IRequestQueryStringAccessor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public IQueryCollection Query => _httpContextAccessor.HttpContext!.Request.Query;

        public RequestQueryStringAccessor(IHttpContextAccessor httpContextAccessor)
        {
            ArgumentGuard.NotNull(httpContextAccessor, nameof(httpContextAccessor));

            _httpContextAccessor = httpContextAccessor;
        }
    }
}
