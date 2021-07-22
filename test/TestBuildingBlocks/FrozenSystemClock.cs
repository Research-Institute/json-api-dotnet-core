using System;
using Microsoft.AspNetCore.Authentication;

namespace TestBuildingBlocks
{
    public sealed class FrozenSystemClock : ISystemClock
    {
        private static readonly DateTimeOffset DefaultTime = new(new DateTime(2000, 1, 1, 1, 1, 1), TimeSpan.FromHours(1));

        public DateTimeOffset UtcNow { get; set; } = DefaultTime;
    }
}
