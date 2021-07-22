using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Serialization.Objects;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.NamingConventions
{
    public sealed class KebabCasingTests : IClassFixture<ExampleIntegrationTestContext<KebabCasingConventionStartup<SwimmingDbContext>, SwimmingDbContext>>
    {
        private readonly ExampleIntegrationTestContext<KebabCasingConventionStartup<SwimmingDbContext>, SwimmingDbContext> _testContext;
        private readonly SwimmingFakers _fakers = new();

        public KebabCasingTests(ExampleIntegrationTestContext<KebabCasingConventionStartup<SwimmingDbContext>, SwimmingDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<DivingBoardsController>();
            testContext.UseController<SwimmingPoolsController>();
        }

        [Fact]
        public async Task Can_get_resources_with_include()
        {
            // Arrange
            List<SwimmingPool> pools = _fakers.SwimmingPool.Generate(2);
            pools[1].DivingBoards = _fakers.DivingBoard.Generate(1);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<SwimmingPool>();
                dbContext.SwimmingPools.AddRange(pools);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/public-api/swimming-pools?include=diving-boards";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(2);
            responseDocument.ManyData.Should().OnlyContain(resourceObject => resourceObject.Type == "swimming-pools");
            responseDocument.ManyData.Should().OnlyContain(resourceObject => resourceObject.Attributes.ContainsKey("is-indoor"));
            responseDocument.ManyData.Should().OnlyContain(resourceObject => resourceObject.Relationships.ContainsKey("water-slides"));
            responseDocument.ManyData.Should().OnlyContain(resourceObject => resourceObject.Relationships.ContainsKey("diving-boards"));

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Type.Should().Be("diving-boards");
            responseDocument.Included[0].Id.Should().Be(pools[1].DivingBoards[0].StringId);
            responseDocument.Included[0].Attributes["height-in-meters"].As<decimal>().Should().BeApproximately(pools[1].DivingBoards[0].HeightInMeters);
            responseDocument.Included[0].Relationships.Should().BeNull();
            responseDocument.Included[0].Links.Self.Should().Be($"/public-api/diving-boards/{pools[1].DivingBoards[0].StringId}");

            responseDocument.Meta["total-resources"].Should().Be(2);
        }

        [Fact]
        public async Task Can_filter_secondary_resources_with_sparse_fieldset()
        {
            // Arrange
            SwimmingPool pool = _fakers.SwimmingPool.Generate();
            pool.WaterSlides = _fakers.WaterSlide.Generate(2);
            pool.WaterSlides[0].LengthInMeters = 1;
            pool.WaterSlides[1].LengthInMeters = 5;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.SwimmingPools.Add(pool);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/public-api/swimming-pools/{pool.StringId}/water-slides" +
                "?filter=greaterThan(length-in-meters,'1')&fields[water-slides]=length-in-meters";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Type.Should().Be("water-slides");
            responseDocument.ManyData[0].Id.Should().Be(pool.WaterSlides[1].StringId);
            responseDocument.ManyData[0].Attributes.Should().HaveCount(1);
        }

        [Fact]
        public async Task Can_create_resource()
        {
            // Arrange
            SwimmingPool newPool = _fakers.SwimmingPool.Generate();

            var requestBody = new
            {
                data = new
                {
                    type = "swimming-pools",
                    attributes = new Dictionary<string, object>
                    {
                        ["is-indoor"] = newPool.IsIndoor
                    }
                }
            };

            const string route = "/public-api/swimming-pools";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Type.Should().Be("swimming-pools");
            responseDocument.SingleData.Attributes["is-indoor"].Should().Be(newPool.IsIndoor);

            int newPoolId = int.Parse(responseDocument.SingleData.Id);
            string poolLink = route + $"/{newPoolId}";

            responseDocument.SingleData.Relationships.Should().NotBeEmpty();
            responseDocument.SingleData.Relationships["water-slides"].Links.Self.Should().Be(poolLink + "/relationships/water-slides");
            responseDocument.SingleData.Relationships["water-slides"].Links.Related.Should().Be(poolLink + "/water-slides");
            responseDocument.SingleData.Relationships["diving-boards"].Links.Self.Should().Be(poolLink + "/relationships/diving-boards");
            responseDocument.SingleData.Relationships["diving-boards"].Links.Related.Should().Be(poolLink + "/diving-boards");

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                SwimmingPool poolInDatabase = await dbContext.SwimmingPools.FirstWithIdAsync(newPoolId);

                poolInDatabase.IsIndoor.Should().Be(newPool.IsIndoor);
            });
        }

        [Fact]
        public async Task Applies_casing_convention_on_error_stack_trace()
        {
            // Arrange
            const string requestBody = "{ \"data\": {";

            const string route = "/public-api/swimming-pools";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Failed to deserialize request body.");
            error.Meta.Data.Should().ContainKey("stack-trace");
        }

        [Fact]
        public async Task Applies_casing_convention_on_source_pointer_from_ModelState()
        {
            // Arrange
            DivingBoard existingBoard = _fakers.DivingBoard.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.DivingBoards.Add(existingBoard);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "diving-boards",
                    id = existingBoard.StringId,
                    attributes = new Dictionary<string, object>
                    {
                        ["height-in-meters"] = -1
                    }
                }
            };

            string route = "/public-api/diving-boards/" + existingBoard.StringId;

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Input validation failed.");
            error.Detail.Should().Be("The field HeightInMeters must be between 1 and 20.");
            error.Source.Pointer.Should().Be("/data/attributes/height-in-meters");
        }
    }
}
