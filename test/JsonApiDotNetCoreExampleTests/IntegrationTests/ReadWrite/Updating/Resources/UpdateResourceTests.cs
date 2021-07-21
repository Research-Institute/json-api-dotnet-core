using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExampleTests.Startups;
using Microsoft.EntityFrameworkCore;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ReadWrite.Updating.Resources
{
    public sealed class UpdateResourceTests : IClassFixture<ExampleIntegrationTestContext<TestableStartup<ReadWriteDbContext>, ReadWriteDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<ReadWriteDbContext>, ReadWriteDbContext> _testContext;
        private readonly ReadWriteFakers _fakers = new();

        public UpdateResourceTests(ExampleIntegrationTestContext<TestableStartup<ReadWriteDbContext>, ReadWriteDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<WorkItemsController>();
            testContext.UseController<WorkItemGroupsController>();
            testContext.UseController<UserAccountsController>();
            testContext.UseController<RgbColorsController>();
        }

        [Fact]
        public async Task Can_update_resource_without_attributes_or_relationships()
        {
            // Arrange
            UserAccount existingUserAccount = _fakers.UserAccount.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.UserAccounts.Add(existingUserAccount);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "userAccounts",
                    id = existingUserAccount.StringId,
                    attributes = new
                    {
                    },
                    relationships = new
                    {
                    }
                }
            };

            string route = "/userAccounts/" + existingUserAccount.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                UserAccount userAccountInDatabase = await dbContext.UserAccounts.FirstWithIdAsync(existingUserAccount.Id);

                userAccountInDatabase.FirstName.Should().Be(existingUserAccount.FirstName);
                userAccountInDatabase.LastName.Should().Be(existingUserAccount.LastName);
            });
        }

        [Fact]
        public async Task Can_update_resource_with_unknown_attribute()
        {
            // Arrange
            UserAccount existingUserAccount = _fakers.UserAccount.Generate();
            string newFirstName = _fakers.UserAccount.Generate().FirstName;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.UserAccounts.Add(existingUserAccount);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "userAccounts",
                    id = existingUserAccount.StringId,
                    attributes = new
                    {
                        firstName = newFirstName,
                        doesNotExist = "Ignored"
                    }
                }
            };

            string route = "/userAccounts/" + existingUserAccount.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                UserAccount userAccountInDatabase = await dbContext.UserAccounts.FirstWithIdAsync(existingUserAccount.Id);

                userAccountInDatabase.FirstName.Should().Be(newFirstName);
                userAccountInDatabase.LastName.Should().Be(existingUserAccount.LastName);
            });
        }

        [Fact]
        public async Task Can_update_resource_with_unknown_relationship()
        {
            // Arrange
            UserAccount existingUserAccount = _fakers.UserAccount.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.UserAccounts.Add(existingUserAccount);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "userAccounts",
                    id = existingUserAccount.StringId,
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

            string route = "/userAccounts/" + existingUserAccount.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();
        }

        [Fact]
        public async Task Can_partially_update_resource_with_guid_ID()
        {
            // Arrange
            WorkItemGroup existingGroup = _fakers.WorkItemGroup.Generate();
            string newName = _fakers.WorkItemGroup.Generate().Name;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Groups.Add(existingGroup);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "workItemGroups",
                    id = existingGroup.StringId,
                    attributes = new
                    {
                        name = newName
                    }
                }
            };

            string route = "/workItemGroups/" + existingGroup.StringId;

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Type.Should().Be("workItemGroups");
            responseDocument.SingleData.Id.Should().Be(existingGroup.StringId);
            responseDocument.SingleData.Attributes["name"].Should().Be(newName);
            responseDocument.SingleData.Attributes["isPublic"].Should().Be(existingGroup.IsPublic);
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                WorkItemGroup groupInDatabase = await dbContext.Groups.FirstWithIdAsync(existingGroup.Id);

                groupInDatabase.Name.Should().Be(newName);
                groupInDatabase.IsPublic.Should().Be(existingGroup.IsPublic);
            });

            PropertyInfo property = typeof(WorkItemGroup).GetProperty(nameof(Identifiable.Id));
            property.Should().NotBeNull().And.Subject.PropertyType.Should().Be(typeof(Guid));
        }

        [Fact]
        public async Task Can_completely_update_resource_with_string_ID()
        {
            // Arrange
            RgbColor existingColor = _fakers.RgbColor.Generate();
            string newDisplayName = _fakers.RgbColor.Generate().DisplayName;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.RgbColors.Add(existingColor);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "rgbColors",
                    id = existingColor.StringId,
                    attributes = new
                    {
                        displayName = newDisplayName
                    }
                }
            };

            string route = "/rgbColors/" + existingColor.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                RgbColor colorInDatabase = await dbContext.RgbColors.FirstWithIdAsync(existingColor.Id);

                colorInDatabase.DisplayName.Should().Be(newDisplayName);
            });

            PropertyInfo property = typeof(RgbColor).GetProperty(nameof(Identifiable.Id));
            property.Should().NotBeNull().And.Subject.PropertyType.Should().Be(typeof(string));
        }

        [Fact]
        public async Task Can_update_resource_without_side_effects()
        {
            // Arrange
            UserAccount existingUserAccount = _fakers.UserAccount.Generate();
            UserAccount newUserAccount = _fakers.UserAccount.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.UserAccounts.Add(existingUserAccount);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "userAccounts",
                    id = existingUserAccount.StringId,
                    attributes = new
                    {
                        firstName = newUserAccount.FirstName,
                        lastName = newUserAccount.LastName
                    }
                }
            };

            string route = "/userAccounts/" + existingUserAccount.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                UserAccount userAccountInDatabase = await dbContext.UserAccounts.FirstWithIdAsync(existingUserAccount.Id);

                userAccountInDatabase.FirstName.Should().Be(newUserAccount.FirstName);
                userAccountInDatabase.LastName.Should().Be(newUserAccount.LastName);
            });
        }

        [Fact]
        public async Task Can_update_resource_with_side_effects()
        {
            // Arrange
            WorkItem existingWorkItem = _fakers.WorkItem.Generate();
            string newDescription = _fakers.WorkItem.Generate().Description;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(existingWorkItem);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    id = existingWorkItem.StringId,
                    attributes = new
                    {
                        description = newDescription,
                        dueAt = (DateTime?)null
                    }
                }
            };

            string route = "/workItems/" + existingWorkItem.StringId;

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Type.Should().Be("workItems");
            responseDocument.SingleData.Id.Should().Be(existingWorkItem.StringId);
            responseDocument.SingleData.Attributes["description"].Should().Be(newDescription);
            responseDocument.SingleData.Attributes["dueAt"].Should().BeNull();
            responseDocument.SingleData.Attributes["priority"].Should().Be(existingWorkItem.Priority.ToString("G"));
            responseDocument.SingleData.Attributes.Should().ContainKey("concurrencyToken");
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                WorkItem workItemInDatabase = await dbContext.WorkItems.FirstWithIdAsync(existingWorkItem.Id);

                workItemInDatabase.Description.Should().Be(newDescription);
                workItemInDatabase.DueAt.Should().BeNull();
                workItemInDatabase.Priority.Should().Be(existingWorkItem.Priority);
            });
        }

        [Fact]
        public async Task Can_update_resource_with_side_effects_with_primary_fieldset()
        {
            // Arrange
            WorkItem existingWorkItem = _fakers.WorkItem.Generate();
            string newDescription = _fakers.WorkItem.Generate().Description;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(existingWorkItem);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    id = existingWorkItem.StringId,
                    attributes = new
                    {
                        description = newDescription,
                        dueAt = (DateTime?)null
                    }
                }
            };

            string route = $"/workItems/{existingWorkItem.StringId}?fields[workItems]=description,priority";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Type.Should().Be("workItems");
            responseDocument.SingleData.Id.Should().Be(existingWorkItem.StringId);
            responseDocument.SingleData.Attributes.Should().HaveCount(2);
            responseDocument.SingleData.Attributes["description"].Should().Be(newDescription);
            responseDocument.SingleData.Attributes["priority"].Should().Be(existingWorkItem.Priority.ToString("G"));
            responseDocument.SingleData.Relationships.Should().BeNull();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                WorkItem workItemInDatabase = await dbContext.WorkItems.FirstWithIdAsync(existingWorkItem.Id);

                workItemInDatabase.Description.Should().Be(newDescription);
                workItemInDatabase.DueAt.Should().BeNull();
                workItemInDatabase.Priority.Should().Be(existingWorkItem.Priority);
            });
        }

        [Fact]
        public async Task Can_update_resource_with_side_effects_with_include_and_fieldsets()
        {
            // Arrange
            WorkItem existingWorkItem = _fakers.WorkItem.Generate();

            existingWorkItem.WorkItemTags = new[]
            {
                new WorkItemTag
                {
                    Tag = _fakers.WorkTag.Generate()
                }
            };

            string newDescription = _fakers.WorkItem.Generate().Description;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(existingWorkItem);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    id = existingWorkItem.StringId,
                    attributes = new
                    {
                        description = newDescription,
                        dueAt = (DateTime?)null
                    }
                }
            };

            string route = $"/workItems/{existingWorkItem.StringId}?fields[workItems]=description,priority,tags&include=tags&fields[workTags]=text";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Type.Should().Be("workItems");
            responseDocument.SingleData.Id.Should().Be(existingWorkItem.StringId);
            responseDocument.SingleData.Attributes.Should().HaveCount(2);
            responseDocument.SingleData.Attributes["description"].Should().Be(newDescription);
            responseDocument.SingleData.Attributes["priority"].Should().Be(existingWorkItem.Priority.ToString("G"));
            responseDocument.SingleData.Relationships.Should().HaveCount(1);
            responseDocument.SingleData.Relationships["tags"].ManyData.Should().HaveCount(1);
            responseDocument.SingleData.Relationships["tags"].ManyData[0].Id.Should().Be(existingWorkItem.WorkItemTags.Single().Tag.StringId);

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Type.Should().Be("workTags");
            responseDocument.Included[0].Id.Should().Be(existingWorkItem.WorkItemTags.Single().Tag.StringId);
            responseDocument.Included[0].Attributes.Should().HaveCount(1);
            responseDocument.Included[0].Attributes["text"].Should().Be(existingWorkItem.WorkItemTags.Single().Tag.Text);
            responseDocument.Included[0].Relationships.Should().BeNull();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                WorkItem workItemInDatabase = await dbContext.WorkItems.FirstWithIdAsync(existingWorkItem.Id);

                workItemInDatabase.Description.Should().Be(newDescription);
                workItemInDatabase.DueAt.Should().BeNull();
                workItemInDatabase.Priority.Should().Be(existingWorkItem.Priority);
            });
        }

        [Fact]
        public async Task Update_resource_with_side_effects_hides_relationship_data_in_response()
        {
            // Arrange
            WorkItem existingWorkItem = _fakers.WorkItem.Generate();
            existingWorkItem.Assignee = _fakers.UserAccount.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(existingWorkItem);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    id = existingWorkItem.StringId
                }
            };

            string route = "/workItems/" + existingWorkItem.StringId;

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();
            responseDocument.SingleData.Relationships.Values.Should().OnlyContain(relationshipEntry => relationshipEntry.Data == null);

            responseDocument.Included.Should().BeNull();
        }

        [Fact]
        public async Task Cannot_update_resource_for_missing_request_body()
        {
            // Arrange
            WorkItem existingWorkItem = _fakers.WorkItem.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(existingWorkItem);
                await dbContext.SaveChangesAsync();
            });

            string requestBody = string.Empty;

            string route = "/workItems/" + existingWorkItem.StringId;

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.BadRequest);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            error.Title.Should().Be("Missing request body.");
            error.Detail.Should().BeNull();
        }

        [Fact]
        public async Task Cannot_update_resource_for_missing_type()
        {
            // Arrange
            WorkItem existingWorkItem = _fakers.WorkItem.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(existingWorkItem);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    id = existingWorkItem.StringId
                }
            };

            string route = "/workItems/" + existingWorkItem.StringId;

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Failed to deserialize request body: Request body must include 'type' element.");
            error.Detail.Should().StartWith("Expected 'type' element in 'data' element. - Request body: <<");
        }

        [Fact]
        public async Task Cannot_update_resource_for_unknown_type()
        {
            // Arrange
            WorkItem existingWorkItem = _fakers.WorkItem.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(existingWorkItem);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "doesNotExist",
                    id = existingWorkItem.StringId
                }
            };

            string route = "/workItems/" + existingWorkItem.StringId;

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Failed to deserialize request body: Request body includes unknown resource type.");
            error.Detail.Should().StartWith("Resource type 'doesNotExist' does not exist. - Request body: <<");
        }

        [Fact]
        public async Task Cannot_update_resource_for_missing_ID()
        {
            // Arrange
            WorkItem existingWorkItem = _fakers.WorkItem.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(existingWorkItem);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "workItems"
                }
            };

            string route = "/workItems/" + existingWorkItem.StringId;

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Failed to deserialize request body: Request body must include 'id' element.");
            error.Detail.Should().StartWith("Request body: <<");
        }

        [Fact]
        public async Task Cannot_update_resource_on_unknown_resource_type_in_url()
        {
            // Arrange
            WorkItem existingWorkItem = _fakers.WorkItem.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(existingWorkItem);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    id = existingWorkItem.StringId
                }
            };

            string route = "/doesNotExist/" + existingWorkItem.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);

            responseDocument.Should().BeEmpty();
        }

        [Fact]
        public async Task Cannot_update_resource_on_unknown_resource_ID_in_url()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    id = 99999999
                }
            };

            const string route = "/workItems/99999999";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.NotFound);
            error.Title.Should().Be("The requested resource does not exist.");
            error.Detail.Should().Be("Resource of type 'workItems' with ID '99999999' does not exist.");
        }

        [Fact]
        public async Task Cannot_update_on_resource_type_mismatch_between_url_and_body()
        {
            // Arrange
            WorkItem existingWorkItem = _fakers.WorkItem.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(existingWorkItem);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "rgbColors",
                    id = existingWorkItem.StringId
                }
            };

            string route = "/workItems/" + existingWorkItem.StringId;

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Conflict);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.Conflict);
            error.Title.Should().Be("Resource type mismatch between request body and endpoint URL.");

            error.Detail.Should().Be("Expected resource of type 'workItems' in PATCH request body at endpoint " +
                $"'/workItems/{existingWorkItem.StringId}', instead of 'rgbColors'.");
        }

        [Fact]
        public async Task Cannot_update_on_resource_ID_mismatch_between_url_and_body()
        {
            // Arrange
            List<WorkItem> existingWorkItems = _fakers.WorkItem.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.AddRange(existingWorkItems);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    id = existingWorkItems[0].StringId
                }
            };

            string route = "/workItems/" + existingWorkItems[1].StringId;

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Conflict);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.Conflict);
            error.Title.Should().Be("Resource ID mismatch between request body and endpoint URL.");

            error.Detail.Should().Be($"Expected resource ID '{existingWorkItems[1].StringId}' in PATCH request body at endpoint " +
                $"'/workItems/{existingWorkItems[1].StringId}', instead of '{existingWorkItems[0].StringId}'.");
        }

        [Fact]
        public async Task Cannot_update_resource_attribute_with_blocked_capability()
        {
            // Arrange
            WorkItem existingWorkItem = _fakers.WorkItem.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(existingWorkItem);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    id = existingWorkItem.StringId,
                    attributes = new
                    {
                        concurrencyToken = "274E1D9A-91BE-4A42-B648-CA75E8B2945E"
                    }
                }
            };

            string route = "/workItems/" + existingWorkItem.StringId;

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Failed to deserialize request body: Changing the value of the requested attribute is not allowed.");
            error.Detail.Should().StartWith("Changing the value of 'concurrencyToken' is not allowed. - Request body:");
        }

        [Fact]
        public async Task Cannot_update_resource_with_readonly_attribute()
        {
            // Arrange
            WorkItem existingWorkItem = _fakers.WorkItem.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(existingWorkItem);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "workItemGroups",
                    id = existingWorkItem.StringId,
                    attributes = new
                    {
                        concurrencyToken = "274E1D9A-91BE-4A42-B648-CA75E8B2945E"
                    }
                }
            };

            string route = "/workItemGroups/" + existingWorkItem.StringId;

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Failed to deserialize request body: Attribute is read-only.");
            error.Detail.Should().StartWith("Attribute 'concurrencyToken' is read-only. - Request body:");
        }

        [Fact]
        public async Task Cannot_update_resource_for_broken_JSON_request_body()
        {
            // Arrange
            WorkItem existingWorkItem = _fakers.WorkItem.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(existingWorkItem);
                await dbContext.SaveChangesAsync();
            });

            const string requestBody = "{ \"data\" {";

            string route = "/workItemGroups/" + existingWorkItem.StringId;

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Failed to deserialize request body.");
            error.Detail.Should().StartWith("Invalid character after parsing");
        }

        [Fact]
        public async Task Cannot_change_ID_of_existing_resource()
        {
            // Arrange
            WorkItem existingWorkItem = _fakers.WorkItem.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(existingWorkItem);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    id = existingWorkItem.StringId,
                    attributes = new
                    {
                        id = existingWorkItem.Id + 123456
                    }
                }
            };

            string route = "/workItems/" + existingWorkItem.StringId;

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Failed to deserialize request body: Resource ID is read-only.");
            error.Detail.Should().StartWith("Resource ID is read-only. - Request body: <<");
        }

        [Fact]
        public async Task Cannot_update_resource_with_incompatible_attribute_value()
        {
            // Arrange
            WorkItem existingWorkItem = _fakers.WorkItem.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(existingWorkItem);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    id = existingWorkItem.StringId,
                    attributes = new
                    {
                        dueAt = "not-a-valid-time"
                    }
                }
            };

            string route = "/workItems/" + existingWorkItem.StringId;

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.UnprocessableEntity);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
            error.Title.Should().Be("Failed to deserialize request body.");
            error.Detail.Should().StartWith("Failed to convert 'not-a-valid-time' of type 'String' to type 'Nullable`1'. - Request body: <<");
        }

        [Fact]
        public async Task Can_update_resource_with_attributes_and_multiple_relationship_types()
        {
            // Arrange
            WorkItem existingWorkItem = _fakers.WorkItem.Generate();
            existingWorkItem.Assignee = _fakers.UserAccount.Generate();
            existingWorkItem.Subscribers = _fakers.UserAccount.Generate(1).ToHashSet();

            existingWorkItem.WorkItemTags = new List<WorkItemTag>
            {
                new()
                {
                    Tag = _fakers.WorkTag.Generate()
                }
            };

            List<UserAccount> existingUserAccounts = _fakers.UserAccount.Generate(2);
            WorkTag existingTag = _fakers.WorkTag.Generate();

            string newDescription = _fakers.WorkItem.Generate().Description;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddInRange(existingWorkItem, existingTag);
                dbContext.UserAccounts.AddRange(existingUserAccounts);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    id = existingWorkItem.StringId,
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

            string route = "/workItems/" + existingWorkItem.StringId;

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Attributes["description"].Should().Be(newDescription);
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                // @formatter:wrap_chained_method_calls chop_always
                // @formatter:keep_existing_linebreaks true

                WorkItem workItemInDatabase = await dbContext.WorkItems
                    .Include(workItem => workItem.Assignee)
                    .Include(workItem => workItem.Subscribers)
                    .Include(workItem => workItem.WorkItemTags)
                    .ThenInclude(workItemTag => workItemTag.Tag)
                    .FirstWithIdAsync(existingWorkItem.Id);

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

        [Fact]
        public async Task Can_update_resource_with_multiple_cyclic_relationship_types()
        {
            // Arrange
            WorkItem existingWorkItem = _fakers.WorkItem.Generate();
            existingWorkItem.Parent = _fakers.WorkItem.Generate();
            existingWorkItem.Children = _fakers.WorkItem.Generate(1);

            existingWorkItem.RelatedToItems = new List<WorkItemToWorkItem>
            {
                new()
                {
                    ToItem = _fakers.WorkItem.Generate()
                }
            };

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkItems.Add(existingWorkItem);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    id = existingWorkItem.StringId,
                    relationships = new
                    {
                        parent = new
                        {
                            data = new
                            {
                                type = "workItems",
                                id = existingWorkItem.StringId
                            }
                        },
                        children = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "workItems",
                                    id = existingWorkItem.StringId
                                }
                            }
                        },
                        relatedTo = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "workItems",
                                    id = existingWorkItem.StringId
                                }
                            }
                        }
                    }
                }
            };

            string route = "/workItems/" + existingWorkItem.StringId;

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.OK);

            responseDocument.SingleData.Should().NotBeNull();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                // @formatter:wrap_chained_method_calls chop_always
                // @formatter:keep_existing_linebreaks true

                WorkItem workItemInDatabase = await dbContext.WorkItems
                    .Include(workItem => workItem.Parent)
                    .Include(workItem => workItem.Children)
                    .Include(workItem => workItem.RelatedToItems)
                    .ThenInclude(workItemToWorkItem => workItemToWorkItem.ToItem)
                    .FirstWithIdAsync(existingWorkItem.Id);

                // @formatter:keep_existing_linebreaks restore
                // @formatter:wrap_chained_method_calls restore

                workItemInDatabase.Parent.Should().NotBeNull();
                workItemInDatabase.Parent.Id.Should().Be(existingWorkItem.Id);

                workItemInDatabase.Children.Should().HaveCount(1);
                workItemInDatabase.Children.Single().Id.Should().Be(existingWorkItem.Id);

                workItemInDatabase.RelatedToItems.Should().HaveCount(1);
                workItemInDatabase.RelatedToItems.Single().ToItem.Id.Should().Be(existingWorkItem.Id);
            });
        }
    }
}
