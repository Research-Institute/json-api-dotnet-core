using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using JetBrains.Annotations;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.Serialization
{
    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    public sealed class Meeting : Identifiable<Guid>
    {
        [Attr]
        public string Title { get; set; }

        [Attr]
        public DateTimeOffset StartTime { get; set; }

        [Attr]
        public TimeSpan Duration { get; set; }

        [Attr]
        [NotMapped]
        public MeetingLocation Location
        {
            get =>
                new()
                {
                    Latitude = Latitude,
                    Longitude = Longitude
                };
            set
            {
                Latitude = value?.Latitude ?? double.NaN;
                Longitude = value?.Longitude ?? double.NaN;
            }
        }

        public double Latitude { get; set; }
        public double Longitude { get; set; }

        [HasMany]
        public IList<MeetingAttendee> Attendees { get; set; }
    }
}
