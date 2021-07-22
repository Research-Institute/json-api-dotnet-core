using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.EntityFrameworkCore;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ReadWrite.Deleting
{
    public sealed class DeleteResourceTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<ReadWriteDbContext>, ReadWriteDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<ReadWriteDbContext>, ReadWriteDbContext> _testContext;
        private readonly ReadWriteFakers _fakers = new();

        public DeleteResourceTests(ExampleIntegrationTestContext<TestableStartup<ReadWriteDbContext>, ReadWriteDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<WorkItemsController>();
            testContext.UseController<WorkItemGroupsController>();
            testContext.UseController<RgbColorsController>();
        }

        [Fact]
        public async Task Can_delete_existing_resource()
        {
            // Arrange
            WorkItem existingWorkItem = _fakers.WorkItem.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(existingWorkItem);
                await dbContext.SaveChangesAsync();
            });

            string route = "/workItems/" + existingWorkItem.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                WorkItem workItemsInDatabase = await dbContext.WorkItems.FirstWithIdOrDefaultAsync(existingWorkItem.Id);

                workItemsInDatabase.Should().BeNull();
            });
        }

        [Fact]
        public async Task Cannot_delete_missing_resource()
        {
            // Arrange
            const string route = "/workItems/99999999";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteDeleteAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.NotFound);
            error.Title.Should().Be("The requested resource does not exist.");
            error.Detail.Should().Be("Resource of type 'workItems' with ID '99999999' does not exist.");
        }

        [Fact]
        public async Task Can_delete_resource_with_OneToOne_relationship_from_dependent_side()
        {
            // Arrange
            RgbColor existingColor = _fakers.RgbColor.Generate();
            existingColor.Group = _fakers.WorkItemGroup.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.RgbColors.Add(existingColor);
                await dbContext.SaveChangesAsync();
            });

            string route = "/rgbColors/" + existingColor.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                RgbColor colorsInDatabase = await dbContext.RgbColors.FirstWithIdOrDefaultAsync(existingColor.Id);

                colorsInDatabase.Should().BeNull();

                WorkItemGroup groupInDatabase = await dbContext.Groups.FirstWithIdAsync(existingColor.Group.Id);

                groupInDatabase.Color.Should().BeNull();
            });
        }

        [Fact]
        public async Task Can_delete_existing_resource_with_OneToOne_relationship_from_principal_side()
        {
            // Arrange
            WorkItemGroup existingGroup = _fakers.WorkItemGroup.Generate();
            existingGroup.Color = _fakers.RgbColor.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Groups.Add(existingGroup);
                await dbContext.SaveChangesAsync();
            });

            string route = "/workItemGroups/" + existingGroup.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                WorkItemGroup groupsInDatabase = await dbContext.Groups.FirstWithIdOrDefaultAsync(existingGroup.Id);

                groupsInDatabase.Should().BeNull();

                RgbColor colorInDatabase = await dbContext.RgbColors.FirstWithIdOrDefaultAsync(existingGroup.Color.Id);

                colorInDatabase.Should().NotBeNull();
                colorInDatabase.Group.Should().BeNull();
            });
        }

        [Fact]
        public async Task Can_delete_existing_resource_with_HasMany_relationship()
        {
            // Arrange
            WorkItem existingWorkItem = _fakers.WorkItem.Generate();
            existingWorkItem.Subscribers = _fakers.UserAccount.Generate(2).ToHashSet();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(existingWorkItem);
                await dbContext.SaveChangesAsync();
            });

            string route = "/workItems/" + existingWorkItem.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                WorkItem workItemInDatabase = await dbContext.WorkItems.FirstWithIdOrDefaultAsync(existingWorkItem.Id);

                workItemInDatabase.Should().BeNull();

                List<UserAccount> userAccountsInDatabase = await dbContext.UserAccounts.ToListAsync();

                userAccountsInDatabase.Should().ContainSingle(userAccount => userAccount.Id == existingWorkItem.Subscribers.ElementAt(0).Id);
                userAccountsInDatabase.Should().ContainSingle(userAccount => userAccount.Id == existingWorkItem.Subscribers.ElementAt(1).Id);
            });
        }

        [Fact]
        public async Task Can_delete_resource_with_HasManyThrough_relationship()
        {
            // Arrange
            var existingWorkItemTag = new WorkItemTag
            {
                Item = _fakers.WorkItem.Generate(),
                Tag = _fakers.WorkTag.Generate()
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItemTags.Add(existingWorkItemTag);
                await dbContext.SaveChangesAsync();
            });

            string route = "/workItems/" + existingWorkItemTag.Item.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                WorkItem workItemsInDatabase = await dbContext.WorkItems.FirstWithIdOrDefaultAsync(existingWorkItemTag.Item.Id);

                workItemsInDatabase.Should().BeNull();

                WorkItemTag workItemTagsInDatabase =
                    await dbContext.WorkItemTags.FirstOrDefaultAsync(workItemTag => workItemTag.Item.Id == existingWorkItemTag.Item.Id);

                workItemTagsInDatabase.Should().BeNull();
            });
        }
    }
}
