using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance
{
    public sealed class FemalesController : JsonApiController<Female>
    {
        public FemalesController(IJsonApiOptions options, ILoggerFactory loggerFactory,
            IResourceService<Female> resourceService)
            : base(options, loggerFactory, resourceService) { }
    }
}
