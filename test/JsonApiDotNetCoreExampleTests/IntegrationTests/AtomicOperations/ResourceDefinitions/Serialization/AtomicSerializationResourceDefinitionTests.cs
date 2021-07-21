using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExample.Controllers;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.AtomicOperations.ResourceDefinitions.Serialization
{
    public sealed class AtomicSerializationResourceDefinitionTests
        : IClassFixture<ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext> _testContext;
        private readonly OperationsFakers _fakers = new();

        public AtomicSerializationResourceDefinitionTests(ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<OperationsController>();

            testContext.ConfigureServicesAfterStartup(services =>
            {
                services.AddResourceDefinition<RecordCompanyDefinition>();

                services.AddSingleton<ResourceDefinitionHitCounter>();
                services.AddScoped(typeof(IResourceChangeTracker<>), typeof(NeverSameResourceChangeTracker<>));
            });

            var hitCounter = _testContext.Factory.Services.GetRequiredService<ResourceDefinitionHitCounter>();
            hitCounter.Reset();
        }

        [Fact]
        public async Task Transforms_on_create_resource_with_side_effects()
        {
            // Arrange
            var hitCounter = _testContext.Factory.Services.GetRequiredService<ResourceDefinitionHitCounter>();

            List<RecordCompany> newCompanies = _fakers.RecordCompany.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<RecordCompany>();
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
                            type = "recordCompanies",
                            attributes = new
                            {
                                name = newCompanies[0].Name,
                                countryOfResidence = newCompanies[0].CountryOfResidence
                            }
                        }
                    },
                    new
                    {
                        op = "add",
                        data = new
                        {
                            type = "recordCompanies",
                            attributes = new
                            {
                                name = newCompanies[1].Name,
                                countryOfResidence = newCompanies[1].CountryOfResidence
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

            responseDocument.Results[0].SingleData.Attributes["name"].Should().Be(newCompanies[0].Name.ToUpperInvariant());
            responseDocument.Results[0].SingleData.Attributes["countryOfResidence"].Should().Be(newCompanies[0].CountryOfResidence.ToUpperInvariant());

            responseDocument.Results[1].SingleData.Attributes["name"].Should().Be(newCompanies[1].Name.ToUpperInvariant());
            responseDocument.Results[1].SingleData.Attributes["countryOfResidence"].Should().Be(newCompanies[1].CountryOfResidence.ToUpperInvariant());

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                List<RecordCompany> companiesInDatabase = await dbContext.RecordCompanies.ToListAsync();
                companiesInDatabase.Should().HaveCount(2);

                companiesInDatabase[0].Name.Should().Be(newCompanies[0].Name.ToUpperInvariant());
                companiesInDatabase[0].CountryOfResidence.Should().Be(newCompanies[0].CountryOfResidence);

                companiesInDatabase[1].Name.Should().Be(newCompanies[1].Name.ToUpperInvariant());
                companiesInDatabase[1].CountryOfResidence.Should().Be(newCompanies[1].CountryOfResidence);
            });

            hitCounter.HitExtensibilityPoints.Should().BeEquivalentTo(new[]
            {
                (typeof(RecordCompany), ResourceDefinitionHitCounter.ExtensibilityPoint.OnDeserialize),
                (typeof(RecordCompany), ResourceDefinitionHitCounter.ExtensibilityPoint.OnDeserialize),
                (typeof(RecordCompany), ResourceDefinitionHitCounter.ExtensibilityPoint.OnSerialize),
                (typeof(RecordCompany), ResourceDefinitionHitCounter.ExtensibilityPoint.OnSerialize)
            }, options => options.WithStrictOrdering());
        }

        [Fact]
        public async Task Skips_on_create_resource_with_ToOne_relationship()
        {
            // Arrange
            var hitCounter = _testContext.Factory.Services.GetRequiredService<ResourceDefinitionHitCounter>();

            RecordCompany existingCompany = _fakers.RecordCompany.Generate();

            string newTrackTitle = _fakers.MusicTrack.Generate().Title;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.RecordCompanies.Add(existingCompany);
                await dbContext.SaveChangesAsync();
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
                            type = "musicTracks",
                            attributes = new
                            {
                                title = newTrackTitle
                            },
                            relationships = new
                            {
                                ownedBy = new
                                {
                                    data = new
                                    {
                                        type = "recordCompanies",
                                        id = existingCompany.StringId
                                    }
                                }
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

            hitCounter.HitExtensibilityPoints.Should().BeEmpty();
        }

        [Fact]
        public async Task Transforms_on_update_resource_with_side_effects()
        {
            // Arrange
            var hitCounter = _testContext.Factory.Services.GetRequiredService<ResourceDefinitionHitCounter>();

            List<RecordCompany> existingCompanies = _fakers.RecordCompany.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<RecordCompany>();
                dbContext.RecordCompanies.AddRange(existingCompanies);
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
                            type = "recordCompanies",
                            id = existingCompanies[0].StringId,
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
                            id = existingCompanies[1].StringId,
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

            responseDocument.Results[0].SingleData.Attributes["name"].Should().Be(existingCompanies[0].Name);
            responseDocument.Results[0].SingleData.Attributes["countryOfResidence"].Should().Be(existingCompanies[0].CountryOfResidence.ToUpperInvariant());

            responseDocument.Results[1].SingleData.Attributes["name"].Should().Be(existingCompanies[1].Name);
            responseDocument.Results[1].SingleData.Attributes["countryOfResidence"].Should().Be(existingCompanies[1].CountryOfResidence.ToUpperInvariant());

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                List<RecordCompany> companiesInDatabase = await dbContext.RecordCompanies.ToListAsync();
                companiesInDatabase.Should().HaveCount(2);

                companiesInDatabase[0].Name.Should().Be(existingCompanies[0].Name);
                companiesInDatabase[0].CountryOfResidence.Should().Be(existingCompanies[0].CountryOfResidence);

                companiesInDatabase[1].Name.Should().Be(existingCompanies[1].Name);
                companiesInDatabase[1].CountryOfResidence.Should().Be(existingCompanies[1].CountryOfResidence);
            });

            hitCounter.HitExtensibilityPoints.Should().BeEquivalentTo(new[]
            {
                (typeof(RecordCompany), ResourceDefinitionHitCounter.ExtensibilityPoint.OnDeserialize),
                (typeof(RecordCompany), ResourceDefinitionHitCounter.ExtensibilityPoint.OnDeserialize),
                (typeof(RecordCompany), ResourceDefinitionHitCounter.ExtensibilityPoint.OnSerialize),
                (typeof(RecordCompany), ResourceDefinitionHitCounter.ExtensibilityPoint.OnSerialize)
            }, options => options.WithStrictOrdering());
        }

        [Fact]
        public async Task Skips_on_update_resource_with_ToOne_relationship()
        {
            // Arrange
            var hitCounter = _testContext.Factory.Services.GetRequiredService<ResourceDefinitionHitCounter>();

            MusicTrack existingTrack = _fakers.MusicTrack.Generate();
            RecordCompany existingCompany = _fakers.RecordCompany.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddRange(existingTrack, existingCompany);
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
                            type = "musicTracks",
                            id = existingTrack.StringId,
                            attributes = new
                            {
                            },
                            relationships = new
                            {
                                ownedBy = new
                                {
                                    data = new
                                    {
                                        type = "recordCompanies",
                                        id = existingCompany.StringId
                                    }
                                }
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

            hitCounter.HitExtensibilityPoints.Should().BeEmpty();
        }

        [Fact]
        public async Task Skips_on_update_ToOne_relationship()
        {
            // Arrange
            var hitCounter = _testContext.Factory.Services.GetRequiredService<ResourceDefinitionHitCounter>();

            MusicTrack existingTrack = _fakers.MusicTrack.Generate();
            RecordCompany existingCompany = _fakers.RecordCompany.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddRange(existingTrack, existingCompany);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                atomic__operations = new[]
                {
                    new
                    {
                        op = "update",
                        @ref = new
                        {
                            type = "musicTracks",
                            id = existingTrack.StringId,
                            relationship = "ownedBy"
                        },
                        data = new
                        {
                            type = "recordCompanies",
                            id = existingCompany.StringId
                        }
                    }
                }
            };

            const string route = "/operations";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePostAtomicAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            hitCounter.HitExtensibilityPoints.Should().BeEmpty();
        }
    }
}
