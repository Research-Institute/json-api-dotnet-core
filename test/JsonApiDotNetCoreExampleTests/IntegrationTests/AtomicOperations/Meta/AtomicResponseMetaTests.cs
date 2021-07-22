using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExample.Controllers;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.AtomicOperations.Meta
{
    public sealed class AtomicResponseMetaTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext> _testContext;
        private readonly OperationsFakers _fakers = new();

        public AtomicResponseMetaTests(ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<OperationsController>();

            testContext.ConfigureServicesAfterStartup(services =>
            {
                services.AddSingleton<IResponseMeta, AtomicResponseMeta>();
            });
        }

        [Fact]
        public async Task Returns_top_level_meta_in_create_resource_with_side_effects()
        {
            // Arrange
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
                            attributes = new
                            {
                            }
                        }
                    }
                }
            };

            const string route = "/operations";

            // Act
            (HttpResponseMessage httpResponse, AtomicOperationsDocument responseDocument) =
                await _testContext.ExecutePostAtomicAsync<AtomicOperationsDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Meta.Should().HaveCount(3);
            responseDocument.Meta["license"].Should().Be("MIT");
            responseDocument.Meta["projectUrl"].Should().Be("https://github.com/json-api-dotnet/JsonApiDotNetCore/");

            string[] versionArray = ((IEnumerable<JToken>)responseDocument.Meta["versions"]).Select(token => token.ToString()).ToArray();

            versionArray.Should().HaveCount(4);
            versionArray.Should().Contain("v4.0.0");
            versionArray.Should().Contain("v3.1.0");
            versionArray.Should().Contain("v2.5.2");
            versionArray.Should().Contain("v1.3.1");
        }

        [Fact]
        public async Task Returns_top_level_meta_in_update_resource_with_side_effects()
        {
            // Arrange
            TextLanguage existingLanguage = _fakers.TextLanguage.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.TextLanguages.Add(existingLanguage);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                atomic__operations = new[]
                {
                    new
                    {
                        op = "update",
                        data = new
                        {
                            type = "textLanguages",
                            id = existingLanguage.StringId,
                            attributes = new
                            {
                            }
                        }
                    }
                }
            };

            const string route = "/operations";

            // Act
            (HttpResponseMessage httpResponse, AtomicOperationsDocument responseDocument) =
                await _testContext.ExecutePostAtomicAsync<AtomicOperationsDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Meta.Should().HaveCount(3);
            responseDocument.Meta["license"].Should().Be("MIT");
            responseDocument.Meta["projectUrl"].Should().Be("https://github.com/json-api-dotnet/JsonApiDotNetCore/");

            string[] versionArray = ((IEnumerable<JToken>)responseDocument.Meta["versions"]).Select(token => token.ToString()).ToArray();

            versionArray.Should().HaveCount(4);
            versionArray.Should().Contain("v4.0.0");
            versionArray.Should().Contain("v3.1.0");
            versionArray.Should().Contain("v2.5.2");
            versionArray.Should().Contain("v1.3.1");
        }
    }
}
