using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance.Models
{
    public sealed class Book : ContentItem
    {
        [Attr]
        public int PageCount { get; set; }
    }
}
