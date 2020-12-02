using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Threading.Tasks;
using Bogus;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Client.Internal;
using JsonApiDotNetCore.Serialization.Objects;
using JsonApiDotNetCoreExample.Data;
using JsonApiDotNetCoreExample.Models;
using JsonApiDotNetCoreExampleTests.Helpers.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Xunit;
using Person = JsonApiDotNetCoreExample.Models.Person;

namespace JsonApiDotNetCoreExampleTests.Acceptance
{
    public sealed class ResourceDefinitionTests : IClassFixture<IntegrationTestContext<ResourceHooksStartup, AppDbContext>>
    {
        private readonly Faker<User> _userFaker;
        private readonly Faker<TodoItem> _todoItemFaker;
        private readonly Faker<Person> _personFaker;
        private readonly Faker<Article> _articleFaker;
        private readonly Faker<Author> _authorFaker;
        private readonly Faker<Tag> _tagFaker;

        private readonly IntegrationTestContext<ResourceHooksStartup, AppDbContext> _testContext;
        private readonly IResponseDeserializer _deserializer;

        public ResourceDefinitionTests(IntegrationTestContext<ResourceHooksStartup, AppDbContext> testContext)
        {
            _testContext = testContext;
            
            _authorFaker = new Faker<Author>()
                .RuleFor(a => a.LastName, f => f.Random.Words(2));

            _articleFaker = new Faker<Article>()
                .RuleFor(a => a.Caption, f => f.Random.AlphaNumeric(10))
                .RuleFor(a => a.Author, f => _authorFaker.Generate());

            var systemClock = testContext.Factory.Services.GetRequiredService<ISystemClock>();
            var options = testContext.Factory.Services.GetRequiredService<DbContextOptions<AppDbContext>>();
            var tempDbContext = new AppDbContext(options, systemClock);
            _userFaker = new Faker<User>()
                .CustomInstantiator(f => new User(tempDbContext))
                .RuleFor(u => u.UserName, f => f.Internet.UserName())
                .RuleFor(u => u.Password, f => f.Internet.Password());

            _todoItemFaker = new Faker<TodoItem>()
                .RuleFor(t => t.Description, f => f.Lorem.Sentence())
                .RuleFor(t => t.Ordinal, f => f.Random.Number())
                .RuleFor(t => t.CreatedDate, f => f.Date.Past());

            _personFaker = new Faker<Person>()
                .RuleFor(p => p.FirstName, f => f.Name.FirstName())
                .RuleFor(p => p.LastName, f => f.Name.LastName());

            _tagFaker = new Faker<Tag>()
                .CustomInstantiator(f => new Tag())
                .RuleFor(a => a.Name, f => f.Random.AlphaNumeric(10));

            _deserializer = GetDeserializer();
            // var options = (JsonApiOptions) _factory.Services.GetRequiredService<IJsonApiOptions>();
            // options.DisableTopPagination = false;
            // options.DisableChildrenPagination = false;
        }

        [Fact]
        public async Task Can_Create_User_With_Password()
        {
            // Arrange
            var user = _userFaker.Generate();
            var serializer = GetSerializer<User>(p => new { p.Password, p.UserName });
            var route = "/users";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<Document>(route, serializer.Serialize(user));

            // Assert
            Assert.Equal(HttpStatusCode.Created, httpResponse.StatusCode);

            var body = await httpResponse.Content.ReadAsStringAsync();
            var returnedUser = _deserializer.DeserializeSingle<User>(body).Data;
            var document = JsonConvert.DeserializeObject<Document>(body);
            Assert.False(document.SingleData.Attributes.ContainsKey("password"));
            Assert.Equal(user.UserName, document.SingleData.Attributes["userName"]);

            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var dbUser = await dbContext.Users.FindAsync(returnedUser.Id);
                Assert.Equal(user.UserName, dbUser.UserName);
                Assert.Equal(user.Password, dbUser.Password);
            });
        }

        [Fact]
        public async Task Can_Update_User_Password()
        {
            // Arrange
            var user = _userFaker.Generate();
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Users.Add(user);
                await dbContext.SaveChangesAsync();
            });
        
            user.Password = _userFaker.Generate().Password;
            var serializer = GetSerializer<User>(p => new { p.Password });
            var route = $"/users/{user.Id}";

            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<Document>(route, serializer.Serialize((user)));
        
            // Assert
            Assert.Equal(HttpStatusCode.OK, httpResponse.StatusCode);
            Assert.False(responseDocument.SingleData.Attributes.ContainsKey("password"));
            Assert.Equal(user.UserName, responseDocument.SingleData.Attributes["userName"]);
        
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                var dbUser = await dbContext.Users.SingleAsync(u => u.Id == user.Id);
                Assert.Equal(user.Password, dbUser.Password);
            });
        }
        
        [Fact]
        public async Task Unauthorized_TodoItem()
        {
            // Arrange
            var route = "/todoItems/1337";
        
            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);
        
            // Assert
            Assert.Single(responseDocument.Errors);
            Assert.Equal(HttpStatusCode.Forbidden, responseDocument.Errors[0].StatusCode);
            Assert.Equal("You are not allowed to update the author of todo items.", responseDocument.Errors[0].Title);
            Assert.Null(responseDocument.Errors[0].Detail);
        }
        
        [Fact]
        public async Task Unauthorized_Passport()
        {
            // Arrange
            var route = "/people/1?include=passport";
        
            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);
        
            // Assert
            Assert.Single(responseDocument.Errors);
            Assert.Equal(HttpStatusCode.Forbidden, responseDocument.Errors[0].StatusCode);
            Assert.Equal("You are not allowed to include passports on individual persons.", responseDocument.Errors[0].Title);
            Assert.Null(responseDocument.Errors[0].Detail);
        }
        
        [Fact]
        public async Task Unauthorized_Article()
        {
            // Arrange
            var article = _articleFaker.Generate();
            article.Caption = "Classified";
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Articles.Add(article);
                await dbContext.SaveChangesAsync();
            });

            var route = $"/articles/{article.Id}";
        
            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteGetAsync<ErrorDocument>(route);
        
            // Assert
            Assert.Single(responseDocument.Errors);
            Assert.Equal(HttpStatusCode.Forbidden, responseDocument.Errors[0].StatusCode);
            Assert.Equal("You are not allowed to see this article.", responseDocument.Errors[0].Title);
            Assert.Null(responseDocument.Errors[0].Detail);
        }
        
        [Fact]
        public async Task Article_Is_Hidden()
        {
            // Arrange
            var articles = _articleFaker.Generate(3);
            string toBeExcluded = "This should not be included";
            articles[0].Caption = toBeExcluded;
        
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.Articles.AddRange(articles);
                await dbContext.SaveChangesAsync();
            });

            var route = "/articles";
        
            // Act
            var (httpResponse, responseBody) = await _testContext.ExecuteGetAsync<string>(route);
            
            Assert.DoesNotContain(toBeExcluded, responseBody);
        }
        
        [Fact]
        public async Task Tag_Is_Hidden()
        {
            // Arrange
            var article = _articleFaker.Generate();
            var tags = _tagFaker.Generate(2);
            string toBeExcluded = "This should not be included";
            tags[0].Name = toBeExcluded;
        
            var articleTags = new[]
            {
                new ArticleTag
                {
                    Article = article,
                    Tag = tags[0]
                },
                new ArticleTag
                {
                    Article = article,
                    Tag = tags[1]
                }
            };
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.ArticleTags.AddRange(articleTags);
                await dbContext.SaveChangesAsync();
            });
        
            // Workaround for https://github.com/dotnet/efcore/issues/21026
            var options = (JsonApiOptions) _testContext.Factory.Services.GetRequiredService<IJsonApiOptions>();
            options.DisableTopPagination = false;
            options.DisableChildrenPagination = true;
        
            var route = "/articles?include=tags";
        
            // Act
            var (httpResponse, responseBody) = await _testContext.ExecuteGetAsync<string>(route);
            
            Assert.DoesNotContain(toBeExcluded, responseBody);
        }
        ///// <summary>
        ///// In the Cascade Permission Error tests, we ensure that  all the relevant 
        ///// resources are provided in the hook definitions. In this case, 
        ///// re-relating the meta object to a different article would require 
        ///// also a check for the lockedTodo, because we're implicitly updating 
        ///// its foreign key.
        ///// </summary>
        [Fact]
        public async Task Cascade_Permission_Error_Create_ToOne_Relationship()
        {
            // Arrange
            var lockedPerson = _personFaker.Generate();
            lockedPerson.IsLocked = true;
            Passport passport;
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                passport = new Passport(dbContext);
                lockedPerson.Passport = passport;
                dbContext.People.AddRange(lockedPerson);
                await dbContext.SaveChangesAsync();
            });
        
            var content = new
            {
                data = new
                {
                    type = "people",
                    relationships = new Dictionary<string, object>
                    {
                        { "passport", new
                            {
                                data = new { type = "passports", id = lockedPerson.Passport.StringId }
                            }
                        }
                    }
                }
            };
        
            var route = "/people";
        
            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, content);
        
            // Assert
            Assert.Single(responseDocument.Errors);
            Assert.Equal(HttpStatusCode.Forbidden, responseDocument.Errors[0].StatusCode);
            Assert.Equal("You are not allowed to update fields or relationships of locked todo items.", responseDocument.Errors[0].Title);
            Assert.Null(responseDocument.Errors[0].Detail);
        }
        
        [Fact]
        public async Task Cascade_Permission_Error_Updating_ToOne_Relationship()
        {
            // Arrange
            var person = _personFaker.Generate();
            Passport passport = null;
            Passport newPassport = null;
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                passport = new Passport(dbContext) { IsLocked = true };
                person.Passport = passport;
                newPassport = new Passport(dbContext);
                dbContext.People.AddRange(person);
                dbContext.Passports.Add(newPassport);
                await dbContext.SaveChangesAsync();
            });
        
            var content = new
            {
                data = new
                {
                    type = "people",
                    id = person.Id,
                    relationships = new Dictionary<string, object>
                    {
                        { "passport", new
                            {
                                data = new { type = "passports", id = newPassport.StringId }
                            }
                        }
                    }
                }
            };
            
            var route = $"/people/{person.Id}";
        
            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, content);
        
            // Assert
            Assert.Single(responseDocument.Errors);
            Assert.Equal(HttpStatusCode.Forbidden, responseDocument.Errors[0].StatusCode);
            Assert.Equal("You are not allowed to update fields or relationships of locked persons.", responseDocument.Errors[0].Title);
            Assert.Null(responseDocument.Errors[0].Detail);
        }
        
        [Fact]
        public async Task Cascade_Permission_Error_Updating_ToOne_Relationship_Deletion()
        {
            // Arrange
            var person = _personFaker.Generate();
            Passport passport = null;
            Passport newPassport = null;
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                passport = new Passport(dbContext) { IsLocked = true };
                person.Passport = passport;
                newPassport = new Passport(dbContext);
                dbContext.People.AddRange(person);
                dbContext.Passports.Add(newPassport);
                await dbContext.SaveChangesAsync();
            });

            var content = new
            {
                data = new
                {
                    type = "people",
                    id = person.Id,
                    relationships = new Dictionary<string, object>
                    {
                        { "passport", new
                            {
                                data = (object)null
                            }
                        }
                    }
                }
            };
        
            var route = $"/people/{person.Id}";
        
            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, content);
        
            // Assert
            Assert.Single(responseDocument.Errors);
            Assert.Equal(HttpStatusCode.Forbidden, responseDocument.Errors[0].StatusCode);
            Assert.Equal("You are not allowed to update fields or relationships of locked persons.", responseDocument.Errors[0].Title);
            Assert.Null(responseDocument.Errors[0].Detail);
        }
        
        [Fact]
        public async Task Cascade_Permission_Error_Delete_ToOne_Relationship()
        {
            // Arrange
            var lockedPerson = _personFaker.Generate();
            lockedPerson.IsLocked = true;
            Passport passport = null;
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                passport = new Passport(dbContext);
                lockedPerson.Passport = passport;
                dbContext.People.AddRange(lockedPerson);
                await dbContext.SaveChangesAsync();
            });
        
            var route = $"/passports/{lockedPerson.Passport.StringId}";
        
            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteDeleteAsync<ErrorDocument>(route);
        
            // Assert
            Assert.Single(responseDocument.Errors);
            Assert.Equal(HttpStatusCode.Forbidden, responseDocument.Errors[0].StatusCode);
            Assert.Equal("You are not allowed to update fields or relationships of locked todo items.", responseDocument.Errors[0].Title);
            Assert.Null(responseDocument.Errors[0].Detail);
        }
        
        [Fact]
        public async Task Cascade_Permission_Error_Create_ToMany_Relationship()
        {
            // Arrange
            var persons = _personFaker.Generate(2);
            var lockedTodo = _todoItemFaker.Generate();
            lockedTodo.IsLocked = true;
            lockedTodo.StakeHolders = persons.ToHashSet();
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.TodoItems.Add(lockedTodo);
                await dbContext.SaveChangesAsync();
            });
        
            var content = new
            {
                data = new
                {
                    type = "todoItems",
                    relationships = new Dictionary<string, object>
                    {
                        { "stakeHolders", new
                            {
                                data = new[]
                                {
                                    new { type = "people", id = persons[0].StringId },
                                    new { type = "people", id = persons[1].StringId }
                                }
        
                            }
                        }
                    }
                }
            };
        
            var route = "/todoItems";
        
            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePostAsync<ErrorDocument>(route, content);
        
            // Assert
            Assert.Single(responseDocument.Errors);
            Assert.Equal(HttpStatusCode.Forbidden, responseDocument.Errors[0].StatusCode);
            Assert.Equal("You are not allowed to update fields or relationships of locked todo items.", responseDocument.Errors[0].Title);
            Assert.Null(responseDocument.Errors[0].Detail);
        }
        
        [Fact]
        public async Task Cascade_Permission_Error_Updating_ToMany_Relationship()
        {
            // Arrange
            var persons = _personFaker.Generate(2);
            var lockedTodo = _todoItemFaker.Generate();
            lockedTodo.IsLocked = true;
            lockedTodo.StakeHolders = persons.ToHashSet();
            var unlockedTodo = _todoItemFaker.Generate();
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.TodoItems.Add(lockedTodo);
                dbContext.TodoItems.Add(unlockedTodo);
                await dbContext.SaveChangesAsync();
            });
        
            var content = new
            {
                data = new
                {
                    type = "todoItems",
                    id = unlockedTodo.Id,
                    relationships = new Dictionary<string, object>
                    {
                        { "stakeHolders", new
                            {
                                data = new[]
                                {
                                    new { type = "people", id = persons[0].StringId },
                                    new { type = "people", id = persons[1].StringId }
                                }
        
                            }
                        }
                    }
                }
            };
        
            var route = $"/todoItems/{unlockedTodo.Id}";
        
            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecutePatchAsync<ErrorDocument>(route, content);
        
            // Assert
            Assert.Single(responseDocument.Errors);
            Assert.Equal(HttpStatusCode.Forbidden, responseDocument.Errors[0].StatusCode);
            Assert.Equal("You are not allowed to update fields or relationships of locked todo items.", responseDocument.Errors[0].Title);
            Assert.Null(responseDocument.Errors[0].Detail);
        }
        
        [Fact]
        public async Task Cascade_Permission_Error_Delete_ToMany_Relationship()
        {
            // Arrange
            var persons = _personFaker.Generate(2);
            var lockedTodo = _todoItemFaker.Generate();
            lockedTodo.IsLocked = true;
            lockedTodo.StakeHolders = persons.ToHashSet();
            await _testContext.RunOnDatabaseAsync(async dbContext =>
            {
                dbContext.TodoItems.Add(lockedTodo);
                await dbContext.SaveChangesAsync();
            });
            var route = $"/people/{persons[0].Id}";
        
            // Act
            var (httpResponse, responseDocument) = await _testContext.ExecuteDeleteAsync<ErrorDocument>(route);
        
            // Assert
            Assert.Single(responseDocument.Errors);
            Assert.Equal(HttpStatusCode.Forbidden, responseDocument.Errors[0].StatusCode);
            Assert.Equal("You are not allowed to update fields or relationships of locked todo items.", responseDocument.Errors[0].Title);
            Assert.Null(responseDocument.Errors[0].Detail);
        }

        private IRequestSerializer GetSerializer<TResource>(Expression<Func<TResource, dynamic>> attributes = null, Expression<Func<TResource, dynamic>> relationships = null) where TResource : class, IIdentifiable
        {
            var serializer = _testContext.Factory.Services.GetService<IRequestSerializer>();
            var graph = _testContext.Factory.Services.GetService<IResourceGraph>();
            serializer.AttributesToSerialize = attributes != null ? graph.GetAttributes(attributes) : null;
            serializer.RelationshipsToSerialize = relationships != null ? graph.GetRelationships(relationships) : null;
            return serializer;
        }

        private IResponseDeserializer GetDeserializer()
        {
            var options = _testContext.Factory.Services.GetService<IJsonApiOptions>();
            var formatter = new ResourceNameFormatter(options);
            var resourcesContexts = _testContext.Factory.Services.GetService<IResourceGraph>().GetResourceContexts();
            var builder = new ResourceGraphBuilder(options, NullLoggerFactory.Instance);
            foreach (var rc in resourcesContexts)
            {
                if (rc.ResourceType == typeof(TodoItem) || rc.ResourceType == typeof(TodoItemCollection))
                {
                    continue;
                }
                builder.Add(rc.ResourceType, rc.IdentityType, rc.PublicName);
            }
            builder.Add<TodoItemClient>(formatter.FormatResourceName(typeof(TodoItem)));
            builder.Add<TodoItemCollectionClient, Guid>(formatter.FormatResourceName(typeof(TodoItemCollection)));
            return new ResponseDeserializer(builder.Build(), new ResourceFactory(_testContext.Factory.Services));
        }
    }
}
