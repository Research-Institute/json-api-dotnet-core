using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JsonApiDotNetCore.Resources
{
    /// <inheritdoc />
    internal sealed class ResourceFactory : IResourceFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public ResourceFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <inheritdoc />
        public object CreateInstance(Type resourceType)
        {
            if (resourceType == null)
            {
                throw new ArgumentNullException(nameof(resourceType));
            }

            return InnerCreateInstance(resourceType, _serviceProvider);
        }

        /// <inheritdoc />
        public TResource CreateInstance<TResource>()
        {
            return (TResource) InnerCreateInstance(typeof(TResource), _serviceProvider);
        }

        private static object InnerCreateInstance(Type type, IServiceProvider serviceProvider)
        {
            bool hasSingleConstructorWithoutParameters = HasSingleConstructorWithoutParameters(type);

            try
            {
                return hasSingleConstructorWithoutParameters
                    ? Activator.CreateInstance(type)
                    : ActivatorUtilities.CreateInstance(serviceProvider, type);
            }
            catch (Exception exception)
            {
                throw new InvalidOperationException(hasSingleConstructorWithoutParameters
                        ? $"Failed to create an instance of '{type.FullName}' using its default constructor."
                        : $"Failed to create an instance of '{type.FullName}' using injected constructor parameters.",
                    exception);
            }
        }

        /// <inheritdoc />
        public NewExpression CreateNewExpression(Type resourceType)
        {
            if (resourceType == null) throw new ArgumentNullException(nameof(resourceType));

            if (HasSingleConstructorWithoutParameters(resourceType))
            {
                return Expression.New(resourceType);
            }

            List<Expression> constructorArguments = new List<Expression>();

            var longestConstructor = GetLongestConstructor(resourceType);
            foreach (ParameterInfo constructorParameter in longestConstructor.GetParameters())
            {
                try
                {
                    object constructorArgument =
                        ActivatorUtilities.GetServiceOrCreateInstance(_serviceProvider, constructorParameter.ParameterType);

                    var argumentExpression = typeof(DbContext).Assembly.GetName().Version.Major >= 5
                        // Workaround for https://github.com/dotnet/efcore/issues/20502 to not fail on injected DbContext in EF Core 5.
                        ? CreateTupleAccessExpressionForConstant(constructorArgument, constructorArgument.GetType())
                        : Expression.Constant(constructorArgument);

                    constructorArguments.Add(argumentExpression);
                }
                catch (Exception exception)
                {
                    throw new InvalidOperationException(
                        $"Failed to create an instance of '{resourceType.FullName}': Parameter '{constructorParameter.Name}' could not be resolved.",
                        exception);
                }
            }

            return Expression.New(longestConstructor, constructorArguments);
        }

        private static Expression CreateTupleAccessExpressionForConstant(object value, Type type)
        {
            MethodInfo tupleCreateMethod = typeof(Tuple).GetMethods()
                .Single(m => m.Name == "Create" && m.IsGenericMethod && m.GetGenericArguments().Length == 1);

            MethodInfo constructedTupleCreateMethod = tupleCreateMethod.MakeGenericMethod(type);

            ConstantExpression constantExpression = Expression.Constant(value, type);

            MethodCallExpression tupleCreateCall = Expression.Call(constructedTupleCreateMethod, constantExpression);
            return Expression.Property(tupleCreateCall, "Item1");
        }

        private static bool HasSingleConstructorWithoutParameters(Type type)
        {
            ConstructorInfo[] constructors = type.GetConstructors().Where(c => !c.IsStatic).ToArray();

            return constructors.Length == 1 && constructors[0].GetParameters().Length == 0;
        }

        private static ConstructorInfo GetLongestConstructor(Type type)
        {
            ConstructorInfo[] constructors = type.GetConstructors().Where(c => !c.IsStatic).ToArray();

            if (constructors.Length == 0)
            {
                throw new InvalidOperationException($"No public constructor was found for '{type.FullName}'.");
            }
            
            ConstructorInfo bestMatch = constructors[0];
            int maxParameterLength = constructors[0].GetParameters().Length;

            for (int index = 1; index < constructors.Length; index++)
            {
                var constructor = constructors[index];
                int length = constructor.GetParameters().Length;
                if (length > maxParameterLength)
                {
                    bestMatch = constructor;
                    maxParameterLength = length;
                }
            }

            return bestMatch;
        }
    }
}
