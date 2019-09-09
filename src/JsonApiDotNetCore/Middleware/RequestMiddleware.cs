using System;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Builders;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Internal.Contracts;
using JsonApiDotNetCore.Internal.Query;
using JsonApiDotNetCore.Managers.Contracts;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace JsonApiDotNetCore.Middleware
{
    /// <summary>
    /// Can be overwritten to help you out during testing
    /// 
    /// This sets all necessary parameters relating to the HttpContext for JADNC
    /// </summary>
    public class RequestMiddleware
    {
        private readonly RequestDelegate _next;
        private IResourceGraph _resourceGraph;
        private HttpContext _httpContext;
        private IRequestManager _requestManager;
        private IPageManager _pageManager;
        private IQueryParser _queryParser;
        private IJsonApiOptions _options;

        public RequestMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext,
                                 IJsonApiContext jsonApiContext,
                                 IResourceGraph resourceGraph,
                                 IRequestManager requestManager,
                                 IPageManager pageManager,
                                 IQueryParser queryParser,
                                 IJsonApiOptions options
            )
        {
            _httpContext = httpContext;
            _resourceGraph = resourceGraph;
            _requestManager = requestManager;
            _pageManager = pageManager;
            _queryParser = queryParser;
            _options = options;

            if (IsValid())
            {

                // HACK: this currently results in allocation of
                // objects that may or may not be used and even double allocation
                // since the JsonApiContext is using field initializers
                // Need to work on finding a better solution.
                jsonApiContext.BeginOperation();
                _requestManager.IsRelationshipPath = PathIsRelationship();

                await _next(httpContext);
            }
        }

        protected bool PathIsRelationship()
        {
            var actionName = (string)_httpContext.GetRouteData().Values["action"];
            return actionName.ToLower().Contains("relationships");
        }
            private bool IsValid()
        {
            return IsValidContentTypeHeader(_httpContext) && IsValidAcceptHeader(_httpContext);
        }

        private bool IsValidContentTypeHeader(HttpContext context)
        {
            var contentType = context.Request.ContentType;
            if (contentType != null && ContainsMediaTypeParameters(contentType))
            {
                FlushResponse(context, 415);
                return false;
            }
            return true;
        }

        private bool IsValidAcceptHeader(HttpContext context)
        {
            if (context.Request.Headers.TryGetValue(Constants.AcceptHeader, out StringValues acceptHeaders) == false)
                return true;

            foreach (var acceptHeader in acceptHeaders)
            {
                if (ContainsMediaTypeParameters(acceptHeader) == false)
                    continue;

                FlushResponse(context, 406);
                return false;
            }
            return true;
        }

        internal static bool ContainsMediaTypeParameters(string mediaType)
        {
            var incomingMediaTypeSpan = mediaType.AsSpan();

            // if the content type is not application/vnd.api+json then continue on
            if (incomingMediaTypeSpan.Length < Constants.ContentType.Length)
                return false;

            var incomingContentType = incomingMediaTypeSpan.Slice(0, Constants.ContentType.Length);
            if (incomingContentType.SequenceEqual(Constants.ContentType.AsSpan()) == false)
                return false;

            // anything appended to "application/vnd.api+json;" will be considered a media type param
            return (
                incomingMediaTypeSpan.Length >= Constants.ContentType.Length + 2
                && incomingMediaTypeSpan[Constants.ContentType.Length] == ';'
            );
        }

        private void FlushResponse(HttpContext context, int statusCode)
        {
            context.Response.StatusCode = statusCode;
            context.Response.Body.Flush();
        }
    }
}
