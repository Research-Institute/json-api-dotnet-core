using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using JsonApiDotNetCore;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Serialization.Objects;
using Newtonsoft.Json;
using UnitTests.TestModels;
using Xunit;

namespace UnitTests.Serialization.Server
{
    public sealed class ResponseSerializerTests : SerializerTestsSetup
    {
        [Fact]
        public void SerializeSingle_ResourceWithDefaultTargetFields_CanSerialize()
        {
            // Arrange
            var resource = new TestResource
            {
                Id = 1,
                StringField = "value",
                NullableIntField = 123
            };

            ResponseSerializer<TestResource> serializer = GetResponseSerializer<TestResource>();

            // Act
            string serialized = serializer.SerializeSingle(resource);

            // Assert
            const string expectedFormatted = @"{
               ""data"":{
                  ""type"":""testResource"",
                  ""id"":""1"",
                  ""attributes"":{
                     ""stringField"":""value"",
                     ""dateTimeField"":""0001-01-01T00:00:00"",
                     ""nullableDateTimeField"":null,
                     ""intField"":0,
                     ""nullableIntField"":123,
                     ""guidField"":""00000000-0000-0000-0000-000000000000"",
                     ""complexField"":null
                  }
               }
            }";

            string expected = Regex.Replace(expectedFormatted, @"\s+", "");

            Assert.Equal(expected, serialized);
        }

        [Fact]
        public void SerializeMany_ResourceWithDefaultTargetFields_CanSerialize()
        {
            // Arrange
            var resource = new TestResource
            {
                Id = 1,
                StringField = "value",
                NullableIntField = 123
            };

            ResponseSerializer<TestResource> serializer = GetResponseSerializer<TestResource>();

            // Act
            string serialized = serializer.SerializeMany(resource.AsArray());

            // Assert
            const string expectedFormatted = @"{
               ""data"":[{
                  ""type"":""testResource"",
                  ""id"":""1"",
                  ""attributes"":{
                     ""stringField"":""value"",
                     ""dateTimeField"":""0001-01-01T00:00:00"",
                     ""nullableDateTimeField"":null,
                     ""intField"":0,
                     ""nullableIntField"":123,
                     ""guidField"":""00000000-0000-0000-0000-000000000000"",
                     ""complexField"":null
                  }
               }]
            }";

            string expected = Regex.Replace(expectedFormatted, @"\s+", "");
            Assert.Equal(expected, serialized);
        }

        [Fact]
        public void SerializeSingle_ResourceWithIncludedRelationships_CanSerialize()
        {
            // Arrange
            var resource = new MultipleRelationshipsPrincipalPart
            {
                Id = 1,
                PopulatedToOne = new OneToOneDependent
                {
                    Id = 10
                },
                PopulatedToManies = new HashSet<OneToManyDependent>
                {
                    new()
                    {
                        Id = 20
                    }
                }
            };

            List<IEnumerable<RelationshipAttribute>> chain = ResourceGraph.GetRelationships<MultipleRelationshipsPrincipalPart>()
                .Select(relationship => relationship.AsEnumerable()).ToList();

            ResponseSerializer<MultipleRelationshipsPrincipalPart> serializer = GetResponseSerializer<MultipleRelationshipsPrincipalPart>(chain);

            // Act
            string serialized = serializer.SerializeSingle(resource);

            // Assert
            const string expectedFormatted = @"{
               ""data"":{
                  ""type"":""multiPrincipals"",
                  ""id"":""1"",
                  ""attributes"":{ ""attributeMember"":null },
                  ""relationships"":{
                     ""populatedToOne"":{
                        ""data"":{
                           ""type"":""oneToOneDependents"",
                           ""id"":""10""
                        }
                     },
                     ""emptyToOne"": { ""data"":null },
                     ""populatedToManies"":{
                        ""data"":[
                           {
                              ""type"":""oneToManyDependents"",
                              ""id"":""20""
                           }
                        ]
                     },
                     ""emptyToManies"": { ""data"":[ ] },
                     ""multi"":{ ""data"":null }
                  }
               },
               ""included"":[
                  {
                     ""type"":""oneToOneDependents"",
                     ""id"":""10"",
                     ""attributes"":{ ""attributeMember"":null }
                  },
                  {
                   ""type"":""oneToManyDependents"",
                     ""id"":""20"",
                     ""attributes"":{ ""attributeMember"":null }
                  }
               ]
            }";

            string expected = Regex.Replace(expectedFormatted, @"\s+", "");
            Assert.Equal(expected, serialized);
        }

        [Fact]
        public void SerializeSingle_ResourceWithDeeplyIncludedRelationships_CanSerialize()
        {
            // Arrange
            var deeplyIncludedResource = new OneToManyPrincipal
            {
                Id = 30,
                AttributeMember = "deep"
            };

            var includedResource = new OneToManyDependent
            {
                Id = 20,
                Principal = deeplyIncludedResource
            };

            var resource = new MultipleRelationshipsPrincipalPart
            {
                Id = 10,
                PopulatedToManies = new HashSet<OneToManyDependent>
                {
                    includedResource
                }
            };

            List<List<RelationshipAttribute>> chains = ResourceGraph.GetRelationships<MultipleRelationshipsPrincipalPart>().Select(relationship =>
            {
                List<RelationshipAttribute> chain = relationship.AsList();

                if (relationship.PublicName != "populatedToManies")
                {
                    return chain;
                }

                chain.AddRange(ResourceGraph.GetRelationships<OneToManyDependent>());
                return chain;
            }).ToList();

            ResponseSerializer<MultipleRelationshipsPrincipalPart> serializer = GetResponseSerializer<MultipleRelationshipsPrincipalPart>(chains);

            // Act
            string serialized = serializer.SerializeSingle(resource);

            // Assert
            const string expectedFormatted = @"{
               ""data"":{ 
                  ""type"":""multiPrincipals"",
                  ""id"":""10"",
                  ""attributes"":{ 
                     ""attributeMember"":null
                  },
                  ""relationships"":{ 
                     ""populatedToOne"":{ 
                        ""data"":null
                     },
                     ""emptyToOne"":{ 
                        ""data"":null
                     },
                     ""populatedToManies"":{ 
                        ""data"":[ 
                           { 
                              ""type"":""oneToManyDependents"",
                              ""id"":""20""
                           }
                        ]
                     },
                     ""emptyToManies"":{ 
                        ""data"":[]
                     },
                     ""multi"":{ 
                        ""data"":null
                     }
                  }
               },
               ""included"":[
                  { 
                     ""type"":""oneToManyDependents"",
                     ""id"":""20"",
                     ""attributes"":{ 
                        ""attributeMember"":null
                     },
                     ""relationships"":{ 
                        ""principal"":{ 
                           ""data"":{ 
                              ""type"":""oneToManyPrincipals"",
                              ""id"":""30""
                           }
                        }
                     }
                  },
                  { 
                     ""type"":""oneToManyPrincipals"",
                     ""id"":""30"",
                     ""attributes"":{ 
                        ""attributeMember"":""deep""
                     }
                  }
               ]
            }";

            string expected = Regex.Replace(expectedFormatted, @"\s+", "");
            Assert.Equal(expected, serialized);
        }

        [Fact]
        public void SerializeSingle_Null_CanSerialize()
        {
            // Arrange
            ResponseSerializer<TestResource> serializer = GetResponseSerializer<TestResource>();

            // Act
            string serialized = serializer.SerializeSingle(null);

            // Assert
            const string expectedFormatted = @"{ ""data"": null }";
            string expected = Regex.Replace(expectedFormatted, @"\s+", "");
            Assert.Equal(expected, serialized);
        }

        [Fact]
        public void SerializeList_EmptyList_CanSerialize()
        {
            // Arrange
            ResponseSerializer<TestResource> serializer = GetResponseSerializer<TestResource>();

            // Act
            string serialized = serializer.SerializeMany(new List<TestResource>());

            // Assert
            const string expectedFormatted = @"{ ""data"": [] }";
            string expected = Regex.Replace(expectedFormatted, @"\s+", "");
            Assert.Equal(expected, serialized);
        }

        [Fact]
        public void SerializeSingle_ResourceWithLinksEnabled_CanSerialize()
        {
            // Arrange
            var resource = new OneToManyPrincipal
            {
                Id = 10
            };

            ResponseSerializer<OneToManyPrincipal> serializer = GetResponseSerializer<OneToManyPrincipal>(topLinks: DummyTopLevelLinks,
                relationshipLinks: DummyRelationshipLinks, resourceLinks: DummyResourceLinks);

            // Act
            string serialized = serializer.SerializeSingle(resource);

            // Assert
            const string expectedFormatted = @"{
               ""links"":{
                  ""self"":""http://www.dummy.com/dummy-self-link"",
                  ""first"":""http://www.dummy.com/dummy-first-link"",
                  ""last"":""http://www.dummy.com/dummy-last-link"",
                  ""prev"":""http://www.dummy.com/dummy-prev-link"",
                  ""next"":""http://www.dummy.com/dummy-next-link""
               },
               ""data"":{
                  ""type"":""oneToManyPrincipals"",
                  ""id"":""10"",
                  ""attributes"":{
                     ""attributeMember"":null
                  },
                  ""relationships"":{
                     ""dependents"":{
                        ""links"":{
                           ""self"":""http://www.dummy.com/dummy-relationship-self-link"",
                           ""related"":""http://www.dummy.com/dummy-relationship-related-link""
                        }
                     }
                  },
                  ""links"":{
                     ""self"":""http://www.dummy.com/dummy-resource-self-link""
                  }
               }
            }";

            string expected = Regex.Replace(expectedFormatted, @"\s+", "");
            Assert.Equal(expected, serialized);
        }

        [Fact]
        public void SerializeSingle_ResourceWithMeta_IncludesMetaInResult()
        {
            // Arrange
            var meta = new Dictionary<string, object>
            {
                ["test"] = "meta"
            };

            var resource = new OneToManyPrincipal
            {
                Id = 10
            };

            ResponseSerializer<OneToManyPrincipal> serializer = GetResponseSerializer<OneToManyPrincipal>(metaDict: meta);

            // Act
            string serialized = serializer.SerializeSingle(resource);

            // Assert
            const string expectedFormatted = @"{
                ""meta"":{ ""test"": ""meta"" },
                ""data"":{
                    ""type"":""oneToManyPrincipals"",
                    ""id"":""10"",
                    ""attributes"":{
                        ""attributeMember"":null
                    }
                }
            }";

            string expected = Regex.Replace(expectedFormatted, @"\s+", "");
            Assert.Equal(expected, serialized);
        }

        [Fact]
        public void SerializeSingle_NullWithLinksAndMeta_StillShowsLinksAndMeta()
        {
            // Arrange
            var meta = new Dictionary<string, object>
            {
                ["test"] = "meta"
            };

            ResponseSerializer<OneToManyPrincipal> serializer = GetResponseSerializer<OneToManyPrincipal>(metaDict: meta, topLinks: DummyTopLevelLinks,
                relationshipLinks: DummyRelationshipLinks, resourceLinks: DummyResourceLinks);

            // Act
            string serialized = serializer.SerializeSingle(null);

            // Assert
            const string expectedFormatted = @"{
                ""meta"":{ ""test"": ""meta"" },
                ""links"":{
                    ""self"":""http://www.dummy.com/dummy-self-link"",
                    ""first"":""http://www.dummy.com/dummy-first-link"",
                    ""last"":""http://www.dummy.com/dummy-last-link"",
                    ""prev"":""http://www.dummy.com/dummy-prev-link"",
                    ""next"":""http://www.dummy.com/dummy-next-link""
                },
                ""data"": null
            }";

            string expected = Regex.Replace(expectedFormatted, @"\s+", "");
            Assert.Equal(expected, serialized);
        }

        [Fact]
        public void SerializeError_Error_CanSerialize()
        {
            // Arrange
            var error = new Error(HttpStatusCode.InsufficientStorage)
            {
                Title = "title",
                Detail = "detail"
            };

            var errorDocument = new ErrorDocument(error);

            string expectedJson = JsonConvert.SerializeObject(new
            {
                errors = new[]
                {
                    new
                    {
                        id = error.Id,
                        status = "507",
                        title = "title",
                        detail = "detail"
                    }
                }
            });

            ResponseSerializer<OneToManyPrincipal> serializer = GetResponseSerializer<OneToManyPrincipal>();

            // Act
            string result = serializer.Serialize(errorDocument);

            // Assert
            Assert.Equal(expectedJson, result);
        }
    }
}
