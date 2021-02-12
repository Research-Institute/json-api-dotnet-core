using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ConcurrencyTokens
{
    public sealed class Disk : Identifiable
    {
        [Attr]
        public string Manufacturer { get; set; }

        [Attr]
        public string SerialCode { get; set; }

        [ConcurrencyCheck]
        [Timestamp]
        [Attr(PublicName = "concurrencyToken")]
        public uint xmin { get; set; }

        [HasMany]
        public IList<Partition > Partitions { get; set; }
    }
}
