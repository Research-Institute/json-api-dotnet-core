using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExample.Controllers;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.EntityFrameworkCore;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.AtomicOperations.Creating
{
    public sealed class AtomicCreateResourceWithToManyRelationshipTests
        : IClassFixture<ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext> _testContext;
        private readonly OperationsFakers _fakers = new();

        public AtomicCreateResourceWithToManyRelationshipTests(
            ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<OperationsController>();

            // These routes need to be registered in ASP.NET for rendering links to resource/relationship endpoints.
            testContext.UseController<PlaylistsController>();
            testContext.UseController<MusicTracksController>();
        }

        [Fact]
        public async Task Can_create_HasMany_relationship()
        {
            // Arrange
            List<Performer> existingPerformers = _fakers.Performer.Generate(2);
            string newTitle = _fakers.MusicTrack.Generate().Title;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Performers.AddRange(existingPerformers);
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
                                title = newTitle
                            },
                            relationships = new
                            {
                                performers = new
                                {
                                    data = new[]
                                    {
                                        new
                                        {
                                            type = "performers",
                                            id = existingPerformers[0].StringId
                                        },
                                        new
                                        {
                                            type = "performers",
                                            id = existingPerformers[1].StringId
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
                MusicTrack trackInDatabase = await dbContext.MusicTracks.Include(musicTrack => musicTrack.Performers).FirstWithIdAsync(newTrackId);

                trackInDatabase.Performers.Should().HaveCount(2);
                trackInDatabase.Performers.Should().ContainSingle(performer => performer.Id == existingPerformers[0].Id);
                trackInDatabase.Performers.Should().ContainSingle(performer => performer.Id == existingPerformers[1].Id);
            });
        }

        [Fact]
        public async Task Can_create_HasManyThrough_relationship()
        {
            // Arrange
            List<MusicTrack> existingTracks = _fakers.MusicTrack.Generate(3);
            string newName = _fakers.Playlist.Generate().Name;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.MusicTracks.AddRange(existingTracks);
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
                            type = "playlists",
                            attributes = new
                            {
                                name = newName
                            },
                            relationships = new
                            {
                                tracks = new
                                {
                                    data = new[]
                                    {
                                        new
                                        {
                                            type = "musicTracks",
                                            id = existingTracks[0].StringId
                                        },
                                        new
                                        {
                                            type = "musicTracks",
                                            id = existingTracks[1].StringId
                                        },
                                        new
                                        {
                                            type = "musicTracks",
                                            id = existingTracks[2].StringId
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
            (HttpResponseMessage httpResponse, AtomicOperationsDocument responseDocument) =
                await _testContext.ExecutePostAtomicAsync<AtomicOperationsDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Results.Should().HaveCount(1);
            responseDocument.Results[0].SingleData.Should().NotBeNull();
            responseDocument.Results[0].SingleData.Type.Should().Be("playlists");
            responseDocument.Results[0].SingleData.Attributes.Should().NotBeEmpty();
            responseDocument.Results[0].SingleData.Relationships.Should().NotBeEmpty();

            long newPlaylistId = long.Parse(responseDocument.Results[0].SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                // @formatter:wrap_chained_method_calls chop_always
                // @formatter:keep_existing_linebreaks true

                Playlist playlistInDatabase = await dbContext.Playlists
                    .Include(playlist => playlist.PlaylistMusicTracks)
                    .ThenInclude(playlistMusicTrack => playlistMusicTrack.MusicTrack)
                    .FirstWithIdAsync(newPlaylistId);

                // @formatter:keep_existing_linebreaks restore
                // @formatter:wrap_chained_method_calls restore

                playlistInDatabase.PlaylistMusicTracks.Should().HaveCount(3);
                playlistInDatabase.PlaylistMusicTracks.Should().ContainSingle(playlistMusicTrack => playlistMusicTrack.MusicTrack.Id == existingTracks[0].Id);
                playlistInDatabase.PlaylistMusicTracks.Should().ContainSingle(playlistMusicTrack => playlistMusicTrack.MusicTrack.Id == existingTracks[1].Id);
                playlistInDatabase.PlaylistMusicTracks.Should().ContainSingle(playlistMusicTrack => playlistMusicTrack.MusicTrack.Id == existingTracks[2].Id);
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
                                performers = new
                                {
                                    data = new[]
                                    {
                                        new
                                        {
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
            error.Title.Should().Be("Failed to deserialize request body: Request body must include 'type' element.");
            error.Detail.Should().Be("Expected 'type' element in 'performers' relationship.");
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
                                performers = new
                                {
                                    data = new[]
                                    {
                                        new
                                        {
                                            type = "doesNotExist",
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
                                performers = new
                                {
                                    data = new[]
                                    {
                                        new
                                        {
                                            type = "performers"
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
            error.Title.Should().Be("Failed to deserialize request body: Request body must include 'id' or 'lid' element.");
            error.Detail.Should().Be("Expected 'id' or 'lid' element in 'performers' relationship.");
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }

        [Fact]
        public async Task Cannot_create_for_unknown_relationship_IDs()
        {
            // Arrange
            string newTitle = _fakers.MusicTrack.Generate().Title;

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
                                title = newTitle
                            },
                            relationships = new
                            {
                                performers = new
                                {
                                    data = new[]
                                    {
                                        new
                                        {
                                            type = "performers",
                                            id = 12345678
                                        },
                                        new
                                        {
                                            type = "performers",
                                            id = 87654321
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
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);

            responseDocument.Errors.Should().HaveCount(2);

            Error error1 = responseDocument.Errors[0];
            error1.StatusCode.Should().Be(HttpStatusCode.NotFound);
            error1.Title.Should().Be("A related resource does not exist.");
            error1.Detail.Should().Be("Related resource of type 'performers' with ID '12345678' in relationship 'performers' does not exist.");
            error1.Source.Pointer.Should().Be("/atomic:operations[0]");

            Error error2 = responseDocument.Errors[1];
            error2.StatusCode.Should().Be(HttpStatusCode.NotFound);
            error2.Title.Should().Be("A related resource does not exist.");
            error2.Detail.Should().Be("Related resource of type 'performers' with ID '87654321' in relationship 'performers' does not exist.");
            error2.Source.Pointer.Should().Be("/atomic:operations[0]");
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
                                performers = new
                                {
                                    data = new[]
                                    {
                                        new
                                        {
                                            type = "playlists",
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
            error.Title.Should().Be("Failed to deserialize request body: Relationship contains incompatible resource type.");
            error.Detail.Should().Be("Relationship 'performers' contains incompatible resource type 'playlists'.");
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }

        [Fact]
        public async Task Can_create_with_duplicates()
        {
            // Arrange
            Performer existingPerformer = _fakers.Performer.Generate();
            string newTitle = _fakers.MusicTrack.Generate().Title;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Performers.Add(existingPerformer);
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
                                title = newTitle
                            },
                            relationships = new
                            {
                                performers = new
                                {
                                    data = new[]
                                    {
                                        new
                                        {
                                            type = "performers",
                                            id = existingPerformer.StringId
                                        },
                                        new
                                        {
                                            type = "performers",
                                            id = existingPerformer.StringId
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
                MusicTrack trackInDatabase = await dbContext.MusicTracks.Include(musicTrack => musicTrack.Performers).FirstWithIdAsync(newTrackId);

                trackInDatabase.Performers.Should().HaveCount(1);
                trackInDatabase.Performers[0].Id.Should().Be(existingPerformer.Id);
            });
        }

        [Fact]
        public async Task Cannot_create_with_null_data_in_HasMany_relationship()
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
                                performers = new
                                {
                                    data = (object)null
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
            error.Title.Should().Be("Failed to deserialize request body: Expected data[] element for to-many relationship.");
            error.Detail.Should().Be("Expected data[] element for 'performers' relationship.");
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }

        [Fact]
        public async Task Cannot_create_with_null_data_in_HasManyThrough_relationship()
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
                            type = "playlists",
                            relationships = new
                            {
                                tracks = new
                                {
                                    data = (object)null
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
            error.Title.Should().Be("Failed to deserialize request body: Expected data[] element for to-many relationship.");
            error.Detail.Should().Be("Expected data[] element for 'tracks' relationship.");
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }
    }
}
