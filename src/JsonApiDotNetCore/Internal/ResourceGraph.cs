using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Internal.Contracts;
using JsonApiDotNetCore.Models;

namespace JsonApiDotNetCore.Internal
{
    internal class ControllerMapping
    {

    }
    /// <summary>
    ///  keeps track of all the models/resources defined in JADNC
    /// </summary>
    public class ResourceGraph : IResourceGraph
    {
        internal List<ContextEntity> Entities { get; }
        internal List<ValidationResult> ValidationResults { get; }
        


        [Obsolete("please instantiate properly")]
        internal static IResourceGraph Instance { get; set; }

        public ResourceGraph() { }
        public ResourceGraph(List<ContextEntity> entities, bool usesDbContext)
        {
            Entities = entities;
            UsesDbContext = usesDbContext;
            ValidationResults = new List<ValidationResult>();
            Instance = this;
        }

        public ContextEntity GetEntityType(string entityName)
        {
            return Entities.Where(e => e.EntityName == entityName).FirstOrDefault();
        }

        // eventually, this is the planned public constructor
        // to avoid breaking changes, we will be leaving the original constructor in place
        // until the context graph validation process is completed
        // you can track progress on this issue here: https://github.com/json-api-dotnet/JsonApiDotNetCore/issues/170
        internal ResourceGraph(List<ContextEntity> entities, bool usesDbContext, List<ValidationResult> validationResults)
        {
            Entities = entities;
            UsesDbContext = usesDbContext;
            ValidationResults = validationResults;
            Instance = this;
        }

        /// <inheritdoc />
        public bool UsesDbContext { get; }

        /// <inheritdoc />
        public ContextEntity GetContextEntity(string entityName)
            => Entities.SingleOrDefault(e => string.Equals(e.EntityName, entityName, StringComparison.OrdinalIgnoreCase));

        /// <inheritdoc />
        public ContextEntity GetContextEntity(Type entityType)
            => Entities.SingleOrDefault(e => e.EntityType == entityType);

        /// <inheritdoc />
        public object GetRelationship<TParent>(TParent entity, string relationshipName)
        {
            var parentEntityType = entity.GetType();

            var navigationProperty = parentEntityType
                .GetProperties()
                .SingleOrDefault(p => string.Equals(p.Name, relationshipName, StringComparison.OrdinalIgnoreCase));

            if (navigationProperty == null)
                throw new JsonApiException(400, $"{parentEntityType} does not contain a relationship named {relationshipName}");

            return navigationProperty.GetValue(entity);
        }

        public object GetRelationshipValue<TParent>(TParent resource, RelationshipAttribute relationship) where TParent : IIdentifiable
        {
            if(relationship is HasManyThroughAttribute hasManyThroughRelationship) 
            {
                return GetHasManyThrough(resource, hasManyThroughRelationship);
            }

            return GetRelationship(resource, relationship.InternalRelationshipName);
        }

        private IEnumerable<IIdentifiable> GetHasManyThrough(IIdentifiable parent, HasManyThroughAttribute hasManyThrough)
        {
            var throughProperty = GetRelationship(parent, hasManyThrough.InternalThroughName);
            if (throughProperty is IEnumerable hasManyNavigationEntity)
            {
                // wrap "yield return" in a sub-function so we can correctly return null if the property is null.
                return GetHasManyThroughIter(hasManyThrough, hasManyNavigationEntity);
            }
            return null;
        }

        private IEnumerable<IIdentifiable> GetHasManyThroughIter(HasManyThroughAttribute hasManyThrough, IEnumerable hasManyNavigationEntity)
        {
            foreach (var includedEntity in hasManyNavigationEntity)
            {
                var targetValue = hasManyThrough.RightProperty.GetValue(includedEntity) as IIdentifiable;
                yield return targetValue;
            }
        }

        /// <inheritdoc />
        public string GetRelationshipName<TParent>(string relationshipName)
        {
            var entityType = typeof(TParent);
            return Entities
                .SingleOrDefault(e => e.EntityType == entityType)
                ?.Relationships
                .SingleOrDefault(r => r.Is(relationshipName))
                ?.InternalRelationshipName;
        }

        public string GetPublicAttributeName<TParent>(string internalAttributeName)
        {
            return GetContextEntity(typeof(TParent))
                .Attributes
                .SingleOrDefault(a => a.InternalAttributeName == internalAttributeName)?
                .PublicAttributeName;
        }


    }
}