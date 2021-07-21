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

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ZeroKeys
{
    public sealed class ZeroAsKeyTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<ZeroKeyDbContext>, ZeroKeyDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<ZeroKeyDbContext>, ZeroKeyDbContext> _testContext;
        private readonly ZeroKeyFakers _fakers = new();

        public ZeroAsKeyTests(ExampleIntegrationTestContext<TestableStartup<ZeroKeyDbContext>, ZeroKeyDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<GamesController>();
            testContext.UseController<MapsController>();
            testContext.UseController<PlayersController>();

            var options = (JsonApiOptions)testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.UseRelativeLinks = true;
            options.AllowClientGeneratedIds = true;
        }

        [Fact]
        public async Task Can_filter_by_zero_ID_on_primary_resources()
        {
            // Arrange
            List<Game> games = _fakers.Game.Generate(2);
            games[0].Id = 0;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Game>();
                dbContext.Games.AddRange(games);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/games?filter=equals(id,'0')";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be("0");
            responseDocument.ManyData[0].Links.Self.Should().Be("/games/0");
        }

        [Fact]
        public async Task Can_get_primary_resource_by_zero_ID_with_include()
        {
            // Arrange
            Game game = _fakers.Game.Generate();
            game.Id = 0;
            game.ActivePlayers = _fakers.Player.Generate(1);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Game>();
                dbContext.Games.Add(game);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/games/0?include=activePlayers";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be("0");
            responseDocument.SingleData.Links.Self.Should().Be("/games/0");

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Id.Should().Be(game.ActivePlayers.ElementAt(0).StringId);
        }

        [Fact]
        public async Task Can_create_resource_with_zero_ID()
        {
            // Arrange
            string newTitle = _fakers.Game.Generate().Title;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Game>();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "games",
                    id = "0",
                    attributes = new
                    {
                        title = newTitle
                    }
                }
            };

            const string route = "/games";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            httpResponse.Headers.Location.Should().Be("/games/0");

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be("0");

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Game gameInDatabase = await dbContext.Games.FirstWithIdAsync((int?)0);

                gameInDatabase.Should().NotBeNull();
                gameInDatabase.Title.Should().Be(newTitle);
            });
        }

        [Fact]
        public async Task Can_update_resource_with_zero_ID()
        {
            // Arrange
            Game existingGame = _fakers.Game.Generate();
            existingGame.Id = 0;

            string newTitle = _fakers.Game.Generate().Title;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Game>();
                dbContext.Games.Add(existingGame);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "games",
                    id = "0",
                    attributes = new
                    {
                        title = newTitle
                    }
                }
            };

            const string route = "/games/0";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be("0");
            responseDocument.SingleData.Attributes["title"].Should().Be(newTitle);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Game gameInDatabase = await dbContext.Games.FirstWithIdAsync((int?)0);

                gameInDatabase.Should().NotBeNull();
                gameInDatabase.Title.Should().Be(newTitle);
            });
        }

        [Fact]
        public async Task Can_clear_ToOne_relationship_with_zero_ID()
        {
            // Arrange
            Player existingPlayer = _fakers.Player.Generate();
            existingPlayer.ActiveGame = _fakers.Game.Generate();
            existingPlayer.ActiveGame.Id = 0;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Game>();
                dbContext.Players.Add(existingPlayer);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = (object)null
            };

            string route = $"/players/{existingPlayer.StringId}/relationships/activeGame";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Player playerInDatabase = await dbContext.Players.Include(player => player.ActiveGame).FirstWithIdAsync(existingPlayer.Id);

                playerInDatabase.Should().NotBeNull();
                playerInDatabase.ActiveGame.Should().BeNull();
            });
        }

        [Fact]
        public async Task Can_assign_ToOne_relationship_with_zero_ID()
        {
            // Arrange
            Player existingPlayer = _fakers.Player.Generate();

            Game existingGame = _fakers.Game.Generate();
            existingGame.Id = 0;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Game>();
                dbContext.AddRange(existingPlayer, existingGame);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "games",
                    id = 0
                }
            };

            string route = $"/players/{existingPlayer.StringId}/relationships/activeGame";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Player playerInDatabase = await dbContext.Players.Include(player => player.ActiveGame).FirstWithIdAsync(existingPlayer.Id);

                playerInDatabase.Should().NotBeNull();
                playerInDatabase.ActiveGame.Id.Should().Be(0);
            });
        }

        [Fact]
        public async Task Can_replace_ToOne_relationship_with_zero_ID()
        {
            // Arrange
            Player existingPlayer = _fakers.Player.Generate();
            existingPlayer.ActiveGame = _fakers.Game.Generate();

            Game existingGame = _fakers.Game.Generate();
            existingGame.Id = 0;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Game>();
                dbContext.AddRange(existingPlayer, existingGame);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "games",
                    id = "0"
                }
            };

            string route = $"/players/{existingPlayer.StringId}/relationships/activeGame";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Player playerInDatabase = await dbContext.Players.Include(player => player.ActiveGame).FirstWithIdAsync(existingPlayer.Id);

                playerInDatabase.Should().NotBeNull();
                playerInDatabase.ActiveGame.Id.Should().Be(0);
            });
        }

        [Fact]
        public async Task Can_clear_ToMany_relationship_with_zero_ID()
        {
            // Arrange
            Player existingPlayer = _fakers.Player.Generate();
            existingPlayer.RecentlyPlayed = _fakers.Game.Generate(2);
            existingPlayer.RecentlyPlayed.ElementAt(0).Id = 0;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Game>();
                dbContext.Players.Add(existingPlayer);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new object[0]
            };

            string route = $"/players/{existingPlayer.StringId}/relationships/recentlyPlayed";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Player playerInDatabase = await dbContext.Players.Include(player => player.RecentlyPlayed).FirstWithIdAsync(existingPlayer.Id);

                playerInDatabase.Should().NotBeNull();
                playerInDatabase.RecentlyPlayed.Should().BeEmpty();
            });
        }

        [Fact]
        public async Task Can_assign_ToMany_relationship_with_zero_ID()
        {
            // Arrange
            Player existingPlayer = _fakers.Player.Generate();

            Game existingGame = _fakers.Game.Generate();
            existingGame.Id = 0;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Game>();
                dbContext.AddRange(existingPlayer, existingGame);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "games",
                        id = 0
                    }
                }
            };

            string route = $"/players/{existingPlayer.StringId}/relationships/recentlyPlayed";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Player playerInDatabase = await dbContext.Players.Include(player => player.RecentlyPlayed).FirstWithIdAsync(existingPlayer.Id);

                playerInDatabase.Should().NotBeNull();
                playerInDatabase.RecentlyPlayed.Should().HaveCount(1);
                playerInDatabase.RecentlyPlayed.ElementAt(0).Id.Should().Be(0);
            });
        }

        [Fact]
        public async Task Can_replace_ToMany_relationship_with_zero_ID()
        {
            // Arrange
            Player existingPlayer = _fakers.Player.Generate();
            existingPlayer.RecentlyPlayed = _fakers.Game.Generate(2);

            Game existingGame = _fakers.Game.Generate();
            existingGame.Id = 0;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Game>();
                dbContext.AddRange(existingPlayer, existingGame);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "games",
                        id = 0
                    }
                }
            };

            string route = $"/players/{existingPlayer.StringId}/relationships/recentlyPlayed";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Player playerInDatabase = await dbContext.Players.Include(player => player.RecentlyPlayed).FirstWithIdAsync(existingPlayer.Id);

                playerInDatabase.Should().NotBeNull();
                playerInDatabase.RecentlyPlayed.Should().HaveCount(1);
                playerInDatabase.RecentlyPlayed.ElementAt(0).Id.Should().Be(0);
            });
        }

        [Fact]
        public async Task Can_add_to_ToMany_relationship_with_zero_ID()
        {
            // Arrange
            Player existingPlayer = _fakers.Player.Generate();
            existingPlayer.RecentlyPlayed = _fakers.Game.Generate(1);

            Game existingGame = _fakers.Game.Generate();
            existingGame.Id = 0;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Game>();
                dbContext.AddRange(existingPlayer, existingGame);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "games",
                        id = 0
                    }
                }
            };

            string route = $"/players/{existingPlayer.StringId}/relationships/recentlyPlayed";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePostAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Player playerInDatabase = await dbContext.Players.Include(player => player.RecentlyPlayed).FirstWithIdAsync(existingPlayer.Id);

                playerInDatabase.Should().NotBeNull();
                playerInDatabase.RecentlyPlayed.Should().HaveCount(2);
                playerInDatabase.RecentlyPlayed.Should().ContainSingle(game => game.Id == 0);
            });
        }

        [Fact]
        public async Task Can_remove_from_ToMany_relationship_with_zero_ID()
        {
            // Arrange
            Player existingPlayer = _fakers.Player.Generate();
            existingPlayer.RecentlyPlayed = _fakers.Game.Generate(2);
            existingPlayer.RecentlyPlayed.ElementAt(0).Id = 0;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Game>();
                dbContext.Players.Add(existingPlayer);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "games",
                        id = 0
                    }
                }
            };

            string route = $"/players/{existingPlayer.StringId}/relationships/recentlyPlayed";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Player playerInDatabase = await dbContext.Players.Include(player => player.RecentlyPlayed).FirstWithIdAsync(existingPlayer.Id);

                playerInDatabase.Should().NotBeNull();
                playerInDatabase.RecentlyPlayed.Should().HaveCount(1);
                playerInDatabase.RecentlyPlayed.Should().ContainSingle(game => game.Id != 0);
            });
        }

        [Fact]
        public async Task Can_delete_resource_with_zero_ID()
        {
            // Arrange
            Game existingGame = _fakers.Game.Generate();
            existingGame.Id = 0;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Game>();
                dbContext.Games.Add(existingGame);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/games/0";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Game gameInDatabase = await dbContext.Games.FirstWithIdOrDefaultAsync(existingGame.Id);

                gameInDatabase.Should().BeNull();
            });
        }
    }
}
