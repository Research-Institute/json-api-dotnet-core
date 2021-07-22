using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Serialization.Objects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;

namespace JsonApiDotNetCore.Middleware
{
    /// <summary>
    /// Intercepts HTTP requests to populate injected <see cref="IJsonApiRequest" /> instance for JSON:API requests.
    /// </summary>
    [PublicAPI]
    public sealed class JsonApiMiddleware
    {
        private static readonly MediaTypeHeaderValue MediaType = MediaTypeHeaderValue.Parse(HeaderConstants.MediaType);
        private static readonly MediaTypeHeaderValue AtomicOperationsMediaType = MediaTypeHeaderValue.Parse(HeaderConstants.AtomicOperationsMediaType);

        private readonly RequestDelegate _next;

        public JsonApiMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext httpContext, IControllerResourceMapping controllerResourceMapping, IJsonApiOptions options,
            IJsonApiRequest request, IResourceContextProvider resourceContextProvider)
        {
            ArgumentGuard.NotNull(httpContext, nameof(httpContext));
            ArgumentGuard.NotNull(controllerResourceMapping, nameof(controllerResourceMapping));
            ArgumentGuard.NotNull(options, nameof(options));
            ArgumentGuard.NotNull(request, nameof(request));
            ArgumentGuard.NotNull(resourceContextProvider, nameof(resourceContextProvider));

            if (!await ValidateIfMatchHeaderAsync(httpContext, options.SerializerSettings))
            {
                return;
            }

            RouteValueDictionary routeValues = httpContext.GetRouteData().Values;
            ResourceContext primaryResourceContext = CreatePrimaryResourceContext(httpContext, controllerResourceMapping, resourceContextProvider);

            if (primaryResourceContext != null)
            {
                if (!await ValidateContentTypeHeaderAsync(HeaderConstants.MediaType, httpContext, options.SerializerSettings) ||
                    !await ValidateAcceptHeaderAsync(MediaType, httpContext, options.SerializerSettings))
                {
                    return;
                }

                SetupResourceRequest((JsonApiRequest)request, primaryResourceContext, routeValues, options, resourceContextProvider, httpContext.Request);

                httpContext.RegisterJsonApiRequest();
            }
            else if (IsOperationsRequest(routeValues))
            {
                if (!await ValidateContentTypeHeaderAsync(HeaderConstants.AtomicOperationsMediaType, httpContext, options.SerializerSettings) ||
                    !await ValidateAcceptHeaderAsync(AtomicOperationsMediaType, httpContext, options.SerializerSettings))
                {
                    return;
                }

                SetupOperationsRequest((JsonApiRequest)request, options, httpContext.Request);

                httpContext.RegisterJsonApiRequest();
            }

            // Workaround for bug https://github.com/dotnet/aspnetcore/issues/33394
            httpContext.Features.Set<IQueryFeature>(new FixedQueryFeature(httpContext.Features));

            await _next(httpContext);
        }

        private async Task<bool> ValidateIfMatchHeaderAsync(HttpContext httpContext, JsonSerializerSettings serializerSettings)
        {
            if (httpContext.Request.Headers.ContainsKey(HeaderNames.IfMatch))
            {
                await FlushResponseAsync(httpContext.Response, serializerSettings, new Error(HttpStatusCode.PreconditionFailed)
                {
                    Title = "Detection of mid-air edit collisions using ETags is not supported."
                });

                return false;
            }

            return true;
        }

        private static ResourceContext CreatePrimaryResourceContext(HttpContext httpContext, IControllerResourceMapping controllerResourceMapping,
            IResourceContextProvider resourceContextProvider)
        {
            Endpoint endpoint = httpContext.GetEndpoint();
            var controllerActionDescriptor = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();

            if (controllerActionDescriptor != null)
            {
                Type controllerType = controllerActionDescriptor.ControllerTypeInfo;
                Type resourceType = controllerResourceMapping.GetResourceTypeForController(controllerType);

                if (resourceType != null)
                {
                    return resourceContextProvider.GetResourceContext(resourceType);
                }
            }

            return null;
        }

        private static async Task<bool> ValidateContentTypeHeaderAsync(string allowedContentType, HttpContext httpContext,
            JsonSerializerSettings serializerSettings)
        {
            string contentType = httpContext.Request.ContentType;

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            // Justification: Workaround for https://github.com/dotnet/aspnetcore/issues/32097 (fixed in .NET 6)
            if (contentType != null && contentType != allowedContentType)
            {
                await FlushResponseAsync(httpContext.Response, serializerSettings, new Error(HttpStatusCode.UnsupportedMediaType)
                {
                    Title = "The specified Content-Type header value is not supported.",
                    Detail = $"Please specify '{allowedContentType}' instead of '{contentType}' " + "for the Content-Type header value."
                });

                return false;
            }

            return true;
        }

        private static async Task<bool> ValidateAcceptHeaderAsync(MediaTypeHeaderValue allowedMediaTypeValue, HttpContext httpContext,
            JsonSerializerSettings serializerSettings)
        {
            string[] acceptHeaders = httpContext.Request.Headers.GetCommaSeparatedValues("Accept");

            if (!acceptHeaders.Any())
            {
                return true;
            }

            bool seenCompatibleMediaType = false;

            foreach (string acceptHeader in acceptHeaders)
            {
                if (MediaTypeHeaderValue.TryParse(acceptHeader, out MediaTypeHeaderValue headerValue))
                {
                    headerValue.Quality = null;

                    if (headerValue.MediaType == "*/*" || headerValue.MediaType == "application/*")
                    {
                        seenCompatibleMediaType = true;
                        break;
                    }

                    if (allowedMediaTypeValue.Equals(headerValue))
                    {
                        seenCompatibleMediaType = true;
                        break;
                    }
                }
            }

            if (!seenCompatibleMediaType)
            {
                await FlushResponseAsync(httpContext.Response, serializerSettings, new Error(HttpStatusCode.NotAcceptable)
                {
                    Title = "The specified Accept header value does not contain any supported media types.",
                    Detail = $"Please include '{allowedMediaTypeValue}' in the Accept header values."
                });

                return false;
            }

            return true;
        }

        private static async Task FlushResponseAsync(HttpResponse httpResponse, JsonSerializerSettings serializerSettings, Error error)
        {
            httpResponse.ContentType = HeaderConstants.MediaType;
            httpResponse.StatusCode = (int)error.StatusCode;

            var serializer = JsonSerializer.CreateDefault(serializerSettings);
            serializer.ApplyErrorSettings();

            // https://github.com/JamesNK/Newtonsoft.Json/issues/1193
            await using (var stream = new MemoryStream())
            {
                await using (var streamWriter = new StreamWriter(stream, leaveOpen: true))
                {
                    using var jsonWriter = new JsonTextWriter(streamWriter);
                    serializer.Serialize(jsonWriter, new ErrorDocument(error));
                }

                stream.Seek(0, SeekOrigin.Begin);
                await stream.CopyToAsync(httpResponse.Body);
            }

            await httpResponse.Body.FlushAsync();
        }

        private static void SetupResourceRequest(JsonApiRequest request, ResourceContext primaryResourceContext, RouteValueDictionary routeValues,
            IJsonApiOptions options, IResourceContextProvider resourceContextProvider, HttpRequest httpRequest)
        {
            request.IsReadOnly = httpRequest.Method == HttpMethod.Get.Method || httpRequest.Method == HttpMethod.Head.Method;
            request.Kind = EndpointKind.Primary;
            request.PrimaryResource = primaryResourceContext;
            request.PrimaryId = GetPrimaryRequestId(routeValues);
            request.BasePath = GetBasePath(primaryResourceContext.PublicName, options, httpRequest);

            string relationshipName = GetRelationshipNameForSecondaryRequest(routeValues);

            if (relationshipName != null)
            {
                request.Kind = IsRouteForRelationship(routeValues) ? EndpointKind.Relationship : EndpointKind.Secondary;

                RelationshipAttribute requestRelationship =
                    primaryResourceContext.Relationships.SingleOrDefault(relationship => relationship.PublicName == relationshipName);

                if (requestRelationship != null)
                {
                    request.Relationship = requestRelationship;
                    request.SecondaryResource = resourceContextProvider.GetResourceContext(requestRelationship.RightType);
                }
            }

            bool isGetAll = request.PrimaryId == null && request.IsReadOnly;
            request.IsCollection = isGetAll || request.Relationship is HasManyAttribute;
        }

        private static string GetPrimaryRequestId(RouteValueDictionary routeValues)
        {
            return routeValues.TryGetValue("id", out object id) ? (string)id : null;
        }

        private static string GetBasePath(string resourceName, IJsonApiOptions options, HttpRequest httpRequest)
        {
            var builder = new StringBuilder();

            if (!options.UseRelativeLinks)
            {
                builder.Append(httpRequest.Scheme);
                builder.Append("://");
                builder.Append(httpRequest.Host);
            }

            if (httpRequest.PathBase.HasValue)
            {
                builder.Append(httpRequest.PathBase);
            }

            string customRoute = GetCustomRoute(resourceName, options.Namespace, httpRequest.HttpContext);

            if (!string.IsNullOrEmpty(customRoute))
            {
                builder.Append('/');
                builder.Append(customRoute);
            }
            else if (!string.IsNullOrEmpty(options.Namespace))
            {
                builder.Append('/');
                builder.Append(options.Namespace);
            }

            return builder.ToString();
        }

        private static string GetCustomRoute(string resourceName, string apiNamespace, HttpContext httpContext)
        {
            if (resourceName != null)
            {
                Endpoint endpoint = httpContext.GetEndpoint();
                var routeAttribute = endpoint?.Metadata.GetMetadata<RouteAttribute>();

                if (routeAttribute != null && httpContext.Request.Path.Value != null)
                {
                    List<string> trimmedComponents = httpContext.Request.Path.Value.Trim('/').Split('/').ToList();
                    int resourceNameIndex = trimmedComponents.FindIndex(component => component == resourceName);
                    string[] newComponents = trimmedComponents.Take(resourceNameIndex).ToArray();
                    string customRoute = string.Join('/', newComponents);
                    return customRoute == apiNamespace ? null : customRoute;
                }
            }

            return null;
        }

        private static string GetRelationshipNameForSecondaryRequest(RouteValueDictionary routeValues)
        {
            return routeValues.TryGetValue("relationshipName", out object routeValue) ? (string)routeValue : null;
        }

        private static bool IsRouteForRelationship(RouteValueDictionary routeValues)
        {
            string actionName = (string)routeValues["action"];
            return actionName.EndsWith("Relationship", StringComparison.Ordinal);
        }

        private static bool IsOperationsRequest(RouteValueDictionary routeValues)
        {
            string actionName = (string)routeValues["action"];
            return actionName == "PostOperations";
        }

        private static void SetupOperationsRequest(JsonApiRequest request, IJsonApiOptions options, HttpRequest httpRequest)
        {
            request.IsReadOnly = false;
            request.Kind = EndpointKind.AtomicOperations;
            request.BasePath = GetBasePath(null, options, httpRequest);
        }
    }
}
