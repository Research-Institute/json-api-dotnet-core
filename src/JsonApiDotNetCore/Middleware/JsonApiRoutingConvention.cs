using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Controllers.Annotations;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace JsonApiDotNetCore.Middleware
{
    /// <summary>
    /// The default routing convention registers the name of the resource as the route
    /// using the serializer naming convention. The default for this is
    /// a camel case formatter. If the controller directly inherits from <see cref="CoreJsonApiController"/> and there is no
    /// resource directly associated, it uses the name of the controller instead of the name of the type.
    /// </summary>
    /// <example><![CDATA[
    /// public class SomeResourceController : JsonApiController<SomeResource> { } // => /someResources/relationship/relatedResource
    ///
    /// public class RandomNameController<SomeResource> : JsonApiController<SomeResource> { } // => /someResources/relationship/relatedResource
    ///
    /// // when using kebab-case naming convention:
    /// public class SomeResourceController<SomeResource> : JsonApiController<SomeResource> { } // => /some-resources/relationship/related-resource
    ///
    /// public class SomeVeryCustomController<SomeResource> : CoreJsonApiController { } // => /someVeryCustoms/relationship/relatedResource
    /// ]]></example>
    public class JsonApiRoutingConvention : IJsonApiRoutingConvention
    {
        private readonly IJsonApiOptions _options;
        private readonly IResourceGraph _resourceGraph;
        private readonly Dictionary<string, ControllerModel> _endpointsByRoutes = new Dictionary<string, ControllerModel>();

        private readonly Dictionary<string, ResourceContext> _resourcesByEndpoint = new Dictionary<string, ResourceContext>();

        public JsonApiRoutingConvention(IJsonApiOptions options, IResourceGraph resourceGraph)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _resourceGraph = resourceGraph ?? throw new ArgumentNullException(nameof(resourceGraph));
        }

        /// <inheritdoc />
        public Type GetResourceForEndpoint(string controllerName)
        {
            if (controllerName == null) throw new ArgumentNullException(nameof(controllerName));
            
            if (_resourcesByEndpoint.TryGetValue(controllerName, out var resourceContext))
            {
                return resourceContext.ResourceType;
            }
            
            return null;
        }

        /// <inheritdoc />
        public void Apply(ApplicationModel application)
        {
            if (application == null) throw new ArgumentNullException(nameof(application));

            RegisterResources(application.Controllers);
            RegisterRoutes(application.Controllers);
        }

        private void RegisterRoutes(IEnumerable<ControllerModel> controllers)
        {
            foreach (var model in controllers)
            {
                if (!RoutingConventionDisabled(model))
                {
                    continue;
                }

                var template = GetRouteTemplateForEndpoint(model);

                model.Selectors[0].AttributeRouteModel = new AttributeRouteModel {Template = template};
                _endpointsByRoutes.Add(template, model);
            }
        }

        private string GetRouteTemplateForEndpoint(ControllerModel controllerModel)
        {
            var template = TryGetResourceBasedTemplate(controllerModel);
            if (template == null || _endpointsByRoutes.ContainsKey(template))
            {
                template = TryGetControllerBasedTemplate(controllerModel);
            }

            if (template == null)
            {
                throw new InvalidConfigurationException($"Failed to create a template for {controllerModel.ControllerType.FullName} " +
                                                        $"based on the controller and resource name");
            }

            if (_endpointsByRoutes.ContainsKey(template))
            {
                var overlappingEndpoint = _endpointsByRoutes[template];
                throw new InvalidConfigurationException(
                    $"Cannot register template {template} for {controllerModel.ControllerType.FullName} " +
                    $"because it is already registered for {overlappingEndpoint.ControllerType.FullName}.");
            }

            return template;
        }

        private void RegisterResources(IEnumerable<ControllerModel> controllers)
        {
            foreach (var model in controllers)
            {
                var resourceType = ExtractResourceTypeFromEndpoint(model.ControllerType);
                if (resourceType != null)
                {
                    var resourceContext = _resourceGraph.GetResourceContext(resourceType);
                    if (resourceContext != null)
                    {
                        _resourcesByEndpoint.Add(model.ControllerType.FullName!, resourceContext);
                    }
                }
            }
        }


        /// <summary>
        /// Verifies if routing convention should be enabled for this controller.
        /// </summary>
        private bool RoutingConventionDisabled(ControllerModel controller)
        {
            var type = controller.ControllerType;
            var notDisabled = type.GetCustomAttribute<DisableRoutingConventionAttribute>() == null;
            return notDisabled && type.IsSubclassOf(typeof(CoreJsonApiController));
        }

        private string TryGetResourceBasedTemplate(ControllerModel model)
        {
            if (_resourcesByEndpoint.TryGetValue(model.ControllerType.FullName, out var resourceContext))
            {
                return $"{_options.Namespace}/{resourceContext.PublicName}";
            }

            return null;
        }

        /// <summary>
        /// Derives a template from the controller name, and checks if this template was already registered.
        /// </summary>
        private string TryGetControllerBasedTemplate(ControllerModel model)
        {
            if (!model.ControllerType.IsGenericType || model.ControllerType.GetGenericTypeDefinition() != typeof(BaseJsonApiController<,>))
            {
                var controllerName = _options.SerializerContractResolver.NamingStrategy!.GetPropertyName(model.ControllerName, false);
                return $"{_options.Namespace}/{controllerName}";
            }

            return null;
        }

        /// <summary>
        /// Determines the resource associated to a controller by inspecting generic arguments in its inheritance tree.
        /// </summary>
        private Type ExtractResourceTypeFromEndpoint(Type type)
        {
            var aspNetControllerType = typeof(ControllerBase);
            var coreControllerType = typeof(CoreJsonApiController);
            var baseControllerType = typeof(BaseJsonApiController<,>);
            var currentType = type;
            while (!currentType.IsGenericType || currentType.GetGenericTypeDefinition() != baseControllerType)
            {
                var nextBaseType = currentType.BaseType;

                if ((nextBaseType == aspNetControllerType || nextBaseType == coreControllerType) &&
                    currentType.IsGenericType)
                {
                    var resourceType = currentType.GetGenericArguments()
                        .FirstOrDefault(t => TypeHelper.IsOrImplementsInterface(t, typeof(IIdentifiable)));
                    if (resourceType != null)
                    {
                        return resourceType;
                    }
                }

                currentType = nextBaseType;
                if (nextBaseType == null)
                {
                    break;
                }
            }

            return currentType?.GetGenericArguments().First();
        }
    }
}
