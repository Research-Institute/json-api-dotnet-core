using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExample.Controllers;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.Extensions.DependencyInjection;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.AtomicOperations.Meta
{
    public sealed class AtomicResourceMetaTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext> _testContext;
        private readonly OperationsFakers _fakers = new();

        public AtomicResourceMetaTests(ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<OperationsController>();

            testContext.ConfigureServicesAfterStartup(services =>
            {
                services.AddResourceDefinition<MusicTrackMetaDefinition>();
                services.AddResourceDefinition<TextLanguageMetaDefinition>();

                services.AddSingleton<ResourceDefinitionHitCounter>();
            });

            var hitCounter = _testContext.Factory.Services.GetRequiredService<ResourceDefinitionHitCounter>();
            hitCounter.Reset();
        }

        [Fact]
        public async Task Returns_resource_meta_in_create_resource_with_side_effects()
        {
            // Arrange
            var hitCounter = _testContext.Factory.Services.GetRequiredService<ResourceDefinitionHitCounter>();

            string newTitle1 = _fakers.MusicTrack.Generate().Title;
            string newTitle2 = _fakers.MusicTrack.Generate().Title;

            var requestBody = new
            {
                atomic__operations = new[]
                {
                    new
                    {
                        op = "add",
                        data = new
                        {
                            type = "musicTracks",
                            attributes = new
                            {
                                title = newTitle1,
                                releasedAt = 1.January(2018)
                            }
                        }
                    },
                    new
                    {
                        op = "add",
                        data = new
                        {
                            type = "musicTracks",
                            attributes = new
                            {
                                title = newTitle2,
                                releasedAt = 23.August(1994)
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

            responseDocument.Results[0].SingleData.Meta.Should().HaveCount(1);
            responseDocument.Results[0].SingleData.Meta["Copyright"].Should().Be("(C) 2018. All rights reserved.");

            responseDocument.Results[1].SingleData.Meta.Should().HaveCount(1);
            responseDocument.Results[1].SingleData.Meta["Copyright"].Should().Be("(C) 1994. All rights reserved.");

            hitCounter.HitExtensibilityPoints.Should().BeEquivalentTo(new[]
            {
                (typeof(MusicTrack), ResourceDefinitionHitCounter.ExtensibilityPoint.GetMeta),
                (typeof(MusicTrack), ResourceDefinitionHitCounter.ExtensibilityPoint.GetMeta)
            }, options => options.WithStrictOrdering());
        }

        [Fact]
        public async Task Returns_resource_meta_in_update_resource_with_side_effects()
        {
            // Arrange
            var hitCounter = _testContext.Factory.Services.GetRequiredService<ResourceDefinitionHitCounter>();

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

            responseDocument.Results.Should().HaveCount(1);
            responseDocument.Results[0].SingleData.Meta.Should().HaveCount(1);
            responseDocument.Results[0].SingleData.Meta["Notice"].Should().Be(TextLanguageMetaDefinition.NoticeText);

            hitCounter.HitExtensibilityPoints.Should().BeEquivalentTo(new[]
            {
                (typeof(TextLanguage), ResourceDefinitionHitCounter.ExtensibilityPoint.GetMeta)
            }, options => options.WithStrictOrdering());
        }
    }
}
