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

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.AtomicOperations.Updating.Relationships
{
    public sealed class AtomicUpdateToOneRelationshipTests
        : IClassFixture<ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext> _testContext;
        private readonly OperationsFakers _fakers = new();

        public AtomicUpdateToOneRelationshipTests(ExampleIntegrationTestContext<TestableStartup<OperationsDbContext>, OperationsDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<OperationsController>();
        }

        [Fact]
        public async Task Can_clear_OneToOne_relationship_from_principal_side()
        {
            // Arrange
            Lyric existingLyric = _fakers.Lyric.Generate();
            existingLyric.Track = _fakers.MusicTrack.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<MusicTrack>();
                dbContext.Lyrics.Add(existingLyric);
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
                            type = "lyrics",
                            id = existingLyric.StringId,
                            relationship = "track"
                        },
                        data = (object)null
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
                Lyric lyricInDatabase = await dbContext.Lyrics.Include(lyric => lyric.Track).FirstWithIdAsync(existingLyric.Id);

                lyricInDatabase.Track.Should().BeNull();

                List<MusicTrack> tracksInDatabase = await dbContext.MusicTracks.ToListAsync();
                tracksInDatabase.Should().HaveCount(1);
            });
        }

        [Fact]
        public async Task Can_clear_OneToOne_relationship_from_dependent_side()
        {
            // Arrange
            MusicTrack existingTrack = _fakers.MusicTrack.Generate();
            existingTrack.Lyric = _fakers.Lyric.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Lyric>();
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
                        @ref = new
                        {
                            type = "musicTracks",
                            id = existingTrack.StringId,
                            relationship = "lyric"
                        },
                        data = (object)null
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
                MusicTrack trackInDatabase = await dbContext.MusicTracks.Include(musicTrack => musicTrack.Lyric).FirstWithIdAsync(existingTrack.Id);

                trackInDatabase.Lyric.Should().BeNull();

                List<Lyric> lyricsInDatabase = await dbContext.Lyrics.ToListAsync();
                lyricsInDatabase.Should().HaveCount(1);
            });
        }

        [Fact]
        public async Task Can_clear_ManyToOne_relationship()
        {
            // Arrange
            MusicTrack existingTrack = _fakers.MusicTrack.Generate();
            existingTrack.OwnedBy = _fakers.RecordCompany.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<RecordCompany>();
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
                        @ref = new
                        {
                            type = "musicTracks",
                            id = existingTrack.StringId,
                            relationship = "ownedBy"
                        },
                        data = (object)null
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

                trackInDatabase.OwnedBy.Should().BeNull();

                List<RecordCompany> companiesInDatabase = await dbContext.RecordCompanies.ToListAsync();
                companiesInDatabase.Should().HaveCount(1);
            });
        }

        [Fact]
        public async Task Can_create_OneToOne_relationship_from_principal_side()
        {
            // Arrange
            Lyric existingLyric = _fakers.Lyric.Generate();
            MusicTrack existingTrack = _fakers.MusicTrack.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddInRange(existingLyric, existingTrack);
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
                            type = "lyrics",
                            id = existingLyric.StringId,
                            relationship = "track"
                        },
                        data = new
                        {
                            type = "musicTracks",
                            id = existingTrack.StringId
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
                Lyric lyricInDatabase = await dbContext.Lyrics.Include(lyric => lyric.Track).FirstWithIdAsync(existingLyric.Id);

                lyricInDatabase.Track.Id.Should().Be(existingTrack.Id);
            });
        }

        [Fact]
        public async Task Can_create_OneToOne_relationship_from_dependent_side()
        {
            // Arrange
            MusicTrack existingTrack = _fakers.MusicTrack.Generate();
            Lyric existingLyric = _fakers.Lyric.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddInRange(existingTrack, existingLyric);
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
                            relationship = "lyric"
                        },
                        data = new
                        {
                            type = "lyrics",
                            id = existingLyric.StringId
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
                MusicTrack trackInDatabase = await dbContext.MusicTracks.Include(musicTrack => musicTrack.Lyric).FirstWithIdAsync(existingTrack.Id);

                trackInDatabase.Lyric.Id.Should().Be(existingLyric.Id);
            });
        }

        [Fact]
        public async Task Can_create_ManyToOne_relationship()
        {
            // Arrange
            MusicTrack existingTrack = _fakers.MusicTrack.Generate();
            RecordCompany existingCompany = _fakers.RecordCompany.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddInRange(existingTrack, existingCompany);
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

                trackInDatabase.OwnedBy.Id.Should().Be(existingCompany.Id);
            });
        }

        [Fact]
        public async Task Can_replace_OneToOne_relationship_from_principal_side()
        {
            // Arrange
            Lyric existingLyric = _fakers.Lyric.Generate();
            existingLyric.Track = _fakers.MusicTrack.Generate();

            MusicTrack existingTrack = _fakers.MusicTrack.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<MusicTrack>();
                dbContext.AddInRange(existingLyric, existingTrack);
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
                            type = "lyrics",
                            id = existingLyric.StringId,
                            relationship = "track"
                        },
                        data = new
                        {
                            type = "musicTracks",
                            id = existingTrack.StringId
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
                Lyric lyricInDatabase = await dbContext.Lyrics.Include(lyric => lyric.Track).FirstWithIdAsync(existingLyric.Id);

                lyricInDatabase.Track.Id.Should().Be(existingTrack.Id);

                List<MusicTrack> tracksInDatabase = await dbContext.MusicTracks.ToListAsync();
                tracksInDatabase.Should().HaveCount(2);
            });
        }

        [Fact]
        public async Task Can_replace_OneToOne_relationship_from_dependent_side()
        {
            // Arrange
            MusicTrack existingTrack = _fakers.MusicTrack.Generate();
            existingTrack.Lyric = _fakers.Lyric.Generate();

            Lyric existingLyric = _fakers.Lyric.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Lyric>();
                dbContext.AddInRange(existingTrack, existingLyric);
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
                            relationship = "lyric"
                        },
                        data = new
                        {
                            type = "lyrics",
                            id = existingLyric.StringId
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
                MusicTrack trackInDatabase = await dbContext.MusicTracks.Include(musicTrack => musicTrack.Lyric).FirstWithIdAsync(existingTrack.Id);

                trackInDatabase.Lyric.Id.Should().Be(existingLyric.Id);

                List<Lyric> lyricsInDatabase = await dbContext.Lyrics.ToListAsync();
                lyricsInDatabase.Should().HaveCount(2);
            });
        }

        [Fact]
        public async Task Can_replace_ManyToOne_relationship()
        {
            // Arrange
            MusicTrack existingTrack = _fakers.MusicTrack.Generate();
            existingTrack.OwnedBy = _fakers.RecordCompany.Generate();

            RecordCompany existingCompany = _fakers.RecordCompany.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<RecordCompany>();
                dbContext.AddInRange(existingTrack, existingCompany);
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

                trackInDatabase.OwnedBy.Id.Should().Be(existingCompany.Id);

                List<RecordCompany> companiesInDatabase = await dbContext.RecordCompanies.ToListAsync();
                companiesInDatabase.Should().HaveCount(2);
            });
        }

        [Fact]
        public async Task Cannot_create_for_href_element()
        {
            // Arrange
            var requestBody = new
            {
                atomic__operations = new[]
                {
                    new
                    {
                        op = "update",
                        href = "/api/v1/musicTracks/1/relationships/ownedBy"
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
            error.Title.Should().Be("Failed to deserialize request body: Usage of the 'href' element is not supported.");
            error.Detail.Should().BeNull();
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }

        [Fact]
        public async Task Cannot_create_for_missing_type_in_ref()
        {
            // Arrange
            var requestBody = new
            {
                atomic__operations = new[]
                {
                    new
                    {
                        op = "update",
                        @ref = new
                        {
                            id = 99999999,
                            relationship = "track"
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
            error.Title.Should().Be("Failed to deserialize request body: The 'ref.type' element is required.");
            error.Detail.Should().BeNull();
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }

        [Fact]
        public async Task Cannot_create_for_unknown_type_in_ref()
        {
            // Arrange
            var requestBody = new
            {
                atomic__operations = new[]
                {
                    new
                    {
                        op = "update",
                        @ref = new
                        {
                            type = "doesNotExist",
                            id = 99999999,
                            relationship = "ownedBy"
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
        public async Task Cannot_create_for_missing_ID_in_ref()
        {
            // Arrange
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
                            relationship = "ownedBy"
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
            error.Title.Should().Be("Failed to deserialize request body: The 'ref.id' or 'ref.lid' element is required.");
            error.Detail.Should().BeNull();
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }

        [Fact]
        public async Task Cannot_create_for_unknown_ID_in_ref()
        {
            // Arrange
            string missingTrackId = Guid.NewGuid().ToString();

            Lyric existingLyric = _fakers.Lyric.Generate();

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
                        op = "update",
                        @ref = new
                        {
                            type = "musicTracks",
                            id = missingTrackId,
                            relationship = "lyric"
                        },
                        data = new
                        {
                            type = "lyrics",
                            id = existingLyric.StringId
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
            error.Title.Should().Be("The requested resource does not exist.");
            error.Detail.Should().Be($"Resource of type 'musicTracks' with ID '{missingTrackId}' does not exist.");
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }

        [Fact]
        public async Task Cannot_create_for_incompatible_ID_in_ref()
        {
            // Arrange
            Lyric existingLyric = _fakers.Lyric.Generate();

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
                        op = "update",
                        @ref = new
                        {
                            type = "musicTracks",
                            id = "invalid-guid",
                            relationship = "lyric"
                        },
                        data = new
                        {
                            type = "lyrics",
                            id = existingLyric.StringId
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
            error.Title.Should().Be("Failed to deserialize request body.");
            error.Detail.Should().Be("Failed to convert 'invalid-guid' of type 'String' to type 'Guid'.");
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }

        [Fact]
        public async Task Cannot_create_for_ID_and_local_ID_in_ref()
        {
            // Arrange
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
                            id = Guid.NewGuid().ToString(),
                            lid = "local-1",
                            relationship = "ownedBy"
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
            error.Title.Should().Be("Failed to deserialize request body: The 'ref.id' or 'ref.lid' element is required.");
            error.Detail.Should().BeNull();
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }

        [Fact]
        public async Task Cannot_create_for_unknown_relationship_in_ref()
        {
            // Arrange
            var requestBody = new
            {
                atomic__operations = new[]
                {
                    new
                    {
                        op = "update",
                        @ref = new
                        {
                            type = "performers",
                            id = 99999999,
                            relationship = "doesNotExist"
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
            error.Title.Should().Be("Failed to deserialize request body: The referenced relationship does not exist.");
            error.Detail.Should().Be("Resource of type 'performers' does not contain a relationship named 'doesNotExist'.");
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }

        [Fact]
        public async Task Cannot_create_for_array_in_data()
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
                        @ref = new
                        {
                            type = "musicTracks",
                            id = existingTrack.StringId,
                            relationship = "lyric"
                        },
                        data = new[]
                        {
                            new
                            {
                                type = "lyrics",
                                id = 99999999
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

        [Fact]
        public async Task Cannot_create_for_missing_type_in_data()
        {
            // Arrange
            var requestBody = new
            {
                atomic__operations = new[]
                {
                    new
                    {
                        op = "update",
                        @ref = new
                        {
                            type = "lyrics",
                            id = 99999999,
                            relationship = "track"
                        },
                        data = new
                        {
                            id = Guid.NewGuid().ToString()
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
            error.Title.Should().Be("Failed to deserialize request body: The 'data.type' element is required.");
            error.Detail.Should().BeNull();
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }

        [Fact]
        public async Task Cannot_create_for_unknown_type_in_data()
        {
            // Arrange
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
                            id = Guid.NewGuid().ToString(),
                            relationship = "lyric"
                        },
                        data = new
                        {
                            type = "doesNotExist",
                            id = 99999999
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
        public async Task Cannot_create_for_missing_ID_in_data()
        {
            // Arrange
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
                            id = Guid.NewGuid().ToString(),
                            relationship = "lyric"
                        },
                        data = new
                        {
                            type = "lyrics"
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
            error.Title.Should().Be("Failed to deserialize request body: The 'data.id' or 'data.lid' element is required.");
            error.Detail.Should().BeNull();
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }

        [Fact]
        public async Task Cannot_create_for_ID_and_local_ID_in_data()
        {
            // Arrange
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
                            id = Guid.NewGuid().ToString(),
                            relationship = "lyric"
                        },
                        data = new
                        {
                            type = "lyrics",
                            id = 99999999,
                            lid = "local-1"
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
            error.Title.Should().Be("Failed to deserialize request body: The 'data.id' or 'data.lid' element is required.");
            error.Detail.Should().BeNull();
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }

        [Fact]
        public async Task Cannot_create_for_unknown_ID_in_data()
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
                        @ref = new
                        {
                            type = "musicTracks",
                            id = existingTrack.StringId,
                            relationship = "lyric"
                        },
                        data = new
                        {
                            type = "lyrics",
                            id = 99999999
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
            error.Detail.Should().Be("Related resource of type 'lyrics' with ID '99999999' in relationship 'lyric' does not exist.");
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }

        [Fact]
        public async Task Cannot_create_for_incompatible_ID_in_data()
        {
            // Arrange
            Lyric existingLyric = _fakers.Lyric.Generate();

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
                        op = "update",
                        @ref = new
                        {
                            type = "lyrics",
                            id = existingLyric.StringId,
                            relationship = "track"
                        },
                        data = new
                        {
                            type = "musicTracks",
                            id = "invalid-guid"
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
            error.Title.Should().Be("Failed to deserialize request body.");
            error.Detail.Should().Be("Failed to convert 'invalid-guid' of type 'String' to type 'Guid'.");
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }

        [Fact]
        public async Task Cannot_create_for_relationship_mismatch_between_ref_and_data()
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
                        @ref = new
                        {
                            type = "musicTracks",
                            id = existingTrack.StringId,
                            relationship = "lyric"
                        },
                        data = new
                        {
                            type = "playlists",
                            id = 99999999
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
            error.Title.Should().Be("Failed to deserialize request body: Resource type mismatch between 'ref.relationship' and 'data.type' element.");
            error.Detail.Should().Be("Expected resource of type 'lyrics' in 'data.type', instead of 'playlists'.");
            error.Source.Pointer.Should().Be("/atomic:operations[0]");
        }
    }
}
