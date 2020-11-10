using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCore.Configuration
{
    /// <summary>
    /// Responsible for populating the <see cref="RelationshipAttribute.InverseNavigationProperty"/> property.
    /// 
    /// This service is instantiated in the configure phase of the application.
    /// 
    /// When using a data access layer different from EF Core, and when using ResourceHooks
    /// that depend on the inverse navigation property (BeforeImplicitUpdateRelationship),
    /// you will need to override this service, or pass along the InverseNavigationProperty in
    /// the RelationshipAttribute.
    /// </summary>
    public interface IInverseRelationshipResolver
    {
        /// <summary>
        /// This method is called upon startup by JsonApiDotNetCore. It resolves inverse relationships. 
        /// </summary>
        void Resolve();
    }
}
