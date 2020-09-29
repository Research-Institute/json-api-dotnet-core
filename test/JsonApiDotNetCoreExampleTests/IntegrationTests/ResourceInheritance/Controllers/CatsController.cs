using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance
{
    public sealed class CatsController : JsonApiController<Cat>
    {
        public CatsController(IJsonApiOptions options, ILoggerFactory loggerFactory,
            IResourceService<Cat> resourceService)
            : base(options, loggerFactory, resourceService)
        {
        }
    }
}
