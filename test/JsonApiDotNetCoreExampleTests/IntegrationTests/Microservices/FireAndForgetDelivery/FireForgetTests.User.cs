using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExampleTests.IntegrationTests.Microservices.Messages;
using Microsoft.Extensions.DependencyInjection;
using TestBuildingBlocks;
using Xunit;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.Microservices.FireAndForgetDelivery
{
    public sealed partial class FireForgetTests
    {
        [Fact]
        public async Task Create_user_sends_messages()
        {
            // Arrange
            var hitCounter = _testContext.Factory.Services.GetRequiredService<ResourceDefinitionHitCounter>();
            var messageBroker = _testContext.Factory.Services.GetRequiredService<MessageBroker>();

            string newLoginName = _fakers.DomainUser.Generate().LoginName;
            string newDisplayName = _fakers.DomainUser.Generate().DisplayName;

            var requestBody = new
            {
                data = new
                {
                    type = "domainUsers",
                    attributes = new
                    {
                        loginName = newLoginName,
                        displayName = newDisplayName
                    }
                }
            };

            const string route = "/domainUsers";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Attributes["loginName"].Should().Be(newLoginName);
            responseDocument.SingleData.Attributes["displayName"].Should().Be(newDisplayName);

            hitCounter.HitExtensibilityPoints.Should().BeEquivalentTo(new[]
            {
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnPrepareWriteAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWritingAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWriteSucceededAsync)
            }, options => options.WithStrictOrdering());

            messageBroker.SentMessages.Should().HaveCount(1);

            Guid newUserId = Guid.Parse(responseDocument.SingleData.Id);

            var content = messageBroker.SentMessages[0].GetContentAs<UserCreatedContent>();
            content.UserId.Should().Be(newUserId);
            content.UserLoginName.Should().Be(newLoginName);
            content.UserDisplayName.Should().Be(newDisplayName);
        }

        [Fact]
        public async Task Create_user_in_group_sends_messages()
        {
            // Arrange
            var hitCounter = _testContext.Factory.Services.GetRequiredService<ResourceDefinitionHitCounter>();
            var messageBroker = _testContext.Factory.Services.GetRequiredService<MessageBroker>();

            DomainGroup existingGroup = _fakers.DomainGroup.Generate();

            string newLoginName = _fakers.DomainUser.Generate().LoginName;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Groups.Add(existingGroup);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "domainUsers",
                    attributes = new
                    {
                        loginName = newLoginName
                    },
                    relationships = new
                    {
                        group = new
                        {
                            data = new
                            {
                                type = "domainGroups",
                                id = existingGroup.StringId
                            }
                        }
                    }
                }
            };

            const string route = "/domainUsers";

            // Act
            (HttpResponseMessage httpResponse, Document responseDocument) = await _testContext.ExecutePostAsync<Document>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.Created);

            responseDocument.SingleData.Should().NotBeNull();
            responseDocument.SingleData.Attributes["loginName"].Should().Be(newLoginName);
            responseDocument.SingleData.Attributes["displayName"].Should().BeNull();

            hitCounter.HitExtensibilityPoints.Should().BeEquivalentTo(new[]
            {
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnPrepareWriteAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnSetToOneRelationshipAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWritingAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWriteSucceededAsync)
            }, options => options.WithStrictOrdering());

            messageBroker.SentMessages.Should().HaveCount(2);

            Guid newUserId = Guid.Parse(responseDocument.SingleData.Id);

            var content1 = messageBroker.SentMessages[0].GetContentAs<UserCreatedContent>();
            content1.UserId.Should().Be(newUserId);
            content1.UserLoginName.Should().Be(newLoginName);
            content1.UserDisplayName.Should().BeNull();

            var content2 = messageBroker.SentMessages[1].GetContentAs<UserAddedToGroupContent>();
            content2.UserId.Should().Be(newUserId);
            content2.GroupId.Should().Be(existingGroup.Id);
        }

        [Fact]
        public async Task Update_user_sends_messages()
        {
            // Arrange
            var hitCounter = _testContext.Factory.Services.GetRequiredService<ResourceDefinitionHitCounter>();
            var messageBroker = _testContext.Factory.Services.GetRequiredService<MessageBroker>();

            DomainUser existingUser = _fakers.DomainUser.Generate();

            string newLoginName = _fakers.DomainUser.Generate().LoginName;
            string newDisplayName = _fakers.DomainUser.Generate().DisplayName;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Users.Add(existingUser);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "domainUsers",
                    id = existingUser.StringId,
                    attributes = new
                    {
                        loginName = newLoginName,
                        displayName = newDisplayName
                    }
                }
            };

            string route = "/domainUsers/" + existingUser.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            hitCounter.HitExtensibilityPoints.Should().BeEquivalentTo(new[]
            {
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnPrepareWriteAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWritingAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWriteSucceededAsync)
            }, options => options.WithStrictOrdering());

            messageBroker.SentMessages.Should().HaveCount(2);

            var content1 = messageBroker.SentMessages[0].GetContentAs<UserLoginNameChangedContent>();
            content1.UserId.Should().Be(existingUser.Id);
            content1.BeforeUserLoginName.Should().Be(existingUser.LoginName);
            content1.AfterUserLoginName.Should().Be(newLoginName);

            var content2 = messageBroker.SentMessages[1].GetContentAs<UserDisplayNameChangedContent>();
            content2.UserId.Should().Be(existingUser.Id);
            content2.BeforeUserDisplayName.Should().Be(existingUser.DisplayName);
            content2.AfterUserDisplayName.Should().Be(newDisplayName);
        }

        [Fact]
        public async Task Update_user_clear_group_sends_messages()
        {
            // Arrange
            var hitCounter = _testContext.Factory.Services.GetRequiredService<ResourceDefinitionHitCounter>();
            var messageBroker = _testContext.Factory.Services.GetRequiredService<MessageBroker>();

            DomainUser existingUser = _fakers.DomainUser.Generate();
            existingUser.Group = _fakers.DomainGroup.Generate();

            string newDisplayName = _fakers.DomainUser.Generate().DisplayName;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Users.Add(existingUser);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "domainUsers",
                    id = existingUser.StringId,
                    attributes = new
                    {
                        displayName = newDisplayName
                    },
                    relationships = new
                    {
                        group = new
                        {
                            data = (object)null
                        }
                    }
                }
            };

            string route = "/domainUsers/" + existingUser.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            hitCounter.HitExtensibilityPoints.Should().BeEquivalentTo(new[]
            {
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnPrepareWriteAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnSetToOneRelationshipAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWritingAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWriteSucceededAsync)
            }, options => options.WithStrictOrdering());

            messageBroker.SentMessages.Should().HaveCount(2);

            var content1 = messageBroker.SentMessages[0].GetContentAs<UserDisplayNameChangedContent>();
            content1.UserId.Should().Be(existingUser.Id);
            content1.BeforeUserDisplayName.Should().Be(existingUser.DisplayName);
            content1.AfterUserDisplayName.Should().Be(newDisplayName);

            var content2 = messageBroker.SentMessages[1].GetContentAs<UserRemovedFromGroupContent>();
            content2.UserId.Should().Be(existingUser.Id);
            content2.GroupId.Should().Be(existingUser.Group.Id);
        }

        [Fact]
        public async Task Update_user_add_to_group_sends_messages()
        {
            // Arrange
            var hitCounter = _testContext.Factory.Services.GetRequiredService<ResourceDefinitionHitCounter>();
            var messageBroker = _testContext.Factory.Services.GetRequiredService<MessageBroker>();

            DomainUser existingUser = _fakers.DomainUser.Generate();
            DomainGroup existingGroup = _fakers.DomainGroup.Generate();

            string newDisplayName = _fakers.DomainUser.Generate().DisplayName;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddInRange(existingUser, existingGroup);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "domainUsers",
                    id = existingUser.StringId,
                    attributes = new
                    {
                        displayName = newDisplayName
                    },
                    relationships = new
                    {
                        group = new
                        {
                            data = new
                            {
                                type = "domainGroups",
                                id = existingGroup.StringId
                            }
                        }
                    }
                }
            };

            string route = "/domainUsers/" + existingUser.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            hitCounter.HitExtensibilityPoints.Should().BeEquivalentTo(new[]
            {
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnPrepareWriteAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnSetToOneRelationshipAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWritingAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWriteSucceededAsync)
            }, options => options.WithStrictOrdering());

            messageBroker.SentMessages.Should().HaveCount(2);

            var content1 = messageBroker.SentMessages[0].GetContentAs<UserDisplayNameChangedContent>();
            content1.UserId.Should().Be(existingUser.Id);
            content1.BeforeUserDisplayName.Should().Be(existingUser.DisplayName);
            content1.AfterUserDisplayName.Should().Be(newDisplayName);

            var content2 = messageBroker.SentMessages[1].GetContentAs<UserAddedToGroupContent>();
            content2.UserId.Should().Be(existingUser.Id);
            content2.GroupId.Should().Be(existingGroup.Id);
        }

        [Fact]
        public async Task Update_user_move_to_group_sends_messages()
        {
            // Arrange
            var hitCounter = _testContext.Factory.Services.GetRequiredService<ResourceDefinitionHitCounter>();
            var messageBroker = _testContext.Factory.Services.GetRequiredService<MessageBroker>();

            DomainUser existingUser = _fakers.DomainUser.Generate();
            existingUser.Group = _fakers.DomainGroup.Generate();

            DomainGroup existingGroup = _fakers.DomainGroup.Generate();

            string newDisplayName = _fakers.DomainUser.Generate().DisplayName;

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddInRange(existingUser, existingGroup);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "domainUsers",
                    id = existingUser.StringId,
                    attributes = new
                    {
                        displayName = newDisplayName
                    },
                    relationships = new
                    {
                        group = new
                        {
                            data = new
                            {
                                type = "domainGroups",
                                id = existingGroup.StringId
                            }
                        }
                    }
                }
            };

            string route = "/domainUsers/" + existingUser.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            hitCounter.HitExtensibilityPoints.Should().BeEquivalentTo(new[]
            {
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnPrepareWriteAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnSetToOneRelationshipAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWritingAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWriteSucceededAsync)
            }, options => options.WithStrictOrdering());

            messageBroker.SentMessages.Should().HaveCount(2);

            var content1 = messageBroker.SentMessages[0].GetContentAs<UserDisplayNameChangedContent>();
            content1.UserId.Should().Be(existingUser.Id);
            content1.BeforeUserDisplayName.Should().Be(existingUser.DisplayName);
            content1.AfterUserDisplayName.Should().Be(newDisplayName);

            var content2 = messageBroker.SentMessages[1].GetContentAs<UserMovedToGroupContent>();
            content2.UserId.Should().Be(existingUser.Id);
            content2.BeforeGroupId.Should().Be(existingUser.Group.Id);
            content2.AfterGroupId.Should().Be(existingGroup.Id);
        }

        [Fact]
        public async Task Delete_user_sends_messages()
        {
            // Arrange
            var hitCounter = _testContext.Factory.Services.GetRequiredService<ResourceDefinitionHitCounter>();
            var messageBroker = _testContext.Factory.Services.GetRequiredService<MessageBroker>();

            DomainUser existingUser = _fakers.DomainUser.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Users.Add(existingUser);
                await dbContext.SaveChangesAsync();
            });

            string route = "/domainUsers/" + existingUser.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            hitCounter.HitExtensibilityPoints.Should().BeEquivalentTo(new[]
            {
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWritingAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWriteSucceededAsync)
            }, options => options.WithStrictOrdering());

            messageBroker.SentMessages.Should().HaveCount(1);

            var content = messageBroker.SentMessages[0].GetContentAs<UserDeletedContent>();
            content.UserId.Should().Be(existingUser.Id);
        }

        [Fact]
        public async Task Delete_user_in_group_sends_messages()
        {
            // Arrange
            var hitCounter = _testContext.Factory.Services.GetRequiredService<ResourceDefinitionHitCounter>();
            var messageBroker = _testContext.Factory.Services.GetRequiredService<MessageBroker>();

            DomainUser existingUser = _fakers.DomainUser.Generate();
            existingUser.Group = _fakers.DomainGroup.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Users.Add(existingUser);
                await dbContext.SaveChangesAsync();
            });

            string route = "/domainUsers/" + existingUser.StringId;

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecuteDeleteAsync<string>(route);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            hitCounter.HitExtensibilityPoints.Should().BeEquivalentTo(new[]
            {
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWritingAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWriteSucceededAsync)
            }, options => options.WithStrictOrdering());

            messageBroker.SentMessages.Should().HaveCount(2);

            var content1 = messageBroker.SentMessages[0].GetContentAs<UserRemovedFromGroupContent>();
            content1.UserId.Should().Be(existingUser.Id);
            content1.GroupId.Should().Be(existingUser.Group.Id);

            var content2 = messageBroker.SentMessages[1].GetContentAs<UserDeletedContent>();
            content2.UserId.Should().Be(existingUser.Id);
        }

        [Fact]
        public async Task Clear_group_from_user_sends_messages()
        {
            // Arrange
            var hitCounter = _testContext.Factory.Services.GetRequiredService<ResourceDefinitionHitCounter>();
            var messageBroker = _testContext.Factory.Services.GetRequiredService<MessageBroker>();

            DomainUser existingUser = _fakers.DomainUser.Generate();
            existingUser.Group = _fakers.DomainGroup.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Users.Add(existingUser);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = (object)null
            };

            string route = $"/domainUsers/{existingUser.StringId}/relationships/group";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            hitCounter.HitExtensibilityPoints.Should().BeEquivalentTo(new[]
            {
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnPrepareWriteAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnSetToOneRelationshipAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWritingAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWriteSucceededAsync)
            }, options => options.WithStrictOrdering());

            messageBroker.SentMessages.Should().HaveCount(1);

            var content = messageBroker.SentMessages[0].GetContentAs<UserRemovedFromGroupContent>();
            content.UserId.Should().Be(existingUser.Id);
            content.GroupId.Should().Be(existingUser.Group.Id);
        }

        [Fact]
        public async Task Assign_group_to_user_sends_messages()
        {
            // Arrange
            var hitCounter = _testContext.Factory.Services.GetRequiredService<ResourceDefinitionHitCounter>();
            var messageBroker = _testContext.Factory.Services.GetRequiredService<MessageBroker>();

            DomainUser existingUser = _fakers.DomainUser.Generate();
            DomainGroup existingGroup = _fakers.DomainGroup.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddInRange(existingUser, existingGroup);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "domainGroups",
                    id = existingGroup.StringId
                }
            };

            string route = $"/domainUsers/{existingUser.StringId}/relationships/group";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            hitCounter.HitExtensibilityPoints.Should().BeEquivalentTo(new[]
            {
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnPrepareWriteAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnSetToOneRelationshipAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWritingAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWriteSucceededAsync)
            }, options => options.WithStrictOrdering());

            messageBroker.SentMessages.Should().HaveCount(1);

            var content = messageBroker.SentMessages[0].GetContentAs<UserAddedToGroupContent>();
            content.UserId.Should().Be(existingUser.Id);
            content.GroupId.Should().Be(existingGroup.Id);
        }

        [Fact]
        public async Task Replace_group_for_user_sends_messages()
        {
            // Arrange
            var hitCounter = _testContext.Factory.Services.GetRequiredService<ResourceDefinitionHitCounter>();
            var messageBroker = _testContext.Factory.Services.GetRequiredService<MessageBroker>();

            DomainUser existingUser = _fakers.DomainUser.Generate();
            existingUser.Group = _fakers.DomainGroup.Generate();

            DomainGroup existingGroup = _fakers.DomainGroup.Generate();

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.AddInRange(existingUser, existingGroup);
                await dbContext.SaveChangesAsync();
            });

            var requestBody = new
            {
                data = new
                {
                    type = "domainGroups",
                    id = existingGroup.StringId
                }
            };

            string route = $"/domainUsers/{existingUser.StringId}/relationships/group";

            // Act
            (HttpResponseMessage httpResponse, string responseDocument) = await _testContext.ExecutePatchAsync<string>(route, requestBody);

            // Assert
            httpResponse.Should().HaveStatusCode(HttpStatusCode.NoContent);

            responseDocument.Should().BeEmpty();

            hitCounter.HitExtensibilityPoints.Should().BeEquivalentTo(new[]
            {
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnPrepareWriteAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnSetToOneRelationshipAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWritingAsync),
                (typeof(DomainUser), ResourceDefinitionHitCounter.ExtensibilityPoint.OnWriteSucceededAsync)
            }, options => options.WithStrictOrdering());

            messageBroker.SentMessages.Should().HaveCount(1);

            var content = messageBroker.SentMessages[0].GetContentAs<UserMovedToGroupContent>();
            content.UserId.Should().Be(existingUser.Id);
            content.BeforeGroupId.Should().Be(existingUser.Group.Id);
            content.AfterGroupId.Should().Be(existingGroup.Id);
        }
    }
}
