using System;
using Bogus;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ConcurrencyTokens
{
    internal sealed class ConcurrencyFakers : FakerContainer
    {
        private const ulong _oneGigabyte = 1024 * 1024 * 1024;
        private static readonly string[] _fileSystems = {"NTFS", "FAT32", "ext4", "XFS", "btrfs"};

        private readonly Lazy<Faker<Disk>> _lazyDiskFaker = new Lazy<Faker<Disk>>(() =>
            new Faker<Disk>()
                .UseSeed(GetFakerSeed())
                .RuleFor(disk => disk.Manufacturer, f => f.Company.CompanyName())
                .RuleFor(disk => disk.SerialCode, f => f.System.ApplePushToken()));

        private readonly Lazy<Faker<Partition>> _lazyPartitionFaker = new Lazy<Faker<Partition>>(() =>
            new Faker<Partition>()
                .UseSeed(GetFakerSeed())
                .RuleFor(partition => partition.MountPoint, f => f.System.DirectoryPath())
                .RuleFor(partition => partition.FileSystem, f => f.PickRandom(_fileSystems))
                .RuleFor(partition => partition.CapacityInBytes, f => f.Random.ULong(_oneGigabyte * 50, _oneGigabyte * 100))
                .RuleFor(partition => partition.FreeSpaceInBytes, f => f.Random.ULong(_oneGigabyte * 10, _oneGigabyte * 40)));

        public Faker<Disk> Disk => _lazyDiskFaker.Value;
        public Faker<Partition> Partition => _lazyPartitionFaker.Value;
    }
}
