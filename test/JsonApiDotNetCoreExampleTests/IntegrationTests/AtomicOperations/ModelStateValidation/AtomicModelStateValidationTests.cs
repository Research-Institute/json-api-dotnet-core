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

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.AtomicOperations.ModelStateValidation
{
    public sealed class AtomicModelStateValidationTests
        : IClassFixture<ExampleIntegrationTestContext<ModelStateValidationStartup<OperationsDbContext>, OperationsDbContext>>
    {
        private readonly ExampleIntegrationTestContext<ModelStateValidationStartup<OperationsDbContext>, OperationsDbContext> _testContext;
        private readonly OperationsFakers _fakers = new();

        public AtomicModelStateValidationTests(ExampleIntegrationTestContext<ModelStateValidationStartup<OperationsDbContext>, OperationsDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<OperationsController>();
        }

        [Fact]
        public async Task Cannot_create_resource_with_multiple_violations()
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
                            attributes = new
                            {
                                lengthInSeconds = -1
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

            responseDocument.Errors.Should().HaveCount(2);

            Error error1 = responseDocument.Errors[0];
            error1.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error1.Title.Should().Be("Input validation failed.");
            error1.Detail.Should().Be("The Title field is required.");
            error1.Source.Pointer.Should().Be("/atomic:operations[0]/data/attributes/title");

            Error error2 = responseDocument.Errors[1];
            error2.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error2.Title.Should().Be("Input validation failed.");
            error2.Detail.Should().Be("The field LengthInSeconds must be between 1 and 1440.");
            error2.Source.Pointer.Should().Be("/atomic:operations[0]/data/attributes/lengthInSeconds");
        }

        [Fact]
        public async Task Can_create_resource_with_annotated_relationship()
        {
            // Arrange
            MusicTrack existingTrack = _fakers.MusicTrack.Generate();
            string newPlaylistName = _fakers.Playlist.Generate().Name;

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
                            type = "playlists",
                            attributes = new
                            {
                                name = newPlaylistName
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
                                            id = existingTrack.StringId
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

                playlistInDatabase.PlaylistMusicTracks.Should().HaveCount(1);
                playlistInDatabase.PlaylistMusicTracks[0].MusicTrack.Id.Should().Be(existingTrack.Id);
            });
        }

        [Fact]
        public async Task Cannot_update_resource_with_multiple_violations()
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
                            attributes = new
                            {
                                title = (string)null,
                                lengthInSeconds = -1
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

            responseDocument.Errors.Should().HaveCount(2);

            Error error1 = responseDocument.Errors[0];
            error1.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error1.Title.Should().Be("Input validation failed.");
            error1.Detail.Should().Be("The Title field is required.");
            error1.Source.Pointer.Should().Be("/atomic:operations[0]/data/attributes/title");

            Error error2 = responseDocument.Errors[1];
            error2.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error2.Title.Should().Be("Input validation failed.");
            error2.Detail.Should().Be("The field LengthInSeconds must be between 1 and 1440.");
            error2.Source.Pointer.Should().Be("/atomic:operations[0]/data/attributes/lengthInSeconds");
        }

        [Fact]
        public async Task Can_update_resource_with_omitted_required_attribute()
        {
            // Arrange
            MusicTrack existingTrack = _fakers.MusicTrack.Generate();
            string newTrackGenre = _fakers.MusicTrack.Generate().Genre;

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
                            attributes = new
                            {
                                genre = newTrackGenre
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
                MusicTrack trackInDatabase = await dbContext.MusicTracks.FirstWithIdAsync(existingTrack.Id);

                trackInDatabase.Title.Should().Be(existingTrack.Title);
                trackInDatabase.Genre.Should().Be(newTrackGenre);
            });
        }

        [Fact]
        public async Task Can_update_resource_with_annotated_relationship()
        {
            // Arrange
            Playlist existingPlaylist = _fakers.Playlist.Generate();
            MusicTrack existingTrack = _fakers.MusicTrack.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddRange(existingPlaylist, existingTrack);
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
                                            id = existingTrack.StringId
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

                playlistInDatabase.PlaylistMusicTracks.Should().HaveCount(1);
                playlistInDatabase.PlaylistMusicTracks[0].MusicTrack.Id.Should().Be(existingTrack.Id);
            });
        }

        [Fact]
        public async Task Can_update_ToOne_relationship()
        {
            // Arrange
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

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                MusicTrack trackInDatabase = await dbContext.MusicTracks.Include(musicTrack => musicTrack.OwnedBy).FirstWithIdAsync(existingTrack.Id);

                trackInDatabase.OwnedBy.Should().NotBeNull();
                trackInDatabase.OwnedBy.Id.Should().Be(existingCompany.Id);
            });
        }

        [Fact]
        public async Task Can_update_ToMany_relationship()
        {
            // Arrange
            Playlist existingPlaylist = _fakers.Playlist.Generate();
            MusicTrack existingTrack = _fakers.MusicTrack.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddRange(existingPlaylist, existingTrack);
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
                            type = "playlists",
                            id = existingPlaylist.StringId,
                            relationship = "tracks"
                        },
                        data = new[]
                        {
                            new
                            {
                                type = "musicTracks",
                                id = existingTrack.StringId
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

                playlistInDatabase.PlaylistMusicTracks.Should().HaveCount(1);
                playlistInDatabase.PlaylistMusicTracks[0].MusicTrack.Id.Should().Be(existingTrack.Id);
            });
        }

        [Fact]
        public async Task Validates_all_operations_before_execution_starts()
        {
            // Arrange
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
                            id = 99999999,
                            attributes = new
                            {
                                name = (string)null
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
                                lengthInSeconds = -1
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

            responseDocument.Errors.Should().HaveCount(3);

            Error error1 = responseDocument.Errors[0];
            error1.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error1.Title.Should().Be("Input validation failed.");
            error1.Detail.Should().Be("The Name field is required.");
            error1.Source.Pointer.Should().Be("/atomic:operations[0]/data/attributes/name");

            Error error2 = responseDocument.Errors[1];
            error2.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error2.Title.Should().Be("Input validation failed.");
            error2.Detail.Should().Be("The Title field is required.");
            error2.Source.Pointer.Should().Be("/atomic:operations[1]/data/attributes/title");

            Error error3 = responseDocument.Errors[2];
            error3.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error3.Title.Should().Be("Input validation failed.");
            error3.Detail.Should().Be("The field LengthInSeconds must be between 1 and 1440.");
            error3.Source.Pointer.Should().Be("/atomic:operations[1]/data/attributes/lengthInSeconds");
        }
    }
}
