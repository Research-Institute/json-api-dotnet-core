using System;
using System.Reflection;
using JsonApiDotNetCore.Configuration;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.IdObfuscation
{
    public sealed class ObfuscatedJsonApiControllerGenerator : JsonApiControllerGenerator
    {
        private readonly Type _obfuscatedIdentifiableType;
        private readonly Type _obfuscatedControllerOpenType;

        public ObfuscatedJsonApiControllerGenerator(IResourceGraph resourceGraph) : base(resourceGraph)
        {
            _obfuscatedIdentifiableType = typeof(ObfuscatedIdentifiable);
            _obfuscatedControllerOpenType = typeof(ObfuscatedIdentifiableController<>);
        }

        protected override TypeInfo GetControllerType(ResourceContext resourceContext)
        {
            if (_obfuscatedIdentifiableType.IsAssignableFrom(resourceContext.ResourceType))
            {
                return _obfuscatedControllerOpenType.MakeGenericType(resourceContext.ResourceType).GetTypeInfo();
            }

            return base.GetControllerType(resourceContext);
        }
    }
}
