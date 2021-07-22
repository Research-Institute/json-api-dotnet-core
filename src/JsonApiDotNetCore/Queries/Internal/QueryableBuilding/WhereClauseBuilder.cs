using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using JetBrains.Annotations;
using JsonApiDotNetCore.Errors;
using JsonApiDotNetCore.Queries.Expressions;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Resources.Internal;

namespace JsonApiDotNetCore.Queries.Internal.QueryableBuilding
{
    /// <summary>
    /// Transforms <see cref="FilterExpression" /> into
    /// <see cref="Queryable.Where{TSource}(IQueryable{TSource}, System.Linq.Expressions.Expression{System.Func{TSource,bool}})" /> calls.
    /// </summary>
    [PublicAPI]
    public class WhereClauseBuilder : QueryClauseBuilder<Type>
    {
        private static readonly CollectionConverter CollectionConverter = new();
        private static readonly ConstantExpression NullConstant = Expression.Constant(null);

        private readonly Expression _source;
        private readonly Type _extensionType;
        private readonly LambdaParameterNameFactory _nameFactory;

        public WhereClauseBuilder(Expression source, LambdaScope lambdaScope, Type extensionType, LambdaParameterNameFactory nameFactory)
            : base(lambdaScope)
        {
            ArgumentGuard.NotNull(source, nameof(source));
            ArgumentGuard.NotNull(extensionType, nameof(extensionType));
            ArgumentGuard.NotNull(nameFactory, nameof(nameFactory));

            _source = source;
            _extensionType = extensionType;
            _nameFactory = nameFactory;
        }

        public Expression ApplyWhere(FilterExpression filter)
        {
            ArgumentGuard.NotNull(filter, nameof(filter));

            LambdaExpression lambda = GetPredicateLambda(filter);

            return WhereExtensionMethodCall(lambda);
        }

        private LambdaExpression GetPredicateLambda(FilterExpression filter)
        {
            Expression body = Visit(filter, null);
            return Expression.Lambda(body, LambdaScope.Parameter);
        }

        private Expression WhereExtensionMethodCall(LambdaExpression predicate)
        {
            return Expression.Call(_extensionType, "Where", LambdaScope.Parameter.Type.AsArray(), _source, predicate);
        }

        public override Expression VisitCollectionNotEmpty(CollectionNotEmptyExpression expression, Type argument)
        {
            Expression property = Visit(expression.TargetCollection, argument);

            Type elementType = CollectionConverter.TryGetCollectionElementType(property.Type);

            if (elementType == null)
            {
                throw new InvalidOperationException("Expression must be a collection.");
            }

            Expression predicate = null;

            if (expression.Filter != null)
            {
                var hasManyThrough = expression.TargetCollection.Fields.Last() as HasManyThroughAttribute;
                var lambdaScopeFactory = new LambdaScopeFactory(_nameFactory, hasManyThrough);
                using LambdaScope lambdaScope = lambdaScopeFactory.CreateScope(elementType);

                var builder = new WhereClauseBuilder(property, lambdaScope, typeof(Enumerable), _nameFactory);
                predicate = builder.GetPredicateLambda(expression.Filter);
            }

            return AnyExtensionMethodCall(elementType, property, predicate);
        }

        private static MethodCallExpression AnyExtensionMethodCall(Type elementType, Expression source, Expression predicate)
        {
            if (predicate != null)
            {
                return Expression.Call(typeof(Enumerable), "Any", elementType.AsArray(), source, predicate);
            }

            return Expression.Call(typeof(Enumerable), "Any", elementType.AsArray(), source);
        }

        public override Expression VisitMatchText(MatchTextExpression expression, Type argument)
        {
            Expression property = Visit(expression.TargetAttribute, argument);

            if (property.Type != typeof(string))
            {
                throw new InvalidOperationException("Expression must be a string.");
            }

            Expression text = Visit(expression.TextValue, property.Type);

            if (expression.MatchKind == TextMatchKind.StartsWith)
            {
                return Expression.Call(property, "StartsWith", null, text);
            }

            if (expression.MatchKind == TextMatchKind.EndsWith)
            {
                return Expression.Call(property, "EndsWith", null, text);
            }

            return Expression.Call(property, "Contains", null, text);
        }

        public override Expression VisitEqualsAnyOf(EqualsAnyOfExpression expression, Type argument)
        {
            Expression property = Visit(expression.TargetAttribute, argument);

            var valueList = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(property.Type));

            foreach (LiteralConstantExpression constant in expression.Constants)
            {
                object value = ConvertTextToTargetType(constant.Value, property.Type);
                valueList!.Add(value);
            }

            ConstantExpression collection = Expression.Constant(valueList);
            return ContainsExtensionMethodCall(collection, property);
        }

        private static Expression ContainsExtensionMethodCall(Expression collection, Expression value)
        {
            return Expression.Call(typeof(Enumerable), "Contains", value.Type.AsArray(), collection, value);
        }

        public override Expression VisitLogical(LogicalExpression expression, Type argument)
        {
            var termQueue = new Queue<Expression>(expression.Terms.Select(filter => Visit(filter, argument)));

            if (expression.Operator == LogicalOperator.And)
            {
                return Compose(termQueue, Expression.AndAlso);
            }

            if (expression.Operator == LogicalOperator.Or)
            {
                return Compose(termQueue, Expression.OrElse);
            }

            throw new InvalidOperationException($"Unknown logical operator '{expression.Operator}'.");
        }

        private static BinaryExpression Compose(Queue<Expression> argumentQueue, Func<Expression, Expression, BinaryExpression> applyOperator)
        {
            Expression left = argumentQueue.Dequeue();
            Expression right = argumentQueue.Dequeue();

            BinaryExpression tempExpression = applyOperator(left, right);

            while (argumentQueue.Any())
            {
                Expression nextArgument = argumentQueue.Dequeue();
                tempExpression = applyOperator(tempExpression, nextArgument);
            }

            return tempExpression;
        }

        public override Expression VisitNot(NotExpression expression, Type argument)
        {
            Expression child = Visit(expression.Child, argument);
            return Expression.Not(child);
        }

        public override Expression VisitComparison(ComparisonExpression expression, Type argument)
        {
            Type commonType = TryResolveCommonType(expression.Left, expression.Right);

            Expression left = WrapInConvert(Visit(expression.Left, commonType), commonType);
            Expression right = WrapInConvert(Visit(expression.Right, commonType), commonType);

            switch (expression.Operator)
            {
                case ComparisonOperator.Equals:
                {
                    return Expression.Equal(left, right);
                }
                case ComparisonOperator.LessThan:
                {
                    return Expression.LessThan(left, right);
                }
                case ComparisonOperator.LessOrEqual:
                {
                    return Expression.LessThanOrEqual(left, right);
                }
                case ComparisonOperator.GreaterThan:
                {
                    return Expression.GreaterThan(left, right);
                }
                case ComparisonOperator.GreaterOrEqual:
                {
                    return Expression.GreaterThanOrEqual(left, right);
                }
            }

            throw new InvalidOperationException($"Unknown comparison operator '{expression.Operator}'.");
        }

        private Type TryResolveCommonType(QueryExpression left, QueryExpression right)
        {
            Type leftType = ResolveFixedType(left);

            if (RuntimeTypeConverter.CanContainNull(leftType))
            {
                return leftType;
            }

            if (right is NullConstantExpression)
            {
                return typeof(Nullable<>).MakeGenericType(leftType);
            }

            Type rightType = TryResolveFixedType(right);

            if (rightType != null && RuntimeTypeConverter.CanContainNull(rightType))
            {
                return rightType;
            }

            return leftType;
        }

        private Type ResolveFixedType(QueryExpression expression)
        {
            Expression result = Visit(expression, null);
            return result.Type;
        }

        private Type TryResolveFixedType(QueryExpression expression)
        {
            if (expression is CountExpression)
            {
                return typeof(int);
            }

            if (expression is ResourceFieldChainExpression chain)
            {
                Expression child = Visit(chain, null);
                return child.Type;
            }

            return null;
        }

        private static Expression WrapInConvert(Expression expression, Type targetType)
        {
            try
            {
                return targetType != null && expression.Type != targetType ? Expression.Convert(expression, targetType) : expression;
            }
            catch (InvalidOperationException exception)
            {
                throw new InvalidQueryException("Query creation failed due to incompatible types.", exception);
            }
        }

        public override Expression VisitNullConstant(NullConstantExpression expression, Type expressionType)
        {
            return NullConstant;
        }

        public override Expression VisitLiteralConstant(LiteralConstantExpression expression, Type expressionType)
        {
            object convertedValue = expressionType != null ? ConvertTextToTargetType(expression.Value, expressionType) : expression.Value;

            return CreateTupleAccessExpressionForConstant(convertedValue, expressionType ?? typeof(string));
        }

        private static object ConvertTextToTargetType(string text, Type targetType)
        {
            try
            {
                return RuntimeTypeConverter.ConvertType(text, targetType);
            }
            catch (FormatException exception)
            {
                throw new InvalidQueryException("Query creation failed due to incompatible types.", exception);
            }
        }

        protected override MemberExpression CreatePropertyExpressionForFieldChain(IReadOnlyCollection<ResourceFieldAttribute> chain, Expression source)
        {
            string[] components = chain.Select(GetPropertyName).ToArray();
            return CreatePropertyExpressionFromComponents(LambdaScope.Accessor, components);
        }

        private static string GetPropertyName(ResourceFieldAttribute field)
        {
            return field is HasManyThroughAttribute hasManyThrough ? hasManyThrough.ThroughProperty.Name : field.Property.Name;
        }
    }
}
