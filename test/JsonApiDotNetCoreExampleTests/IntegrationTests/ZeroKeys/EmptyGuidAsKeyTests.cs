using System;
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
    public sealed class EmptyGuidAsKeyTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<ZeroKeyDbContext>, ZeroKeyDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<ZeroKeyDbContext>, ZeroKeyDbContext> _testContext;
        private readonly ZeroKeyFakers _fakers = new();

        public EmptyGuidAsKeyTests(ExampleIntegrationTestContext<TestableStartup<ZeroKeyDbContext>, ZeroKeyDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<PlayersController>();
            testContext.UseController<GamesController>();
            testContext.UseController<MapsController>();

            var options = (JsonApiOptions)testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.UseRelativeLinks = true;
            options.AllowClientGeneratedIds = true;
        }

        [Fact]
        public async Task Can_filter_by_empty_ID_on_primary_resources()
        {
            // Arrange
            List<Map> maps = _fakers.Map.Generate(2);
            maps[0].Id = Guid.Empty;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Map>();
                dbContext.Maps.AddRange(maps);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/maps?filter=equals(id,'00000000-0000-0000-0000-000000000000')";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be("00000000-0000-0000-0000-000000000000");
            responseDocument.ManyData[0].Links.Self.Should().Be("/maps/00000000-0000-0000-0000-000000000000");
        }

        [Fact]
        public async Task Can_get_primary_resource_by_empty_ID_with_include()
        {
            // Arrange
            Map map = _fakers.Map.Generate();
            map.Id = Guid.Empty;
            map.Game = _fakers.Game.Generate();
            map.Game.Id = 0;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTablesAsync<Map, Game>();
                dbContext.Maps.Add(map);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/maps/00000000-0000-0000-0000-000000000000?include=game";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be("00000000-0000-0000-0000-000000000000");
            responseDocument.SingleData.Links.Self.Should().Be("/maps/00000000-0000-0000-0000-000000000000");

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Id.Should().Be("0");
        }

        [Fact]
        public async Task Can_create_resource_with_empty_ID()
        {
            // Arrange
            string newName = _fakers.Map.Generate().Name;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Map>();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "maps",
                    id = "00000000-0000-0000-0000-000000000000",
                    attributes = new
                    {
                        name = newName
                    }
                }
            };

            const string route = "/maps";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePostAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            httpResponse.Headers.Location.Should().Be("/maps/00000000-0000-0000-0000-000000000000");

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Map mapInDatabase = await dbContext.Maps.FirstWithIdAsync((Guid?)Guid.Empty);

                mapInDatabase.Should().NotBeNull();
                mapInDatabase.Name.Should().Be(newName);
            });
        }

        [Fact]
        public async Task Can_update_resource_with_empty_ID()
        {
            // Arrange
            Map existingMap = _fakers.Map.Generate();
            existingMap.Id = Guid.Empty;

            string newName = _fakers.Map.Generate().Name;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Map>();
                dbContext.Maps.Add(existingMap);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "maps",
                    id = Guid.Empty,
                    attributes = new
                    {
                        name = newName
                    }
                }
            };

            const string route = "/maps/00000000-0000-0000-0000-000000000000";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Map mapInDatabase = await dbContext.Maps.FirstWithIdAsync((Guid?)Guid.Empty);

                mapInDatabase.Should().NotBeNull();
                mapInDatabase.Name.Should().Be(newName);
            });
        }

        [Fact]
        public async Task Can_clear_ToOne_relationship_with_empty_ID()
        {
            // Arrange
            Game existingGame = _fakers.Game.Generate();
            existingGame.ActiveMap = _fakers.Map.Generate();
            existingGame.ActiveMap.Id = Guid.Empty;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Map>();
                dbContext.Games.Add(existingGame);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = (object)null
            };

            string route = $"/games/{existingGame.StringId}/relationships/activeMap";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Game gameInDatabase = await dbContext.Games.Include(game => game.ActiveMap).FirstWithIdAsync(existingGame.Id);

                gameInDatabase.Should().NotBeNull();
                gameInDatabase.ActiveMap.Should().BeNull();
            });
        }

        [Fact]
        public async Task Can_assign_ToOne_relationship_with_empty_ID()
        {
            // Arrange
            Game existingGame = _fakers.Game.Generate();

            Map existingMap = _fakers.Map.Generate();
            existingMap.Id = Guid.Empty;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Map>();
                dbContext.AddInRange(existingGame, existingMap);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "maps",
                    id = Guid.Empty
                }
            };

            string route = $"/games/{existingGame.StringId}/relationships/activeMap";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Game gameInDatabase = await dbContext.Games.Include(game => game.ActiveMap).FirstWithIdAsync(existingGame.Id);

                gameInDatabase.Should().NotBeNull();
                gameInDatabase.ActiveMap.Id.Should().Be(Guid.Empty);
            });
        }

        [Fact]
        public async Task Can_replace_ToOne_relationship_with_empty_ID()
        {
            // Arrange
            Game existingGame = _fakers.Game.Generate();
            existingGame.ActiveMap = _fakers.Map.Generate();

            Map existingMap = _fakers.Map.Generate();
            existingMap.Id = Guid.Empty;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Map>();
                dbContext.AddInRange(existingGame, existingMap);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "maps",
                    id = Guid.Empty
                }
            };

            string route = $"/games/{existingGame.StringId}/relationships/activeMap";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Game gameInDatabase = await dbContext.Games.Include(game => game.ActiveMap).FirstWithIdAsync(existingGame.Id);

                gameInDatabase.Should().NotBeNull();
                gameInDatabase.ActiveMap.Id.Should().Be(Guid.Empty);
            });
        }

        [Fact]
        public async Task Can_clear_ToMany_relationship_with_empty_ID()
        {
            // Arrange
            Game existingGame = _fakers.Game.Generate();
            existingGame.Maps = _fakers.Map.Generate(2);
            existingGame.Maps.ElementAt(0).Id = Guid.Empty;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Map>();
                dbContext.Games.Add(existingGame);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new object[0]
            };

            string route = $"/games/{existingGame.StringId}/relationships/maps";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Game gameInDatabase = await dbContext.Games.Include(game => game.Maps).FirstWithIdAsync(existingGame.Id);

                gameInDatabase.Should().NotBeNull();
                gameInDatabase.Maps.Should().BeEmpty();
            });
        }

        [Fact]
        public async Task Can_assign_ToMany_relationship_with_empty_ID()
        {
            // Arrange
            Game existingGame = _fakers.Game.Generate();

            Map existingMap = _fakers.Map.Generate();
            existingMap.Id = Guid.Empty;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Map>();
                dbContext.AddInRange(existingGame, existingMap);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "maps",
                        id = "00000000-0000-0000-0000-000000000000"
                    }
                }
            };

            string route = $"/games/{existingGame.StringId}/relationships/maps";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Game gameInDatabase = await dbContext.Games.Include(game => game.Maps).FirstWithIdAsync(existingGame.Id);

                gameInDatabase.Should().NotBeNull();
                gameInDatabase.Maps.Should().HaveCount(1);
                gameInDatabase.Maps.ElementAt(0).Id.Should().Be(Guid.Empty);
            });
        }

        [Fact]
        public async Task Can_replace_ToMany_relationship_with_empty_ID()
        {
            // Arrange
            Game existingGame = _fakers.Game.Generate();
            existingGame.Maps = _fakers.Map.Generate(2);

            Map existingMap = _fakers.Map.Generate();
            existingMap.Id = Guid.Empty;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Map>();
                dbContext.AddInRange(existingGame, existingMap);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "maps",
                        id = "00000000-0000-0000-0000-000000000000"
                    }
                }
            };

            string route = $"/games/{existingGame.StringId}/relationships/maps";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Game gameInDatabase = await dbContext.Games.Include(game => game.Maps).FirstWithIdAsync(existingGame.Id);

                gameInDatabase.Should().NotBeNull();
                gameInDatabase.Maps.Should().HaveCount(1);
                gameInDatabase.Maps.ElementAt(0).Id.Should().Be(Guid.Empty);
            });
        }

        [Fact]
        public async Task Can_add_to_ToMany_relationship_with_empty_ID()
        {
            // Arrange
            Game existingGame = _fakers.Game.Generate();
            existingGame.Maps = _fakers.Map.Generate(1);

            Map existingMap = _fakers.Map.Generate();
            existingMap.Id = Guid.Empty;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Map>();
                dbContext.AddInRange(existingGame, existingMap);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "maps",
                        id = "00000000-0000-0000-0000-000000000000"
                    }
                }
            };

            string route = $"/games/{existingGame.StringId}/relationships/maps";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePostAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Game gameInDatabase = await dbContext.Games.Include(game => game.Maps).FirstWithIdAsync(existingGame.Id);

                gameInDatabase.Should().NotBeNull();
                gameInDatabase.Maps.Should().HaveCount(2);
                gameInDatabase.Maps.Should().ContainSingle(map => map.Id == Guid.Empty);
            });
        }

        [Fact]
        public async Task Can_remove_from_ToMany_relationship_with_empty_ID()
        {
            // Arrange
            Game existingGame = _fakers.Game.Generate();
            existingGame.Maps = _fakers.Map.Generate(2);
            existingGame.Maps.ElementAt(0).Id = Guid.Empty;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Map>();
                dbContext.Games.Add(existingGame);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "maps",
                        id = "00000000-0000-0000-0000-000000000000"
                    }
                }
            };

            string route = $"/games/{existingGame.StringId}/relationships/maps";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Game gameInDatabase = await dbContext.Games.Include(game => game.Maps).FirstWithIdAsync(existingGame.Id);

                gameInDatabase.Should().NotBeNull();
                gameInDatabase.Maps.Should().HaveCount(1);
                gameInDatabase.Maps.Should().ContainSingle(map => map.Id != Guid.Empty);
            });
        }

        [Fact]
        public async Task Can_delete_resource_with_empty_ID()
        {
            // Arrange
            Map existingMap = _fakers.Map.Generate();
            existingMap.Id = Guid.Empty;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<Map>();
                dbContext.Maps.Add(existingMap);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/maps/00000000-0000-0000-0000-000000000000";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                Map gameInDatabase = await dbContext.Maps.FirstWithIdOrDefaultAsync(existingMap.Id);

                gameInDatabase.Should().BeNull();
            });
        }
    }
}
