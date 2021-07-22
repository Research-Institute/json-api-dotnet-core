using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExampleTests.Startups;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.CustomRoutes
{
    public sealed class CustomRouteTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<CustomRouteDbContext>, CustomRouteDbContext>>
    {
        private const string HostPrefix = "http://localhost";

        private readonly ExampleIntegrationTestContext<TestableStartup<CustomRouteDbContext>, CustomRouteDbContext> _testContext;
        private readonly CustomRouteFakers _fakers = new();

        public CustomRouteTests(ExampleIntegrationTestContext<TestableStartup<CustomRouteDbContext>, CustomRouteDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<CiviliansController>();
            testContext.UseController<TownsController>();
        }

        [Fact]
        public async Task Can_get_resource_at_custom_route()
        {
            // Arrange
            Town town = _fakers.Town.Generate();
            town.Civilians = _fakers.Civilian.Generate(1).ToHashSet();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Towns.Add(town);
                await dbContext.SaveChangesAsync();
            });

            string route = "/world-api/civilization/popular/towns/" + town.StringId;

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Type.Should().Be("towns");
            responseDocument.SingleData.Id.Should().Be(town.StringId);
            responseDocument.SingleData.Attributes["name"].Should().Be(town.Name);
            responseDocument.SingleData.Attributes["latitude"].Should().Be(town.Latitude);
            responseDocument.SingleData.Attributes["longitude"].Should().Be(town.Longitude);
            responseDocument.SingleData.Relationships["civilians"].Links.Self.Should().Be(HostPrefix + route + "/relationships/civilians");
            responseDocument.SingleData.Relationships["civilians"].Links.Related.Should().Be(HostPrefix + route + "/civilians");
            responseDocument.SingleData.Links.Self.Should().Be(HostPrefix + route);
            responseDocument.Links.Self.Should().Be(HostPrefix + route);
        }

        [Fact]
        public async Task Can_get_resources_at_custom_action_method()
        {
            // Arrange
            List<Town> town = _fakers.Town.Generate(7);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Town>();
                dbContext.Towns.AddRange(town);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/world-api/civilization/popular/towns/largest-5";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(5);
            responseDocument.ManyData.Should().OnlyContain(resourceObject => resourceObject.Type == "towns");
            responseDocument.ManyData.Should().OnlyContain(resourceObject => resourceObject.Attributes.Any());
            responseDocument.ManyData.Should().OnlyContain(resourceObject => resourceObject.Relationships.Any());
        }
    }
}
