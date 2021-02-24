using JetBrains.Annotations;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance.Models
{
    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    public sealed class HumanFavoriteContentItem
    {
        public int ContentItemId { get; set; }
        public ContentItem ContentItem { get; set; }

        public int HumanId { get; set; }
        public Human Human { get; set; }
    }
}
