using System;
using System.Linq;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Resources;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.DependencyInjection;

namespace JsonApiDotNetCore.Configuration
{
    /// <summary>
    /// Validation filter that blocks ASP.NET Core ModelState validation on data according to the JSON:API spec.
    /// </summary>
    internal sealed class JsonApiValidationFilter : IPropertyValidationFilter
    {
        private readonly IRequestScopedServiceProvider _serviceProvider;

        public JsonApiValidationFilter(IRequestScopedServiceProvider serviceProvider)
        {
            ArgumentGuard.NotNull(serviceProvider, nameof(serviceProvider));

            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        public bool ShouldValidateEntry(ValidationEntry entry, ValidationEntry parentEntry)
        {
            var request = _serviceProvider.GetRequiredService<IJsonApiRequest>();

            if (IsId(entry.Key))
            {
                return true;
            }

            bool isTopResourceInPrimaryRequest = string.IsNullOrEmpty(parentEntry.Key) && IsAtPrimaryEndpoint(request);

            if (!isTopResourceInPrimaryRequest)
            {
                return false;
            }

            var httpContextAccessor = _serviceProvider.GetRequiredService<IHttpContextAccessor>();

            if (httpContextAccessor.HttpContext!.Request.Method == HttpMethods.Patch || request.OperationKind == OperationKind.UpdateResource)
            {
                var targetedFields = _serviceProvider.GetRequiredService<ITargetedFields>();
                return IsFieldTargeted(entry, targetedFields);
            }

            return true;
        }

        private static bool IsId(string key)
        {
            return key == nameof(Identifiable.Id) || key.EndsWith("." + nameof(Identifiable.Id), StringComparison.Ordinal);
        }

        private static bool IsAtPrimaryEndpoint(IJsonApiRequest request)
        {
            return request.Kind == EndpointKind.Primary || request.Kind == EndpointKind.AtomicOperations;
        }

        private static bool IsFieldTargeted(ValidationEntry entry, ITargetedFields targetedFields)
        {
            return targetedFields.Attributes.Any(attribute => attribute.Property.Name == entry.Key);
        }
    }
}
