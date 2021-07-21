using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.CompositeKeys
{
    public sealed class CompositeKeyTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<CompositeDbContext>, CompositeDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<CompositeDbContext>, CompositeDbContext> _testContext;

        public CompositeKeyTests(ExampleIntegrationTestContext<TestableStartup<CompositeDbContext>, CompositeDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<DealershipsController>();
            testContext.UseController<EnginesController>();
            testContext.UseController<CarsController>();

            testContext.ConfigureServicesAfterStartup(services =>
            {
                services.AddResourceRepository<CarRepository>();
            });

            var options = (JsonApiOptions)testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.AllowClientGeneratedIds = true;
        }

        [Fact]
        public async Task Can_filter_on_ID_in_primary_resources()
        {
            // Arrange
            var car = new Car
            {
                RegionId = 123,
                LicensePlate = "AA-BB-11"
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Car>();
                dbContext.Cars.Add(car);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/cars?filter=any(id,'123:AA-BB-11','999:XX-YY-22')";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(car.StringId);
        }

        [Fact]
        public async Task Can_get_primary_resource_by_ID()
        {
            // Arrange
            var car = new Car
            {
                RegionId = 123,
                LicensePlate = "AA-BB-11"
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Car>();
                dbContext.Cars.Add(car);
                await dbContext.SaveChangesAsync();
            });

            string route = "/cars/" + car.StringId;

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(car.StringId);
        }

        [Fact]
        public async Task Can_sort_on_ID()
        {
            // Arrange
            var car = new Car
            {
                RegionId = 123,
                LicensePlate = "AA-BB-11"
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Car>();
                dbContext.Cars.Add(car);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/cars?sort=id";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(car.StringId);
        }

        [Fact]
        public async Task Can_select_ID()
        {
            // Arrange
            var car = new Car
            {
                RegionId = 123,
                LicensePlate = "AA-BB-11"
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Car>();
                dbContext.Cars.Add(car);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/cars?fields[cars]=id";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(car.StringId);
        }

        [Fact]
        public async Task Can_create_resource()
        {
            // Arrange
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Car>();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "cars",
                    attributes = new
                    {
                        regionId = 123,
                        licensePlate = "AA-BB-11"
                    }
                }
            };

            const string route = "/cars";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePostAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Car carInDatabase = await dbContext.Cars.FirstOrDefaultAsync(car => car.RegionId == 123 && car.LicensePlate == "AA-BB-11");

                carInDatabase.Should().NotBeNull();
                carInDatabase.Id.Should().Be("123:AA-BB-11");
            });
        }

        [Fact]
        public async Task Can_create_OneToOne_relationship()
        {
            // Arrange
            var existingCar = new Car
            {
                RegionId = 123,
                LicensePlate = "AA-BB-11"
            };

            var existingEngine = new Engine
            {
                SerialCode = "1234567890"
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Car>();
                dbContext.AddRange(existingCar, existingEngine);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "engines",
                    id = existingEngine.StringId,
                    relationships = new
                    {
                        car = new
                        {
                            data = new
                            {
                                type = "cars",
                                id = existingCar.StringId
                            }
                        }
                    }
                }
            };

            string route = "/engines/" + existingEngine.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Engine engineInDatabase = await dbContext.Engines.Include(engine => engine.Car).FirstWithIdAsync(existingEngine.Id);

                engineInDatabase.Car.Should().NotBeNull();
                engineInDatabase.Car.Id.Should().Be(existingCar.StringId);
            });
        }

        [Fact]
        public async Task Can_clear_OneToOne_relationship()
        {
            // Arrange
            var existingEngine = new Engine
            {
                SerialCode = "1234567890",
                Car = new Car
                {
                    RegionId = 123,
                    LicensePlate = "AA-BB-11"
                }
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Car>();
                dbContext.Engines.Add(existingEngine);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "engines",
                    id = existingEngine.StringId,
                    relationships = new
                    {
                        car = new
                        {
                            data = (object)null
                        }
                    }
                }
            };

            string route = "/engines/" + existingEngine.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Engine engineInDatabase = await dbContext.Engines.Include(engine => engine.Car).FirstWithIdAsync(existingEngine.Id);

                engineInDatabase.Car.Should().BeNull();
            });
        }

        [Fact]
        public async Task Can_remove_from_OneToMany_relationship()
        {
            // Arrange
            var existingDealership = new Dealership
            {
                Address = "Dam 1, 1012JS Amsterdam, the Netherlands",
                Inventory = new HashSet<Car>
                {
                    new()
                    {
                        RegionId = 123,
                        LicensePlate = "AA-BB-11"
                    },
                    new()
                    {
                        RegionId = 456,
                        LicensePlate = "CC-DD-22"
                    }
                }
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Car>();
                dbContext.Dealerships.Add(existingDealership);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "cars",
                        id = "123:AA-BB-11"
                    }
                }
            };

            string route = $"/dealerships/{existingDealership.StringId}/relationships/inventory";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Dealership dealershipInDatabase = await dbContext.Dealerships
                    .Include(dealership => dealership.Inventory).FirstWithIdOrDefaultAsync(existingDealership.Id);

                dealershipInDatabase.Inventory.Should().HaveCount(1);
                dealershipInDatabase.Inventory.Should().ContainSingle(car => car.Id == existingDealership.Inventory.ElementAt(1).Id);
            });
        }

        [Fact]
        public async Task Can_add_to_OneToMany_relationship()
        {
            // Arrange
            var existingDealership = new Dealership
            {
                Address = "Dam 1, 1012JS Amsterdam, the Netherlands"
            };

            var existingCar = new Car
            {
                RegionId = 123,
                LicensePlate = "AA-BB-11"
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Car>();
                dbContext.AddRange(existingDealership, existingCar);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "cars",
                        id = "123:AA-BB-11"
                    }
                }
            };

            string route = $"/dealerships/{existingDealership.StringId}/relationships/inventory";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePostAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Dealership dealershipInDatabase = await dbContext.Dealerships
                    .Include(dealership => dealership.Inventory).FirstWithIdOrDefaultAsync(existingDealership.Id);

                dealershipInDatabase.Inventory.Should().HaveCount(1);
                dealershipInDatabase.Inventory.Should().ContainSingle(car => car.Id == existingCar.Id);
            });
        }

        [Fact]
        public async Task Can_replace_OneToMany_relationship()
        {
            // Arrange
            var existingDealership = new Dealership
            {
                Address = "Dam 1, 1012JS Amsterdam, the Netherlands",
                Inventory = new HashSet<Car>
                {
                    new()
                    {
                        RegionId = 123,
                        LicensePlate = "AA-BB-11"
                    },
                    new()
                    {
                        RegionId = 456,
                        LicensePlate = "CC-DD-22"
                    }
                }
            };

            var existingCar = new Car
            {
                RegionId = 789,
                LicensePlate = "EE-FF-33"
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Car>();
                dbContext.AddRange(existingDealership, existingCar);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "cars",
                        id = "123:AA-BB-11"
                    },
                    new
                    {
                        type = "cars",
                        id = "789:EE-FF-33"
                    }
                }
            };

            string route = $"/dealerships/{existingDealership.StringId}/relationships/inventory";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Dealership dealershipInDatabase = await dbContext.Dealerships
                    .Include(dealership => dealership.Inventory).FirstWithIdOrDefaultAsync(existingDealership.Id);

                dealershipInDatabase.Inventory.Should().HaveCount(2);
                dealershipInDatabase.Inventory.Should().ContainSingle(car => car.Id == existingCar.Id);
                dealershipInDatabase.Inventory.Should().ContainSingle(car => car.Id == existingDealership.Inventory.ElementAt(0).Id);
            });
        }

        [Fact]
        public async Task Cannot_remove_from_ManyToOne_relationship_for_unknown_relationship_ID()
        {
            // Arrange
            var existingDealership = new Dealership
            {
                Address = "Dam 1, 1012JS Amsterdam, the Netherlands"
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Car>();
                dbContext.Dealerships.Add(existingDealership);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "cars",
                        id = "999:XX-YY-22"
                    }
                }
            };

            string route = $"/dealerships/{existingDealership.StringId}/relationships/inventory";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteDeleteAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.NotFound);
            error.Title.Should().Be("A related resource does not exist.");
            error.Detail.Should().Be("Related resource of type 'cars' with ID '999:XX-YY-22' in relationship 'inventory' does not exist.");
        }

        [Fact]
        public async Task Can_delete_resource()
        {
            // Arrange
            var existingCar = new Car
            {
                RegionId = 123,
                LicensePlate = "AA-BB-11"
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Car>();
                dbContext.Cars.Add(existingCar);
                await dbContext.SaveChangesAsync();
            });

            string route = "/cars/" + existingCar.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Car carInDatabase =
                    await dbContext.Cars.FirstOrDefaultAsync(car => car.RegionId == existingCar.RegionId && car.LicensePlate == existingCar.LicensePlate);

                carInDatabase.Should().BeNull();
            });
        }
    }
}
