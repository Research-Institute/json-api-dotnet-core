using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Queries;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Queries.Internal.Parsing;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Serialization.Objects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Routing;

namespace JsonApiDotNetCore.Serialization.Building
{
    [PublicAPI]
    public class LinkBuilder : ILinkBuilder
    {
        private const string PageSizeParameterName = "page[size]";
        private const string PageNumberParameterName = "page[number]";

        private static readonly string GetPrimaryControllerActionName = NoAsyncSuffix(nameof(BaseJsonApiController<Identifiable>.GetAsync));
        private static readonly string GetSecondaryControllerActionName = NoAsyncSuffix(nameof(BaseJsonApiController<Identifiable>.GetSecondaryAsync));
        private static readonly string GetRelationshipControllerActionName = NoAsyncSuffix(nameof(BaseJsonApiController<Identifiable>.GetRelationshipAsync));

        private readonly IJsonApiOptions _options;
        private readonly IJsonApiRequest _request;
        private readonly IPaginationContext _paginationContext;
        private readonly IResourceContextProvider _resourceContextProvider;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly LinkGenerator _linkGenerator;
        private readonly IControllerResourceMapping _controllerResourceMapping;

        public LinkBuilder(IJsonApiOptions options, IJsonApiRequest request, IPaginationContext paginationContext,
            IResourceContextProvider resourceContextProvider, IHttpContextAccessor httpContextAccessor, LinkGenerator linkGenerator,
            IControllerResourceMapping controllerResourceMapping)
        {
            ArgumentGuard.NotNull(options, nameof(options));
            ArgumentGuard.NotNull(request, nameof(request));
            ArgumentGuard.NotNull(paginationContext, nameof(paginationContext));
            ArgumentGuard.NotNull(resourceContextProvider, nameof(resourceContextProvider));
            ArgumentGuard.NotNull(linkGenerator, nameof(linkGenerator));
            ArgumentGuard.NotNull(controllerResourceMapping, nameof(controllerResourceMapping));

            _options = options;
            _request = request;
            _paginationContext = paginationContext;
            _resourceContextProvider = resourceContextProvider;
            _httpContextAccessor = httpContextAccessor;
            _linkGenerator = linkGenerator;
            _controllerResourceMapping = controllerResourceMapping;
        }

        private static string NoAsyncSuffix(string actionName)
        {
            return actionName.EndsWith("Async", StringComparison.Ordinal) ? actionName[..^"Async".Length] : actionName;
        }

        /// <inheritdoc />
        public TopLevelLinks GetTopLevelLinks()
        {
            var links = new TopLevelLinks();

            ResourceContext requestContext = _request.SecondaryResource ?? _request.PrimaryResource;

            if (ShouldIncludeTopLevelLink(LinkTypes.Self, requestContext))
            {
                links.Self = GetLinkForTopLevelSelf();
            }

            if (_request.Kind == EndpointKind.Relationship && ShouldIncludeTopLevelLink(LinkTypes.Related, requestContext))
            {
                links.Related = GetLinkForRelationshipRelated(_request.PrimaryId, _request.Relationship);
            }

            if (_request.IsCollection && _paginationContext.PageSize != null && ShouldIncludeTopLevelLink(LinkTypes.Paging, requestContext))
            {
                SetPaginationInTopLevelLinks(requestContext, links);
            }

            return links.HasValue() ? links : null;
        }

        /// <summary>
        /// Checks if the top-level <paramref name="linkType" /> should be added by first checking configuration on the <see cref="ResourceContext" />, and if
        /// not configured, by checking with the global configuration in <see cref="IJsonApiOptions" />.
        /// </summary>
        private bool ShouldIncludeTopLevelLink(LinkTypes linkType, ResourceContext resourceContext)
        {
            if (resourceContext.TopLevelLinks != LinkTypes.NotConfigured)
            {
                return resourceContext.TopLevelLinks.HasFlag(linkType);
            }

            return _options.TopLevelLinks.HasFlag(linkType);
        }

        private string GetLinkForTopLevelSelf()
        {
            return _options.UseRelativeLinks
                ? _httpContextAccessor.HttpContext!.Request.GetEncodedPathAndQuery()
                : _httpContextAccessor.HttpContext!.Request.GetEncodedUrl();
        }

        private void SetPaginationInTopLevelLinks(ResourceContext requestContext, TopLevelLinks links)
        {
            string pageSizeValue = CalculatePageSizeValue(_paginationContext.PageSize, requestContext);

            links.First = GetLinkForPagination(1, pageSizeValue);

            if (_paginationContext.TotalPageCount > 0)
            {
                links.Last = GetLinkForPagination(_paginationContext.TotalPageCount.Value, pageSizeValue);
            }

            if (_paginationContext.PageNumber.OneBasedValue > 1)
            {
                links.Prev = GetLinkForPagination(_paginationContext.PageNumber.OneBasedValue - 1, pageSizeValue);
            }

            bool hasNextPage = _paginationContext.PageNumber.OneBasedValue < _paginationContext.TotalPageCount;
            bool possiblyHasNextPage = _paginationContext.TotalPageCount == null && _paginationContext.IsPageFull;

            if (hasNextPage || possiblyHasNextPage)
            {
                links.Next = GetLinkForPagination(_paginationContext.PageNumber.OneBasedValue + 1, pageSizeValue);
            }
        }

        private string CalculatePageSizeValue(PageSize topPageSize, ResourceContext requestContext)
        {
            string pageSizeParameterValue = _httpContextAccessor.HttpContext!.Request.Query[PageSizeParameterName];

            PageSize newTopPageSize = Equals(topPageSize, _options.DefaultPageSize) ? null : topPageSize;
            return ChangeTopPageSize(pageSizeParameterValue, newTopPageSize, requestContext);
        }

        private string ChangeTopPageSize(string pageSizeParameterValue, PageSize topPageSize, ResourceContext requestContext)
        {
            IList<PaginationElementQueryStringValueExpression> elements = ParsePageSizeExpression(pageSizeParameterValue, requestContext);
            int elementInTopScopeIndex = elements.FindIndex(expression => expression.Scope == null);

            if (topPageSize != null)
            {
                var topPageSizeElement = new PaginationElementQueryStringValueExpression(null, topPageSize.Value);

                if (elementInTopScopeIndex != -1)
                {
                    elements[elementInTopScopeIndex] = topPageSizeElement;
                }
                else
                {
                    elements.Insert(0, topPageSizeElement);
                }
            }
            else
            {
                if (elementInTopScopeIndex != -1)
                {
                    elements.RemoveAt(elementInTopScopeIndex);
                }
            }

            string parameterValue = string.Join(',',
                elements.Select(expression => expression.Scope == null ? expression.Value.ToString() : $"{expression.Scope}:{expression.Value}"));

            return parameterValue == string.Empty ? null : parameterValue;
        }

        private IList<PaginationElementQueryStringValueExpression> ParsePageSizeExpression(string pageSizeParameterValue, ResourceContext requestResource)
        {
            if (pageSizeParameterValue == null)
            {
                return new List<PaginationElementQueryStringValueExpression>();
            }

            var parser = new PaginationParser(_resourceContextProvider);
            PaginationQueryStringValueExpression paginationExpression = parser.Parse(pageSizeParameterValue, requestResource);

            return paginationExpression.Elements.ToList();
        }

        private string GetLinkForPagination(int pageOffset, string pageSizeValue)
        {
            string queryStringValue = GetQueryStringInPaginationLink(pageOffset, pageSizeValue);

            var builder = new UriBuilder(_httpContextAccessor.HttpContext!.Request.GetEncodedUrl())
            {
                Query = queryStringValue
            };

            UriComponents components = _options.UseRelativeLinks ? UriComponents.PathAndQuery : UriComponents.AbsoluteUri;
            return builder.Uri.GetComponents(components, UriFormat.SafeUnescaped);
        }

        private string GetQueryStringInPaginationLink(int pageOffset, string pageSizeValue)
        {
            IDictionary<string, string> parameters =
                _httpContextAccessor.HttpContext!.Request.Query.ToDictionary(pair => pair.Key, pair => pair.Value.ToString());

            if (pageSizeValue == null)
            {
                parameters.Remove(PageSizeParameterName);
            }
            else
            {
                parameters[PageSizeParameterName] = pageSizeValue;
            }

            if (pageOffset == 1)
            {
                parameters.Remove(PageNumberParameterName);
            }
            else
            {
                parameters[PageNumberParameterName] = pageOffset.ToString();
            }

            string queryStringValue = QueryString.Create(parameters).Value;
            return DecodeSpecialCharacters(queryStringValue);
        }

        private static string DecodeSpecialCharacters(string uri)
        {
            return uri.Replace("%5B", "[").Replace("%5D", "]").Replace("%27", "'").Replace("%3A", ":");
        }

        /// <inheritdoc />
        public ResourceLinks GetResourceLinks(string resourceName, string id)
        {
            ArgumentGuard.NotNullNorEmpty(resourceName, nameof(resourceName));
            ArgumentGuard.NotNullNorEmpty(id, nameof(id));

            var links = new ResourceLinks();
            ResourceContext resourceContext = _resourceContextProvider.GetResourceContext(resourceName);

            if (_request.Kind != EndpointKind.Relationship && ShouldIncludeResourceLink(LinkTypes.Self, resourceContext))
            {
                links.Self = GetLinkForResourceSelf(resourceContext, id);
            }

            return links.HasValue() ? links : null;
        }

        /// <summary>
        /// Checks if the resource object level <paramref name="linkType" /> should be added by first checking configuration on the
        /// <see cref="ResourceContext" />, and if not configured, by checking with the global configuration in <see cref="IJsonApiOptions" />.
        /// </summary>
        private bool ShouldIncludeResourceLink(LinkTypes linkType, ResourceContext resourceContext)
        {
            if (resourceContext.ResourceLinks != LinkTypes.NotConfigured)
            {
                return resourceContext.ResourceLinks.HasFlag(linkType);
            }

            return _options.ResourceLinks.HasFlag(linkType);
        }

        private string GetLinkForResourceSelf(ResourceContext resourceContext, string resourceId)
        {
            string controllerName = _controllerResourceMapping.GetControllerNameForResourceType(resourceContext.ResourceType);
            IDictionary<string, object> routeValues = GetRouteValues(resourceId, null);

            return RenderLinkForAction(controllerName, GetPrimaryControllerActionName, routeValues);
        }

        /// <inheritdoc />
        public RelationshipLinks GetRelationshipLinks(RelationshipAttribute relationship, IIdentifiable parent)
        {
            ArgumentGuard.NotNull(relationship, nameof(relationship));
            ArgumentGuard.NotNull(parent, nameof(parent));

            var links = new RelationshipLinks();
            ResourceContext leftResourceContext = _resourceContextProvider.GetResourceContext(parent.GetType());

            if (ShouldIncludeRelationshipLink(LinkTypes.Self, relationship, leftResourceContext))
            {
                links.Self = GetLinkForRelationshipSelf(parent.StringId, relationship);
            }

            if (ShouldIncludeRelationshipLink(LinkTypes.Related, relationship, leftResourceContext))
            {
                links.Related = GetLinkForRelationshipRelated(parent.StringId, relationship);
            }

            return links.HasValue() ? links : null;
        }

        private string GetLinkForRelationshipSelf(string primaryId, RelationshipAttribute relationship)
        {
            string controllerName = _controllerResourceMapping.GetControllerNameForResourceType(relationship.LeftType);
            IDictionary<string, object> routeValues = GetRouteValues(primaryId, relationship.PublicName);

            return RenderLinkForAction(controllerName, GetRelationshipControllerActionName, routeValues);
        }

        private string GetLinkForRelationshipRelated(string primaryId, RelationshipAttribute relationship)
        {
            string controllerName = _controllerResourceMapping.GetControllerNameForResourceType(relationship.LeftType);
            IDictionary<string, object> routeValues = GetRouteValues(primaryId, relationship.PublicName);

            return RenderLinkForAction(controllerName, GetSecondaryControllerActionName, routeValues);
        }

        private IDictionary<string, object> GetRouteValues(string primaryId, string relationshipName)
        {
            // By default, we copy all route parameters from the *current* endpoint, which helps in case all endpoints have the same
            // set of non-standard parameters. There is no way we can know which non-standard parameters a *different* endpoint needs,
            // so users must override RenderLinkForAction to supply them, if applicable.
            RouteValueDictionary routeValues = _httpContextAccessor.HttpContext!.Request.RouteValues;

            routeValues["id"] = primaryId;
            routeValues["relationshipName"] = relationshipName;

            return routeValues;
        }

        protected virtual string RenderLinkForAction(string controllerName, string actionName, IDictionary<string, object> routeValues)
        {
            return _options.UseRelativeLinks
                ? _linkGenerator.GetPathByAction(_httpContextAccessor.HttpContext, actionName, controllerName, routeValues)
                : _linkGenerator.GetUriByAction(_httpContextAccessor.HttpContext, actionName, controllerName, routeValues);
        }

        /// <summary>
        /// Checks if the relationship object level <paramref name="linkType" /> should be added by first checking configuration on the
        /// <paramref name="relationship" /> attribute, if not configured by checking <see cref="ResourceLinksAttribute.RelationshipLinks" /> on the resource
        /// type that contains this relationship, and if not configured by checking with the global configuration in <see cref="IJsonApiOptions" />.
        /// </summary>
        private bool ShouldIncludeRelationshipLink(LinkTypes linkType, RelationshipAttribute relationship, ResourceContext leftResourceContext)
        {
            if (relationship.Links != LinkTypes.NotConfigured)
            {
                return relationship.Links.HasFlag(linkType);
            }

            if (leftResourceContext.RelationshipLinks != LinkTypes.NotConfigured)
            {
                return leftResourceContext.RelationshipLinks.HasFlag(linkType);
            }

            return _options.RelationshipLinks.HasFlag(linkType);
        }
    }
}
