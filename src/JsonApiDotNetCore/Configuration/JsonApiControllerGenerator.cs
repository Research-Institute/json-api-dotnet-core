using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JsonApiDotNetCore.Controllers;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace JsonApiDotNetCore.Configuration
{
    public class JsonApiControllerGenerator : IJsonApiControllerGenerator
    {
        private readonly IResourceGraph _resourceGraph;
        private readonly Type _controllerOpenType;
        private readonly Type _baseControllerOpenType;

        public JsonApiControllerGenerator(IResourceGraph resourceGraph)
        {
            _resourceGraph = resourceGraph ?? throw new ArgumentNullException(nameof(resourceGraph));
            _controllerOpenType = typeof(JsonApiController<,>);
            _baseControllerOpenType = typeof(BaseJsonApiController<,>);
        }

        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            var exposedTypes = parts.SelectMany(part => ((AssemblyPart)part).Types).ToList();
            foreach (var resourceContext in _resourceGraph.GetResourceContexts())
            {
                RegisterControllerForResource(feature, resourceContext, exposedTypes);
            }
        }


        private void RegisterControllerForResource(ControllerFeature feature, ResourceContext resourceContext, List<TypeInfo> exposedTypes)
        {
            if (resourceContext != null && !resourceContext.ResourceType.IsAbstract)
            {
                var existingControllerType = _baseControllerOpenType.MakeGenericType(resourceContext.ResourceType, resourceContext.IdentityType).GetTypeInfo();
                if (!exposedTypes.Any(exposedType => existingControllerType.IsAssignableFrom(exposedType)))
                {
                    var controllerType = GetControllerType(resourceContext);
                    feature.Controllers.Add(controllerType);
                }
            }
        }

        protected virtual TypeInfo GetControllerType(ResourceContext resourceContext)
        {
            var controllerType = _controllerOpenType.MakeGenericType(resourceContext.ResourceType, resourceContext.IdentityType).GetTypeInfo();
            return controllerType;
        }
    }
}
