using System.Collections.Generic;
using System.Text.RegularExpressions;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Building;
using JsonApiDotNetCore.Serialization.Client.Internal;
using UnitTests.TestModels;
using Xunit;

namespace UnitTests.Serialization.Client
{
    public sealed class RequestSerializerTests : SerializerTestsSetup
    {
        private readonly RequestSerializer _serializer;

        public RequestSerializerTests()
        {
            var builder = new ResourceObjectBuilder(ResourceGraph, new ResourceObjectBuilderSettings());
            _serializer = new RequestSerializer(ResourceGraph, builder);
        }

        [Fact]
        public void SerializeSingle_ResourceWithDefaultTargetFields_CanBuild()
        {
            // Arrange
            var resource = new TestResource
            {
                Id = 1,
                StringField = "value",
                NullableIntField = 123
            };

            // Act
            string serialized = _serializer.Serialize(resource);

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
        public void SerializeSingle_ResourceWithTargetedSetAttributes_CanBuild()
        {
            // Arrange
            var resource = new TestResource
            {
                Id = 1,
                StringField = "value",
                NullableIntField = 123
            };

            _serializer.AttributesToSerialize = ResourceGraph.GetAttributes<TestResource>(tr => tr.StringField);

            // Act
            string serialized = _serializer.Serialize(resource);

            // Assert
            const string expectedFormatted = @"{
               ""data"":{
                  ""type"":""testResource"",
                  ""id"":""1"",
                  ""attributes"":{
                     ""stringField"":""value""
                  }
               }
            }";

            string expected = Regex.Replace(expectedFormatted, @"\s+", "");
            Assert.Equal(expected, serialized);
        }

        [Fact]
        public void SerializeSingle_NoIdWithTargetedSetAttributes_CanBuild()
        {
            // Arrange
            var resourceNoId = new TestResource
            {
                Id = 0,
                StringField = "value",
                NullableIntField = 123
            };

            _serializer.AttributesToSerialize = ResourceGraph.GetAttributes<TestResource>(tr => tr.StringField);

            // Act
            string serialized = _serializer.Serialize(resourceNoId);

            // Assert
            const string expectedFormatted = @"{
               ""data"":{
                  ""type"":""testResource"",
                  ""attributes"":{
                     ""stringField"":""value""
                  }
               }
            }";

            string expected = Regex.Replace(expectedFormatted, @"\s+", "");
            Assert.Equal(expected, serialized);
        }

        [Fact]
        public void SerializeSingle_ResourceWithoutTargetedAttributes_CanBuild()
        {
            // Arrange
            var resource = new TestResource
            {
                Id = 1,
                StringField = "value",
                NullableIntField = 123
            };

            _serializer.AttributesToSerialize = ResourceGraph.GetAttributes<TestResource>(_ => new
            {
            });

            // Act
            string serialized = _serializer.Serialize(resource);

            // Assert
            const string expectedFormatted = @"{
               ""data"":{
                  ""type"":""testResource"",
                  ""id"":""1""
               }
            }";

            string expected = Regex.Replace(expectedFormatted, @"\s+", "");
            Assert.Equal(expected, serialized);
        }

        [Fact]
        public void SerializeSingle_ResourceWithTargetedRelationships_CanBuild()
        {
            // Arrange
            var resourceWithRelationships = new MultipleRelationshipsPrincipalPart
            {
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

            _serializer.RelationshipsToSerialize = ResourceGraph.GetRelationships<MultipleRelationshipsPrincipalPart>(tr => new
            {
                tr.EmptyToOne,
                tr.EmptyToManies,
                tr.PopulatedToOne,
                tr.PopulatedToManies
            });

            // Act
            string serialized = _serializer.Serialize(resourceWithRelationships);

            // Assert
            const string expectedFormatted = @"{
                ""data"":{
                    ""type"":""multiPrincipals"",
                    ""attributes"":{
                        ""attributeMember"":null
                    },
                    ""relationships"":{
                        ""emptyToOne"":{
                        ""data"":null
                        },
                        ""emptyToManies"":{
                        ""data"":[ ]
                        },
                        ""populatedToOne"":{
                        ""data"":{
                            ""type"":""oneToOneDependents"",
                            ""id"":""10""
                           }
                        },
                        ""populatedToManies"":{
                        ""data"":[
                            {
                                ""type"":""oneToManyDependents"",
                                ""id"":""20""
                            }
                          ]
                        }
                    }
                }
            }";

            string expected = Regex.Replace(expectedFormatted, @"\s+", "");
            Assert.Equal(expected, serialized);
        }

        [Fact]
        public void SerializeMany_ResourcesWithTargetedAttributes_CanBuild()
        {
            // Arrange
            var resources = new List<TestResource>
            {
                new()
                {
                    Id = 1,
                    StringField = "value1",
                    NullableIntField = 123
                },
                new()
                {
                    Id = 2,
                    StringField = "value2",
                    NullableIntField = 123
                }
            };

            _serializer.AttributesToSerialize = ResourceGraph.GetAttributes<TestResource>(tr => tr.StringField);

            // Act
            string serialized = _serializer.Serialize(resources);

            // Assert
            const string expectedFormatted = @"{
                ""data"":[
                    {
                        ""type"":""testResource"",
                        ""id"":""1"",
                        ""attributes"":{
                        ""stringField"":""value1""
                        }
                    },
                    {
                        ""type"":""testResource"",
                        ""id"":""2"",
                        ""attributes"":{
                        ""stringField"":""value2""
                        }
                    }
                ]
            }";

            string expected = Regex.Replace(expectedFormatted, @"\s+", "");
            Assert.Equal(expected, serialized);
        }

        [Fact]
        public void SerializeSingle_Null_CanBuild()
        {
            // Arrange
            _serializer.AttributesToSerialize = ResourceGraph.GetAttributes<TestResource>(tr => tr.StringField);

            // Act
            string serialized = _serializer.Serialize((IIdentifiable)null);

            // Assert
            const string expectedFormatted = @"{
                ""data"":null
            }";

            string expected = Regex.Replace(expectedFormatted, @"\s+", "");
            Assert.Equal(expected, serialized);
        }

        [Fact]
        public void SerializeMany_EmptyList_CanBuild()
        {
            // Arrange
            _serializer.AttributesToSerialize = ResourceGraph.GetAttributes<TestResource>(tr => tr.StringField);

            // Act
            string serialized = _serializer.Serialize(new List<TestResource>());

            // Assert
            const string expectedFormatted = @"{
                ""data"":[]
            }";

            string expected = Regex.Replace(expectedFormatted, @"\s+", "");
            Assert.Equal(expected, serialized);
        }
    }
}
