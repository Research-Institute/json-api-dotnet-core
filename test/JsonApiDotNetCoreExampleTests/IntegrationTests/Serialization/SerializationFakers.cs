using System;
using Bogus;
using TestBuildingBlocks;

// @formatter:wrap_chained_method_calls chop_always
// @formatter:keep_existing_linebreaks true

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.Serialization
{
    internal sealed class SerializationFakers : FakerContainer
    {
        private static readonly TimeSpan[] MeetingDurations =
        {
            TimeSpan.FromMinutes(15),
            TimeSpan.FromMinutes(30),
            TimeSpan.FromMinutes(45),
            TimeSpan.FromMinutes(60)
        };

        private readonly Lazy<Faker<Meeting>> _lazyMeetingFaker = new(() =>
            new Faker<Meeting>()
                .UseSeed(GetFakerSeed())
                .RuleFor(meeting => meeting.Title, faker => faker.Lorem.Word())
                .RuleFor(meeting => meeting.StartTime, faker => TruncateToWholeMilliseconds(faker.Date.FutureOffset()))
                .RuleFor(meeting => meeting.Duration, faker => faker.PickRandom(MeetingDurations))
                .RuleFor(meeting => meeting.Latitude, faker => faker.Address.Latitude())
                .RuleFor(meeting => meeting.Longitude, faker => faker.Address.Longitude()));

        private readonly Lazy<Faker<MeetingAttendee>> _lazyMeetingAttendeeFaker = new(() =>
            new Faker<MeetingAttendee>()
                .UseSeed(GetFakerSeed())
                .RuleFor(attendee => attendee.DisplayName, faker => faker.Random.Utf16String()));

        public Faker<Meeting> Meeting => _lazyMeetingFaker.Value;
        public Faker<MeetingAttendee> MeetingAttendee => _lazyMeetingAttendeeFaker.Value;

        private static DateTimeOffset TruncateToWholeMilliseconds(DateTimeOffset value)
        {
            long ticksToSubtract = value.DateTime.Ticks % TimeSpan.TicksPerMillisecond;
            long ticksInWholeMilliseconds = value.DateTime.Ticks - ticksToSubtract;

            return new DateTimeOffset(new DateTime(ticksInWholeMilliseconds), value.Offset);
        }
    }
}
