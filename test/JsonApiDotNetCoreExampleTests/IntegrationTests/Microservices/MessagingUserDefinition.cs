using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCoreExampleTests.IntegrationTests.Microservices.Messages;
using Microsoft.EntityFrameworkCore;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.Microservices
{
    public abstract class MessagingUserDefinition : JsonApiResourceDefinition<DomainUser, Guid>
    {
        private readonly DbSet<DomainUser> _userSet;
        private readonly ResourceDefinitionHitCounter _hitCounter;
        private readonly List<OutgoingMessage> _pendingMessages = new();

        private string _beforeLoginName;
        private string _beforeDisplayName;

        protected MessagingUserDefinition(IResourceGraph resourceGraph, DbSet<DomainUser> userSet, ResourceDefinitionHitCounter hitCounter)
            : base(resourceGraph)
        {
            _userSet = userSet;
            _hitCounter = hitCounter;
        }

        public override Task OnPrepareWriteAsync(DomainUser user, OperationKind operationKind, CancellationToken cancellationToken)
        {
            _hitCounter.TrackInvocation<DomainUser>(ResourceDefinitionHitCounter.ExtensibilityPoint.OnPrepareWriteAsync);

            if (operationKind == OperationKind.CreateResource)
            {
                user.Id = Guid.NewGuid();
            }
            else if (operationKind == OperationKind.UpdateResource)
            {
                _beforeLoginName = user.LoginName;
                _beforeDisplayName = user.DisplayName;
            }

            return Task.CompletedTask;
        }

        public override Task<IIdentifiable> OnSetToOneRelationshipAsync(DomainUser user, HasOneAttribute hasOneRelationship, IIdentifiable rightResourceId,
            OperationKind operationKind, CancellationToken cancellationToken)
        {
            _hitCounter.TrackInvocation<DomainUser>(ResourceDefinitionHitCounter.ExtensibilityPoint.OnSetToOneRelationshipAsync);

            if (hasOneRelationship.Property.Name == nameof(DomainUser.Group))
            {
                var afterGroupId = (Guid?)rightResourceId?.GetTypedId();
                IMessageContent content = null;

                if (user.Group != null && afterGroupId == null)
                {
                    content = new UserRemovedFromGroupContent
                    {
                        UserId = user.Id,
                        GroupId = user.Group.Id
                    };
                }
                else if (user.Group == null && afterGroupId != null)
                {
                    content = new UserAddedToGroupContent
                    {
                        UserId = user.Id,
                        GroupId = afterGroupId.Value
                    };
                }
                else if (user.Group != null && afterGroupId != null && user.Group.Id != afterGroupId)
                {
                    content = new UserMovedToGroupContent
                    {
                        UserId = user.Id,
                        BeforeGroupId = user.Group.Id,
                        AfterGroupId = afterGroupId.Value
                    };
                }

                if (content != null)
                {
                    var message = OutgoingMessage.CreateFromContent(content);
                    _pendingMessages.Add(message);
                }
            }

            return Task.FromResult(rightResourceId);
        }

        protected async Task FinishWriteAsync(DomainUser user, OperationKind operationKind, CancellationToken cancellationToken)
        {
            if (operationKind == OperationKind.CreateResource)
            {
                var message = OutgoingMessage.CreateFromContent(new UserCreatedContent
                {
                    UserId = user.Id,
                    UserLoginName = user.LoginName,
                    UserDisplayName = user.DisplayName
                });

                await FlushMessageAsync(message, cancellationToken);
            }
            else if (operationKind == OperationKind.UpdateResource)
            {
                if (_beforeLoginName != user.LoginName)
                {
                    var message = OutgoingMessage.CreateFromContent(new UserLoginNameChangedContent
                    {
                        UserId = user.Id,
                        BeforeUserLoginName = _beforeLoginName,
                        AfterUserLoginName = user.LoginName
                    });

                    await FlushMessageAsync(message, cancellationToken);
                }

                if (_beforeDisplayName != user.DisplayName)
                {
                    var message = OutgoingMessage.CreateFromContent(new UserDisplayNameChangedContent
                    {
                        UserId = user.Id,
                        BeforeUserDisplayName = _beforeDisplayName,
                        AfterUserDisplayName = user.DisplayName
                    });

                    await FlushMessageAsync(message, cancellationToken);
                }
            }
            else if (operationKind == OperationKind.DeleteResource)
            {
                DomainUser userToDelete = await GetUserToDeleteAsync(user.Id, cancellationToken);

                if (userToDelete?.Group != null)
                {
                    var removeMessage = OutgoingMessage.CreateFromContent(new UserRemovedFromGroupContent
                    {
                        UserId = user.Id,
                        GroupId = userToDelete.Group.Id
                    });

                    await FlushMessageAsync(removeMessage, cancellationToken);
                }

                var deleteMessage = OutgoingMessage.CreateFromContent(new UserDeletedContent
                {
                    UserId = user.Id
                });

                await FlushMessageAsync(deleteMessage, cancellationToken);
            }

            foreach (OutgoingMessage nextMessage in _pendingMessages)
            {
                await FlushMessageAsync(nextMessage, cancellationToken);
            }
        }

        protected abstract Task FlushMessageAsync(OutgoingMessage message, CancellationToken cancellationToken);

        protected virtual async Task<DomainUser> GetUserToDeleteAsync(Guid userId, CancellationToken cancellationToken)
        {
            return await _userSet.Include(domainUser => domainUser.Group).FirstOrDefaultAsync(domainUser => domainUser.Id == userId, cancellationToken);
        }
    }
}
