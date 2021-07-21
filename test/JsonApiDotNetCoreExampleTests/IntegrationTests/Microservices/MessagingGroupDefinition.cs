using System;
using System.Collections.Generic;
using System.Linq;
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
    public abstract class MessagingGroupDefinition : JsonApiResourceDefinition<DomainGroup, Guid>
    {
        private readonly DbSet<DomainUser> _userSet;
        private readonly DbSet<DomainGroup> _groupSet;
        private readonly ResourceDefinitionHitCounter _hitCounter;
        private readonly List<OutgoingMessage> _pendingMessages = new();

        private string _beforeGroupName;

        protected MessagingGroupDefinition(IResourceGraph resourceGraph, DbSet<DomainUser> userSet, DbSet<DomainGroup> groupSet,
            ResourceDefinitionHitCounter hitCounter)
            : base(resourceGraph)
        {
            _userSet = userSet;
            _groupSet = groupSet;
            _hitCounter = hitCounter;
        }

        public override Task OnPrepareWriteAsync(DomainGroup group, OperationKind operationKind, CancellationToken cancellationToken)
        {
            _hitCounter.TrackInvocation<DomainGroup>(ResourceDefinitionHitCounter.ExtensibilityPoint.OnPrepareWriteAsync);

            if (operationKind == OperationKind.CreateResource)
            {
                group.Id = Guid.NewGuid();
            }
            else if (operationKind == OperationKind.UpdateResource)
            {
                _beforeGroupName = group.Name;
            }

            return Task.CompletedTask;
        }

        public override async Task OnSetToManyRelationshipAsync(DomainGroup group, HasManyAttribute hasManyRelationship, ISet<IIdentifiable> rightResourceIds,
            OperationKind operationKind, CancellationToken cancellationToken)
        {
            _hitCounter.TrackInvocation<DomainGroup>(ResourceDefinitionHitCounter.ExtensibilityPoint.OnSetToManyRelationshipAsync);

            if (hasManyRelationship.Property.Name == nameof(DomainGroup.Users))
            {
                HashSet<Guid> rightUserIds = rightResourceIds.Select(resource => (Guid)resource.GetTypedId()).ToHashSet();

                List<DomainUser> beforeUsers = await _userSet.Include(user => user.Group).Where(user => rightUserIds.Contains(user.Id))
                    .ToListAsync(cancellationToken);

                foreach (DomainUser beforeUser in beforeUsers)
                {
                    IMessageContent content = null;

                    if (beforeUser.Group == null)
                    {
                        content = new UserAddedToGroupContent
                        {
                            UserId = beforeUser.Id,
                            GroupId = group.Id
                        };
                    }
                    else if (beforeUser.Group != null && beforeUser.Group.Id != group.Id)
                    {
                        content = new UserMovedToGroupContent
                        {
                            UserId = beforeUser.Id,
                            BeforeGroupId = beforeUser.Group.Id,
                            AfterGroupId = group.Id
                        };
                    }

                    if (content != null)
                    {
                        _pendingMessages.Add(OutgoingMessage.CreateFromContent(content));
                    }
                }

                if (group.Users != null)
                {
                    foreach (DomainUser userToRemoveFromGroup in group.Users.Where(user => !rightUserIds.Contains(user.Id)))
                    {
                        var message = OutgoingMessage.CreateFromContent(new UserRemovedFromGroupContent
                        {
                            UserId = userToRemoveFromGroup.Id,
                            GroupId = group.Id
                        });

                        _pendingMessages.Add(message);
                    }
                }
            }
        }

        public override async Task OnAddToRelationshipAsync(Guid groupId, HasManyAttribute hasManyRelationship, ISet<IIdentifiable> rightResourceIds,
            CancellationToken cancellationToken)
        {
            _hitCounter.TrackInvocation<DomainGroup>(ResourceDefinitionHitCounter.ExtensibilityPoint.OnAddToRelationshipAsync);

            if (hasManyRelationship.Property.Name == nameof(DomainGroup.Users))
            {
                HashSet<Guid> rightUserIds = rightResourceIds.Select(resource => (Guid)resource.GetTypedId()).ToHashSet();

                List<DomainUser> beforeUsers = await _userSet.Include(user => user.Group).Where(user => rightUserIds.Contains(user.Id))
                    .ToListAsync(cancellationToken);

                foreach (DomainUser beforeUser in beforeUsers)
                {
                    IMessageContent content = null;

                    if (beforeUser.Group == null)
                    {
                        content = new UserAddedToGroupContent
                        {
                            UserId = beforeUser.Id,
                            GroupId = groupId
                        };
                    }
                    else if (beforeUser.Group != null && beforeUser.Group.Id != groupId)
                    {
                        content = new UserMovedToGroupContent
                        {
                            UserId = beforeUser.Id,
                            BeforeGroupId = beforeUser.Group.Id,
                            AfterGroupId = groupId
                        };
                    }

                    if (content != null)
                    {
                        _pendingMessages.Add(OutgoingMessage.CreateFromContent(content));
                    }
                }
            }
        }

        public override Task OnRemoveFromRelationshipAsync(DomainGroup group, HasManyAttribute hasManyRelationship, ISet<IIdentifiable> rightResourceIds,
            CancellationToken cancellationToken)
        {
            _hitCounter.TrackInvocation<DomainGroup>(ResourceDefinitionHitCounter.ExtensibilityPoint.OnRemoveFromRelationshipAsync);

            if (hasManyRelationship.Property.Name == nameof(DomainGroup.Users))
            {
                HashSet<Guid> rightUserIds = rightResourceIds.Select(resource => (Guid)resource.GetTypedId()).ToHashSet();

                foreach (DomainUser userToRemoveFromGroup in group.Users.Where(user => rightUserIds.Contains(user.Id)))
                {
                    var message = OutgoingMessage.CreateFromContent(new UserRemovedFromGroupContent
                    {
                        UserId = userToRemoveFromGroup.Id,
                        GroupId = group.Id
                    });

                    _pendingMessages.Add(message);
                }
            }

            return Task.CompletedTask;
        }

        protected async Task FinishWriteAsync(DomainGroup group, OperationKind operationKind, CancellationToken cancellationToken)
        {
            if (operationKind == OperationKind.CreateResource)
            {
                var message = OutgoingMessage.CreateFromContent(new GroupCreatedContent
                {
                    GroupId = group.Id,
                    GroupName = group.Name
                });

                await FlushMessageAsync(message, cancellationToken);
            }
            else if (operationKind == OperationKind.UpdateResource)
            {
                if (_beforeGroupName != group.Name)
                {
                    var message = OutgoingMessage.CreateFromContent(new GroupRenamedContent
                    {
                        GroupId = group.Id,
                        BeforeGroupName = _beforeGroupName,
                        AfterGroupName = group.Name
                    });

                    await FlushMessageAsync(message, cancellationToken);
                }
            }
            else if (operationKind == OperationKind.DeleteResource)
            {
                DomainGroup groupToDelete = await GetGroupToDeleteAsync(group.Id, cancellationToken);

                if (groupToDelete != null)
                {
                    foreach (DomainUser user in groupToDelete.Users)
                    {
                        var removeMessage = OutgoingMessage.CreateFromContent(new UserRemovedFromGroupContent
                        {
                            UserId = user.Id,
                            GroupId = group.Id
                        });

                        await FlushMessageAsync(removeMessage, cancellationToken);
                    }
                }

                var deleteMessage = OutgoingMessage.CreateFromContent(new GroupDeletedContent
                {
                    GroupId = group.Id
                });

                await FlushMessageAsync(deleteMessage, cancellationToken);
            }

            foreach (OutgoingMessage nextMessage in _pendingMessages)
            {
                await FlushMessageAsync(nextMessage, cancellationToken);
            }
        }

        protected abstract Task FlushMessageAsync(OutgoingMessage message, CancellationToken cancellationToken);

        protected virtual async Task<DomainGroup> GetGroupToDeleteAsync(Guid groupId, CancellationToken cancellationToken)
        {
            return await _groupSet.Include(group => group.Users).FirstOrDefaultAsync(group => group.Id == groupId, cancellationToken);
        }
    }
}
