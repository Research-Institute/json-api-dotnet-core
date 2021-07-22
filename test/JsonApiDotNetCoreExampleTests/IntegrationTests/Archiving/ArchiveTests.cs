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
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.Archiving
{
    public sealed class ArchiveTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<TelevisionDbContext>, TelevisionDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<TelevisionDbContext>, TelevisionDbContext> _testContext;
        private readonly TelevisionFakers _fakers = new();

        public ArchiveTests(ExampleIntegrationTestContext<TestableStartup<TelevisionDbContext>, TelevisionDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<TelevisionNetworksController>();
            testContext.UseController<TelevisionStationsController>();
            testContext.UseController<TelevisionBroadcastsController>();
            testContext.UseController<BroadcastCommentsController>();

            testContext.ConfigureServicesAfterStartup(services =>
            {
                services.AddResourceDefinition<TelevisionBroadcastDefinition>();
            });
        }

        [Fact]
        public async Task Can_get_archived_resource_by_ID()
        {
            // Arrange
            TelevisionBroadcast broadcast = _fakers.TelevisionBroadcast.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Broadcasts.Add(broadcast);
                await dbContext.SaveChangesAsync();
            });

            string route = "/televisionBroadcasts/" + broadcast.StringId;

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(broadcast.StringId);
            responseDocument.SingleData.Attributes["archivedAt"].Should().BeCloseTo(broadcast.ArchivedAt);
        }

        [Fact]
        public async Task Can_get_unarchived_resource_by_ID()
        {
            // Arrange
            TelevisionBroadcast broadcast = _fakers.TelevisionBroadcast.Generate();
            broadcast.ArchivedAt = null;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Broadcasts.Add(broadcast);
                await dbContext.SaveChangesAsync();
            });

            string route = "/televisionBroadcasts/" + broadcast.StringId;

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(broadcast.StringId);
            responseDocument.SingleData.Attributes["archivedAt"].Should().BeNull();
        }

        [Fact]
        public async Task Get_primary_resources_excludes_archived()
        {
            // Arrange
            List<TelevisionBroadcast> broadcasts = _fakers.TelevisionBroadcast.Generate(2);
            broadcasts[1].ArchivedAt = null;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<TelevisionBroadcast>();
                dbContext.Broadcasts.AddRange(broadcasts);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/televisionBroadcasts";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(broadcasts[1].StringId);
            responseDocument.ManyData[0].Attributes["archivedAt"].Should().BeNull();
        }

        [Fact]
        public async Task Get_primary_resources_with_filter_includes_archived()
        {
            // Arrange
            List<TelevisionBroadcast> broadcasts = _fakers.TelevisionBroadcast.Generate(2);
            broadcasts[1].ArchivedAt = null;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<TelevisionBroadcast>();
                dbContext.Broadcasts.AddRange(broadcasts);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/televisionBroadcasts?filter=or(equals(archivedAt,null),not(equals(archivedAt,null)))";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(2);
            responseDocument.ManyData[0].Id.Should().Be(broadcasts[0].StringId);
            responseDocument.ManyData[0].Attributes["archivedAt"].Should().BeCloseTo(broadcasts[0].ArchivedAt);
            responseDocument.ManyData[1].Id.Should().Be(broadcasts[1].StringId);
            responseDocument.ManyData[1].Attributes["archivedAt"].Should().BeNull();
        }

        [Fact]
        public async Task Get_primary_resource_by_ID_with_include_excludes_archived()
        {
            // Arrange
            TelevisionStation station = _fakers.TelevisionStation.Generate();
            station.Broadcasts = _fakers.TelevisionBroadcast.Generate(2).ToHashSet();
            station.Broadcasts.ElementAt(1).ArchivedAt = null;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Stations.Add(station);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/televisionStations/{station.StringId}?include=broadcasts";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(station.StringId);

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Id.Should().Be(station.Broadcasts.ElementAt(1).StringId);
            responseDocument.Included[0].Attributes["archivedAt"].Should().BeNull();
        }

        [Fact]
        public async Task Get_primary_resource_by_ID_with_include_and_filter_includes_archived()
        {
            // Arrange
            TelevisionStation station = _fakers.TelevisionStation.Generate();
            station.Broadcasts = _fakers.TelevisionBroadcast.Generate(2).ToHashSet();
            station.Broadcasts.ElementAt(1).ArchivedAt = null;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Stations.Add(station);
                await dbContext.SaveChangesAsync();
            });

            string route =
                $"/televisionStations/{station.StringId}?include=broadcasts&filter[broadcasts]=or(equals(archivedAt,null),not(equals(archivedAt,null)))";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(station.StringId);

            responseDocument.Included.Should().HaveCount(2);
            responseDocument.Included[0].Id.Should().Be(station.Broadcasts.ElementAt(0).StringId);
            responseDocument.Included[0].Attributes["archivedAt"].Should().BeCloseTo(station.Broadcasts.ElementAt(0).ArchivedAt);
            responseDocument.Included[1].Id.Should().Be(station.Broadcasts.ElementAt(1).StringId);
            responseDocument.Included[1].Attributes["archivedAt"].Should().BeNull();
        }

        [Fact]
        public async Task Get_secondary_resource_includes_archived()
        {
            // Arrange
            BroadcastComment comment = _fakers.BroadcastComment.Generate();
            comment.AppliesTo = _fakers.TelevisionBroadcast.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Comments.Add(comment);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/broadcastComments/{comment.StringId}/appliesTo";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Id.Should().Be(comment.AppliesTo.StringId);
            responseDocument.SingleData.Attributes["archivedAt"].Should().BeCloseTo(comment.AppliesTo.ArchivedAt);
        }

        [Fact]
        public async Task Get_secondary_resources_excludes_archived()
        {
            // Arrange
            TelevisionStation station = _fakers.TelevisionStation.Generate();
            station.Broadcasts = _fakers.TelevisionBroadcast.Generate(2).ToHashSet();
            station.Broadcasts.ElementAt(1).ArchivedAt = null;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Stations.Add(station);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/televisionStations/{station.StringId}/broadcasts";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(station.Broadcasts.ElementAt(1).StringId);
            responseDocument.ManyData[0].Attributes["archivedAt"].Should().BeNull();
        }

        [Fact]
        public async Task Get_secondary_resources_with_filter_includes_archived()
        {
            // Arrange
            TelevisionStation station = _fakers.TelevisionStation.Generate();
            station.Broadcasts = _fakers.TelevisionBroadcast.Generate(2).ToHashSet();
            station.Broadcasts.ElementAt(1).ArchivedAt = null;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Stations.Add(station);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/televisionStations/{station.StringId}/broadcasts?filter=or(equals(archivedAt,null),not(equals(archivedAt,null)))";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(2);
            responseDocument.ManyData[0].Id.Should().Be(station.Broadcasts.ElementAt(0).StringId);
            responseDocument.ManyData[0].Attributes["archivedAt"].Should().BeCloseTo(station.Broadcasts.ElementAt(0).ArchivedAt);
            responseDocument.ManyData[1].Id.Should().Be(station.Broadcasts.ElementAt(1).StringId);
            responseDocument.ManyData[1].Attributes["archivedAt"].Should().BeNull();
        }

        [Fact]
        public async Task Get_secondary_resource_by_ID_with_include_excludes_archived()
        {
            // Arrange
            TelevisionNetwork network = _fakers.TelevisionNetwork.Generate();
            network.Stations = _fakers.TelevisionStation.Generate(1).ToHashSet();
            network.Stations.ElementAt(0).Broadcasts = _fakers.TelevisionBroadcast.Generate(2).ToHashSet();
            network.Stations.ElementAt(0).Broadcasts.ElementAt(1).ArchivedAt = null;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Networks.Add(network);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/televisionNetworks/{network.StringId}/stations?include=broadcasts";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(network.Stations.ElementAt(0).StringId);

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Id.Should().Be(network.Stations.ElementAt(0).Broadcasts.ElementAt(1).StringId);
            responseDocument.Included[0].Attributes["archivedAt"].Should().BeNull();
        }

        [Fact]
        public async Task Get_secondary_resource_by_ID_with_include_and_filter_includes_archived()
        {
            TelevisionNetwork network = _fakers.TelevisionNetwork.Generate();
            network.Stations = _fakers.TelevisionStation.Generate(1).ToHashSet();
            network.Stations.ElementAt(0).Broadcasts = _fakers.TelevisionBroadcast.Generate(2).ToHashSet();
            network.Stations.ElementAt(0).Broadcasts.ElementAt(1).ArchivedAt = null;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Networks.Add(network);
                await dbContext.SaveChangesAsync();
            });

            string route =
                $"/televisionNetworks/{network.StringId}/stations?include=broadcasts&filter[broadcasts]=or(equals(archivedAt,null),not(equals(archivedAt,null)))";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(network.Stations.ElementAt(0).StringId);

            responseDocument.Included.Should().HaveCount(2);
            responseDocument.Included[0].Id.Should().Be(network.Stations.ElementAt(0).Broadcasts.ElementAt(0).StringId);
            responseDocument.Included[0].Attributes["archivedAt"].Should().BeCloseTo(network.Stations.ElementAt(0).Broadcasts.ElementAt(0).ArchivedAt);
            responseDocument.Included[1].Id.Should().Be(network.Stations.ElementAt(0).Broadcasts.ElementAt(1).StringId);
            responseDocument.Included[1].Attributes["archivedAt"].Should().BeNull();
        }

        [Fact]
        public async Task Get_ToMany_relationship_excludes_archived()
        {
            // Arrange
            TelevisionStation station = _fakers.TelevisionStation.Generate();
            station.Broadcasts = _fakers.TelevisionBroadcast.Generate(2).ToHashSet();
            station.Broadcasts.ElementAt(1).ArchivedAt = null;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Stations.Add(station);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/televisionStations/{station.StringId}/relationships/broadcasts";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(1);
            responseDocument.ManyData[0].Id.Should().Be(station.Broadcasts.ElementAt(1).StringId);
        }

        [Fact]
        public async Task Get_ToMany_relationship_with_filter_includes_archived()
        {
            // Arrange
            TelevisionStation station = _fakers.TelevisionStation.Generate();
            station.Broadcasts = _fakers.TelevisionBroadcast.Generate(2).ToHashSet();
            station.Broadcasts.ElementAt(1).ArchivedAt = null;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Stations.Add(station);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/televisionStations/{station.StringId}/relationships/broadcasts?filter=or(equals(archivedAt,null),not(equals(archivedAt,null)))";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(2);
            responseDocument.ManyData[0].Id.Should().Be(station.Broadcasts.ElementAt(0).StringId);
            responseDocument.ManyData[1].Id.Should().Be(station.Broadcasts.ElementAt(1).StringId);
        }

        [Fact]
        public async Task Can_create_unarchived_resource()
        {
            // Arrange
            TelevisionBroadcast newBroadcast = _fakers.TelevisionBroadcast.Generate();

            var requestBody = new
            {
                data = new
                {
                    type = "televisionBroadcasts",
                    attributes = new
                    {
                        title = newBroadcast.Title,
                        airedAt = newBroadcast.AiredAt
                    }
                }
            };

            const string route = "/televisionBroadcasts";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Attributes["title"].Should().Be(newBroadcast.Title);
            responseDocument.SingleData.Attributes["airedAt"].Should().BeCloseTo(newBroadcast.AiredAt);
            responseDocument.SingleData.Attributes["archivedAt"].Should().BeNull();
        }

        [Fact]
        public async Task Cannot_create_archived_resource()
        {
            // Arrange
            TelevisionBroadcast newBroadcast = _fakers.TelevisionBroadcast.Generate();

            var requestBody = new
            {
                data = new
                {
                    type = "televisionBroadcasts",
                    attributes = new
                    {
                        title = newBroadcast.Title,
                        airedAt = newBroadcast.AiredAt,
                        archivedAt = newBroadcast.ArchivedAt
                    }
                }
            };

            const string route = "/televisionBroadcasts";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Forbidden);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            error.Title.Should().Be("Television broadcasts cannot be created in archived state.");
            error.Detail.Should().BeNull();
        }

        [Fact]
        public async Task Can_archive_resource()
        {
            // Arrange
            TelevisionBroadcast existingBroadcast = _fakers.TelevisionBroadcast.Generate();
            existingBroadcast.ArchivedAt = null;

            DateTimeOffset newArchivedAt = _fakers.TelevisionBroadcast.Generate().ArchivedAt!.Value;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Broadcasts.Add(existingBroadcast);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "televisionBroadcasts",
                    id = existingBroadcast.StringId,
                    attributes = new
                    {
                        archivedAt = newArchivedAt
                    }
                }
            };

            string route = "/televisionBroadcasts/" + existingBroadcast.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                TelevisionBroadcast broadcastInDatabase = await dbContext.Broadcasts.FirstWithIdAsync(existingBroadcast.Id);

                broadcastInDatabase.ArchivedAt.Should().BeCloseTo(newArchivedAt);
            });
        }

        [Fact]
        public async Task Can_unarchive_resource()
        {
            // Arrange
            TelevisionBroadcast broadcast = _fakers.TelevisionBroadcast.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Broadcasts.Add(broadcast);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "televisionBroadcasts",
                    id = broadcast.StringId,
                    attributes = new
                    {
                        archivedAt = (DateTimeOffset?)null
                    }
                }
            };

            string route = "/televisionBroadcasts/" + broadcast.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                TelevisionBroadcast broadcastInDatabase = await dbContext.Broadcasts.FirstWithIdAsync(broadcast.Id);

                broadcastInDatabase.ArchivedAt.Should().BeNull();
            });
        }

        [Fact]
        public async Task Cannot_shift_archive_date()
        {
            // Arrange
            TelevisionBroadcast broadcast = _fakers.TelevisionBroadcast.Generate();

            DateTimeOffset? newArchivedAt = _fakers.TelevisionBroadcast.Generate().ArchivedAt;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Broadcasts.Add(broadcast);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "televisionBroadcasts",
                    id = broadcast.StringId,
                    attributes = new
                    {
                        archivedAt = newArchivedAt
                    }
                }
            };

            string route = "/televisionBroadcasts/" + broadcast.StringId;

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Forbidden);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            error.Title.Should().Be("Archive date of television broadcasts cannot be shifted. Unarchive it first.");
            error.Detail.Should().BeNull();
        }

        [Fact]
        public async Task Can_delete_archived_resource()
        {
            // Arrange
            TelevisionBroadcast broadcast = _fakers.TelevisionBroadcast.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Broadcasts.Add(broadcast);
                await dbContext.SaveChangesAsync();
            });

            string route = "/televisionBroadcasts/" + broadcast.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                TelevisionBroadcast broadcastInDatabase = await dbContext.Broadcasts.FirstWithIdOrDefaultAsync(broadcast.Id);

                broadcastInDatabase.Should().BeNull();
            });
        }

        [Fact]
        public async Task Cannot_delete_unarchived_resource()
        {
            // Arrange
            TelevisionBroadcast broadcast = _fakers.TelevisionBroadcast.Generate();
            broadcast.ArchivedAt = null;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Broadcasts.Add(broadcast);
                await dbContext.SaveChangesAsync();
            });

            string route = "/televisionBroadcasts/" + broadcast.StringId;

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteDeleteAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Forbidden);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            error.Title.Should().Be("Television broadcasts must first be archived before they can be deleted.");
            error.Detail.Should().BeNull();
        }
    }
}
