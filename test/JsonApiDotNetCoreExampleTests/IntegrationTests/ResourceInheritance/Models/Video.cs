using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance
{
    public class Video : Content
    {
        [Attr]
        public int Duration { get; set; }
    }
}
