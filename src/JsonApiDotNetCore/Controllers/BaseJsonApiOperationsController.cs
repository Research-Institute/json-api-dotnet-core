using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using JsonApiDotNetCore.AtomicOperations;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;

namespace JsonApiDotNetCore.Controllers
{
    /// <summary>
    /// Implements the foundational ASP.NET Core controller layer in the JsonApiDotNetCore architecture for handling atomic:operations requests. See
    /// https://jsonapi.org/ext/atomic/ for details. Delegates work to <see cref="IOperationsProcessor" />.
    /// </summary>
    [PublicAPI]
    public abstract class BaseJsonApiOperationsController : CoreJsonApiController
    {
        private readonly IJsonApiOptions _options;
        private readonly IOperationsProcessor _processor;
        private readonly IJsonApiRequest _request;
        private readonly ITargetedFields _targetedFields;
        private readonly TraceLogWriter<BaseJsonApiOperationsController> _traceWriter;

        protected BaseJsonApiOperationsController(IJsonApiOptions options, ILoggerFactory loggerFactory, IOperationsProcessor processor,
            IJsonApiRequest request, ITargetedFields targetedFields)
        {
            ArgumentGuard.NotNull(options, nameof(options));
            ArgumentGuard.NotNull(loggerFactory, nameof(loggerFactory));
            ArgumentGuard.NotNull(processor, nameof(processor));
            ArgumentGuard.NotNull(request, nameof(request));
            ArgumentGuard.NotNull(targetedFields, nameof(targetedFields));

            _options = options;
            _processor = processor;
            _request = request;
            _targetedFields = targetedFields;
            _traceWriter = new TraceLogWriter<BaseJsonApiOperationsController>(loggerFactory);
        }

        /// <summary>
        /// Atomically processes a list of operations and returns a list of results. All changes are reverted if processing fails. If processing succeeds but
        /// none of the operations returns any data, then HTTP 201 is returned instead of 200.
        /// </summary>
        /// <example>
        /// The next example creates a new resource.
        /// <code><![CDATA[
        /// POST /operations HTTP/1.1
        /// Content-Type: application/vnd.api+json;ext="https://jsonapi.org/ext/atomic"
        /// 
        /// {
        ///   "atomic:operations": [{
        ///     "op": "add",
        ///     "data": {
        ///       "type": "authors",
        ///       "attributes": {
        ///         "name": "John Doe"
        ///       }
        ///     }
        ///   }]
        /// }
        /// ]]></code>
        /// </example>
        /// <example>
        /// The next example updates an existing resource.
        /// <code><![CDATA[
        /// POST /operations HTTP/1.1
        /// Content-Type: application/vnd.api+json;ext="https://jsonapi.org/ext/atomic"
        /// 
        /// {
        ///   "atomic:operations": [{
        ///     "op": "update",
        ///     "data": {
        ///       "type": "authors",
        ///       "id": 1,
        ///       "attributes": {
        ///         "name": "Jane Doe"
        ///       }
        ///     }
        ///   }]
        /// }
        /// ]]></code>
        /// </example>
        /// <example>
        /// The next example deletes an existing resource.
        /// <code><![CDATA[
        /// POST /operations HTTP/1.1
        /// Content-Type: application/vnd.api+json;ext="https://jsonapi.org/ext/atomic"
        /// 
        /// {
        ///   "atomic:operations": [{
        ///     "op": "remove",
        ///     "ref": {
        ///       "type": "authors",
        ///       "id": 1
        ///     }
        ///   }]
        /// }
        /// ]]></code>
        /// </example>
        public virtual async Task<IActionResult> PostOperationsAsync([FromBody] IList<OperationContainer> operations, CancellationToken cancellationToken)
        {
            _traceWriter.LogMethodStart(new
            {
                operations
            });

            ArgumentGuard.NotNull(operations, nameof(operations));

            ValidateClientGeneratedIds(operations);

            if (_options.ValidateModelState)
            {
                ValidateModelState(operations);
            }

            IList<OperationContainer> results = await _processor.ProcessAsync(operations, cancellationToken);
            return results.Any(result => result != null) ? Ok(results) : NoContent();
        }

        protected virtual void ValidateClientGeneratedIds(IEnumerable<OperationContainer> operations)
        {
            if (!_options.AllowClientGeneratedIds)
            {
                int index = 0;

                foreach (OperationContainer operation in operations)
                {
                    if (operation.Kind == OperationKind.CreateResource && operation.Resource.StringId != null)
                    {
                        throw new ResourceIdInCreateResourceNotAllowedException(index);
                    }

                    index++;
                }
            }
        }

        protected virtual void ValidateModelState(IEnumerable<OperationContainer> operations)
        {
            // We must validate the resource inside each operation manually, because they are typed as IIdentifiable.
            // Instead of validating IIdentifiable we need to validate the resource runtime-type.

            var violations = new List<ModelStateViolation>();

            int index = 0;

            foreach (OperationContainer operation in operations)
            {
                if (operation.Kind == OperationKind.CreateResource || operation.Kind == OperationKind.UpdateResource)
                {
                    _targetedFields.Attributes = operation.TargetedFields.Attributes;
                    _targetedFields.Relationships = operation.TargetedFields.Relationships;

                    _request.CopyFrom(operation.Request);

                    var validationContext = new ActionContext();
                    ObjectValidator.Validate(validationContext, null, string.Empty, operation.Resource);

                    if (!validationContext.ModelState.IsValid)
                    {
                        AddValidationErrors(validationContext.ModelState, operation.Resource.GetType(), index, violations);
                    }
                }

                index++;
            }

            if (violations.Any())
            {
                throw new InvalidModelStateException(violations, _options.IncludeExceptionStackTraceInErrors, _options.SerializerNamingStrategy);
            }
        }

        private static void AddValidationErrors(ModelStateDictionary modelState, Type resourceType, int operationIndex, List<ModelStateViolation> violations)
        {
            foreach ((string propertyName, ModelStateEntry entry) in modelState)
            {
                AddValidationErrors(entry, propertyName, resourceType, operationIndex, violations);
            }
        }

        private static void AddValidationErrors(ModelStateEntry entry, string propertyName, Type resourceType, int operationIndex,
            List<ModelStateViolation> violations)
        {
            foreach (ModelError error in entry.Errors)
            {
                string prefix = $"/atomic:operations[{operationIndex}]/data/attributes/";
                var violation = new ModelStateViolation(prefix, propertyName, resourceType, error);

                violations.Add(violation);
            }
        }
    }
}
