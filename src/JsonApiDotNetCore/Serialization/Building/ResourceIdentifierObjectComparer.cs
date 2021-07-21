using System;
using System.Collections.Generic;
using JsonApiDotNetCore.Serialization.Objects;

namespace JsonApiDotNetCore.Serialization.Building
{
    internal sealed class ResourceIdentifierObjectComparer : IEqualityComparer<ResourceIdentifierObject>
    {
        public static readonly ResourceIdentifierObjectComparer Instance = new();

        private ResourceIdentifierObjectComparer()
        {
        }

        public bool Equals(ResourceIdentifierObject x, ResourceIdentifierObject y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }

            if (x is null || y is null || x.GetType() != y.GetType())
            {
                return false;
            }

            return x.Type == y.Type && x.Id == y.Id && x.Lid == y.Lid;
        }

        public int GetHashCode(ResourceIdentifierObject obj)
        {
            return HashCode.Combine(obj.Type, obj.Id, obj.Lid);
        }
    }
}
