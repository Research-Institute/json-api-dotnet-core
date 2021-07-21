using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;

// ReSharper disable NonReadonlyMemberInGetHashCode

namespace JsonApiDotNetCore.Resources.Annotations
{
    /// <summary>
    /// Used to expose a property on a resource class as a JSON:API to-many relationship (https://jsonapi.org/format/#document-resource-object-relationships)
    /// through a many-to-many join relationship.
    /// </summary>
    /// <example>
    /// In the following example, we expose a relationship named "tags" through the navigation property `ArticleTags`. The `Tags` property is decorated with
    /// `NotMapped` so that EF does not try to map this to a database relationship.
    /// <code><![CDATA[
    /// public sealed class Article : Identifiable
    /// {
    ///     [NotMapped]
    ///     [HasManyThrough(nameof(ArticleTags), PublicName = "tags")]
    ///     public ISet<Tag> Tags { get; set; }
    ///     public ISet<ArticleTag> ArticleTags { get; set; }
    /// }
    /// 
    /// public class Tag : Identifiable
    /// {
    ///     [Attr]
    ///     public string Name { get; set; }
    /// }
    /// 
    /// public sealed class ArticleTag
    /// {
    ///     public int ArticleId { get; set; }
    ///     public Article Article { get; set; }
    /// 
    ///     public int TagId { get; set; }
    ///     public Tag Tag { get; set; }
    /// }
    /// ]]></code>
    /// </example>
    [PublicAPI]
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class HasManyThroughAttribute : HasManyAttribute
    {
        private static readonly CollectionConverter CollectionConverter = new();

        /// <summary>
        /// The name of the join property on the parent resource. In the example described above, this would be "ArticleTags".
        /// </summary>
        public string ThroughPropertyName { get; }

        /// <summary>
        /// The join type. In the example described above, this would be `ArticleTag`.
        /// </summary>
        public Type ThroughType { get; internal set; }

        /// <summary>
        /// The navigation property back to the parent resource from the through type. In the example described above, this would point to the
        /// `Article.ArticleTags.Article` property.
        /// </summary>
        public PropertyInfo LeftProperty { get; internal set; }

        /// <summary>
        /// The ID property back to the parent resource from the through type. In the example described above, this would point to the
        /// `Article.ArticleTags.ArticleId` property.
        /// </summary>
        public PropertyInfo LeftIdProperty { get; internal set; }

        /// <summary>
        /// The navigation property to the related resource from the through type. In the example described above, this would point to the
        /// `Article.ArticleTags.Tag` property.
        /// </summary>
        public PropertyInfo RightProperty { get; internal set; }

        /// <summary>
        /// The ID property to the related resource from the through type. In the example described above, this would point to the `Article.ArticleTags.TagId`
        /// property.
        /// </summary>
        public PropertyInfo RightIdProperty { get; internal set; }

        /// <summary>
        /// The join resource property on the parent resource. In the example described above, this would point to the `Article.ArticleTags` property.
        /// </summary>
        public PropertyInfo ThroughProperty { get; internal set; }

        /// <summary>
        /// The internal navigation property path to the related resource. In the example described above, this would contain "ArticleTags.Tag".
        /// </summary>
        public override string RelationshipPath => $"{ThroughProperty.Name}.{RightProperty.Name}";

        /// <summary>
        /// Required for a self-referencing many-to-many relationship. Contains the name of the property back to the parent resource from the through type.
        /// </summary>
        public string LeftPropertyName { get; set; }

        /// <summary>
        /// Required for a self-referencing many-to-many relationship. Contains the name of the property to the related resource from the through type.
        /// </summary>
        public string RightPropertyName { get; set; }

        /// <summary>
        /// Optional. Can be used to indicate a non-default name for the ID property back to the parent resource from the through type. Defaults to the name of
        /// <see cref="LeftProperty" /> suffixed with "Id". In the example described above, this would be "ArticleId".
        /// </summary>
        public string LeftIdPropertyName { get; set; }

        /// <summary>
        /// Optional. Can be used to indicate a non-default name for the ID property to the related resource from the through type. Defaults to the name of
        /// <see cref="RightProperty" /> suffixed with "Id". In the example described above, this would be "TagId".
        /// </summary>
        public string RightIdPropertyName { get; set; }

        /// <summary>
        /// Creates a HasMany relationship through a many-to-many join relationship.
        /// </summary>
        /// <param name="throughPropertyName">
        /// The name of the navigation property that will be used to access the join relationship.
        /// </param>
        public HasManyThroughAttribute(string throughPropertyName)
        {
            ArgumentGuard.NotNullNorEmpty(throughPropertyName, nameof(throughPropertyName));

            ThroughPropertyName = throughPropertyName;
        }

        /// <summary>
        /// Traverses through the provided resource and returns the value of the relationship on the other side of the through type. In the example described
        /// above, this would be the value of "Articles.ArticleTags.Tag".
        /// </summary>
        public override object GetValue(object resource)
        {
            ArgumentGuard.NotNull(resource, nameof(resource));

            object throughEntity = ThroughProperty.GetValue(resource);

            if (throughEntity == null)
            {
                return null;
            }

            IEnumerable<object> rightResources = ((IEnumerable)throughEntity).Cast<object>().Select(rightResource => RightProperty.GetValue(rightResource));

            return CollectionConverter.CopyToTypedCollection(rightResources, Property.PropertyType);
        }

        /// <summary>
        /// Traverses through the provided resource and sets the value of the relationship on the other side of the through type. In the example described above,
        /// this would be the value of "Articles.ArticleTags.Tag".
        /// </summary>
        public override void SetValue(object resource, object newValue)
        {
            ArgumentGuard.NotNull(resource, nameof(resource));

            base.SetValue(resource, newValue);

            if (newValue == null)
            {
                ThroughProperty.SetValue(resource, null);
            }
            else
            {
                var throughResources = new List<object>();

                foreach (IIdentifiable rightResource in (IEnumerable)newValue)
                {
                    object throughEntity = Activator.CreateInstance(ThroughType);

                    LeftProperty.SetValue(throughEntity, resource);
                    RightProperty.SetValue(throughEntity, rightResource);
                    throughResources.Add(throughEntity);
                }

                IEnumerable typedCollection = CollectionConverter.CopyToTypedCollection(throughResources, ThroughProperty.PropertyType);
                ThroughProperty.SetValue(resource, typedCollection);
            }
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj is null || GetType() != obj.GetType())
            {
                return false;
            }

            var other = (HasManyThroughAttribute)obj;

            return ThroughPropertyName == other.ThroughPropertyName && ThroughType == other.ThroughType && LeftProperty == other.LeftProperty &&
                LeftIdProperty == other.LeftIdProperty && RightProperty == other.RightProperty && RightIdProperty == other.RightIdProperty &&
                ThroughProperty == other.ThroughProperty && base.Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ThroughPropertyName, ThroughType, LeftProperty, LeftIdProperty, RightProperty, RightIdProperty, ThroughProperty,
                base.GetHashCode());
        }
    }
}
