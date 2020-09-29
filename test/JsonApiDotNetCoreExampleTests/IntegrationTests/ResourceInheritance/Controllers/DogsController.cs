using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance
{
    public sealed class DogsController : JsonApiController<Dog>
    {
        public DogsController(IJsonApiOptions options, ILoggerFactory loggerFactory,
            IResourceService<Dog> resourceService)
            : base(options, loggerFactory, resourceService)
        {
        }
    }
}
