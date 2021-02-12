using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ConcurrencyTokens
{
    public sealed class DisksController : JsonApiController<Disk>
    {
        public DisksController(IJsonApiOptions options, ILoggerFactory loggerFactory,
            IResourceService<Disk> resourceService)
            : base(options, loggerFactory, resourceService)
        {
        }
    }
}
