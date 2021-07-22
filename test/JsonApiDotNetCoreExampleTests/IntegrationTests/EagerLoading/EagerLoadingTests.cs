using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.EntityFrameworkCore;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.EagerLoading
{
    public sealed class EagerLoadingTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<EagerLoadingDbContext>, EagerLoadingDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<EagerLoadingDbContext>, EagerLoadingDbContext> _testContext;
        private readonly EagerLoadingFakers _fakers = new();

        public EagerLoadingTests(ExampleIntegrationTestContext<TestableStartup<EagerLoadingDbContext>, EagerLoadingDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<StreetsController>();
            testContext.UseController<StatesController>();
            testContext.UseController<BuildingsController>();

            testContext.ConfigureServicesAfterStartup(services =>
            {
                services.AddResourceRepository<BuildingRepository>();
            });
        }

        [Fact]
        public async Task Can_get_primary_resource_with_eager_loads()
        {
            // Arrange
            Building building = _fakers.Building.Generate();
            building.Windows = _fakers.Window.Generate(4);
            building.PrimaryDoor = _fakers.Door.Generate();
            building.SecondaryDoor = _fakers.Door.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Buildings.Add(building);
                await dbContext.SaveChangesAsync();
            });

            string route = "/buildings/" + building.StringId;

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(building.StringId);
            responseDocument.SingleData.Attributes["number"].Should().Be(building.Number);
            responseDocument.SingleData.Attributes["windowCount"].Should().Be(4);
            responseDocument.SingleData.Attributes["primaryDoorColor"].Should().Be(building.PrimaryDoor.Color);
            responseDocument.SingleData.Attributes["secondaryDoorColor"].Should().Be(building.SecondaryDoor.Color);
        }

        [Fact]
        public async Task Can_get_primary_resource_with_nested_eager_loads()
        {
            // Arrange
            Street street = _fakers.Street.Generate();
            street.Buildings = _fakers.Building.Generate(2);

            street.Buildings[0].Windows = _fakers.Window.Generate(2);
            street.Buildings[0].PrimaryDoor = _fakers.Door.Generate();

            street.Buildings[1].Windows = _fakers.Window.Generate(3);
            street.Buildings[1].PrimaryDoor = _fakers.Door.Generate();
            street.Buildings[1].SecondaryDoor = _fakers.Door.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Streets.Add(street);
                await dbContext.SaveChangesAsync();
            });

            string route = "/streets/" + street.StringId;

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(street.StringId);
            responseDocument.SingleData.Attributes["name"].Should().Be(street.Name);
            responseDocument.SingleData.Attributes["buildingCount"].Should().Be(2);
            responseDocument.SingleData.Attributes["doorTotalCount"].Should().Be(3);
            responseDocument.SingleData.Attributes["windowTotalCount"].Should().Be(5);
        }

        [Fact]
        public async Task Can_get_primary_resource_with_fieldset()
        {
            // Arrange
            Street street = _fakers.Street.Generate();
            street.Buildings = _fakers.Building.Generate(1);
            street.Buildings[0].Windows = _fakers.Window.Generate(3);
            street.Buildings[0].PrimaryDoor = _fakers.Door.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Streets.Add(street);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/streets/{street.StringId}?fields[streets]=windowTotalCount";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(street.StringId);
            responseDocument.SingleData.Attributes.Should().HaveCount(1);
            responseDocument.SingleData.Attributes["windowTotalCount"].Should().Be(3);
            responseDocument.SingleData.Relationships.Should().BeNull();
        }

        [Fact]
        public async Task Can_get_primary_resource_with_includes()
        {
            // Arrange
            State state = _fakers.State.Generate();
            state.Cities = _fakers.City.Generate(1);
            state.Cities[0].Streets = _fakers.Street.Generate(1);
            state.Cities[0].Streets[0].Buildings = _fakers.Building.Generate(1);
            state.Cities[0].Streets[0].Buildings[0].PrimaryDoor = _fakers.Door.Generate();
            state.Cities[0].Streets[0].Buildings[0].Windows = _fakers.Window.Generate(3);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.States.Add(state);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/states/{state.StringId}?include=cities.streets";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(state.StringId);
            responseDocument.SingleData.Attributes["name"].Should().Be(state.Name);

            responseDocument.Included.Should().HaveCount(2);

            responseDocument.Included[0].Type.Should().Be("cities");
            responseDocument.Included[0].Id.Should().Be(state.Cities[0].StringId);
            responseDocument.Included[0].Attributes["name"].Should().Be(state.Cities[0].Name);

            responseDocument.Included[1].Type.Should().Be("streets");
            responseDocument.Included[1].Id.Should().Be(state.Cities[0].Streets[0].StringId);
            responseDocument.Included[1].Attributes["buildingCount"].Should().Be(1);
            responseDocument.Included[1].Attributes["doorTotalCount"].Should().Be(1);
            responseDocument.Included[1].Attributes["windowTotalCount"].Should().Be(3);
        }

        [Fact]
        public async Task Can_get_secondary_resources_with_include_and_fieldsets()
        {
            // Arrange
            State state = _fakers.State.Generate();
            state.Cities = _fakers.City.Generate(1);
            state.Cities[0].Streets = _fakers.Street.Generate(1);
            state.Cities[0].Streets[0].Buildings = _fakers.Building.Generate(1);
            state.Cities[0].Streets[0].Buildings[0].PrimaryDoor = _fakers.Door.Generate();
            state.Cities[0].Streets[0].Buildings[0].SecondaryDoor = _fakers.Door.Generate();
            state.Cities[0].Streets[0].Buildings[0].Windows = _fakers.Window.Generate(1);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.States.Add(state);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/states/{state.StringId}/cities?include=streets&fields[cities]=name&fields[streets]=doorTotalCount,windowTotalCount";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(state.Cities[0].StringId);
            responseDocument.ManyData[0].Attributes.Should().HaveCount(1);
            responseDocument.ManyData[0].Attributes["name"].Should().Be(state.Cities[0].Name);
            responseDocument.ManyData[0].Relationships.Should().BeNull();

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Type.Should().Be("streets");
            responseDocument.Included[0].Id.Should().Be(state.Cities[0].Streets[0].StringId);
            responseDocument.Included[0].Attributes.Should().HaveCount(2);
            responseDocument.Included[0].Attributes["doorTotalCount"].Should().Be(2);
            responseDocument.Included[0].Attributes["windowTotalCount"].Should().Be(1);
            responseDocument.Included[0].Relationships.Should().BeNull();
        }

        [Fact]
        public async Task Can_create_resource()
        {
            // Arrange
            Building newBuilding = _fakers.Building.Generate();

            var requestBody = new
            {
                data = new
                {
                    type = "buildings",
                    attributes = new
                    {
                        number = newBuilding.Number
                    }
                }
            };

            const string route = "/buildings";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Attributes["number"].Should().Be(newBuilding.Number);
            responseDocument.SingleData.Attributes["windowCount"].Should().Be(0);
            responseDocument.SingleData.Attributes["primaryDoorColor"].Should().BeNull();
            responseDocument.SingleData.Attributes["secondaryDoorColor"].Should().BeNull();

            int newBuildingId = int.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                // @formatter:wrap_chained_method_calls chop_always
                // @formatter:keep_existing_linebreaks true

                Building buildingInDatabase = await dbContext.Buildings
                    .Include(building => building.PrimaryDoor)
                    .Include(building => building.SecondaryDoor)
                    .Include(building => building.Windows)
                    .FirstWithIdOrDefaultAsync(newBuildingId);

                // @formatter:keep_existing_linebreaks restore
                // @formatter:wrap_chained_method_calls restore

                buildingInDatabase.Should().NotBeNull();
                buildingInDatabase.Number.Should().Be(newBuilding.Number);
                buildingInDatabase.PrimaryDoor.Should().NotBeNull();
                buildingInDatabase.SecondaryDoor.Should().BeNull();
                buildingInDatabase.Windows.Should().BeEmpty();
            });
        }

        [Fact]
        public async Task Can_update_resource()
        {
            // Arrange
            Building existingBuilding = _fakers.Building.Generate();
            existingBuilding.PrimaryDoor = _fakers.Door.Generate();
            existingBuilding.SecondaryDoor = _fakers.Door.Generate();
            existingBuilding.Windows = _fakers.Window.Generate(2);

            string newBuildingNumber = _fakers.Building.Generate().Number;
            string newPrimaryDoorColor = _fakers.Door.Generate().Color;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Buildings.Add(existingBuilding);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "buildings",
                    id = existingBuilding.StringId,
                    attributes = new
                    {
                        number = newBuildingNumber,
                        primaryDoorColor = newPrimaryDoorColor
                    }
                }
            };

            string route = "/buildings/" + existingBuilding.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                // @formatter:wrap_chained_method_calls chop_always
                // @formatter:keep_existing_linebreaks true

                Building buildingInDatabase = await dbContext.Buildings
                    .Include(building => building.PrimaryDoor)
                    .Include(building => building.SecondaryDoor)
                    .Include(building => building.Windows)
                    .FirstWithIdOrDefaultAsync(existingBuilding.Id);

                // @formatter:keep_existing_linebreaks restore
                // @formatter:wrap_chained_method_calls restore

                buildingInDatabase.Should().NotBeNull();
                buildingInDatabase.Number.Should().Be(newBuildingNumber);
                buildingInDatabase.PrimaryDoor.Should().NotBeNull();
                buildingInDatabase.PrimaryDoor.Color.Should().Be(newPrimaryDoorColor);
                buildingInDatabase.SecondaryDoor.Should().NotBeNull();
                buildingInDatabase.Windows.Should().HaveCount(2);
            });
        }

        [Fact]
        public async Task Can_delete_resource()
        {
            // Arrange
            Building existingBuilding = _fakers.Building.Generate();
            existingBuilding.PrimaryDoor = _fakers.Door.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Buildings.Add(existingBuilding);
                await dbContext.SaveChangesAsync();
            });

            string route = "/buildings/" + existingBuilding.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Building buildingInDatabase = await dbContext.Buildings.FirstWithIdOrDefaultAsync(existingBuilding.Id);

                buildingInDatabase.Should().BeNull();
            });
        }
    }
}
