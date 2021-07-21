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

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ReadWrite.Creating
{
    public sealed class CreateResourceWithToManyRelationshipTests
        : IClassFixture<ExampleIntegrationTestContext<TestableStartup<ReadWriteDbContext>, ReadWriteDbContext>>
    {
        private readonly ExampleIntegrationTestContext<TestableStartup<ReadWriteDbContext>, ReadWriteDbContext> _testContext;
        private readonly ReadWriteFakers _fakers = new();

        public CreateResourceWithToManyRelationshipTests(ExampleIntegrationTestContext<TestableStartup<ReadWriteDbContext>, ReadWriteDbContext> testContext)
        {
            _testContext = testContext;

            testContext.UseController<WorkItemsController>();
            testContext.UseController<UserAccountsController>();
        }

        [Fact]
        public async Task Can_create_HasMany_relationship()
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
                        subscribers = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "userAccounts",
                                    id = existingUserAccounts[0].StringId
                                },
                                new
                                {
                                    type = "userAccounts",
                                    id = existingUserAccounts[1].StringId
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
            responseDocument.SingleData.Attributes.Should().NotBeEmpty();
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();
            responseDocument.Included.Should().BeNull();

            int newWorkItemId = int.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                WorkItem workItemInDatabase = await dbContext.WorkItems.Include(workItem => workItem.Subscribers).FirstWithIdAsync(newWorkItemId);

                workItemInDatabase.Subscribers.Should().HaveCount(2);
                workItemInDatabase.Subscribers.Should().ContainSingle(subscriber => subscriber.Id == existingUserAccounts[0].Id);
                workItemInDatabase.Subscribers.Should().ContainSingle(subscriber => subscriber.Id == existingUserAccounts[1].Id);
            });
        }

        [Fact]
        public async Task Can_create_HasMany_relationship_with_include()
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
                        subscribers = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "userAccounts",
                                    id = existingUserAccounts[0].StringId
                                },
                                new
                                {
                                    type = "userAccounts",
                                    id = existingUserAccounts[1].StringId
                                }
                            }
                        }
                    }
                }
            };

            const string route = "/workItems?include=subscribers";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Attributes.Should().NotBeEmpty();
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();

            responseDocument.Included.Should().HaveCount(2);
            responseDocument.Included.Should().OnlyContain(resource => resource.Type == "userAccounts");
            responseDocument.Included.Should().ContainSingle(resource => resource.Id == existingUserAccounts[0].StringId);
            responseDocument.Included.Should().ContainSingle(resource => resource.Id == existingUserAccounts[1].StringId);
            responseDocument.Included.Should().OnlyContain(resource => resource.Attributes["firstName"] != null);
            responseDocument.Included.Should().OnlyContain(resource => resource.Attributes["lastName"] != null);
            responseDocument.Included.Should().OnlyContain(resource => resource.Relationships.Count > 0);

            int newWorkItemId = int.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                WorkItem workItemInDatabase = await dbContext.WorkItems.Include(workItem => workItem.Subscribers).FirstWithIdAsync(newWorkItemId);

                workItemInDatabase.Subscribers.Should().HaveCount(2);
                workItemInDatabase.Subscribers.Should().ContainSingle(userAccount => userAccount.Id == existingUserAccounts[0].Id);
                workItemInDatabase.Subscribers.Should().ContainSingle(userAccount => userAccount.Id == existingUserAccounts[1].Id);
            });
        }

        [Fact]
        public async Task Can_create_HasMany_relationship_with_include_and_secondary_fieldset()
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
                        subscribers = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "userAccounts",
                                    id = existingUserAccounts[0].StringId
                                },
                                new
                                {
                                    type = "userAccounts",
                                    id = existingUserAccounts[1].StringId
                                }
                            }
                        }
                    }
                }
            };

            const string route = "/workItems?include=subscribers&fields[userAccounts]=firstName";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Attributes.Should().NotBeEmpty();
            responseDocument.SingleData.Relationships.Should().NotBeEmpty();

            responseDocument.Included.Should().HaveCount(2);
            responseDocument.Included.Should().OnlyContain(resource => resource.Type == "userAccounts");
            responseDocument.Included.Should().ContainSingle(resource => resource.Id == existingUserAccounts[0].StringId);
            responseDocument.Included.Should().ContainSingle(resource => resource.Id == existingUserAccounts[1].StringId);
            responseDocument.Included.Should().OnlyContain(resource => resource.Attributes.Count == 1);
            responseDocument.Included.Should().OnlyContain(resource => resource.Attributes["firstName"] != null);
            responseDocument.Included.Should().OnlyContain(resource => resource.Relationships == null);

            int newWorkItemId = int.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                WorkItem workItemInDatabase = await dbContext.WorkItems.Include(workItem => workItem.Subscribers).FirstWithIdAsync(newWorkItemId);

                workItemInDatabase.Subscribers.Should().HaveCount(2);
                workItemInDatabase.Subscribers.Should().ContainSingle(userAccount => userAccount.Id == existingUserAccounts[0].Id);
                workItemInDatabase.Subscribers.Should().ContainSingle(userAccount => userAccount.Id == existingUserAccounts[1].Id);
            });
        }

        [Fact]
        public async Task Can_create_HasManyThrough_relationship_with_include_and_fieldsets()
        {
            // Arrange
            List<WorkTag> existingTags = _fakers.WorkTag.Generate(3);
            WorkItem workItemToCreate = _fakers.WorkItem.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.WorkTags.AddRange(existingTags);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    attributes = new
                    {
                        description = workItemToCreate.Description,
                        priority = workItemToCreate.Priority
                    },
                    relationships = new
                    {
                        tags = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "workTags",
                                    id = existingTags[0].StringId
                                },
                                new
                                {
                                    type = "workTags",
                                    id = existingTags[1].StringId
                                },
                                new
                                {
                                    type = "workTags",
                                    id = existingTags[2].StringId
                                }
                            }
                        }
                    }
                }
            };

            const string route = "/workItems?fields[workItems]=priority,tags&include=tags&fields[workTags]=text";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Attributes.Should().HaveCount(1);
            responseDocument.SingleData.Attributes["priority"].Should().Be(workItemToCreate.Priority.ToString("G"));
            responseDocument.SingleData.Relationships.Should().HaveCount(1);
            responseDocument.SingleData.Relationships["tags"].ManyData.Should().HaveCount(3);
            responseDocument.SingleData.Relationships["tags"].ManyData[0].Id.Should().Be(existingTags[0].StringId);
            responseDocument.SingleData.Relationships["tags"].ManyData[1].Id.Should().Be(existingTags[1].StringId);
            responseDocument.SingleData.Relationships["tags"].ManyData[2].Id.Should().Be(existingTags[2].StringId);

            responseDocument.Included.Should().HaveCount(3);
            responseDocument.Included.Should().OnlyContain(resource => resource.Type == "workTags");
            responseDocument.Included.Should().ContainSingle(resource => resource.Id == existingTags[0].StringId);
            responseDocument.Included.Should().ContainSingle(resource => resource.Id == existingTags[1].StringId);
            responseDocument.Included.Should().ContainSingle(resource => resource.Id == existingTags[2].StringId);
            responseDocument.Included.Should().OnlyContain(resource => resource.Attributes.Count == 1);
            responseDocument.Included.Should().OnlyContain(resource => resource.Attributes["text"] != null);
            responseDocument.Included.Should().OnlyContain(resource => resource.Relationships == null);

            int newWorkItemId = int.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                // @formatter:wrap_chained_method_calls chop_always
                // @formatter:keep_existing_linebreaks true

                WorkItem workItemInDatabase = await dbContext.WorkItems
                    .Include(workItem => workItem.WorkItemTags)
                    .ThenInclude(workItemTag => workItemTag.Tag)
                    .FirstWithIdAsync(newWorkItemId);

                // @formatter:keep_existing_linebreaks restore
                // @formatter:wrap_chained_method_calls restore

                workItemInDatabase.WorkItemTags.Should().HaveCount(3);
                workItemInDatabase.WorkItemTags.Should().ContainSingle(workItemTag => workItemTag.Tag.Id == existingTags[0].Id);
                workItemInDatabase.WorkItemTags.Should().ContainSingle(workItemTag => workItemTag.Tag.Id == existingTags[1].Id);
                workItemInDatabase.WorkItemTags.Should().ContainSingle(workItemTag => workItemTag.Tag.Id == existingTags[2].Id);
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
                        subscribers = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    id = 12345678
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
            error.Title.Should().Be("Failed to deserialize request body: Request body must include 'type' element.");
            error.Detail.Should().StartWith("Expected 'type' element in 'subscribers' relationship. - Request body: <<");
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
                        subscribers = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "doesNotExist",
                                    id = 12345678
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
                        subscribers = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "userAccounts"
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
            error.Title.Should().Be("Failed to deserialize request body: Request body must include 'id' element.");
            error.Detail.Should().StartWith("Expected 'id' element in 'subscribers' relationship. - Request body: <<");
        }

        [Fact]
        public async Task Cannot_create_for_unknown_relationship_IDs()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "userAccounts",
                    relationships = new
                    {
                        assignedItems = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "workItems",
                                    id = 12345678
                                },
                                new
                                {
                                    type = "workItems",
                                    id = 87654321
                                }
                            }
                        }
                    }
                }
            };

            const string route = "/userAccounts";

            // Act
            (HttpResponseMessage httpResponse, ErrorDocument responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NotFound);

            responseDocument.Errors.Should().HaveCount(2);

            Error error1 = responseDocument.Errors[0];
            error1.StatusCode.Should().Be(HttpStatusCode.NotFound);
            error1.Title.Should().Be("A related resource does not exist.");
            error1.Detail.Should().Be("Related resource of type 'workItems' with ID '12345678' in relationship 'assignedItems' does not exist.");

            Error error2 = responseDocument.Errors[1];
            error2.StatusCode.Should().Be(HttpStatusCode.NotFound);
            error2.Title.Should().Be("A related resource does not exist.");
            error2.Detail.Should().Be("Related resource of type 'workItems' with ID '87654321' in relationship 'assignedItems' does not exist.");
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
                        subscribers = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "rgbColors",
                                    id = "0A0B0C"
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
            error.Title.Should().Be("Failed to deserialize request body: Relationship contains incompatible resource type.");
            error.Detail.Should().StartWith("Relationship 'subscribers' contains incompatible resource type 'rgbColors'. - Request body: <<");
        }

        [Fact]
        public async Task Can_create_with_duplicates()
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
                        subscribers = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "userAccounts",
                                    id = existingUserAccount.StringId
                                },
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

            const string route = "/workItems?include=subscribers";

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

            int newWorkItemId = int.Parse(responseDocument.SingleData.Id);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                WorkItem workItemInDatabase = await dbContext.WorkItems.Include(workItem => workItem.Subscribers).FirstWithIdAsync(newWorkItemId);

                workItemInDatabase.Subscribers.Should().HaveCount(1);
                workItemInDatabase.Subscribers.Single().Id.Should().Be(existingUserAccount.Id);
            });
        }

        [Fact]
        public async Task Cannot_create_with_null_data_in_HasMany_relationship()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    relationships = new
                    {
                        subscribers = new
                        {
                            data = (object)null
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
            error.Title.Should().Be("Failed to deserialize request body: Expected data[] element for to-many relationship.");
            error.Detail.Should().StartWith("Expected data[] element for 'subscribers' relationship. - Request body: <<");
        }

        [Fact]
        public async Task Cannot_create_with_null_data_in_HasManyThrough_relationship()
        {
            // Arrange
            var requestBody = new
            {
                data = new
                {
                    type = "workItems",
                    relationships = new
                    {
                        tags = new
                        {
                            data = (object)null
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
            error.Title.Should().Be("Failed to deserialize request body: Expected data[] element for to-many relationship.");
            error.Detail.Should().StartWith("Expected data[] element for 'tags' relationship. - Request body: <<");
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
                        children = new
                        {
                            data = new[]
                            {
                                new
                                {
                                    type = "workItems",
                                    lid = workItemLocalId
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
            error.Title.Should().Be("Failed to deserialize request body: Local IDs cannot be used at this endpoint.");
            error.Detail.Should().StartWith("Local IDs cannot be used at this endpoint. - Request body: <<");
        }
    }
}
