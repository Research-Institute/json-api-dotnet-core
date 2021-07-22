using System;
using System.Linq;
using JetBrains.Annotations;
using JsonApiDotNetCore;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Primitives;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.AtomicOperations.QueryStrings
{
    [UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
    public sealed class MusicTrackReleaseDefinition : JsonApiResourceDefinition<MusicTrack, Guid>
    {
        private readonly ISystemClock _systemClock;

        public MusicTrackReleaseDefinition(IResourceGraph resourceGraph, ISystemClock systemClock)
            : base(resourceGraph)
        {
            ArgumentGuard.NotNull(systemClock, nameof(systemClock));

            _systemClock = systemClock;
        }

        public override QueryStringParameterHandlers<MusicTrack> OnRegisterQueryableHandlersForQueryStringParameters()
        {
            return new()
            {
                ["isRecentlyReleased"] = FilterOnRecentlyReleased
            };
        }

        private IQueryable<MusicTrack> FilterOnRecentlyReleased(IQueryable<MusicTrack> source, StringValues parameterValue)
        {
            IQueryable<MusicTrack> tracks = source;

            if (bool.Parse(parameterValue))
            {
                tracks = tracks.Where(musicTrack => musicTrack.ReleasedAt < _systemClock.UtcNow && musicTrack.ReleasedAt > _systemClock.UtcNow.AddMonths(-3));
            }

            return tracks;
        }
    }
}
