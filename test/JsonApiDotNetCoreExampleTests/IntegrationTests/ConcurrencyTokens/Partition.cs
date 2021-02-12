using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ConcurrencyTokens
{
    public sealed class Partition : Identifiable
    {
        [Attr]
        public string MountPoint { get; set; }

        [Attr]
        public string FileSystem { get; set; }

        [Attr]
        public ulong CapacityInBytes { get; set; }

        [Attr]
        public ulong FreeSpaceInBytes { get; set; }

        [Attr(PublicName = "concurrencyToken")]
        public uint xmin { get; set; }

        [HasOne]
        public Disk Owner { get; set; }
    }
}
