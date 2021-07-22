using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExample.Controllers;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.AtomicOperations.Creating
{
    public sealed class AtomicCreateResourceWithToOneRelationshipTests
        : IClassFixture<ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext> _testContext;
        private readonly OperationsFakers _fakers = new();

        public AtomicCreateResourceWithToOneRelationshipTests(
            ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<OperationsController>();

            // These routes need to be registered in ASP.NET for rendering links to resource/relationship endpoints.
            testContext.UseController<MusicTracksController>();
            testContext.UseController<LyricsController>();
        }

        [Fact]
        public async Task Can_create_OneToOne_relationship_from_principal_side()
        {
            // Arrange
            MusicTrack existingTrack = _fakers.MusicTrack.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.MusicTracks.Add(existingTrack);
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
                            type = "lyrics",
                            relationships = new
                            {
                                track = new
                                {
                                    data = new
                                    {
                                        type = "musicTracks",
                                        id = existingTrack.StringId
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
            responseDocument.Results[0].SingleData.Should().NotBeNull();
            responseDocument.Results[0].SingleData.Type.Should().Be("lyrics");
            responseDocument.Results[0].SingleData.Attributes.Should().NotBeEmpty();
            responseDocument.Results[0].SingleData.Relationships.Should().NotBeEmpty();

            long newLyricId = long.Parse(responseDocument.Results[0].SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Lyric lyricInDatabase = await dbContext.Lyrics.Include(lyric => lyric.Track).FirstWithIdAsync(newLyricId);

                lyricInDatabase.Track.Should().NotBeNull();
                lyricInDatabase.Track.Id.Should().Be(existingTrack.Id);
            });
        }

        [Fact]
        public async Task Can_create_OneToOne_relationship_from_dependent_side()
        {
            // Arrange
            Lyric existingLyric = _fakers.Lyric.Generate();
            string newTrackTitle = _fakers.MusicTrack.Generate().Title;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Lyrics.Add(existingLyric);
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
                                lyric = new
                                {
                                    data = new
                                    {
                                        type = "lyrics",
                                        id = existingLyric.StringId
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
            responseDocument.Results[0].SingleData.Should().NotBeNull();
            responseDocument.Results[0].SingleData.Type.Should().Be("musicTracks");
            responseDocument.Results[0].SingleData.Attributes.Should().NotBeEmpty();
            responseDocument.Results[0].SingleData.Relationships.Should().NotBeEmpty();

            Guid newTrackId = Guid.Parse(responseDocument.Results[0].SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                MusicTrack trackInDatabase = await dbContext.MusicTracks.Include(musicTrack => musicTrack.Lyric).FirstWithIdAsync(newTrackId);

                trackInDatabase.Lyric.Should().NotBeNull();
                trackInDatabase.Lyric.Id.Should().Be(existingLyric.Id);
            });
        }

        [Fact]
        public async Task Can_create_resources_with_ToOne_relationship()
        {
            // Arrange
            const int elementCount = 5;

            RecordCompany existingCompany = _fakers.RecordCompany.Generate();
            string[] newTrackTitles = _fakers.MusicTrack.Generate(elementCount).Select(musicTrack => musicTrack.Title).ToArray();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.RecordCompanies.Add(existingCompany);
                await dbContext.SaveChangesAsync();
            });

            var operationElements = new List<object>(elementCount);

            for (int index = 0; index < elementCount; index++)
            {
                operationElements.Add(new
                {
                    op = "add",
                    data = new
                    {
                        type = "musicTracks",
                        attributes = new
                        {
                            title = newTrackTitles[index]
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
                });
            }

            var requestBody = new
            {
                atomic__operations = operationElements
            };

            const string route = "/operations";

            // Act
            (HttpResponseMessage httpResponse, AtomicOperationsDocument responseDocument) =
                await _testContext.ExecutePostAtomicAsync<AtomicOperationsDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Results.Should().HaveCount(elementCount);

            for (int index = 0; index < elementCount; index++)
            {
                responseDocument.Results[index].SingleData.Should().NotBeNull();
                responseDocument.Results[index].SingleData.Type.Should().Be("musicTracks");
                responseDocument.Results[index].SingleData.Attributes["title"].Should().Be(newTrackTitles[index]);
            }

            Guid[] newTrackIds = responseDocument.Results.Select(result => Guid.Parse(result.SingleData.Id)).ToArray();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                // @formatter:wrap_chained_method_calls chop_always
                // @formatter:keep_existing_linebreaks true

                List<MusicTrack> tracksInDatabase = await dbContext.MusicTracks
                    .Include(musicTrack => musicTrack.OwnedBy)
                    .Where(musicTrack => newTrackIds.Contains(musicTrack.Id))
                    .ToListAsync();

                // @formatter:keep_existing_linebreaks restore
                // @formatter:wrap_chained_method_calls restore

                tracksInDatabase.Should().HaveCount(elementCount);

                for (int index = 0; index < elementCount; index++)
                {
                    MusicTrack trackInDatabase = tracksInDatabase.Single(musicTrack => musicTrack.Id == newTrackIds[index]);

                    trackInDatabase.Title.Should().Be(newTrackTitles[index]);

                    trackInDatabase.OwnedBy.Should().NotBeNull();
                    trackInDatabase.OwnedBy.Id.Should().Be(existingCompany.Id);
                }
            });
        }

        [Fact]
        public async Task Cannot_create_for_missing_relationship_type()
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
                            type = "musicTracks",
                            relationships = new
                            {
                                lyric = new
                                {
                                    data = new
                                    {
                                        id = 12345678
                                    }
                                }
                            }
                        }
                    }
                }
            };

            const string route = "/operations";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAtomicAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Failed to deserialize request body: Request body must include 'type' element.");
            error.Detail.Should().Be("Expected 'type' element in 'lyric' relationship.");
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }

        [Fact]
        public async Task Cannot_create_for_unknown_relationship_type()
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
                            type = "musicTracks",
                            relationships = new
                            {
                                lyric = new
                                {
                                    data = new
                                    {
                                        type = "doesNotExist",
                                        id = 12345678
                                    }
                                }
                            }
                        }
                    }
                }
            };

            const string route = "/operations";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAtomicAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Failed to deserialize request body: Request body includes unknown resource type.");
            error.Detail.Should().Be("Resource type 'doesNotExist' does not exist.");
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }

        [Fact]
        public async Task Cannot_create_for_missing_relationship_ID()
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
                            type = "musicTracks",
                            relationships = new
                            {
                                lyric = new
                                {
                                    data = new
                                    {
                                        type = "lyrics"
                                    }
                                }
                            }
                        }
                    }
                }
            };

            const string route = "/operations";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAtomicAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Failed to deserialize request body: Request body must include 'id' or 'lid' element.");
            error.Detail.Should().Be("Expected 'id' or 'lid' element in 'lyric' relationship.");
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }

        [Fact]
        public async Task Cannot_create_with_unknown_relationship_ID()
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
                            type = "musicTracks",
                            relationships = new
                            {
                                lyric = new
                                {
                                    data = new
                                    {
                                        type = "lyrics",
                                        id = 12345678
                                    }
                                }
                            }
                        }
                    }
                }
            };

            const string route = "/operations";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAtomicAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.NotFound);
            error.Title.Should().Be("A related resource does not exist.");
            error.Detail.Should().Be("Related resource of type 'lyrics' with ID '12345678' in relationship 'lyric' does not exist.");
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }

        [Fact]
        public async Task Cannot_create_on_relationship_type_mismatch()
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
                            type = "musicTracks",
                            relationships = new
                            {
                                lyric = new
                                {
                                    data = new
                                    {
                                        type = "playlists",
                                        id = 12345678
                                    }
                                }
                            }
                        }
                    }
                }
            };

            const string route = "/operations";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAtomicAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Failed to deserialize request body: Relationship contains incompatible resource type.");
            error.Detail.Should().Be("Relationship 'lyric' contains incompatible resource type 'playlists'.");
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }

        [Fact]
        public async Task Can_create_resource_with_duplicate_relationship()
        {
            // Arrange
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
                                },
                                ownedBy_duplicate = new
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

            string requestBodyText = JsonConvert.SerializeObject(requestBody).Replace("ownedBy_duplicate", "ownedBy");

            const string route = "/operations";

            // Act
            (HttpResponseMessage httpResponse, AtomicOperationsDocument responseDocument) =
                await _testContext.ExecutePostAtomicAsync<AtomicOperationsDocument>(route, requestBodyText);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Results.Should().HaveCount(1);
            responseDocument.Results[0].SingleData.Should().NotBeNull();
            responseDocument.Results[0].SingleData.Type.Should().Be("musicTracks");
            responseDocument.Results[0].SingleData.Attributes.Should().NotBeEmpty();
            responseDocument.Results[0].SingleData.Relationships.Should().NotBeEmpty();

            Guid newTrackId = Guid.Parse(responseDocument.Results[0].SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                MusicTrack trackInDatabase = await dbContext.MusicTracks.Include(musicTrack => musicTrack.OwnedBy).FirstWithIdAsync(newTrackId);

                trackInDatabase.OwnedBy.Should().NotBeNull();
                trackInDatabase.OwnedBy.Id.Should().Be(existingCompany.Id);
            });
        }

        [Fact]
        public async Task Cannot_create_with_data_array_in_relationship()
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
                            type = "musicTracks",
                            relationships = new
                            {
                                lyric = new
                                {
                                    data = new[]
                                    {
                                        new
                                        {
                                            type = "lyrics",
                                            id = 12345678
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            const string route = "/operations";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAtomicAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Failed to deserialize request body: Expected single data element for to-one relationship.");
            error.Detail.Should().Be("Expected single data element for 'lyric' relationship.");
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }
    }
}
