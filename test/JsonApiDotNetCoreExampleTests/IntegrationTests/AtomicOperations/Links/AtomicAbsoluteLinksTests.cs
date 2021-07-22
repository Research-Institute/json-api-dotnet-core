using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExample.Controllers;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.Extensions.DependencyInjection;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.AtomicOperations.Links
{
    public sealed class AtomicAbsoluteLinksTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext>>
    {
        private const string HostPrefix = "http://localhost";

        private readonly ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext> _testContext;
        private readonly OperationsFakers _fakers = new();

        public AtomicAbsoluteLinksTests(ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<OperationsController>();

            // These routes need to be registered in ASP.NET for rendering links to resource/relationship endpoints.
            testContext.UseController<TextLanguagesController>();
            testContext.UseController<RecordCompaniesController>();

            testContext.ConfigureServicesAfterStartup(services =>
            {
                services.AddScoped(typeof(IResourceChangeTracker<>), typeof(NeverSameResourceChangeTracker<>));
            });
        }

        [Fact]
        public async Task Update_resource_with_side_effects_returns_absolute_links()
        {
            // Arrange
            TextLanguage existingLanguage = _fakers.TextLanguage.Generate();
            RecordCompany existingCompany = _fakers.RecordCompany.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddInRange(existingLanguage, existingCompany);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                atomic__operations = new object[]
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
                    },
                    new
                    {
                        op = "update",
                        data = new
                        {
                            type = "recordCompanies",
                            id = existingCompany.StringId,
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

            responseDocument.Results.Should().HaveCount(2);

            string languageLink = HostPrefix + "/textLanguages/" + existingLanguage.StringId;

            ResourceObject singleData1 = responseDocument.Results[0].SingleData;
            singleData1.Should().NotBeNull();
            singleData1.Links.Should().NotBeNull();
            singleData1.Links.Self.Should().Be(languageLink);
            singleData1.Relationships.Should().NotBeEmpty();
            singleData1.Relationships["lyrics"].Links.Should().NotBeNull();
            singleData1.Relationships["lyrics"].Links.Self.Should().Be(languageLink + "/relationships/lyrics");
            singleData1.Relationships["lyrics"].Links.Related.Should().Be(languageLink + "/lyrics");

            string companyLink = HostPrefix + "/recordCompanies/" + existingCompany.StringId;

            ResourceObject singleData2 = responseDocument.Results[1].SingleData;
            singleData2.Should().NotBeNull();
            singleData2.Links.Should().NotBeNull();
            singleData2.Links.Self.Should().Be(companyLink);
            singleData2.Relationships.Should().NotBeEmpty();
            singleData2.Relationships["tracks"].Links.Should().NotBeNull();
            singleData2.Relationships["tracks"].Links.Self.Should().Be(companyLink + "/relationships/tracks");
            singleData2.Relationships["tracks"].Links.Related.Should().Be(companyLink + "/tracks");
        }

        [Fact]
        public async Task Update_resource_with_side_effects_and_missing_resource_controller_hides_links()
        {
            // Arrange
            Playlist existingPlaylist = _fakers.Playlist.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Playlists.Add(existingPlaylist);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                atomic__operations = new object[]
                {
                    new
                    {
                        op = "update",
                        data = new
                        {
                            type = "playlists",
                            id = existingPlaylist.StringId,
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

            responseDocument.Results.Should().HaveCount(1);

            ResourceObject singleData = responseDocument.Results[0].SingleData;
            singleData.Should().NotBeNull();
            singleData.Links.Should().BeNull();
            singleData.Relationships.Should().BeNull();
        }
    }
}
