using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance.Models
{
    public sealed class Man : Human
    {
        [Attr]
        public bool HasBeard { get; set; }
    }
}
