using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance.Models
{
    public sealed class FamilyHealthInsurance : HealthInsurance
    {
        [Attr]
        public int PermittedFamilySize  { get; set; }
    }
}
