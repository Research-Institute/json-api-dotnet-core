using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance.Models
{
    public sealed class Video : ContentItem
    {
        [Attr]
        public int Duration { get; set; }
    }
}
