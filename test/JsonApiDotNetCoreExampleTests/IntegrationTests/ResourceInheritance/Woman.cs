using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance.Models
{
    public sealed class Woman : Human
    {
        [Attr]
        public bool IsPregnant { get; set; }
    }
}
