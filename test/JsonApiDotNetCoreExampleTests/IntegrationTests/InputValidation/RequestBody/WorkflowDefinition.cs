using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Objects;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.InputValidation.RequestBody
{
    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
    public sealed class WorkflowDefinition : JsonApiResourceDefinition<Workflow, Guid>
    {
        private static readonly Dictionary<WorkflowStage, ICollection<WorkflowStage>> StageTransitionTable = new()
        {
            [WorkflowStage.Created] = new[]
            {
                WorkflowStage.InProgress
            },
            [WorkflowStage.InProgress] = new[]
            {
                WorkflowStage.OnHold,
                WorkflowStage.Succeeded,
                WorkflowStage.Failed,
                WorkflowStage.Canceled
            },
            [WorkflowStage.OnHold] = new[]
            {
                WorkflowStage.InProgress,
                WorkflowStage.Canceled
            }
        };

        private WorkflowStage _previousStage;

        public WorkflowDefinition(IResourceGraph resourceGraph)
            : base(resourceGraph)
        {
        }

        public override Task OnPrepareWriteAsync(Workflow resource, OperationKind operationKind, CancellationToken cancellationToken)
        {
            if (operationKind == OperationKind.UpdateResource)
            {
                _previousStage = resource.Stage;
            }

            return Task.CompletedTask;
        }

        public override Task OnWritingAsync(Workflow resource, OperationKind operationKind, CancellationToken cancellationToken)
        {
            if (operationKind == OperationKind.CreateResource)
            {
                AssertHasValidInitialStage(resource);
            }
            else if (operationKind == OperationKind.UpdateResource && resource.Stage != _previousStage)
            {
                AssertCanTransitionToStage(_previousStage, resource.Stage);
            }

            return Task.CompletedTask;
        }

        [AssertionMethod]
        private static void AssertHasValidInitialStage(Workflow resource)
        {
            if (resource.Stage != WorkflowStage.Created)
            {
                throw new JsonApiException(new Error(HttpStatusCode.UnprocessableEntity)
                {
                    Title = "Invalid workflow stage.",
                    Detail = $"Initial stage of workflow must be '{WorkflowStage.Created}'.",
                    Source =
                    {
                        Pointer = "/data/attributes/stage"
                    }
                });
            }
        }

        [AssertionMethod]
        private static void AssertCanTransitionToStage(WorkflowStage fromStage, WorkflowStage toStage)
        {
            if (!CanTransitionToStage(fromStage, toStage))
            {
                throw new JsonApiException(new Error(HttpStatusCode.UnprocessableEntity)
                {
                    Title = "Invalid workflow stage.",
                    Detail = $"Cannot transition from '{fromStage}' to '{toStage}'.",
                    Source =
                    {
                        Pointer = "/data/attributes/stage"
                    }
                });
            }
        }

        private static bool CanTransitionToStage(WorkflowStage fromStage, WorkflowStage toStage)
        {
            if (StageTransitionTable.ContainsKey(fromStage))
            {
                ICollection<WorkflowStage> possibleNextStages = StageTransitionTable[fromStage];
                return possibleNextStages.Contains(toStage);
            }

            return false;
        }
    }
}
