using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance
{
    public sealed class Book : Content
    {
        [Attr]
        public int PageCount { get; set; }
    }
}
