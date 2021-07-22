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
    public sealed class HttpReadOnlyTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<RestrictionDbContext>, RestrictionDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<RestrictionDbContext>, RestrictionDbContext> _testContext;
        private readonly RestrictionFakers _fakers = new();

        public HttpReadOnlyTests(ExampleIntegrationTestContext<TestableStartup<RestrictionDbContext>, RestrictionDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<BlockingWritesController>();
        }

        [Fact]
        public async Task Can_get_resources()
        {
            // Arrange
            const string route = "/beds";

            // Act
            (HttpResponseMessage httpResponse, _) = await _testContext.ExecuteGetAsync<string>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);
        }

        [Fact]
        public async Task Cannot_create_resource()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "beds",
                    attributes = new
                    {
                    }
                }
            };

            const string route = "/beds";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.MethodNotAllowed);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
            error.Title.Should().Be("The request method is not allowed.");
            error.Detail.Should().Be("Resource does not support POST requests.");
        }

        [Fact]
        public async Task Cannot_update_resource()
        {
            // Arrange
            Bed existingBed = _fakers.Bed.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Beds.Add(existingBed);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "beds",
                    id = existingBed.StringId,
                    attributes = new
                    {
                    }
                }
            };

            string route = "/beds/" + existingBed.StringId;

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
        public async Task Cannot_delete_resource()
        {
            // Arrange
            Bed existingBed = _fakers.Bed.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Beds.Add(existingBed);
                await dbContext.SaveChangesAsync();
            });

            string route = "/beds/" + existingBed.StringId;

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteDeleteAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.MethodNotAllowed);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
            error.Title.Should().Be("The request method is not allowed.");
            error.Detail.Should().Be("Resource does not support DELETE requests.");
        }
    }
}
