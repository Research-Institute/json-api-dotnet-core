using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Services;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance
{
    public sealed class FictionBooksController : JsonApiController<Book>
    {
        public FictionBooksController(IJsonApiOptions options, ILoggerFactory loggerFactory,
            IResourceService<Book> resourceService)
            : base(options, loggerFactory, resourceService)
        {
        }
    }
}
