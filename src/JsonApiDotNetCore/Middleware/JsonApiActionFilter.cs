using System;
using System.Linq;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Internal;
using JsonApiDotNetCore.Internal.Contracts;
using JsonApiDotNetCore.Managers.Contracts;
using JsonApiDotNetCore.Query;
using JsonApiDotNetCore.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace JsonApiDotNetCore.Middleware
{
    public class JsonApiActionFilter : IActionFilter
    {
        private readonly IResourceGraph _resourceGraph;
        private readonly ICurrentRequest _currentRequest;
        private readonly IPageQueryService _pageManager;
        private readonly IQueryParser _queryParser;
        private readonly IJsonApiOptions _options;
        private HttpContext _httpContext;
        public JsonApiActionFilter(IResourceGraph resourceGraph,
                                 ICurrentRequest currentRequest,
                                 IPageQueryService pageManager,
                                 IQueryParser queryParser,
                                 IJsonApiOptions options)
        {
            _resourceGraph = resourceGraph;
            _currentRequest = currentRequest;
            _pageManager = pageManager;
            _queryParser = queryParser;
            _options = options;
        }

        /// <summary>
        /// </summary>
        public void OnActionExecuting(ActionExecutingContext context)
        {
            _httpContext = context.HttpContext;
            ContextEntity contextEntityCurrent = GetCurrentEntity();

            // the contextEntity is null eg when we're using a non-JsonApiDotNetCore route. 
            if (contextEntityCurrent != null)
            {
                _currentRequest.SetRequestResource(contextEntityCurrent);
                _currentRequest.BasePath = GetBasePath(contextEntityCurrent.EntityName);
                HandleUriParameters();
            }

        }

        /// <summary>
        /// Parses the uri
        /// </summary>
        protected void HandleUriParameters()
        {
            if (_httpContext.Request.Query.Count > 0)
            {
                var querySet = _queryParser.Parse(_httpContext.Request.Query);
                _currentRequest.QuerySet = querySet; //this shouldn't be exposed?
                _pageManager.PageSize = querySet.PageQuery.PageSize ?? _pageManager.PageSize;
                _pageManager.CurrentPage = querySet.PageQuery.PageOffset ?? _pageManager.CurrentPage;

            }
        }

        private string GetBasePath(string entityName)
        {
            var r = _httpContext.Request;
            if (_options.RelativeLinks)
            {
                return GetNamespaceFromPath(r.Path, entityName);
            }
            else
            {
                return $"{r.Scheme}://{r.Host}{GetNamespaceFromPath(r.Path, entityName)}";
            }
        }
        internal static string GetNamespaceFromPath(string path, string entityName)
        {
            var entityNameSpan = entityName.AsSpan();
            var pathSpan = path.AsSpan();
            const char delimiter = '/';
            for (var i = 0; i < pathSpan.Length; i++)
            {
                if (pathSpan[i].Equals(delimiter))
                {
                    var nextPosition = i + 1;
                    if (pathSpan.Length > i + entityNameSpan.Length)
                    {
                        var possiblePathSegment = pathSpan.Slice(nextPosition, entityNameSpan.Length);
                        if (entityNameSpan.SequenceEqual(possiblePathSegment))
                        {
                            // check to see if it's the last position in the string
                            //   or if the next character is a /
                            var lastCharacterPosition = nextPosition + entityNameSpan.Length;

                            if (lastCharacterPosition == pathSpan.Length || pathSpan.Length >= lastCharacterPosition + 2 && pathSpan[lastCharacterPosition].Equals(delimiter))
                            {
                                return pathSpan.Slice(0, i).ToString();
                            }
                        }
                    }
                }
            }

            return string.Empty;
        }
        /// <summary>
        /// Gets the current entity that we need for serialization and deserialization.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="resourceGraph"></param>
        /// <returns></returns>
        private ContextEntity GetCurrentEntity()
        {
            var controllerName = (string)_httpContext.GetRouteData().Values["controller"];
            var rd = _httpContext.GetRouteData().Values;
            var requestResource = _resourceGraph.GetEntityFromControllerName(controllerName);

            if (rd.TryGetValue("relationshipName", out object relationshipName))
                _currentRequest.RequestRelationship = requestResource.Relationships.Single(r => r.PublicRelationshipName == (string)relationshipName);
            return requestResource;
        }


        private bool IsJsonApiRequest(HttpRequest request)
        {
            return (request.ContentType?.Equals(Constants.ContentType, StringComparison.OrdinalIgnoreCase) == true);
        }

        public void OnActionExecuted(ActionExecutedContext context) { /* noop */ }
    }
}
