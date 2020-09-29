using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Linq;
using System.Threading.Tasks;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Objects;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCore.Serialization
{
    /// <inheritdoc />
    public class JsonApiReader : IJsonApiReader
    {
        private readonly IJsonApiDeserializer _deserializer;
        private readonly IJsonApiRequest _request;
        private readonly IResourceContextProvider _resourceContextProvider;
        private readonly TraceLogWriter<JsonApiReader> _traceWriter;

        public JsonApiReader(IJsonApiDeserializer deserializer,
            IJsonApiRequest request,
            IResourceContextProvider resourceContextProvider,
            ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));

            _deserializer = deserializer ?? throw new ArgumentNullException(nameof(deserializer));
            _request = request ?? throw new ArgumentNullException(nameof(request));
            _resourceContextProvider = resourceContextProvider ??  throw new ArgumentNullException(nameof(resourceContextProvider));
            _traceWriter = new TraceLogWriter<JsonApiReader>(loggerFactory);
        }

        public async Task<InputFormatterResult> ReadAsync(InputFormatterContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            var request = context.HttpContext.Request;
            if (request.ContentLength == 0)
            {
                return await InputFormatterResult.SuccessAsync(null);
            }

            string body = await GetRequestBody(context.HttpContext.Request.Body);

            string url = context.HttpContext.Request.GetEncodedUrl();
            _traceWriter.LogMessage(() => $"Received request at '{url}' with body: <<{body}>>");

            object model;
            try
            {
                model = _deserializer.Deserialize(body);
            }
            catch (InvalidRequestBodyException exception)
            {
                exception.SetRequestBody(body);
                throw;
            }
            catch (Exception exception)
            {
                throw new InvalidRequestBodyException(null, null, body, exception);
            }

            ValidatePatchRequestIncludesId(context, model, body);

            ValidateIncomingResourceType(context, model);
            
            return await InputFormatterResult.SuccessAsync(model);
        }

        private void ValidateIncomingResourceType(InputFormatterContext context, object model)
        {
            if (context.HttpContext.IsJsonApiRequest() && IsPatchOrPostRequest(context.HttpContext.Request))
            {
                var endpointResourceType = GetEndpointResourceType();
                if (endpointResourceType == null)
                {
                    return;
                }
                
                var bodyResourceTypes = GetBodyResourceTypes(model);
                foreach (var bodyResourceType in bodyResourceTypes)
                {
                    if (!endpointResourceType.IsAssignableFrom(bodyResourceType))
                    {
                        var resourceFromEndpoint = _resourceContextProvider.GetResourceContext(endpointResourceType);
                        var resourceFromBody = _resourceContextProvider.GetResourceContext(bodyResourceType);

                        throw new ResourceTypeMismatchException(new HttpMethod(context.HttpContext.Request.Method),
                            context.HttpContext.Request.Path,
                            resourceFromEndpoint, resourceFromBody);
                    }
                }
            }
        }

        private void ValidatePatchRequestIncludesId(InputFormatterContext context, object model, string body)
        {
            if (context.HttpContext.Request.Method == HttpMethods.Patch)
            {
                bool hasMissingId = model is IList list ? HasMissingId(list) : HasMissingId(model);
                if (hasMissingId)
                {
                    throw new InvalidRequestBodyException("Payload must include 'id' element.", null, body);
                }

                if (_request.Kind == EndpointKind.Primary && TryGetId(model, out var bodyId) && bodyId != _request.PrimaryId)
                {
                    throw new ResourceIdMismatchException(bodyId, _request.PrimaryId, context.HttpContext.Request.GetDisplayUrl());
                }
            }
        }

        /// <summary> Checks if the deserialized payload has an ID included </summary>
        private bool HasMissingId(object model)
        {
            return TryGetId(model, out string id) && string.IsNullOrEmpty(id);
        }

        /// <summary> Checks if all elements in the deserialized payload have an ID included </summary>
        private bool HasMissingId(IEnumerable models)
        {
            foreach (var model in models)
            {
                if (TryGetId(model, out string id) && string.IsNullOrEmpty(id))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetId(object model, out string id)
        {
            if (model is ResourceObject resourceObject)
            {
                id = resourceObject.Id;
                return true;
            }

            if (model is IIdentifiable identifiable)
            {
                id = identifiable.StringId;
                return true;
            }

            id = null;
            return false;
        }

        /// <summary>
        /// Fetches the request from body asynchronously.
        /// </summary>
        /// <param name="body">Input stream for body</param>
        /// <returns>String content of body sent to server.</returns>
        private async Task<string> GetRequestBody(Stream body)
        {
            using var reader = new StreamReader(body);
            // This needs to be set to async because
            // Synchronous IO operations are 
            // https://github.com/aspnet/AspNetCore/issues/7644
            return await reader.ReadToEndAsync();
        }
        
        private bool IsPatchOrPostRequest(HttpRequest request)
        {
            return request.Method == HttpMethods.Patch || request.Method == HttpMethods.Post;
        }

        private IEnumerable<Type> GetBodyResourceTypes(object model)
        {
            if (model is IEnumerable<IIdentifiable> resourceCollection)
            {
                return resourceCollection.Select(r => r.GetType()).Distinct();
            }

            return model == null ? new Type[0] : new[] { model.GetType() };
        }

        private Type GetEndpointResourceType()
        {
            return _request.Kind == EndpointKind.Primary
                ? _request.PrimaryResource.ResourceType 
                : _request.SecondaryResource?.ResourceType;
        }
    }
}
