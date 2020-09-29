using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance
{
    public sealed class NonFictionBooksController : JsonApiController<Video>
    {
        public NonFictionBooksController(IJsonApiOptions options, ILoggerFactory loggerFactory,
            IResourceService<Video> resourceService)
            : base(options, loggerFactory, resourceService)
        {
        }
    }
}
