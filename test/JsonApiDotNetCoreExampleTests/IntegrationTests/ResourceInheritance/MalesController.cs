using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance.Models;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance
{
    public sealed class MalesController : JsonApiController<Male>
    {
        public MalesController(IJsonApiOptions options, ILoggerFactory loggerFactory,
            IResourceService<Male> resourceService)
            : base(options, loggerFactory, resourceService) { }
    }
}