using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
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
    /// The default routing convention registers the name of the resource as the route using the serializer naming convention. The default for this is a
    /// camel case formatter. If the controller directly inherits from <see cref="CoreJsonApiController" /> and there is no resource directly associated, it
    /// uses the name of the controller instead of the name of the type.
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
    [PublicAPI]
    public class JsonApiRoutingConvention : IJsonApiRoutingConvention
    {
        private readonly IJsonApiOptions _options;
        private readonly IResourceContextProvider _resourceContextProvider;
        private readonly Dictionary<string, string> _registeredControllerNameByTemplate = new();
        private readonly Dictionary<Type, ResourceContext> _resourceContextPerControllerTypeMap = new();
        private readonly Dictionary<ResourceContext, ControllerModel> _controllerPerResourceContextMap = new();

        public JsonApiRoutingConvention(IJsonApiOptions options, IResourceContextProvider resourceContextProvider)
        {
            ArgumentGuard.NotNull(options, nameof(options));
            ArgumentGuard.NotNull(resourceContextProvider, nameof(resourceContextProvider));

            _options = options;
            _resourceContextProvider = resourceContextProvider;
        }

        /// <inheritdoc />
        public Type GetResourceTypeForController(Type controllerType)
        {
            ArgumentGuard.NotNull(controllerType, nameof(controllerType));

            if (_resourceContextPerControllerTypeMap.TryGetValue(controllerType, out ResourceContext resourceContext))
            {
                return resourceContext.ResourceType;
            }

            return null;
        }

        /// <inheritdoc />
        public string GetControllerNameForResourceType(Type resourceType)
        {
            ArgumentGuard.NotNull(resourceType, nameof(resourceType));

            ResourceContext resourceContext = _resourceContextProvider.GetResourceContext(resourceType);

            if (_controllerPerResourceContextMap.TryGetValue(resourceContext, out ControllerModel controllerModel))

            {
                return controllerModel.ControllerName;
            }

            return null;
        }

        /// <inheritdoc />
        public void Apply(ApplicationModel application)
        {
            ArgumentGuard.NotNull(application, nameof(application));

            foreach (ControllerModel controller in application.Controllers)
            {
                bool isOperationsController = IsOperationsController(controller.ControllerType);

                if (!isOperationsController)
                {
                    Type resourceType = ExtractResourceTypeFromController(controller.ControllerType);

                    if (resourceType != null)
                    {
                        ResourceContext resourceContext = _resourceContextProvider.GetResourceContext(resourceType);

                        if (resourceContext != null)
                        {
                            _resourceContextPerControllerTypeMap.Add(controller.ControllerType, resourceContext);
                            _controllerPerResourceContextMap.Add(resourceContext, controller);
                        }
                    }
                }

                if (!IsRoutingConventionEnabled(controller))
                {
                    continue;
                }

                string template = TemplateFromResource(controller) ?? TemplateFromController(controller);

                if (_registeredControllerNameByTemplate.ContainsKey(template))
                {
                    throw new InvalidConfigurationException(
                        $"Cannot register '{controller.ControllerType.FullName}' for template '{template}' because '{_registeredControllerNameByTemplate[template]}' was already registered for this template.");
                }

                _registeredControllerNameByTemplate.Add(template, controller.ControllerType.FullName);

                controller.Selectors[0].AttributeRouteModel = new AttributeRouteModel
                {
                    Template = template
                };
            }
        }

        private bool IsRoutingConventionEnabled(ControllerModel controller)
        {
            return controller.ControllerType.IsSubclassOf(typeof(CoreJsonApiController)) &&
                controller.ControllerType.GetCustomAttribute<DisableRoutingConventionAttribute>() == null;
        }

        /// <summary>
        /// Derives a template from the resource type, and checks if this template was already registered.
        /// </summary>
        private string TemplateFromResource(ControllerModel model)
        {
            if (_resourceContextPerControllerTypeMap.TryGetValue(model.ControllerType, out ResourceContext resourceContext))
            {
                return $"{_options.Namespace}/{resourceContext.PublicName}";
            }

            return null;
        }

        /// <summary>
        /// Derives a template from the controller name, and checks if this template was already registered.
        /// </summary>
        private string TemplateFromController(ControllerModel model)
        {
            string controllerName = _options.SerializerNamingStrategy.GetPropertyName(model.ControllerName, false);
            return $"{_options.Namespace}/{controllerName}";
        }

        /// <summary>
        /// Determines the resource associated to a controller by inspecting generic arguments in its inheritance tree.
        /// </summary>
        private Type ExtractResourceTypeFromController(Type type)
        {
            Type aspNetControllerType = typeof(ControllerBase);
            Type coreControllerType = typeof(CoreJsonApiController);
            Type baseControllerType = typeof(BaseJsonApiController<,>);
            Type currentType = type;

            while (!currentType.IsGenericType || currentType.GetGenericTypeDefinition() != baseControllerType)
            {
                Type nextBaseType = currentType.BaseType;

                if ((nextBaseType == aspNetControllerType || nextBaseType == coreControllerType) && currentType.IsGenericType)
                {
                    Type resourceType = currentType.GetGenericArguments()
                        .FirstOrDefault(typeArgument => typeArgument.IsOrImplementsInterface(typeof(IIdentifiable)));

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

        private static bool IsOperationsController(Type type)
        {
            Type baseControllerType = typeof(BaseJsonApiOperationsController);
            return baseControllerType.IsAssignableFrom(type);
        }
    }
}
