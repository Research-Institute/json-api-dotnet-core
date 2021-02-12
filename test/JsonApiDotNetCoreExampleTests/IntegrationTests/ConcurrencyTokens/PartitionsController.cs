using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ConcurrencyTokens
{
    public sealed class PartitionsController : JsonApiController<Partition>
    {
        public PartitionsController(IJsonApiOptions options, ILoggerFactory loggerFactory,
            IResourceService<Partition> resourceService)
            : base(options, loggerFactory, resourceService)
        {
        }
    }
}
