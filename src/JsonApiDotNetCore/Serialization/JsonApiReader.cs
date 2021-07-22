using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using JetBrains.Annotations;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Serialization.Objects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCore.Serialization
{
    /// <inheritdoc />
    [PublicAPI]
    public class JsonApiReader : IJsonApiReader
    {
        private readonly IJsonApiDeserializer _deserializer;
        private readonly IJsonApiRequest _request;
        private readonly IResourceContextProvider _resourceContextProvider;
        private readonly TraceLogWriter<JsonApiReader> _traceWriter;

        public JsonApiReader(IJsonApiDeserializer deserializer, IJsonApiRequest request, IResourceContextProvider resourceContextProvider,
            ILoggerFactory loggerFactory)
        {
            ArgumentGuard.NotNull(deserializer, nameof(deserializer));
            ArgumentGuard.NotNull(request, nameof(request));
            ArgumentGuard.NotNull(resourceContextProvider, nameof(resourceContextProvider));
            ArgumentGuard.NotNull(loggerFactory, nameof(loggerFactory));

            _deserializer = deserializer;
            _request = request;
            _resourceContextProvider = resourceContextProvider;
            _traceWriter = new TraceLogWriter<JsonApiReader>(loggerFactory);
        }

        public async Task<InputFormatterResult> ReadAsync(InputFormatterContext context)
        {
            ArgumentGuard.NotNull(context, nameof(context));

            string body = await GetRequestBodyAsync(context.HttpContext.Request.Body);

            string url = context.HttpContext.Request.GetEncodedUrl();
            _traceWriter.LogMessage(() => $"Received request at '{url}' with body: <<{body}>>");

            object model = null;

            if (!string.IsNullOrWhiteSpace(body))
            {
                try
                {
                    model = _deserializer.Deserialize(body);
                }
                catch (JsonApiSerializationException exception)
                {
                    throw ToInvalidRequestBodyException(exception, body);
                }
#pragma warning disable AV1210 // Catch a specific exception instead of Exception, SystemException or ApplicationException
                catch (Exception exception)
#pragma warning restore AV1210 // Catch a specific exception instead of Exception, SystemException or ApplicationException
                {
                    throw new InvalidRequestBodyException(null, null, body, exception);
                }
            }

            if (_request.Kind == EndpointKind.AtomicOperations)
            {
                AssertHasRequestBody(model, body);
            }
            else if (RequiresRequestBody(context.HttpContext.Request.Method))
            {
                ValidateRequestBody(model, body, context.HttpContext.Request);
            }

            // ReSharper disable once AssignNullToNotNullAttribute
            // Justification: According to JSON:API we must return 200 OK without a body in some cases.
            return await InputFormatterResult.SuccessAsync(model);
        }

        private async Task<string> GetRequestBodyAsync(Stream bodyStream)
        {
            using var reader = new StreamReader(bodyStream, leaveOpen: true);
            return await reader.ReadToEndAsync();
        }

        private InvalidRequestBodyException ToInvalidRequestBodyException(JsonApiSerializationException exception, string body)
        {
            if (_request.Kind != EndpointKind.AtomicOperations)
            {
                return new InvalidRequestBodyException(exception.GenericMessage, exception.SpecificMessage, body, exception);
            }

            // In contrast to resource endpoints, we don't include the request body for operations because they are usually very long.
            var requestException = new InvalidRequestBodyException(exception.GenericMessage, exception.SpecificMessage, null, exception.InnerException);

            if (exception.AtomicOperationIndex != null)
            {
                foreach (Error error in requestException.Errors)
                {
                    error.Source.Pointer = $"/atomic:operations[{exception.AtomicOperationIndex}]";
                }
            }

            return requestException;
        }

        private bool RequiresRequestBody(string requestMethod)
        {
            if (requestMethod == HttpMethods.Post || requestMethod == HttpMethods.Patch)
            {
                return true;
            }

            return requestMethod == HttpMethods.Delete && _request.Kind == EndpointKind.Relationship;
        }

        private void ValidateRequestBody(object model, string body, HttpRequest httpRequest)
        {
            AssertHasRequestBody(model, body);

            ValidateIncomingResourceType(model, httpRequest);

            if (httpRequest.Method != HttpMethods.Post || _request.Kind == EndpointKind.Relationship)
            {
                ValidateRequestIncludesId(model, body);
                ValidatePrimaryIdValue(model, httpRequest.Path);
            }

            if (_request.Kind == EndpointKind.Relationship)
            {
                ValidateForRelationshipType(httpRequest.Method, model, body);
            }
        }

        [AssertionMethod]
        private static void AssertHasRequestBody(object model, string body)
        {
            if (model == null && string.IsNullOrWhiteSpace(body))
            {
                throw new JsonApiException(new Error(HttpStatusCode.BadRequest)
                {
                    Title = "Missing request body."
                });
            }
        }

        private void ValidateIncomingResourceType(object model, HttpRequest httpRequest)
        {
            Type endpointResourceType = GetResourceTypeFromEndpoint();

            if (endpointResourceType == null)
            {
                return;
            }

            IEnumerable<Type> bodyResourceTypes = GetResourceTypesFromRequestBody(model);

            foreach (Type bodyResourceType in bodyResourceTypes)
            {
                if (!endpointResourceType.IsAssignableFrom(bodyResourceType))
                {
                    ResourceContext resourceFromEndpoint = _resourceContextProvider.GetResourceContext(endpointResourceType);
                    ResourceContext resourceFromBody = _resourceContextProvider.GetResourceContext(bodyResourceType);

                    throw new ResourceTypeMismatchException(new HttpMethod(httpRequest.Method), httpRequest.Path, resourceFromEndpoint, resourceFromBody);
                }
            }
        }

        private Type GetResourceTypeFromEndpoint()
        {
            return _request.Kind == EndpointKind.Primary ? _request.PrimaryResource.ResourceType : _request.SecondaryResource?.ResourceType;
        }

        private IEnumerable<Type> GetResourceTypesFromRequestBody(object model)
        {
            if (model is IEnumerable<IIdentifiable> resourceCollection)
            {
                return resourceCollection.Select(resource => resource.GetType()).Distinct();
            }

            return model == null ? Enumerable.Empty<Type>() : model.GetType().AsEnumerable();
        }

        private void ValidateRequestIncludesId(object model, string body)
        {
            bool hasMissingId = model is IEnumerable list ? HasMissingId(list) : HasMissingId(model);

            if (hasMissingId)
            {
                throw new InvalidRequestBodyException("Request body must include 'id' element.", null, body);
            }
        }

        private void ValidatePrimaryIdValue(object model, PathString requestPath)
        {
            if (_request.Kind == EndpointKind.Primary)
            {
                if (TryGetId(model, out string bodyId) && bodyId != _request.PrimaryId)
                {
                    throw new ResourceIdMismatchException(bodyId, _request.PrimaryId, requestPath);
                }
            }
        }

        /// <summary>
        /// Checks if the deserialized request body has an ID included.
        /// </summary>
        private bool HasMissingId(object model)
        {
            return TryGetId(model, out string id) && id == null;
        }

        /// <summary>
        /// Checks if all elements in the deserialized request body have an ID included.
        /// </summary>
        private bool HasMissingId(IEnumerable models)
        {
            foreach (object model in models)
            {
                if (TryGetId(model, out string id) && id == null)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetId(object model, out string id)
        {
            if (model is IIdentifiable identifiable)
            {
                id = identifiable.StringId;
                return true;
            }

            id = null;
            return false;
        }

        [AssertionMethod]
        private void ValidateForRelationshipType(string requestMethod, object model, string body)
        {
            if (_request.Relationship is HasOneAttribute)
            {
                if (requestMethod == HttpMethods.Post || requestMethod == HttpMethods.Delete)
                {
                    throw new ToManyRelationshipRequiredException(_request.Relationship.PublicName);
                }

                if (model != null && !(model is IIdentifiable))
                {
                    throw new InvalidRequestBodyException("Expected single data element for to-one relationship.",
                        $"Expected single data element for '{_request.Relationship.PublicName}' relationship.", body);
                }
            }

            if (_request.Relationship is HasManyAttribute && !(model is IEnumerable<IIdentifiable>))
            {
                throw new InvalidRequestBodyException("Expected data[] element for to-many relationship.",
                    $"Expected data[] element for '{_request.Relationship.PublicName}' relationship.", body);
            }
        }
    }
}
