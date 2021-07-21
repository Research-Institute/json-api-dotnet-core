using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCoreExample.Controllers;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.Extensions.DependencyInjection;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.AtomicOperations.Mixed
{
    public sealed class AtomicSerializationTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext> _testContext;
        private readonly OperationsFakers _fakers = new();

        public AtomicSerializationTests(ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<OperationsController>();

            // These routes need to be registered in ASP.NET for rendering links to resource/relationship endpoints.
            testContext.UseController<PerformersController>();

            testContext.ConfigureServicesAfterStartup(services =>
            {
                services.AddScoped(typeof(IResourceChangeTracker<>), typeof(NeverSameResourceChangeTracker<>));
            });

            var options = (JsonApiOptions)testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.IncludeJsonApiVersion = true;
            options.AllowClientGeneratedIds = true;
        }

        [Fact]
        public async Task Includes_version_with_ext_on_operations_endpoint()
        {
            // Arrange
            const int newArtistId = 12345;
            string newArtistName = _fakers.Performer.Generate().ArtistName;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Performer>();
            });

            var requestBody = new
            {
                atomic__operations = new[]
                {
                    new
                    {
                        op = "add",
                        data = new
                        {
                            type = "performers",
                            id = newArtistId,
                            attributes = new
                            {
                                artistName = newArtistName
                            }
                        }
                    }
                }
            };

            const string route = "/operations";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePostAtomicAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Should().BeJson(@"{
  ""jsonapi"": {
    ""version"": ""1.1"",
    ""ext"": [
      ""https://jsonapi.org/ext/atomic""
    ]
  },
  ""atomic:results"": [
    {
      ""data"": {
        ""type"": ""performers"",
        ""id"": """ + newArtistId + @""",
        ""attributes"": {
          ""artistName"": """ + newArtistName + @""",
          ""bornAt"": ""0001-01-01T01:00:00+01:00""
        },
        ""links"": {
          ""self"": ""http://localhost/performers/" + newArtistId + @"""
        }
      }
    }
  ]
}");
        }
    }
}
