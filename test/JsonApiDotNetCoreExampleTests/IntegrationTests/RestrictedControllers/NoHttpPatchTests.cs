using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExampleTests.Startups;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.RestrictedControllers
{
    public sealed class NoHttpPatchTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<RestrictionDbContext>, RestrictionDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<RestrictionDbContext>, RestrictionDbContext> _testContext;
        private readonly RestrictionFakers _fakers = new();

        public NoHttpPatchTests(ExampleIntegrationTestContext<TestableStartup<RestrictionDbContext>, RestrictionDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<BlockingHttpPatchController>();
        }

        [Fact]
        public async Task Can_get_resources()
        {
            // Arrange
            const string route = "/chairs";

            // Act
            (HttpResponseMessage httpResponse, _) = await _testContext.ExecuteGetAsync<string>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Can_create_resource()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "chairs",
                    attributes = new
                    {
                    }
                }
            };

            const string route = "/chairs";

            // Act
            (HttpResponseMessage httpResponse, _) = await _testContext.ExecutePostAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);
        }

        [Fact]
        public async Task Cannot_update_resource()
        {
            // Arrange
            Chair existingChair = _fakers.Chair.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Chairs.Add(existingChair);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "chairs",
                    id = existingChair.StringId,
                    attributes = new
                    {
                    }
                }
            };

            string route = "/chairs/" + existingChair.StringId;

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.MethodNotAllowed);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
            error.Title.Should().Be("The request method is not allowed.");
            error.Detail.Should().Be("Resource does not support PATCH requests.");
        }

        [Fact]
        public async Task Can_delete_resource()
        {
            // Arrange
            Chair existingChair = _fakers.Chair.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Chairs.Add(existingChair);
                await dbContext.SaveChangesAsync();
            });

            string route = "/chairs/" + existingChair.StringId;

            // Act
            (HttpResponseMessage httpResponse, _) = await _testContext.ExecuteDeleteAsync<string>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);
        }
    }
}
