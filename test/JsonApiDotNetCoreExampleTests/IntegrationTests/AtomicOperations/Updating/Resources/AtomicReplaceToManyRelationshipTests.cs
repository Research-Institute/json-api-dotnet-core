using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExample.Controllers;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.EntityFrameworkCore;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.AtomicOperations.Updating.Resources
{
    public sealed class AtomicReplaceToManyRelationshipTests
        : IClassFixture<ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext> _testContext;
        private readonly OperationsFakers _fakers = new();

        public AtomicReplaceToManyRelationshipTests(ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<OperationsController>();
        }

        [Fact]
        public async Task Can_clear_HasMany_relationship()
        {
            // Arrange
            MusicTrack existingTrack = _fakers.MusicTrack.Generate();
            existingTrack.Performers = _fakers.Performer.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Performer>();
                dbContext.MusicTracks.Add(existingTrack);
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
                            relationships = new
                            {
                                performers = new
                                {
                                    data = new object[0]
                                }
                            }
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

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                MusicTrack trackInDatabase = await dbContext.MusicTracks.Include(musicTrack => musicTrack.Performers).FirstWithIdAsync(existingTrack.Id);

                trackInDatabase.Performers.Should().BeEmpty();

                List<Performer> performersInDatabase = await dbContext.Performers.ToListAsync();
                performersInDatabase.Should().HaveCount(2);
            });
        }

        [Fact]
        public async Task Can_clear_HasManyThrough_relationship()
        {
            // Arrange
            Playlist existingPlaylist = _fakers.Playlist.Generate();

            existingPlaylist.PlaylistMusicTracks = new List<PlaylistMusicTrack>
            {
                new()
                {
                    MusicTrack = _fakers.MusicTrack.Generate()
                },
                new()
                {
                    MusicTrack = _fakers.MusicTrack.Generate()
                }
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<MusicTrack>();
                dbContext.Playlists.Add(existingPlaylist);
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
                            type = "playlists",
                            id = existingPlaylist.StringId,
                            relationships = new
                            {
                                tracks = new
                                {
                                    data = new object[0]
                                }
                            }
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

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                // @formatter:wrap_chained_method_calls chop_always
                // @formatter:keep_existing_linebreaks true

                Playlist playlistInDatabase = await dbContext.Playlists
                    .Include(playlist => playlist.PlaylistMusicTracks)
                    .ThenInclude(playlistMusicTrack => playlistMusicTrack.MusicTrack)
                    .FirstWithIdAsync(existingPlaylist.Id);

                // @formatter:keep_existing_linebreaks restore
                // @formatter:wrap_chained_method_calls restore

                playlistInDatabase.PlaylistMusicTracks.Should().BeEmpty();

                List<MusicTrack> tracksInDatabase = await dbContext.MusicTracks.ToListAsync();
                tracksInDatabase.Should().HaveCount(2);
            });
        }

        [Fact]
        public async Task Can_replace_HasMany_relationship()
        {
            // Arrange
            MusicTrack existingTrack = _fakers.MusicTrack.Generate();
            existingTrack.Performers = _fakers.Performer.Generate(1);

            List<Performer> existingPerformers = _fakers.Performer.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Performer>();
                dbContext.MusicTracks.Add(existingTrack);
                dbContext.Performers.AddRange(existingPerformers);
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
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePostAtomicAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                MusicTrack trackInDatabase = await dbContext.MusicTracks.Include(musicTrack => musicTrack.Performers).FirstWithIdAsync(existingTrack.Id);

                trackInDatabase.Performers.Should().HaveCount(2);
                trackInDatabase.Performers.Should().ContainSingle(performer => performer.Id == existingPerformers[0].Id);
                trackInDatabase.Performers.Should().ContainSingle(performer => performer.Id == existingPerformers[1].Id);

                List<Performer> performersInDatabase = await dbContext.Performers.ToListAsync();
                performersInDatabase.Should().HaveCount(3);
            });
        }

        [Fact]
        public async Task Can_replace_HasManyThrough_relationship()
        {
            // Arrange
            Playlist existingPlaylist = _fakers.Playlist.Generate();

            existingPlaylist.PlaylistMusicTracks = new List<PlaylistMusicTrack>
            {
                new()
                {
                    MusicTrack = _fakers.MusicTrack.Generate()
                }
            };

            List<MusicTrack> existingTracks = _fakers.MusicTrack.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<MusicTrack>();
                dbContext.Playlists.Add(existingPlaylist);
                dbContext.MusicTracks.AddRange(existingTracks);
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
                            type = "playlists",
                            id = existingPlaylist.StringId,
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
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePostAtomicAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                // @formatter:wrap_chained_method_calls chop_always
                // @formatter:keep_existing_linebreaks true

                Playlist playlistInDatabase = await dbContext.Playlists
                    .Include(playlist => playlist.PlaylistMusicTracks)
                    .ThenInclude(playlistMusicTrack => playlistMusicTrack.MusicTrack)
                    .FirstWithIdAsync(existingPlaylist.Id);

                // @formatter:keep_existing_linebreaks restore
                // @formatter:wrap_chained_method_calls restore

                playlistInDatabase.PlaylistMusicTracks.Should().HaveCount(2);
                playlistInDatabase.PlaylistMusicTracks.Should().ContainSingle(playlistMusicTrack => playlistMusicTrack.MusicTrack.Id == existingTracks[0].Id);
                playlistInDatabase.PlaylistMusicTracks.Should().ContainSingle(playlistMusicTrack => playlistMusicTrack.MusicTrack.Id == existingTracks[1].Id);

                List<MusicTrack> tracksInDatabase = await dbContext.MusicTracks.ToListAsync();
                tracksInDatabase.Should().HaveCount(3);
            });
        }

        [Fact]
        public async Task Cannot_replace_for_null_relationship_data()
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
                        op = "update",
                        data = new
                        {
                            type = "musicTracks",
                            id = existingTrack.StringId,
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
        public async Task Cannot_replace_for_missing_type_in_relationship_data()
        {
            // Arrange
            var requestBody = new
            {
                atomic__operations = new[]
                {
                    new
                    {
                        op = "update",
                        data = new
                        {
                            type = "playlists",
                            id = 99999999,
                            relationships = new
                            {
                                tracks = new
                                {
                                    data = new[]
                                    {
                                        new
                                        {
                                            id = Guid.NewGuid().ToString()
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
            error.Detail.Should().Be("Expected 'type' element in 'tracks' relationship.");
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }

        [Fact]
        public async Task Cannot_replace_for_unknown_type_in_relationship_data()
        {
            // Arrange
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
                            id = Guid.NewGuid().ToString(),
                            relationships = new
                            {
                                performers = new
                                {
                                    data = new[]
                                    {
                                        new
                                        {
                                            type = "doesNotExist",
                                            id = 99999999
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
        public async Task Cannot_replace_for_missing_ID_in_relationship_data()
        {
            // Arrange
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
                            id = Guid.NewGuid().ToString(),
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
        public async Task Cannot_replace_for_ID_and_local_ID_relationship_in_data()
        {
            // Arrange
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
                            id = Guid.NewGuid().ToString(),
                            relationships = new
                            {
                                performers = new
                                {
                                    data = new[]
                                    {
                                        new
                                        {
                                            type = "performers",
                                            id = 99999999,
                                            lid = "local-1"
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
        public async Task Cannot_replace_for_unknown_IDs_in_relationship_data()
        {
            // Arrange
            RecordCompany existingCompany = _fakers.RecordCompany.Generate();
            Guid[] trackIds = ArrayFactory.Create(Guid.NewGuid(), Guid.NewGuid());

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
                        op = "update",
                        data = new
                        {
                            type = "recordCompanies",
                            id = existingCompany.StringId,
                            relationships = new
                            {
                                tracks = new
                                {
                                    data = new[]
                                    {
                                        new
                                        {
                                            type = "musicTracks",
                                            id = trackIds[0].ToString()
                                        },
                                        new
                                        {
                                            type = "musicTracks",
                                            id = trackIds[1].ToString()
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
            error1.Detail.Should().Be($"Related resource of type 'musicTracks' with ID '{trackIds[0]}' in relationship 'tracks' does not exist.");
            error1.Source.Pointer.Should().Be("/atomic:operations[0]");

            Error error2 = responseDocument.Errors[1];
            error2.StatusCode.Should().Be(HttpStatusCode.NotFound);
            error2.Title.Should().Be("A related resource does not exist.");
            error2.Detail.Should().Be($"Related resource of type 'musicTracks' with ID '{trackIds[1]}' in relationship 'tracks' does not exist.");
            error2.Source.Pointer.Should().Be("/atomic:operations[0]");
        }

        [Fact]
        public async Task Cannot_create_for_relationship_mismatch()
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
                        op = "update",
                        data = new
                        {
                            type = "musicTracks",
                            id = existingTrack.StringId,
                            relationships = new
                            {
                                performers = new
                                {
                                    data = new[]
                                    {
                                        new
                                        {
                                            type = "playlists",
                                            id = 88888888
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
    }
}
