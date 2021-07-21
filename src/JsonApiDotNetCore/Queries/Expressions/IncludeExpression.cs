using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace JsonApiDotNetCore.Queries.Expressions
{
    /// <summary>
    /// Represents an inclusion tree, resulting from text such as: owner,articles.revisions
    /// </summary>
    [PublicAPI]
    public class IncludeExpression : QueryExpression
    {
        private static readonly IncludeChainConverter IncludeChainConverter = new();

        public static readonly IncludeExpression Empty = new();

        public IReadOnlyCollection<IncludeElementExpression> Elements { get; }

        public IncludeExpression(IReadOnlyCollection<IncludeElementExpression> elements)
        {
            ArgumentGuard.NotNullNorEmpty(elements, nameof(elements));

            Elements = elements;
        }

        private IncludeExpression()
        {
            Elements = Array.Empty<IncludeElementExpression>();
        }

        public override TResult Accept<TArgument, TResult>(QueryExpressionVisitor<TArgument, TResult> visitor, TArgument argument)
        {
            return visitor.VisitInclude(this, argument);
        }

        public override string ToString()
        {
            IReadOnlyCollection<ResourceFieldChainExpression> chains = IncludeChainConverter.GetRelationshipChains(this);
            return string.Join(",", chains.Select(child => child.ToString()));
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

            var other = (IncludeExpression)obj;

            return Elements.SequenceEqual(other.Elements);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();

            foreach (IncludeElementExpression element in Elements)
            {
                hashCode.Add(element);
            }

            return hashCode.ToHashCode();
        }
    }
}
