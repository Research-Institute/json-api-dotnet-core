using Bogus;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance;
using Microsoft.Extensions.Logging.Abstractions;
using UnitTests.TestModels;
using InheritancePerson = JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance.Person;
using Person = UnitTests.TestModels.Person;

namespace UnitTests.Serialization
{
    public class SerializationTestsSetupBase
    {
        protected IResourceGraph _resourceGraph;
        protected readonly Faker<Food> _foodFaker;
        protected readonly Faker<Song> _songFaker;
        protected readonly Faker<Article> _articleFaker;
        protected readonly Faker<Blog> _blogFaker;
        protected readonly Faker<Person> _personFaker;

        public SerializationTestsSetupBase()
        {
            _resourceGraph = BuildGraph();
            _articleFaker = new Faker<Article>()
                    .RuleFor(f => f.Title, f => f.Hacker.Phrase())
                    .RuleFor(f => f.Id, f => f.UniqueIndex + 1);
            _personFaker = new Faker<Person>()
                    .RuleFor(f => f.Name, f => f.Person.FullName)
                    .RuleFor(f => f.Id, f => f.UniqueIndex + 1);
            _blogFaker = new Faker<Blog>()
                    .RuleFor(f => f.Title, f => f.Hacker.Phrase())
                    .RuleFor(f => f.Id, f => f.UniqueIndex + 1);
            _songFaker = new Faker<Song>()
                    .RuleFor(f => f.Title, f => f.Lorem.Sentence())
                    .RuleFor(f => f.Id, f => f.UniqueIndex + 1);
            _foodFaker = new Faker<Food>()
                    .RuleFor(f => f.Dish, f => f.Lorem.Sentence())
                    .RuleFor(f => f.Id, f => f.UniqueIndex + 1);
        }

        protected IResourceGraph BuildGraph()
        {
            var resourceGraphBuilder = new ResourceGraphBuilder(new JsonApiOptions(), NullLoggerFactory.Instance);
            resourceGraphBuilder.Add<TestResource>("testResource");
            resourceGraphBuilder.Add<TestResourceWithList>("testResource-with-list");
            // one to one relationships
            resourceGraphBuilder.Add<OneToOnePrincipal>("oneToOnePrincipals");
            resourceGraphBuilder.Add<OneToOneDependent>("oneToOneDependents");
            resourceGraphBuilder.Add<OneToOneRequiredDependent>("oneToOneRequiredDependents");
            // one to many relationships
            resourceGraphBuilder.Add<OneToManyPrincipal>("oneToManyPrincipals");
            resourceGraphBuilder.Add<OneToManyDependent>("oneToManyDependents");
            resourceGraphBuilder.Add<OneToManyRequiredDependent>("oneToMany-requiredDependents");
            // collective relationships
            resourceGraphBuilder.Add<MultipleRelationshipsPrincipalPart>("multiPrincipals");
            resourceGraphBuilder.Add<MultipleRelationshipsDependentPart>("multiDependents");

            resourceGraphBuilder.Add<Article>();
            resourceGraphBuilder.Add<Person>();
            resourceGraphBuilder.Add<Blog>();
            resourceGraphBuilder.Add<Food>();
            resourceGraphBuilder.Add<Song>();
            
            resourceGraphBuilder.Add<Animal>();
            resourceGraphBuilder.Add<Cat>();
            resourceGraphBuilder.Add<Dog>();
            
            resourceGraphBuilder.Add<InheritancePerson>();
            resourceGraphBuilder.Add<Male>();
            resourceGraphBuilder.Add<Female>();

            return resourceGraphBuilder.Build();
        }
   }
}
