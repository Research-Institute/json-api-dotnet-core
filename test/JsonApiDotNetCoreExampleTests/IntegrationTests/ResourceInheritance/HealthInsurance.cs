using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance.Models
{
    public abstract class HealthInsurance : Identifiable
    {
        [Attr]
        public bool MonthlyFee { get; set; }
    }
}
