using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExampleTests.Startups;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ReadWrite.Fetching
{
    public sealed class FetchResourceTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<ReadWriteDbContext>, ReadWriteDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<ReadWriteDbContext>, ReadWriteDbContext> _testContext;
        private readonly ReadWriteFakers _fakers = new();

        public FetchResourceTests(ExampleIntegrationTestContext<TestableStartup<ReadWriteDbContext>, ReadWriteDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<WorkItemsController>();
            testContext.UseController<UserAccountsController>();
        }

        [Fact]
        public async Task Can_get_primary_resources()
        {
            // Arrange
            List<WorkItem> workItems = _fakers.WorkItem.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                await dbContext.ClearTableAsync<WorkItem>();
                dbContext.WorkItems.AddRange(workItems);
                await dbContext.SaveChangesAsync();
            });

            const string route = "/workItems";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(2);

            ResourceObject item1 = responseDocument.ManyData.Single(resource => resource.Id == workItems[0].StringId);
            item1.Type.Should().Be("workItems");
            item1.Attributes["description"].Should().Be(workItems[0].Description);
            item1.Attributes["dueAt"].Should().BeCloseTo(workItems[0].DueAt);
            item1.Attributes["priority"].Should().Be(workItems[0].Priority.ToString("G"));
            item1.Relationships.Should().NotBeEmpty();

            ResourceObject item2 = responseDocument.ManyData.Single(resource => resource.Id == workItems[1].StringId);
            item2.Type.Should().Be("workItems");
            item2.Attributes["description"].Should().Be(workItems[1].Description);
            item2.Attributes["dueAt"].Should().BeCloseTo(workItems[1].DueAt);
            item2.Attributes["priority"].Should().Be(workItems[1].Priority.ToString("G"));
            item2.Relationships.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Cannot_get_primary_resources_for_unknown_type()
        {
            // Arrange
            const string route = "/doesNotExist";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecuteGetAsync<string>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);

            responseDocument.Should().BeEmpty();
        }

        [Fact]
        public async Task Can_get_primary_resource_by_ID()
        {
            // Arrange
            WorkItem workItem = _fakers.WorkItem.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(workItem);
                await dbContext.SaveChangesAsync();
            });

            string route = "/workItems/" + workItem.StringId;

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Type.Should().Be("workItems");
            responseDocument.SingleData.Id.Should().Be(workItem.StringId);
            responseDocument.SingleData.Attributes["description"].Should().Be(workItem.Description);
            responseDocument.SingleData.Attributes["dueAt"].Should().BeCloseTo(workItem.DueAt);
            responseDocument.SingleData.Attributes["priority"].Should().Be(workItem.Priority.ToString("G"));
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Cannot_get_primary_resource_for_unknown_type()
        {
            // Arrange
            const string route = "/doesNotExist/99999999";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecuteGetAsync<string>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);

            responseDocument.Should().BeEmpty();
        }

        [Fact]
        public async Task Cannot_get_primary_resource_for_unknown_ID()
        {
            // Arrange
            const string route = "/workItems/99999999";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.NotFound);
            error.Title.Should().Be("The requested resource does not exist.");
            error.Detail.Should().Be("Resource of type 'workItems' with ID '99999999' does not exist.");
        }

        [Fact]
        public async Task Can_get_secondary_HasOne_resource()
        {
            // Arrange
            WorkItem workItem = _fakers.WorkItem.Generate();
            workItem.Assignee = _fakers.UserAccount.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(workItem);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/workItems/{workItem.StringId}/assignee";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Type.Should().Be("userAccounts");
            responseDocument.SingleData.Id.Should().Be(workItem.Assignee.StringId);
            responseDocument.SingleData.Attributes["firstName"].Should().Be(workItem.Assignee.FirstName);
            responseDocument.SingleData.Attributes["lastName"].Should().Be(workItem.Assignee.LastName);
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Can_get_unknown_secondary_HasOne_resource()
        {
            // Arrange
            WorkItem workItem = _fakers.WorkItem.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(workItem);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/workItems/{workItem.StringId}/assignee";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.Data.Should().BeNull();
        }

        [Fact]
        public async Task Can_get_secondary_HasMany_resources()
        {
            // Arrange
            UserAccount userAccount = _fakers.UserAccount.Generate();
            userAccount.AssignedItems = _fakers.WorkItem.Generate(2).ToHashSet();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.UserAccounts.Add(userAccount);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/userAccounts/{userAccount.StringId}/assignedItems";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(2);

            ResourceObject item1 = responseDocument.ManyData.Single(resource => resource.Id == userAccount.AssignedItems.ElementAt(0).StringId);
            item1.Type.Should().Be("workItems");
            item1.Attributes["description"].Should().Be(userAccount.AssignedItems.ElementAt(0).Description);
            item1.Attributes["dueAt"].Should().BeCloseTo(userAccount.AssignedItems.ElementAt(0).DueAt);
            item1.Attributes["priority"].Should().Be(userAccount.AssignedItems.ElementAt(0).Priority.ToString("G"));
            item1.Relationships.Should().NotBeEmpty();

            ResourceObject item2 = responseDocument.ManyData.Single(resource => resource.Id == userAccount.AssignedItems.ElementAt(1).StringId);
            item2.Type.Should().Be("workItems");
            item2.Attributes["description"].Should().Be(userAccount.AssignedItems.ElementAt(1).Description);
            item2.Attributes["dueAt"].Should().BeCloseTo(userAccount.AssignedItems.ElementAt(1).DueAt);
            item2.Attributes["priority"].Should().Be(userAccount.AssignedItems.ElementAt(1).Priority.ToString("G"));
            item2.Relationships.Should().NotBeEmpty();
        }

        [Fact]
        public async Task Can_get_unknown_secondary_HasMany_resource()
        {
            // Arrange
            UserAccount userAccount = _fakers.UserAccount.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.UserAccounts.Add(userAccount);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/userAccounts/{userAccount.StringId}/assignedItems";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().BeEmpty();
        }

        [Fact]
        public async Task Can_get_secondary_HasManyThrough_resources()
        {
            // Arrange
            WorkItem workItem = _fakers.WorkItem.Generate();

            workItem.WorkItemTags = new List<WorkItemTag>
            {
                new()
                {
                    Tag = _fakers.WorkTag.Generate()
                },
                new()
                {
                    Tag = _fakers.WorkTag.Generate()
                }
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(workItem);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/workItems/{workItem.StringId}/tags";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().HaveCount(2);

            ResourceObject item1 = responseDocument.ManyData.Single(resource => resource.Id == workItem.WorkItemTags.ElementAt(0).Tag.StringId);
            item1.Type.Should().Be("workTags");
            item1.Attributes["text"].Should().Be(workItem.WorkItemTags.ElementAt(0).Tag.Text);
            item1.Attributes["isBuiltIn"].Should().Be(workItem.WorkItemTags.ElementAt(0).Tag.IsBuiltIn);
            item1.Relationships.Should().BeNull();

            ResourceObject item2 = responseDocument.ManyData.Single(resource => resource.Id == workItem.WorkItemTags.ElementAt(1).Tag.StringId);
            item2.Type.Should().Be("workTags");
            item2.Attributes["text"].Should().Be(workItem.WorkItemTags.ElementAt(1).Tag.Text);
            item2.Attributes["isBuiltIn"].Should().Be(workItem.WorkItemTags.ElementAt(1).Tag.IsBuiltIn);
            item2.Relationships.Should().BeNull();
        }

        [Fact]
        public async Task Can_get_unknown_secondary_HasManyThrough_resources()
        {
            // Arrange
            WorkItem workItem = _fakers.WorkItem.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(workItem);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/workItems/{workItem.StringId}/tags";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecuteGetAsync<Document>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.ManyData.Should().BeEmpty();
        }

        [Fact]
        public async Task Cannot_get_secondary_resource_for_unknown_primary_type()
        {
            // Arrange
            const string route = "/doesNotExist/99999999/assignee";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecuteGetAsync<string>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);

            responseDocument.Should().BeEmpty();
        }

        [Fact]
        public async Task Cannot_get_secondary_resource_for_unknown_primary_ID()
        {
            // Arrange
            const string route = "/workItems/99999999/assignee";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.NotFound);
            error.Title.Should().Be("The requested resource does not exist.");
            error.Detail.Should().Be("Resource of type 'workItems' with ID '99999999' does not exist.");
        }

        [Fact]
        public async Task Cannot_get_secondary_resource_for_unknown_secondary_type()
        {
            // Arrange
            WorkItem workItem = _fakers.WorkItem.Generate();
            workItem.Assignee = _fakers.UserAccount.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(workItem);
                await dbContext.SaveChangesAsync();
            });

            string route = $"/workItems/{workItem.StringId}/doesNotExist";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.NotFound);
            error.Title.Should().Be("The requested relationship does not exist.");
            error.Detail.Should().Be("Resource of type 'workItems' does not contain a relationship named 'doesNotExist'.");
        }
    }
}
