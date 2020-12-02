using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance.Models
{
    public sealed class CompanyHealthInsurance : HealthInsurance
    {
        [Attr]
        public string CompanyCode { get; set; }
    }
}
