using System;
using Bogus;
using TestBuildingBlocks;

// @formatter:wrap_chained_method_calls chop_always
// @formatter:keep_existing_linebreaks true

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.QueryStrings
{
    internal sealed class QueryStringFakers : FakerContainer
    {
        private readonly Lazy<Faker<Blog>> _lazyBlogFaker = new(() =>
            new Faker<Blog>()
                .UseSeed(GetFakerSeed())
                .RuleFor(blog => blog.Title, faker => faker.Lorem.Word())
                .RuleFor(blog => blog.PlatformName, faker => faker.Company.CompanyName()));

        private readonly Lazy<Faker<BlogPost>> _lazyBlogPostFaker = new(() =>
            new Faker<BlogPost>()
                .UseSeed(GetFakerSeed())
                .RuleFor(blogPost => blogPost.Caption, faker => faker.Lorem.Sentence())
                .RuleFor(blogPost => blogPost.Url, faker => faker.Internet.Url()));

        private readonly Lazy<Faker<Label>> _lazyLabelFaker = new(() =>
            new Faker<Label>()
                .UseSeed(GetFakerSeed())
                .RuleFor(label => label.Name, faker => faker.Lorem.Word())
                .RuleFor(label => label.Color, faker => faker.PickRandom<LabelColor>()));

        private readonly Lazy<Faker<Comment>> _lazyCommentFaker = new(() =>
            new Faker<Comment>()
                .UseSeed(GetFakerSeed())
                .RuleFor(comment => comment.Text, faker => faker.Lorem.Paragraph())
                .RuleFor(comment => comment.CreatedAt, faker => faker.Date.Past()));

        private readonly Lazy<Faker<WebAccount>> _lazyWebAccountFaker = new(() =>
            new Faker<WebAccount>()
                .UseSeed(GetFakerSeed())
                .RuleFor(webAccount => webAccount.UserName, faker => faker.Person.UserName)
                .RuleFor(webAccount => webAccount.Password, faker => faker.Internet.Password())
                .RuleFor(webAccount => webAccount.DisplayName, faker => faker.Person.FullName)
                .RuleFor(webAccount => webAccount.DateOfBirth, faker => faker.Person.DateOfBirth)
                .RuleFor(webAccount => webAccount.EmailAddress, faker => faker.Internet.Email()));

        private readonly Lazy<Faker<AccountPreferences>> _lazyAccountPreferencesFaker = new(() =>
            new Faker<AccountPreferences>()
                .UseSeed(GetFakerSeed())
                .RuleFor(accountPreferences => accountPreferences.UseDarkTheme, faker => faker.Random.Bool()));

        private readonly Lazy<Faker<Calendar>> _lazyCalendarFaker = new(() =>
            new Faker<Calendar>()
                .UseSeed(GetFakerSeed())
                .RuleFor(calendar => calendar.TimeZone, faker => faker.Date.TimeZoneString())
                .RuleFor(calendar => calendar.DefaultAppointmentDurationInMinutes, faker => faker.PickRandom(15, 30, 45, 60)));

        private readonly Lazy<Faker<Appointment>> _lazyAppointmentFaker = new(() =>
            new Faker<Appointment>()
                .UseSeed(GetFakerSeed())
                .RuleFor(appointment => appointment.Title, faker => faker.Random.Word())
                .RuleFor(appointment => appointment.StartTime, faker => faker.Date.FutureOffset())
                .RuleFor(appointment => appointment.EndTime, (faker, appointment) => appointment.StartTime.AddHours(faker.Random.Double(1, 4))));

        public Faker<Blog> Blog => _lazyBlogFaker.Value;
        public Faker<BlogPost> BlogPost => _lazyBlogPostFaker.Value;
        public Faker<Label> Label => _lazyLabelFaker.Value;
        public Faker<Comment> Comment => _lazyCommentFaker.Value;
        public Faker<WebAccount> WebAccount => _lazyWebAccountFaker.Value;
        public Faker<AccountPreferences> AccountPreferences => _lazyAccountPreferencesFaker.Value;
        public Faker<Calendar> Calendar => _lazyCalendarFaker.Value;
        public Faker<Appointment> Appointment => _lazyAppointmentFaker.Value;
    }
}
