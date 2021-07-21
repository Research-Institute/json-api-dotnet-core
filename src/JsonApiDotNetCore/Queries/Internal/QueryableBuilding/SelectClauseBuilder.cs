using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Repositories;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace JsonApiDotNetCore.Queries.Internal.QueryableBuilding
{
    /// <summary>
    /// Transforms <see cref="SparseFieldSetExpression" /> into
    /// <see cref="Queryable.Select{TSource, TKey}(IQueryable{TSource}, System.Linq.Expressions.Expression{System.Func{TSource,TKey}})" /> calls.
    /// </summary>
    [PublicAPI]
    public class SelectClauseBuilder : QueryClauseBuilder<object>
    {
        private static readonly CollectionConverter CollectionConverter = new();
        private static readonly ConstantExpression NullConstant = Expression.Constant(null);

        private readonly Expression _source;
        private readonly IModel _entityModel;
        private readonly Type _extensionType;
        private readonly LambdaParameterNameFactory _nameFactory;
        private readonly IResourceFactory _resourceFactory;
        private readonly IResourceContextProvider _resourceContextProvider;

        public SelectClauseBuilder(Expression source, LambdaScope lambdaScope, IModel entityModel, Type extensionType, LambdaParameterNameFactory nameFactory,
            IResourceFactory resourceFactory, IResourceContextProvider resourceContextProvider)
            : base(lambdaScope)
        {
            ArgumentGuard.NotNull(source, nameof(source));
            ArgumentGuard.NotNull(entityModel, nameof(entityModel));
            ArgumentGuard.NotNull(extensionType, nameof(extensionType));
            ArgumentGuard.NotNull(nameFactory, nameof(nameFactory));
            ArgumentGuard.NotNull(resourceFactory, nameof(resourceFactory));
            ArgumentGuard.NotNull(resourceContextProvider, nameof(resourceContextProvider));

            _source = source;
            _entityModel = entityModel;
            _extensionType = extensionType;
            _nameFactory = nameFactory;
            _resourceFactory = resourceFactory;
            _resourceContextProvider = resourceContextProvider;
        }

        public Expression ApplySelect(IDictionary<ResourceFieldAttribute, QueryLayer> selectors, ResourceContext resourceContext)
        {
            ArgumentGuard.NotNull(selectors, nameof(selectors));

            if (!selectors.Any())
            {
                return _source;
            }

            Expression bodyInitializer = CreateLambdaBodyInitializer(selectors, resourceContext, LambdaScope, false);

            LambdaExpression lambda = Expression.Lambda(bodyInitializer, LambdaScope.Parameter);

            return SelectExtensionMethodCall(_source, LambdaScope.Parameter.Type, lambda);
        }

        private Expression CreateLambdaBodyInitializer(IDictionary<ResourceFieldAttribute, QueryLayer> selectors, ResourceContext resourceContext,
            LambdaScope lambdaScope, bool lambdaAccessorRequiresTestForNull)
        {
            ICollection<PropertySelector> propertySelectors = ToPropertySelectors(selectors, resourceContext, lambdaScope.Accessor.Type);

            MemberBinding[] propertyAssignments =
                propertySelectors.Select(selector => CreatePropertyAssignment(selector, lambdaScope)).Cast<MemberBinding>().ToArray();

            NewExpression newExpression = _resourceFactory.CreateNewExpression(lambdaScope.Accessor.Type);
            Expression memberInit = Expression.MemberInit(newExpression, propertyAssignments);

            if (lambdaScope.HasManyThrough != null)
            {
                MemberBinding outerPropertyAssignment = Expression.Bind(lambdaScope.HasManyThrough.RightProperty, memberInit);

                NewExpression outerNewExpression = _resourceFactory.CreateNewExpression(lambdaScope.HasManyThrough.ThroughType);
                memberInit = Expression.MemberInit(outerNewExpression, outerPropertyAssignment);
            }

            if (!lambdaAccessorRequiresTestForNull)
            {
                return memberInit;
            }

            return TestForNull(lambdaScope.Accessor, memberInit);
        }

        private ICollection<PropertySelector> ToPropertySelectors(IDictionary<ResourceFieldAttribute, QueryLayer> resourceFieldSelectors,
            ResourceContext resourceContext, Type elementType)
        {
            var propertySelectors = new Dictionary<PropertyInfo, PropertySelector>();

            // If a read-only attribute is selected, its value likely depends on another property, so select all resource properties.
            bool includesReadOnlyAttribute = resourceFieldSelectors.Any(selector =>
                selector.Key is AttrAttribute attribute && attribute.Property.SetMethod == null);

            bool containsOnlyRelationships = resourceFieldSelectors.All(selector => selector.Key is RelationshipAttribute);

            foreach (KeyValuePair<ResourceFieldAttribute, QueryLayer> fieldSelector in resourceFieldSelectors)
            {
                var propertySelector = new PropertySelector(fieldSelector.Key, fieldSelector.Value);

                if (propertySelector.Property.SetMethod != null)
                {
                    propertySelectors[propertySelector.Property] = propertySelector;
                }
            }

            if (includesReadOnlyAttribute || containsOnlyRelationships)
            {
                IEntityType entityModel = _entityModel.GetEntityTypes().Single(type => type.ClrType == elementType);
                IEnumerable<IProperty> entityProperties = entityModel.GetProperties().Where(property => !property.IsShadowProperty()).ToArray();

                foreach (IProperty entityProperty in entityProperties)
                {
                    var propertySelector = new PropertySelector(entityProperty.PropertyInfo);

                    if (propertySelector.Property.SetMethod != null)
                    {
                        propertySelectors[propertySelector.Property] = propertySelector;
                    }
                }
            }

            foreach (EagerLoadAttribute eagerLoad in resourceContext.EagerLoads)
            {
                var propertySelector = new PropertySelector(eagerLoad.Property);

                // When an entity navigation property is decorated with both EagerLoadAttribute and RelationshipAttribute,
                // it may already exist with a sub-layer. So do not overwrite in that case.
                if (!propertySelectors.ContainsKey(propertySelector.Property))
                {
                    propertySelectors[propertySelector.Property] = propertySelector;
                }

                propertySelectors[propertySelector.Property] = propertySelector;
            }

            return propertySelectors.Values;
        }

        private MemberAssignment CreatePropertyAssignment(PropertySelector selector, LambdaScope lambdaScope)
        {
            MemberExpression propertyAccess = Expression.Property(lambdaScope.Accessor, selector.Property);

            Expression assignmentRightHandSide = propertyAccess;

            if (selector.NextLayer != null)
            {
                var hasManyThrough = selector.OriginatingField as HasManyThroughAttribute;
                var lambdaScopeFactory = new LambdaScopeFactory(_nameFactory, hasManyThrough);

                assignmentRightHandSide = CreateAssignmentRightHandSideForLayer(selector.NextLayer, lambdaScope, propertyAccess,
                    selector.Property, lambdaScopeFactory);
            }

            return Expression.Bind(selector.Property, assignmentRightHandSide);
        }

        private Expression CreateAssignmentRightHandSideForLayer(QueryLayer layer, LambdaScope outerLambdaScope, MemberExpression propertyAccess,
            PropertyInfo selectorPropertyInfo, LambdaScopeFactory lambdaScopeFactory)
        {
            Type collectionElementType = CollectionConverter.TryGetCollectionElementType(selectorPropertyInfo.PropertyType);
            Type bodyElementType = collectionElementType ?? selectorPropertyInfo.PropertyType;

            if (collectionElementType != null)
            {
                return CreateCollectionInitializer(outerLambdaScope, selectorPropertyInfo, bodyElementType, layer, lambdaScopeFactory);
            }

            if (layer.Projection.IsNullOrEmpty())
            {
                return propertyAccess;
            }

            using LambdaScope scope = lambdaScopeFactory.CreateScope(bodyElementType, propertyAccess);
            return CreateLambdaBodyInitializer(layer.Projection, layer.ResourceContext, scope, true);
        }

        private Expression CreateCollectionInitializer(LambdaScope lambdaScope, PropertyInfo collectionProperty, Type elementType, QueryLayer layer,
            LambdaScopeFactory lambdaScopeFactory)
        {
            MemberExpression propertyExpression = Expression.Property(lambdaScope.Accessor, collectionProperty);

            var builder = new QueryableBuilder(propertyExpression, elementType, typeof(Enumerable), _nameFactory, _resourceFactory, _resourceContextProvider,
                _entityModel, lambdaScopeFactory);

            Expression layerExpression = builder.ApplyQuery(layer);

            // Earlier versions of EF Core 3.x failed to understand `query.ToHashSet()`, so we emit `new HashSet(query)` instead.
            // Interestingly, EF Core 5 RC1 fails to understand `new HashSet(query)`, so we emit `query.ToHashSet()` instead.
            // https://github.com/dotnet/efcore/issues/22902

            if (EntityFrameworkCoreSupport.Version.Major < 5)
            {
                Type enumerableOfElementType = typeof(IEnumerable<>).MakeGenericType(elementType);
                Type typedCollection = CollectionConverter.ToConcreteCollectionType(collectionProperty.PropertyType);

                ConstructorInfo typedCollectionConstructor = typedCollection.GetConstructor(enumerableOfElementType.AsArray());

                if (typedCollectionConstructor == null)
                {
                    throw new InvalidOperationException($"Constructor on '{typedCollection.Name}' that accepts '{enumerableOfElementType.Name}' not found.");
                }

                return Expression.New(typedCollectionConstructor, layerExpression);
            }

            string operationName = CollectionConverter.TypeCanContainHashSet(collectionProperty.PropertyType) ? "ToHashSet" : "ToList";
            return CopyCollectionExtensionMethodCall(layerExpression, operationName, elementType);
        }

        private static Expression TestForNull(Expression expressionToTest, Expression ifFalseExpression)
        {
            BinaryExpression equalsNull = Expression.Equal(expressionToTest, NullConstant);
            return Expression.Condition(equalsNull, Expression.Convert(NullConstant, expressionToTest.Type), ifFalseExpression);
        }

        private static Expression CopyCollectionExtensionMethodCall(Expression source, string operationName, Type elementType)
        {
            return Expression.Call(typeof(Enumerable), operationName, elementType.AsArray(), source);
        }

        private Expression SelectExtensionMethodCall(Expression source, Type elementType, Expression selectorBody)
        {
            Type[] typeArguments = ArrayFactory.Create(elementType, elementType);
            return Expression.Call(_extensionType, "Select", typeArguments, source, selectorBody);
        }

        private sealed class PropertySelector
        {
            public PropertyInfo Property { get; }
            public ResourceFieldAttribute OriginatingField { get; }
            public QueryLayer NextLayer { get; }

            public PropertySelector(PropertyInfo property, QueryLayer nextLayer = null)
            {
                ArgumentGuard.NotNull(property, nameof(property));

                Property = property;
                NextLayer = nextLayer;
            }

            public PropertySelector(ResourceFieldAttribute field, QueryLayer nextLayer = null)
            {
                ArgumentGuard.NotNull(field, nameof(field));

                OriginatingField = field;
                NextLayer = nextLayer;
                Property = field is HasManyThroughAttribute hasManyThrough ? hasManyThrough.ThroughProperty : field.Property;
            }

            public override string ToString()
            {
                return "Property: " + (NextLayer != null ? Property.Name + "..." : Property.Name);
            }
        }
    }
}
