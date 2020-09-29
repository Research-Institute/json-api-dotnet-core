namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance
{
    public sealed class ContentPerson
    {
        public int ContentId { get; set; }
        
        public Content Content { get; set; }

        public int PersonId { get; set; }
        
        public Person Person { get; set; }
    }
}
