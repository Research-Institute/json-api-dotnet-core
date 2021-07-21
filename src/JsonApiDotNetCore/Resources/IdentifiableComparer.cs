using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace JsonApiDotNetCore.Resources
{
    /// <summary>
    /// Compares `IIdentifiable` instances with each other based on their type and <see cref="IIdentifiable.StringId" />, falling back to
    /// <see cref="IIdentifiable.LocalId" /> when both StringIds are null.
    /// </summary>
    [PublicAPI]
    public sealed class IdentifiableComparer : IEqualityComparer<IIdentifiable>
    {
        public static readonly IdentifiableComparer Instance = new();

        private IdentifiableComparer()
        {
        }

        public bool Equals(IIdentifiable x, IIdentifiable y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null || x.GetType() != y.GetType())
            {
                return false;
            }

            if (x.StringId == null && y.StringId == null)
            {
                return x.LocalId == y.LocalId;
            }

            return x.StringId == y.StringId;
        }

        public int GetHashCode(IIdentifiable obj)
        {
            // LocalId is intentionally omitted here, it is okay for hashes to collide.
            return HashCode.Combine(obj.GetType(), obj.StringId);
        }
    }
}
