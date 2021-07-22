using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Bogus;
using TestBuildingBlocks;

// @formatter:wrap_chained_method_calls chop_always
// @formatter:keep_existing_linebreaks true

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.AtomicOperations
{
    internal sealed class OperationsFakers : FakerContainer
    {
        private static readonly Lazy<IReadOnlyList<string>> LazyLanguageIsoCodes =
            new(() => CultureInfo
                .GetCultures(CultureTypes.NeutralCultures)
                .Where(culture => !string.IsNullOrEmpty(culture.Name))
                .Select(culture => culture.Name)
                .ToArray());

        private readonly Lazy<Faker<Playlist>> _lazyPlaylistFaker = new(() =>
            new Faker<Playlist>()
                .UseSeed(GetFakerSeed())
                .RuleFor(playlist => playlist.Name, faker => faker.Lorem.Sentence()));

        private readonly Lazy<Faker<MusicTrack>> _lazyMusicTrackFaker = new(() =>
            new Faker<MusicTrack>()
                .UseSeed(GetFakerSeed())
                .RuleFor(musicTrack => musicTrack.Title, faker => faker.Lorem.Word())
                .RuleFor(musicTrack => musicTrack.LengthInSeconds, faker => faker.Random.Decimal(3 * 60, 5 * 60))
                .RuleFor(musicTrack => musicTrack.Genre, faker => faker.Lorem.Word())
                .RuleFor(musicTrack => musicTrack.ReleasedAt, faker => faker.Date.PastOffset()));

        private readonly Lazy<Faker<Lyric>> _lazyLyricFaker = new(() =>
            new Faker<Lyric>()
                .UseSeed(GetFakerSeed())
                .RuleFor(lyric => lyric.Text, faker => faker.Lorem.Text())
                .RuleFor(lyric => lyric.Format, "LRC"));

        private readonly Lazy<Faker<TextLanguage>> _lazyTextLanguageFaker = new(() =>
            new Faker<TextLanguage>()
                .UseSeed(GetFakerSeed())
                .RuleFor(textLanguage => textLanguage.IsoCode, faker => faker.PickRandom<string>(LazyLanguageIsoCodes.Value)));

        private readonly Lazy<Faker<Performer>> _lazyPerformerFaker = new(() =>
            new Faker<Performer>()
                .UseSeed(GetFakerSeed())
                .RuleFor(performer => performer.ArtistName, faker => faker.Name.FullName())
                .RuleFor(performer => performer.BornAt, faker => faker.Date.PastOffset()));

        private readonly Lazy<Faker<RecordCompany>> _lazyRecordCompanyFaker = new(() =>
            new Faker<RecordCompany>()
                .UseSeed(GetFakerSeed())
                .RuleFor(recordCompany => recordCompany.Name, faker => faker.Company.CompanyName())
                .RuleFor(recordCompany => recordCompany.CountryOfResidence, faker => faker.Address.Country()));

        public Faker<Playlist> Playlist => _lazyPlaylistFaker.Value;
        public Faker<MusicTrack> MusicTrack => _lazyMusicTrackFaker.Value;
        public Faker<Lyric> Lyric => _lazyLyricFaker.Value;
        public Faker<TextLanguage> TextLanguage => _lazyTextLanguageFaker.Value;
        public Faker<Performer> Performer => _lazyPerformerFaker.Value;
        public Faker<RecordCompany> RecordCompany => _lazyRecordCompanyFaker.Value;
    }
}
