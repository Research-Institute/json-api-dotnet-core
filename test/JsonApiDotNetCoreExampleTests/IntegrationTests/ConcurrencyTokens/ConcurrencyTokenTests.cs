using System.Net;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Serialization.Objects;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ConcurrencyTokens
{
    public sealed class ConcurrencyTokenTests
        : IClassFixture<IntegrationTestContext<TestableStartup<ConcurrencyDbContext>, ConcurrencyDbContext>>
    {
        private readonly IntegrationTestContext<TestableStartup<ConcurrencyDbContext>, ConcurrencyDbContext> _testContext;
        private readonly ConcurrencyFakers _fakers = new ConcurrencyFakers();

        public ConcurrencyTokenTests(IntegrationTestContext<TestableStartup<ConcurrencyDbContext>, ConcurrencyDbContext> testContext)
        {
            _testContext = testContext;
        }

        [Fact]
        public async Task Can_get_primary_resource_by_ID_with_include()
        {
            // Arrange
            var disk = _fakers.Disk.Generate();
            disk.Partitions = _fakers.Partition.Generate(1);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Disks.Add(disk);
                await dbContext.SaveChangesAsync();
            });

            var route = $"/disks/{disk.StringId}?include=partitions";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Type.Should().Be("disks");
            responseDocument.SingleData.Id.Should().Be(disk.StringId);
            responseDocument.SingleData.Attributes["manufacturer"].Should().Be(disk.Manufacturer);
            responseDocument.SingleData.Attributes["concurrencyToken"].Should().Be(disk.xmin);
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Type.Should().Be("partitions");
            responseDocument.Included[0].Id.Should().Be(disk.Partitions[0].StringId);
            responseDocument.Included[0].Attributes["mountPoint"].Should().Be(disk.Partitions[0].MountPoint);
            responseDocument.Included[0].Attributes["fileSystem"].Should().Be(disk.Partitions[0].FileSystem);
            responseDocument.Included[0].Attributes["capacityInBytes"].Should().Be(disk.Partitions[0].CapacityInBytes);
            responseDocument.Included[0].Attributes["freeSpaceInBytes"].Should().Be(disk.Partitions[0].FreeSpaceInBytes);
            responseDocument.Included[0].Attributes["concurrencyToken"].Should().Be(disk.Partitions[0].xmin);
            responseDocument.Included[0].Relationships.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Can_create_resource()
        {
            // Arrange
            var newManufacturer = _fakers.Disk.Generate().Manufacturer;
            var newSerialCode = _fakers.Disk.Generate().SerialCode;

            var requestBody = new
            {
                data = new
                {
                    type = "disks",
                    attributes = new
                    {
                        manufacturer = newManufacturer,
                        serialCode = newSerialCode
                    }
                }
            };

            var route = "/disks";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Type.Should().Be("disks");
            responseDocument.SingleData.Attributes["manufacturer"].Should().Be(newManufacturer);
            responseDocument.SingleData.Attributes["serialCode"].Should().Be(newSerialCode);
            responseDocument.SingleData.Attributes["concurrencyToken"].As<long>().Should().BeGreaterThan(0);
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();

            var newDiskId = int.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var diskInDatabase = await dbContext.Disks
                    .FirstAsync(disk => disk.Id == newDiskId);

                diskInDatabase.Manufacturer.Should().Be(newManufacturer);
                diskInDatabase.SerialCode.Should().Be(newSerialCode);
                diskInDatabase.xmin.Should().BeGreaterThan(0);
            });
        }

        [Fact]
        public async Task Can_create_resource_with_ignored_token()
        {
            // Arrange
            var newManufacturer = _fakers.Disk.Generate().Manufacturer;
            var newSerialCode = _fakers.Disk.Generate().SerialCode;
            const uint ignoredToken = 98765432;

            var requestBody = new
            {
                data = new
                {
                    type = "disks",
                    attributes = new
                    {
                        manufacturer = newManufacturer,
                        serialCode = newSerialCode,
                        concurrencyToken = ignoredToken
                    }
                }
            };

            var route = "/disks";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Type.Should().Be("disks");
            responseDocument.SingleData.Attributes["manufacturer"].Should().Be(newManufacturer);
            responseDocument.SingleData.Attributes["serialCode"].Should().Be(newSerialCode);
            responseDocument.SingleData.Attributes["concurrencyToken"].As<long>().Should().BeGreaterThan(0).And.NotBe(ignoredToken);
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();

            var newDiskId = int.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var diskInDatabase = await dbContext.Disks
                    .FirstAsync(disk => disk.Id == newDiskId);

                diskInDatabase.Manufacturer.Should().Be(newManufacturer);
                diskInDatabase.SerialCode.Should().Be(newSerialCode);
                diskInDatabase.xmin.Should().BeGreaterThan(0).And.NotBe(ignoredToken);
            });
        }

        [Fact(Skip = "There is no way to send the token, which is needed to find the related resource.")]
        public async Task Can_create_resource_with_relationship()
        {
            // Arrange
            var existingPartition = _fakers.Partition.Generate();

            var newManufacturer = _fakers.Disk.Generate().Manufacturer;
            var newSerialCode = _fakers.Disk.Generate().SerialCode;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Partitions.Add(existingPartition);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "disks",
                    attributes = new
                    {
                        manufacturer = newManufacturer,
                        serialCode = newSerialCode
                    },
                    relationships = new
                    {
                        partitions = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "partitions",
                                    id = existingPartition.StringId
                                }
                            }
                        }
                    }
                }
            };

            var route = "/disks";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Type.Should().Be("disks");
            responseDocument.SingleData.Attributes["manufacturer"].Should().Be(newManufacturer);
            responseDocument.SingleData.Attributes["serialCode"].Should().Be(newSerialCode);
            responseDocument.SingleData.Attributes["concurrencyToken"].As<long>().Should().BeGreaterThan(0);
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();

            var newDiskId = int.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var diskInDatabase = await dbContext.Disks
                    .Include(disk => disk.Partitions)
                    .FirstAsync(disk => disk.Id == newDiskId);

                diskInDatabase.Manufacturer.Should().Be(newManufacturer);
                diskInDatabase.SerialCode.Should().Be(newSerialCode);
                diskInDatabase.xmin.Should().BeGreaterThan(0);

                diskInDatabase.Partitions.Should().HaveCount(1);
                diskInDatabase.Partitions[0].Id.Should().Be(existingPartition.Id);
            });
        }

        [Fact]
        public async Task Can_update_resource_using_fresh_token()
        {
            // Arrange
            var existingDisk = _fakers.Disk.Generate();

            var newSerialCode = _fakers.Disk.Generate().SerialCode;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Disks.Add(existingDisk);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "disks",
                    id = existingDisk.StringId,
                    attributes = new
                    {
                        serialCode = newSerialCode,
                        concurrencyToken = existingDisk.xmin
                    }
                }
            };

            var route = "/disks/" + existingDisk.StringId;

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Type.Should().Be("disks");
            responseDocument.SingleData.Id.Should().Be(existingDisk.StringId);
            responseDocument.SingleData.Attributes["manufacturer"].Should().Be(existingDisk.Manufacturer);
            responseDocument.SingleData.Attributes["serialCode"].Should().Be(newSerialCode);
            responseDocument.SingleData.Attributes["concurrencyToken"].Should().NotBe(existingDisk.xmin);
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var diskInDatabase = await dbContext.Disks
                    .FirstAsync(disk => disk.Id == existingDisk.Id);

                diskInDatabase.Manufacturer.Should().Be(existingDisk.Manufacturer);
                diskInDatabase.SerialCode.Should().Be(newSerialCode);
                diskInDatabase.xmin.Should().NotBe(existingDisk.xmin);
            });
        }

        [Fact]
        public async Task Cannot_update_resource_using_stale_token()
        {
            // Arrange
            var existingDisk = _fakers.Disk.Generate();

            var newSerialCode = _fakers.Disk.Generate().SerialCode;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Disks.Add(existingDisk);
                await dbContext.SaveChangesAsync();

                await dbContext.Database.ExecuteSqlInterpolatedAsync(
                    $"update \"Disks\" set \"Manufacturer\" = 'other' where \"Id\" = {existingDisk.Id}");
            });

            var requestBody = new
            {
                data = new
                {
                    type = "disks",
                    id = existingDisk.StringId,
                    attributes = new
                    {
                        serialCode = newSerialCode,
                        concurrencyToken = existingDisk.xmin
                    }
                }
            };

            var route = "/disks/" + existingDisk.StringId;

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Conflict);

            responseDocument.Errors.Should().HaveCount(1);
            responseDocument.Errors[0].StatusCode.Should().Be(HttpStatusCode.Conflict);
            responseDocument.Errors[0].Title.Should().Be("The concurrency token is missing or does not match the server version. This indicates that data has been modified since the resource was retrieved.");
            responseDocument.Errors[0].Detail.Should().BeNull();
        }

        [Fact]
        public async Task Cannot_update_resource_without_token()
        {
            // Arrange
            var existingDisk = _fakers.Disk.Generate();

            var newSerialCode = _fakers.Disk.Generate().SerialCode;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Disks.Add(existingDisk);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "disks",
                    id = existingDisk.StringId,
                    attributes = new
                    {
                        serialCode = newSerialCode
                    }
                }
            };

            var route = "/disks/" + existingDisk.StringId;

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Conflict);

            responseDocument.Errors.Should().HaveCount(1);
            responseDocument.Errors[0].StatusCode.Should().Be(HttpStatusCode.Conflict);
            responseDocument.Errors[0].Title.Should().Be("The concurrency token is missing or does not match the server version. This indicates that data has been modified since the resource was retrieved.");
            responseDocument.Errors[0].Detail.Should().BeNull();
        }

        [Fact]
        public async Task Can_update_resource_with_HasOne_relationship()
        {
            // Arrange
            var existingPartition = _fakers.Partition.Generate();
            existingPartition.Owner = _fakers.Disk.Generate();

            var existingDisk = _fakers.Disk.Generate();

            var newFreeSpaceInBytes = _fakers.Partition.Generate().FreeSpaceInBytes;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddRange(existingPartition, existingDisk);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "partitions",
                    id = existingPartition.StringId,
                    attributes = new
                    {
                        freeSpaceInBytes = newFreeSpaceInBytes,
                        concurrencyToken = existingPartition.xmin
                    },
                    relationships = new
                    {
                        owner = new
                        {
                            data = new
                            {
                                type = "disks",
                                id = existingDisk.StringId
                            }
                        }
                    }
                }
            };

            var route = "/partitions/" + existingPartition.StringId;

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Type.Should().Be("partitions");
            responseDocument.SingleData.Attributes["capacityInBytes"].Should().Be(existingPartition.CapacityInBytes);
            responseDocument.SingleData.Attributes["freeSpaceInBytes"].Should().Be(newFreeSpaceInBytes);
            responseDocument.SingleData.Attributes["concurrencyToken"].As<long>().Should().BeGreaterThan(0);
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var partitionInDatabase = await dbContext.Partitions
                    .Include(partition => partition.Owner)
                    .FirstAsync(partition => partition.Id == existingPartition.Id);

                partitionInDatabase.CapacityInBytes.Should().Be(existingPartition.CapacityInBytes);
                partitionInDatabase.FreeSpaceInBytes.Should().Be(newFreeSpaceInBytes);
                partitionInDatabase.xmin.Should().BeGreaterThan(0);

                partitionInDatabase.Owner.Should().NotBeNull();
                partitionInDatabase.Owner.Id.Should().Be(existingDisk.Id);
            });
        }

        [Fact(Skip = "There is no way to send the token, which is needed to find the related resource.")]
        public async Task Can_update_resource_with_HasMany_relationship()
        {
            // Arrange
            var existingDisk = _fakers.Disk.Generate();
            existingDisk.Partitions = _fakers.Partition.Generate(1);

            var existingPartition = _fakers.Partition.Generate();

            var newSerialCode = _fakers.Disk.Generate().SerialCode;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddRange(existingDisk, existingPartition);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "disks",
                    id = existingDisk.StringId,
                    attributes = new
                    {
                        serialCode = newSerialCode,
                        concurrencyToken = existingDisk.xmin
                    },
                    relationships = new
                    {
                        partitions = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "partitions",
                                    id = existingPartition.StringId
                                }
                            }
                        }
                    }
                }
            };

            var route = "/disks/" + existingDisk.StringId;

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Type.Should().Be("disks");
            responseDocument.SingleData.Attributes["manufacturer"].Should().Be(existingDisk.Manufacturer);
            responseDocument.SingleData.Attributes["serialCode"].Should().Be(newSerialCode);
            responseDocument.SingleData.Attributes["concurrencyToken"].As<long>().Should().BeGreaterThan(0);
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var diskInDatabase = await dbContext.Disks
                    .Include(disk => disk.Partitions)
                    .FirstAsync(disk => disk.Id == existingDisk.Id);

                diskInDatabase.Manufacturer.Should().Be(existingDisk.Manufacturer);
                diskInDatabase.SerialCode.Should().Be(newSerialCode);
                diskInDatabase.xmin.Should().BeGreaterThan(0);

                diskInDatabase.Partitions.Should().HaveCount(1);
                diskInDatabase.Partitions[0].Id.Should().Be(existingPartition.Id);
            });
        }

        [Fact(Skip = "There is no way to send the token, which is needed to find the resource.")]
        public async Task Can_delete_resource()
        {
            // Arrange
            var existingDisk = _fakers.Disk.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Disks.Add(existingDisk);
                await dbContext.SaveChangesAsync();
            });

            var route = "/disks/" + existingDisk.StringId;

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var diskInDatabase = await dbContext.Disks
                    .FirstOrDefaultAsync(disk => disk.Id == existingDisk.Id);

                diskInDatabase.Should().BeNull();
            });
        }

        [Fact(Skip = "There is no way to send the token, which is needed to find the related resource.")]
        public async Task Can_add_to_HasMany_relationship()
        {
            // Arrange
            var existingDisk = _fakers.Disk.Generate();
            var existingPartition = _fakers.Partition.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddRange(existingDisk, existingPartition);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "partitions",
                        id = existingPartition.StringId
                    }
                }
            };

            var route = $"/disks/{existingDisk.StringId}/relationships/partitions";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var diskInDatabase = await dbContext.Disks
                    .Include(disk => disk.Partitions)
                    .FirstAsync(disk => disk.Id == existingDisk.Id);

                diskInDatabase.Partitions.Should().HaveCount(1);
                diskInDatabase.Partitions[0].Id.Should().Be(existingPartition.Id);
            });
        }

        [Fact]
        public async Task Can_remove_from_HasMany_relationship()
        {
            // Arrange
            var existingDisk = _fakers.Disk.Generate();
            existingDisk.Partitions = _fakers.Partition.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Disks.Add(existingDisk);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new[]
                {
                    new
                    {
                        type = "partitions",
                        id = existingDisk.Partitions[1].StringId
                    }
                }
            };

            var route = $"/disks/{existingDisk.StringId}/relationships/partitions";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var diskInDatabase = await dbContext.Disks
                    .Include(disk => disk.Partitions)
                    .FirstAsync(disk => disk.Id == existingDisk.Id);

                diskInDatabase.Partitions.Should().HaveCount(1);
                diskInDatabase.Partitions[0].Id.Should().Be(existingDisk.Partitions[0].Id);
            });
        }
    }
}
