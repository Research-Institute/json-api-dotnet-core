namespace JsonApiDotNetCoreExampleTests.IntegrationTests.Writing
{
    public sealed class WorkItemTag
    {
        public WorkItem Item { get; set; }
        public int ItemId { get; set; }

        public WorkTag Tag { get; set; }
        public int TagId { get; set; }
    }
}
