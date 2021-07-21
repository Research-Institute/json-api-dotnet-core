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
using Newtonsoft.Json;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ReadWrite.Creating
{
    public sealed class CreateResourceWithToOneRelationshipTests
        : IClassFixture<ExampleIntegrationTestContext<TestableStartup<ReadWriteDbContext>, ReadWriteDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<ReadWriteDbContext>, ReadWriteDbContext> _testContext;
        private readonly ReadWriteFakers _fakers = new();

        public CreateResourceWithToOneRelationshipTests(ExampleIntegrationTestContext<TestableStartup<ReadWriteDbContext>, ReadWriteDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<WorkItemGroupsController>();
            testContext.UseController<WorkItemsController>();
            testContext.UseController<RgbColorsController>();

            var options = (JsonApiOptions)testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.AllowClientGeneratedIds = true;
        }

        [Fact]
        public async Task Can_create_OneToOne_relationship_from_principal_side()
        {
            // Arrange
            WorkItemGroup existingGroup = _fakers.WorkItemGroup.Generate();
            existingGroup.Color = _fakers.RgbColor.Generate();

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
                    relationships = new
                    {
                        color = new
                        {
                            data = new
                            {
                                type = "rgbColors",
                                id = existingGroup.Color.StringId
                            }
                        }
                    }
                }
            };

            const string route = "/workItemGroups";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Attributes.Should().NotBeEmpty();
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();

            string newGroupId = responseDocument.SingleData.Id;
            newGroupId.Should().NotBeNullOrEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                List<WorkItemGroup> groupsInDatabase = await dbContext.Groups.Include(group => group.Color).ToListAsync();

                WorkItemGroup newGroupInDatabase = groupsInDatabase.Single(group => group.StringId == newGroupId);
                newGroupInDatabase.Color.Should().NotBeNull();
                newGroupInDatabase.Color.Id.Should().Be(existingGroup.Color.Id);

                WorkItemGroup existingGroupInDatabase = groupsInDatabase.Single(group => group.Id == existingGroup.Id);
                existingGroupInDatabase.Color.Should().BeNull();
            });
        }

        [Fact]
        public async Task Can_create_OneToOne_relationship_from_dependent_side()
        {
            // Arrange
            RgbColor existingColor = _fakers.RgbColor.Generate();
            existingColor.Group = _fakers.WorkItemGroup.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.RgbColors.Add(existingColor);
                await dbContext.SaveChangesAsync();
            });

            const string colorId = "0A0B0C";

            var requestBody = new
            {
                data = new
                {
                    type = "rgbColors",
                    id = colorId,
                    relationships = new
                    {
                        group = new
                        {
                            data = new
                            {
                                type = "workItemGroups",
                                id = existingColor.Group.StringId
                            }
                        }
                    }
                }
            };

            const string route = "/rgbColors";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePostAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                List<RgbColor> colorsInDatabase = await dbContext.RgbColors.Include(rgbColor => rgbColor.Group).ToListAsync();

                RgbColor newColorInDatabase = colorsInDatabase.Single(color => color.Id == colorId);
                newColorInDatabase.Group.Should().NotBeNull();
                newColorInDatabase.Group.Id.Should().Be(existingColor.Group.Id);

                RgbColor existingColorInDatabase = colorsInDatabase.SingleOrDefault(color => color.Id == existingColor.Id);
                existingColorInDatabase.Should().NotBeNull();
                existingColorInDatabase!.Group.Should().BeNull();
            });
        }

        [Fact]
        public async Task Can_create_relationship_with_include()
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
                    type = "workItems",
                    relationships = new
                    {
                        assignee = new
                        {
                            data = new
                            {
                                type = "userAccounts",
                                id = existingUserAccount.StringId
                            }
                        }
                    }
                }
            };

            const string route = "/workItems?include=assignee";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Attributes.Should().NotBeEmpty();
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Type.Should().Be("userAccounts");
            responseDocument.Included[0].Id.Should().Be(existingUserAccount.StringId);
            responseDocument.Included[0].Attributes["firstName"].Should().Be(existingUserAccount.FirstName);
            responseDocument.Included[0].Attributes["lastName"].Should().Be(existingUserAccount.LastName);
            responseDocument.Included[0].Relationships.Should().NotBeEmpty();

            int newWorkItemId = int.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                WorkItem workItemInDatabase = await dbContext.WorkItems.Include(workItem => workItem.Assignee).FirstWithIdAsync(newWorkItemId);

                workItemInDatabase.Assignee.Should().NotBeNull();
                workItemInDatabase.Assignee.Id.Should().Be(existingUserAccount.Id);
            });
        }

        [Fact]
        public async Task Can_create_relationship_with_include_and_primary_fieldset()
        {
            // Arrange
            UserAccount existingUserAccount = _fakers.UserAccount.Generate();
            WorkItem newWorkItem = _fakers.WorkItem.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.UserAccounts.Add(existingUserAccount);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    attributes = new
                    {
                        description = newWorkItem.Description,
                        priority = newWorkItem.Priority
                    },
                    relationships = new
                    {
                        assignee = new
                        {
                            data = new
                            {
                                type = "userAccounts",
                                id = existingUserAccount.StringId
                            }
                        }
                    }
                }
            };

            const string route = "/workItems?fields[workItems]=description,assignee&include=assignee";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Attributes.Should().HaveCount(1);
            responseDocument.SingleData.Attributes["description"].Should().Be(newWorkItem.Description);
            responseDocument.SingleData.Relationships.Should().HaveCount(1);
            responseDocument.SingleData.Relationships["assignee"].SingleData.Id.Should().Be(existingUserAccount.StringId);

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Type.Should().Be("userAccounts");
            responseDocument.Included[0].Id.Should().Be(existingUserAccount.StringId);
            responseDocument.Included[0].Attributes["firstName"].Should().Be(existingUserAccount.FirstName);
            responseDocument.Included[0].Attributes["lastName"].Should().Be(existingUserAccount.LastName);
            responseDocument.Included[0].Relationships.Should().NotBeEmpty();

            int newWorkItemId = int.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                WorkItem workItemInDatabase = await dbContext.WorkItems.Include(workItem => workItem.Assignee).FirstWithIdAsync(newWorkItemId);

                workItemInDatabase.Description.Should().Be(newWorkItem.Description);
                workItemInDatabase.Priority.Should().Be(newWorkItem.Priority);
                workItemInDatabase.Assignee.Should().NotBeNull();
                workItemInDatabase.Assignee.Id.Should().Be(existingUserAccount.Id);
            });
        }

        [Fact]
        public async Task Cannot_create_for_missing_relationship_type()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    relationships = new
                    {
                        assignee = new
                        {
                            data = new
                            {
                                id = 12345678
                            }
                        }
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
            error.Detail.Should().StartWith("Expected 'type' element in 'assignee' relationship. - Request body: <<");
        }

        [Fact]
        public async Task Cannot_create_for_unknown_relationship_type()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    relationships = new
                    {
                        assignee = new
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
        public async Task Cannot_create_for_missing_relationship_ID()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    relationships = new
                    {
                        assignee = new
                        {
                            data = new
                            {
                                type = "userAccounts"
                            }
                        }
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
            error.Title.Should().Be("Failed to deserialize request body: Request body must include 'id' element.");
            error.Detail.Should().StartWith("Expected 'id' element in 'assignee' relationship. - Request body: <<");
        }

        [Fact]
        public async Task Cannot_create_with_unknown_relationship_ID()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    relationships = new
                    {
                        assignee = new
                        {
                            data = new
                            {
                                type = "userAccounts",
                                id = 12345678
                            }
                        }
                    }
                }
            };

            const string route = "/workItems";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);

            responseDocument.Errors.Should().HaveCount(1);

            Error error = responseDocument.Errors[0];
            error.StatusCode.Should().Be(HttpStatusCode.NotFound);
            error.Title.Should().Be("A related resource does not exist.");
            error.Detail.Should().Be("Related resource of type 'userAccounts' with ID '12345678' in relationship 'assignee' does not exist.");
        }

        [Fact]
        public async Task Cannot_create_on_relationship_type_mismatch()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    relationships = new
                    {
                        assignee = new
                        {
                            data = new
                            {
                                type = "rgbColors",
                                id = "0A0B0C"
                            }
                        }
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
            error.Title.Should().Be("Failed to deserialize request body: Relationship contains incompatible resource type.");
            error.Detail.Should().StartWith("Relationship 'assignee' contains incompatible resource type 'rgbColors'. - Request body: <<");
        }

        [Fact]
        public async Task Can_create_resource_with_duplicate_relationship()
        {
            // Arrange
            List<UserAccount> existingUserAccounts = _fakers.UserAccount.Generate(2);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.UserAccounts.AddRange(existingUserAccounts);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
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
                        assignee_duplicate = new
                        {
                            data = new
                            {
                                type = "userAccounts",
                                id = existingUserAccounts[1].StringId
                            }
                        }
                    }
                }
            };

            string requestBodyText = JsonConvert.SerializeObject(requestBody).Replace("assignee_duplicate", "assignee");

            const string route = "/workItems?include=assignee";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBodyText);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Attributes.Should().NotBeEmpty();
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();

            responseDocument.Included.Should().HaveCount(1);
            responseDocument.Included[0].Type.Should().Be("userAccounts");
            responseDocument.Included[0].Id.Should().Be(existingUserAccounts[1].StringId);
            responseDocument.Included[0].Attributes["firstName"].Should().Be(existingUserAccounts[1].FirstName);
            responseDocument.Included[0].Attributes["lastName"].Should().Be(existingUserAccounts[1].LastName);
            responseDocument.Included[0].Relationships.Should().NotBeEmpty();

            int newWorkItemId = int.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                WorkItem workItemInDatabase = await dbContext.WorkItems.Include(workItem => workItem.Assignee).FirstWithIdAsync(newWorkItemId);

                workItemInDatabase.Assignee.Should().NotBeNull();
                workItemInDatabase.Assignee.Id.Should().Be(existingUserAccounts[1].Id);
            });
        }

        [Fact]
        public async Task Cannot_create_with_data_array_in_relationship()
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
                    type = "workItems",
                    relationships = new
                    {
                        assignee = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "userAccounts",
                                    id = existingUserAccount.StringId
                                }
                            }
                        }
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
            error.Title.Should().Be("Failed to deserialize request body: Expected single data element for to-one relationship.");
            error.Detail.Should().StartWith("Expected single data element for 'assignee' relationship. - Request body: <<");
        }

        [Fact]
        public async Task Cannot_create_resource_with_local_ID()
        {
            // Arrange
            const string workItemLocalId = "wo-1";

            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    lid = workItemLocalId,
                    relationships = new
                    {
                        parent = new
                        {
                            data = new
                            {
                                type = "workItems",
                                lid = workItemLocalId
                            }
                        }
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
            error.Title.Should().Be("Failed to deserialize request body: Local IDs cannot be used at this endpoint.");
            error.Detail.Should().StartWith("Local IDs cannot be used at this endpoint. - Request body: <<");
        }
    }
}
