using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Controllers;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCore.Services;
using JsonApiDotNetCoreExample;
using JsonApiDotNetCoreExample.Data;
using JsonApiDotNetCoreExample.Models;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.Acceptance.Spec.DocumentTests
{
    public sealed class LinksWithoutNamespaceTests : IClassFixture<IntegrationTestContext<NoNamespaceStartup, AppDbContext>>
    {
        private readonly IntegrationTestContext<NoNamespaceStartup, AppDbContext> _testContext;

        public LinksWithoutNamespaceTests(IntegrationTestContext<NoNamespaceStartup, AppDbContext> testContext)
        {
            _testContext = testContext;
        }

        [Fact]
        public async Task GET_RelativeLinks_True_Without_Namespace_Returns_RelativeLinks()
        {
            // Arrange
            var options = (JsonApiOptions) _testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.UseRelativeLinks = true;

            var blog = new Blog();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Blogs.Add(blog);

                await dbContext.SaveChangesAsync();
            });

            var route = "/blogs/" + blog.StringId;

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Links.Self.Should().Be("/blogs/" + blog.StringId);
        }

        [Fact]
        public async Task GET_RelativeLinks_False_Without_Namespace_Returns_AbsoluteLinks()
        {
            // Arrange
            var options = (JsonApiOptions) _testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.UseRelativeLinks = false;

            var blog = new Blog();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Blogs.Add(blog);

                await dbContext.SaveChangesAsync();
            });

            var route = "/blogs/" + blog.StringId;

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Links.Self.Should().Be("http://localhost/blogs/" + blog.StringId);
        }
    }

    public sealed class NoNamespaceStartup : TestStartup
    {
        public NoNamespaceStartup(IConfiguration configuration) : base(configuration)
        {
        }

        protected override void ConfigureJsonApiOptions(JsonApiOptions options)
        {
            base.ConfigureJsonApiOptions(options);

            options.Namespace = null;
        }
    }
    
    public sealed class BlogsController : JsonApiController<Blog>
    {
        public BlogsController(
            IJsonApiOptions options,
            ILoggerFactory loggerFactory,
            IResourceService<Blog> resourceService)
            : base(options, loggerFactory, resourceService)
        { }
    }
}
