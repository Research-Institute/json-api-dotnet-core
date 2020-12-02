using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance.Models
{
    public abstract class ContentItem : Identifiable
    {
        [Attr]
        public string Title { get; set; }
    }
}
