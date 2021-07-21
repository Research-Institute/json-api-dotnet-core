using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ReadWrite.Creating
{
    public sealed class CreateResourceTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<ReadWriteDbContext>, ReadWriteDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<ReadWriteDbContext>, ReadWriteDbContext> _testContext;
        private readonly ReadWriteFakers _fakers = new();

        public CreateResourceTests(ExampleIntegrationTestContext<TestableStartup<ReadWriteDbContext>, ReadWriteDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<WorkItemsController>();
            testContext.UseController<WorkItemGroupsController>();
            testContext.UseController<UserAccountsController>();
            testContext.UseController<RgbColorsController>();

            var options = (JsonApiOptions)testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.UseRelativeLinks = false;
            options.AllowClientGeneratedIds = false;
        }

        [Fact]
        public async Task Sets_location_header_for_created_resource()
        {
            // Arrange
            WorkItem newWorkItem = _fakers.WorkItem.Generate();

            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    attributes = new
                    {
                        description = newWorkItem.Description
                    }
                }
            };

            const string route = "/workItems";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            string newWorkItemId = responseDocument.SingleData.Id;
            httpResponse.Headers.Location.Should().Be("/workItems/" + newWorkItemId);

            responseDocument.Links.Self.Should().Be("http://localhost/workItems");
            responseDocument.Links.First.Should().BeNull();

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Links.Self.Should().Be("http://localhost" + httpResponse.Headers.Location);
        }

        [Fact]
        public async Task Can_create_resource_with_int_ID()
        {
            // Arrange
            WorkItem newWorkItem = _fakers.WorkItem.Generate();
            newWorkItem.DueAt = null;

            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    attributes = new
                    {
                        description = newWorkItem.Description
                    }
                }
            };

            const string route = "/workItems";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Type.Should().Be("workItems");
            responseDocument.SingleData.Attributes["description"].Should().Be(newWorkItem.Description);
            responseDocument.SingleData.Attributes["dueAt"].Should().Be(newWorkItem.DueAt);
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();

            int newWorkItemId = int.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                WorkItem workItemInDatabase = await dbContext.WorkItems.FirstWithIdAsync(newWorkItemId);

                workItemInDatabase.Description.Should().Be(newWorkItem.Description);
                workItemInDatabase.DueAt.Should().Be(newWorkItem.DueAt);
            });

            PropertyInfo property = typeof(WorkItem).GetProperty(nameof(Identifiable.Id));
            property.Should().NotBeNull().And.Subject.PropertyType.Should().Be(typeof(int));
        }

        [Fact]
        public async Task Can_create_resource_with_long_ID()
        {
            // Arrange
            UserAccount newUserAccount = _fakers.UserAccount.Generate();

            var requestBody = new
            {
                data = new
                {
                    type = "userAccounts",
                    attributes = new
                    {
                        firstName = newUserAccount.FirstName,
                        lastName = newUserAccount.LastName
                    }
                }
            };

            const string route = "/userAccounts";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Type.Should().Be("userAccounts");
            responseDocument.SingleData.Attributes["firstName"].Should().Be(newUserAccount.FirstName);
            responseDocument.SingleData.Attributes["lastName"].Should().Be(newUserAccount.LastName);
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();

            long newUserAccountId = long.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                UserAccount userAccountInDatabase = await dbContext.UserAccounts.FirstWithIdAsync(newUserAccountId);

                userAccountInDatabase.FirstName.Should().Be(newUserAccount.FirstName);
                userAccountInDatabase.LastName.Should().Be(newUserAccount.LastName);
            });

            PropertyInfo property = typeof(UserAccount).GetProperty(nameof(Identifiable.Id));
            property.Should().NotBeNull().And.Subject.PropertyType.Should().Be(typeof(long));
        }

        [Fact]
        public async Task Can_create_resource_with_guid_ID()
        {
            // Arrange
            WorkItemGroup newGroup = _fakers.WorkItemGroup.Generate();

            var requestBody = new
            {
                data = new
                {
                    type = "workItemGroups",
                    attributes = new
                    {
                        name = newGroup.Name
                    }
                }
            };

            const string route = "/workItemGroups";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Type.Should().Be("workItemGroups");
            responseDocument.SingleData.Attributes["name"].Should().Be(newGroup.Name);
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();

            Guid newGroupId = Guid.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                WorkItemGroup groupInDatabase = await dbContext.Groups.FirstWithIdAsync(newGroupId);

                groupInDatabase.Name.Should().Be(newGroup.Name);
            });

            PropertyInfo property = typeof(WorkItemGroup).GetProperty(nameof(Identifiable.Id));
            property.Should().NotBeNull().And.Subject.PropertyType.Should().Be(typeof(Guid));
        }

        [Fact]
        public async Task Can_create_resource_without_attributes_or_relationships()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    attributes = new
                    {
                    },
                    relationship = new
                    {
                    }
                }
            };

            const string route = "/workItems";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Type.Should().Be("workItems");
            responseDocument.SingleData.Attributes["description"].Should().BeNull();
            responseDocument.SingleData.Attributes["dueAt"].Should().BeNull();
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();

            int newWorkItemId = int.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                WorkItem workItemInDatabase = await dbContext.WorkItems.FirstWithIdAsync(newWorkItemId);

                workItemInDatabase.Description.Should().BeNull();
                workItemInDatabase.DueAt.Should().BeNull();
            });
        }

        [Fact]
        public async Task Can_create_resource_with_unknown_attribute()
        {
            // Arrange
            WorkItem newWorkItem = _fakers.WorkItem.Generate();

            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    attributes = new
                    {
                        doesNotExist = "ignored",
                        description = newWorkItem.Description
                    }
                }
            };

            const string route = "/workItems";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Type.Should().Be("workItems");
            responseDocument.SingleData.Attributes["description"].Should().Be(newWorkItem.Description);
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();

            int newWorkItemId = int.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                WorkItem workItemInDatabase = await dbContext.WorkItems.FirstWithIdAsync(newWorkItemId);

                workItemInDatabase.Description.Should().Be(newWorkItem.Description);
            });
        }

        [Fact]
        public async Task Can_create_resource_with_unknown_relationship()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    relationships = new
                    {
                        doesNotExist = new
                        {
                            data = new
                            {
                                type = "doesNotExist",
                                id = 12345678
                            }
                        }
                    }
                }
            };

            const string route = "/workItems";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Type.Should().Be("workItems");
            responseDocument.SingleData.Attributes.Should().NotBeEmpty();
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();

            int newWorkItemId = int.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                WorkItem workItemInDatabase = await dbContext.WorkItems.FirstWithIdOrDefaultAsync(newWorkItemId);

                workItemInDatabase.Should().NotBeNull();
            });
        }

        [Fact]
        public async Task Cannot_create_resource_with_client_generated_ID()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "rgbColors",
                    id = "0A0B0C",
                    attributes = new
                    {
                        name = "Black"
                    }
                }
            };

            const string route = "/rgbColors";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Forbidden);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.Forbidden);
            error.Title.Should().Be("Specifying the resource ID in POST requests is not allowed.");
            error.Detail.Should().BeNull();
            error.Source.Pointer.Should().Be("/data/id");
        }

        [Fact]
        public async Task Cannot_create_resource_for_missing_request_body()
        {
            // Arrange
            string requestBody = string.Empty;

            const string route = "/workItems";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("Missing request body.");
            error.Detail.Should().BeNull();
        }

        [Fact]
        public async Task Cannot_create_resource_for_missing_type()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    attributes = new
                    {
                    }
                }
            };

            const string route = "/workItems";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Failed to deserialize request body: Request body must include 'type' element.");
            error.Detail.Should().StartWith("Expected 'type' element in 'data' element. - Request body: <<");
        }

        [Fact]
        public async Task Cannot_create_resource_for_unknown_type()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "doesNotExist",
                    attributes = new
                    {
                    }
                }
            };

            const string route = "/workItems";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Failed to deserialize request body: Request body includes unknown resource type.");
            error.Detail.Should().StartWith("Resource type 'doesNotExist' does not exist. - Request body: <<");
        }

        [Fact]
        public async Task Cannot_create_resource_on_unknown_resource_type_in_url()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    attributes = new
                    {
                    }
                }
            };

            const string route = "/doesNotExist";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePostAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);

            responseDocument.Should().BeEmpty();
        }

        [Fact]
        public async Task Cannot_create_on_resource_type_mismatch_between_url_and_body()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "rgbColors",
                    id = "0A0B0C"
                }
            };

            const string route = "/workItems";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Conflict);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.Conflict);
            error.Title.Should().Be("Resource type mismatch between request body and endpoint URL.");
            error.Detail.Should().Be("Expected resource of type 'workItems' in POST request body at endpoint '/workItems', instead of 'rgbColors'.");
        }

        [Fact]
        public async Task Cannot_create_resource_attribute_with_blocked_capability()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    attributes = new
                    {
                        concurrencyToken = "274E1D9A-91BE-4A42-B648-CA75E8B2945E"
                    }
                }
            };

            const string route = "/workItems";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Failed to deserialize request body: Setting the initial value of the requested attribute is not allowed.");
            error.Detail.Should().StartWith("Setting the initial value of 'concurrencyToken' is not allowed. - Request body:");
        }

        [Fact]
        public async Task Cannot_create_resource_with_readonly_attribute()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "workItemGroups",
                    attributes = new
                    {
                        concurrencyToken = "274E1D9A-91BE-4A42-B648-CA75E8B2945E"
                    }
                }
            };

            const string route = "/workItemGroups";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Failed to deserialize request body: Attribute is read-only.");
            error.Detail.Should().StartWith("Attribute 'concurrencyToken' is read-only. - Request body:");
        }

        [Fact]
        public async Task Cannot_create_resource_for_broken_JSON_request_body()
        {
            // Arrange
            const string requestBody = "{ \"data\" {";

            const string route = "/workItemGroups";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Failed to deserialize request body.");
            error.Detail.Should().StartWith("Invalid character after parsing");
        }

        [Fact]
        public async Task Cannot_create_resource_with_incompatible_attribute_value()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    attributes = new
                    {
                        dueAt = "not-a-valid-time"
                    }
                }
            };

            const string route = "/workItems";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Failed to deserialize request body.");
            error.Detail.Should().StartWith("Failed to convert 'not-a-valid-time' of type 'String' to type 'Nullable`1'. - Request body: <<");
        }

        [Fact]
        public async Task Can_create_resource_with_attributes_and_multiple_relationship_types()
        {
            // Arrange
            List<UserAccount> existingUserAccounts = _fakers.UserAccount.Generate(2);
            WorkTag existingTag = _fakers.WorkTag.Generate();

            string newDescription = _fakers.WorkItem.Generate().Description;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.UserAccounts.AddRange(existingUserAccounts);
                dbContext.WorkTags.Add(existingTag);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    attributes = new
                    {
                        description = newDescription
                    },
                    relationships = new
                    {
                        assignee = new
                        {
                            data = new
                            {
                                type = "userAccounts",
                                id = existingUserAccounts[0].StringId
                            }
                        },
                        subscribers = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "userAccounts",
                                    id = existingUserAccounts[1].StringId
                                }
                            }
                        },
                        tags = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "workTags",
                                    id = existingTag.StringId
                                }
                            }
                        }
                    }
                }
            };

            const string route = "/workItems";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Attributes["description"].Should().Be(newDescription);
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();

            int newWorkItemId = int.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                // @formatter:wrap_chained_method_calls chop_always
                // @formatter:keep_existing_linebreaks true

                WorkItem workItemInDatabase = await dbContext.WorkItems
                    .Include(workItem => workItem.Assignee)
                    .Include(workItem => workItem.Subscribers)
                    .Include(workItem => workItem.WorkItemTags)
                    .ThenInclude(workItemTag => workItemTag.Tag)
                    .FirstWithIdAsync(newWorkItemId);

                // @formatter:keep_existing_linebreaks restore
                // @formatter:wrap_chained_method_calls restore

                workItemInDatabase.Description.Should().Be(newDescription);

                workItemInDatabase.Assignee.Should().NotBeNull();
                workItemInDatabase.Assignee.Id.Should().Be(existingUserAccounts[0].Id);

                workItemInDatabase.Subscribers.Should().HaveCount(1);
                workItemInDatabase.Subscribers.Single().Id.Should().Be(existingUserAccounts[1].Id);

                workItemInDatabase.WorkItemTags.Should().HaveCount(1);
                workItemInDatabase.WorkItemTags.Single().Tag.Id.Should().Be(existingTag.Id);
            });
        }
    }
}
