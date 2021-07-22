using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Reflection;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Serialization.Objects;
using Newtonsoft.Json;
using UnitTests.TestModels;
using Xunit;

namespace UnitTests.Serialization.Common
{
    public sealed class BaseDocumentParserTests : DeserializerTestsSetup
    {
        private readonly TestDeserializer _deserializer;

        public BaseDocumentParserTests()
        {
            _deserializer = new TestDeserializer(ResourceGraph, new ResourceFactory(new ServiceContainer()));
        }

        [Fact]
        public void DeserializeResourceIdentifiers_SingleData_CanDeserialize()
        {
            // Arrange
            var content = new Document
            {
                Data = new ResourceObject
                {
                    Type = "testResource",
                    Id = "1"
                }
            };

            string body = JsonConvert.SerializeObject(content);

            // Act
            var result = (TestResource)_deserializer.Deserialize(body);

            // Assert
            Assert.Equal(1, result.Id);
        }

        [Fact]
        public void DeserializeResourceIdentifiers_EmptySingleData_CanDeserialize()
        {
            // Arrange
            var content = new Document();
            string body = JsonConvert.SerializeObject(content);

            // Act
            object result = _deserializer.Deserialize(body);

            // Arrange
            Assert.Null(result);
        }

        [Fact]
        public void DeserializeResourceIdentifiers_ArrayData_CanDeserialize()
        {
            // Arrange
            var content = new Document
            {
                Data = new List<ResourceObject>
                {
                    new()
                    {
                        Type = "testResource",
                        Id = "1"
                    }
                }
            };

            string body = JsonConvert.SerializeObject(content);

            // Act
            var result = (IEnumerable<IIdentifiable>)_deserializer.Deserialize(body);

            // Assert
            Assert.Equal("1", result.First().StringId);
        }

        [Fact]
        public void DeserializeResourceIdentifiers_EmptyArrayData_CanDeserialize()
        {
            var content = new Document
            {
                Data = new List<ResourceObject>()
            };

            string body = JsonConvert.SerializeObject(content);

            // Act
            var result = (IEnumerable<IIdentifiable>)_deserializer.Deserialize(body);

            // Assert
            Assert.Empty(result);
        }

        [Theory]
        [InlineData("stringField", "some string")]
        [InlineData("stringField", null)]
        [InlineData("intField", null, true)]
        [InlineData("intField", 1)]
        [InlineData("intField", "1")]
        [InlineData("nullableIntField", null)]
        [InlineData("nullableIntField", "1")]
        [InlineData("guidField", "bad format", true)]
        [InlineData("guidField", "1a68be43-cc84-4924-a421-7f4d614b7781")]
        [InlineData("dateTimeField", "9/11/2019 11:41:40 AM")]
        [InlineData("dateTimeField", null, true)]
        [InlineData("nullableDateTimeField", null)]
        public void DeserializeAttributes_VariousDataTypes_CanDeserialize(string member, object value, bool expectError = false)
        {
            // Arrange
            var content = new Document
            {
                Data = new ResourceObject
                {
                    Type = "testResource",
                    Id = "1",
                    Attributes = new Dictionary<string, object>
                    {
                        [member] = value
                    }
                }
            };

            string body = JsonConvert.SerializeObject(content);

            // Act
            Func<TestResource> action = () => (TestResource)_deserializer.Deserialize(body);

            // Assert
            if (expectError)
            {
                Assert.ThrowsAny<FormatException>(action);
            }
            else
            {
                TestResource resource = action();

                PropertyInfo pi = ResourceGraph.GetResourceContext("testResource").Attributes.Single(attr => attr.PublicName == member).Property;
                object deserializedValue = pi.GetValue(resource);

                if (member == "intField")
                {
                    Assert.Equal(1, deserializedValue);
                }
                else if (member == "nullableIntField" && value == null)
                {
                    Assert.Null(deserializedValue);
                }
                else if (member == "nullableIntField" && (string)value == "1")
                {
                    Assert.Equal(1, deserializedValue);
                }
                else if (member == "guidField")
                {
                    Assert.Equal(deserializedValue, Guid.Parse("1a68be43-cc84-4924-a421-7f4d614b7781"));
                }
                else if (member == "dateTimeField")
                {
                    Assert.Equal(deserializedValue, DateTime.Parse("9/11/2019 11:41:40 AM"));
                }
                else
                {
                    Assert.Equal(value, deserializedValue);
                }
            }
        }

        [Fact]
        public void DeserializeAttributes_ComplexType_CanDeserialize()
        {
            // Arrange
            var content = new Document
            {
                Data = new ResourceObject
                {
                    Type = "testResource",
                    Id = "1",
                    Attributes = new Dictionary<string, object>
                    {
                        ["complexField"] = new Dictionary<string, object>
                        {
                            ["compoundName"] = "testName"
                        }
                    }
                }
            };

            string body = JsonConvert.SerializeObject(content);

            // Act
            var result = (TestResource)_deserializer.Deserialize(body);

            // Assert
            Assert.NotNull(result.ComplexField);
            Assert.Equal("testName", result.ComplexField.CompoundName);
        }

        [Fact]
        public void DeserializeAttributes_ComplexListType_CanDeserialize()
        {
            // Arrange
            var content = new Document
            {
                Data = new ResourceObject
                {
                    Type = "testResource-with-list",
                    Id = "1",
                    Attributes = new Dictionary<string, object>
                    {
                        ["complexFields"] = new[]
                        {
                            new Dictionary<string, object>
                            {
                                ["compoundName"] = "testName"
                            }
                        }
                    }
                }
            };

            string body = JsonConvert.SerializeObject(content);

            // Act
            var result = (TestResourceWithList)_deserializer.Deserialize(body);

            // Assert
            Assert.NotNull(result.ComplexFields);
            Assert.NotEmpty(result.ComplexFields);
            Assert.Equal("testName", result.ComplexFields[0].CompoundName);
        }

        [Fact]
        public void DeserializeRelationship_SingleDataForToOneRelationship_CannotDeserialize()
        {
            // Arrange
            Document content = CreateDocumentWithRelationships("oneToManyPrincipals", "dependents");

            content.SingleData.Relationships["dependents"] = new RelationshipEntry
            {
                Data = new ResourceIdentifierObject
                {
                    Type = "Dependents",
                    Id = "1"
                }
            };

            string body = JsonConvert.SerializeObject(content);

            // Act
            Action action = () => _deserializer.Deserialize(body);

            // Assert
            Assert.Throws<JsonApiSerializationException>(action);
        }

        [Fact]
        public void DeserializeRelationship_ManyDataForToManyRelationship_CannotDeserialize()
        {
            // Arrange
            Document content = CreateDocumentWithRelationships("oneToOnePrincipals", "dependent");

            content.SingleData.Relationships["dependent"] = new RelationshipEntry
            {
                Data = new List<ResourceIdentifierObject>
                {
                    new()
                    {
                        Type = "Dependent",
                        Id = "1"
                    }
                }
            };

            string body = JsonConvert.SerializeObject(content);

            // Act
            Action action = () => _deserializer.Deserialize(body);

            // Assert
            Assert.Throws<JsonApiSerializationException>(action);
        }

        [Fact]
        public void DeserializeRelationships_EmptyOneToOneDependent_NavigationPropertyIsNull()
        {
            // Arrange
            Document content = CreateDocumentWithRelationships("oneToOnePrincipals", "dependent");
            string body = JsonConvert.SerializeObject(content);

            // Act
            var result = (OneToOnePrincipal)_deserializer.Deserialize(body);

            // Assert
            Assert.Equal(1, result.Id);
            Assert.Null(result.Dependent);
            Assert.Null(result.AttributeMember);
        }

        [Fact]
        public void DeserializeRelationships_PopulatedOneToOneDependent_NavigationPropertyIsPopulated()
        {
            // Arrange
            Document content = CreateDocumentWithRelationships("oneToOnePrincipals", "dependent", "oneToOneDependents");
            string body = JsonConvert.SerializeObject(content);

            // Act
            var result = (OneToOnePrincipal)_deserializer.Deserialize(body);

            // Assert
            Assert.Equal(1, result.Id);
            Assert.Equal(10, result.Dependent.Id);
            Assert.Null(result.AttributeMember);
        }

        [Fact]
        public void DeserializeRelationships_EmptyOneToOnePrincipal_NavigationIsNull()
        {
            // Arrange
            Document content = CreateDocumentWithRelationships("oneToOneDependents", "principal");
            string body = JsonConvert.SerializeObject(content);

            // Act
            var result = (OneToOneDependent)_deserializer.Deserialize(body);

            // Assert
            Assert.Equal(1, result.Id);
            Assert.Null(result.Principal);
        }

        [Fact]
        public void DeserializeRelationships_EmptyRequiredOneToOnePrincipal_NavigationIsNull()
        {
            // Arrange
            Document content = CreateDocumentWithRelationships("oneToOneRequiredDependents", "principal");
            string body = JsonConvert.SerializeObject(content);

            // Act
            var result = (OneToOneRequiredDependent)_deserializer.Deserialize(body);

            // Assert
            Assert.Equal(1, result.Id);
            Assert.Null(result.Principal);
        }

        [Fact]
        public void DeserializeRelationships_PopulatedOneToOnePrincipal_NavigationIsPopulated()
        {
            // Arrange
            Document content = CreateDocumentWithRelationships("oneToOneDependents", "principal", "oneToOnePrincipals");
            string body = JsonConvert.SerializeObject(content);

            // Act
            var result = (OneToOneDependent)_deserializer.Deserialize(body);

            // Assert
            Assert.Equal(1, result.Id);
            Assert.NotNull(result.Principal);
            Assert.Equal(10, result.Principal.Id);
            Assert.Null(result.AttributeMember);
        }

        [Fact]
        public void DeserializeRelationships_EmptyOneToManyPrincipal_NavigationIsNull()
        {
            // Arrange
            Document content = CreateDocumentWithRelationships("oneToManyDependents", "principal");
            string body = JsonConvert.SerializeObject(content);

            // Act
            var result = (OneToManyDependent)_deserializer.Deserialize(body);

            // Assert
            Assert.Equal(1, result.Id);
            Assert.Null(result.Principal);
            Assert.Null(result.AttributeMember);
        }

        [Fact]
        public void DeserializeRelationships_EmptyOneToManyRequiredPrincipal_NavigationIsNull()
        {
            // Arrange
            Document content = CreateDocumentWithRelationships("oneToMany-requiredDependents", "principal");
            string body = JsonConvert.SerializeObject(content);

            // Act
            var result = (OneToManyRequiredDependent)_deserializer.Deserialize(body);

            // Assert
            Assert.Equal(1, result.Id);
            Assert.Null(result.Principal);
            Assert.Null(result.AttributeMember);
        }

        [Fact]
        public void DeserializeRelationships_PopulatedOneToManyPrincipal_NavigationIsPopulated()
        {
            // Arrange
            Document content = CreateDocumentWithRelationships("oneToManyDependents", "principal", "oneToManyPrincipals");
            string body = JsonConvert.SerializeObject(content);

            // Act
            var result = (OneToManyDependent)_deserializer.Deserialize(body);

            // Assert
            Assert.Equal(1, result.Id);
            Assert.NotNull(result.Principal);
            Assert.Equal(10, result.Principal.Id);
            Assert.Null(result.AttributeMember);
        }

        [Fact]
        public void DeserializeRelationships_EmptyOneToManyDependent_NavigationIsNull()
        {
            // Arrange
            Document content = CreateDocumentWithRelationships("oneToManyPrincipals", "dependents", isToManyData: true);
            string body = JsonConvert.SerializeObject(content);

            // Act
            var result = (OneToManyPrincipal)_deserializer.Deserialize(body);

            // Assert
            Assert.Equal(1, result.Id);
            Assert.Empty(result.Dependents);
            Assert.Null(result.AttributeMember);
        }

        [Fact]
        public void DeserializeRelationships_PopulatedOneToManyDependent_NavigationIsPopulated()
        {
            // Arrange
            Document content = CreateDocumentWithRelationships("oneToManyPrincipals", "dependents", "oneToManyDependents", true);
            string body = JsonConvert.SerializeObject(content);

            // Act
            var result = (OneToManyPrincipal)_deserializer.Deserialize(body);

            // Assert
            Assert.Equal(1, result.Id);
            Assert.Single(result.Dependents);
            Assert.Equal(10, result.Dependents.First().Id);
            Assert.Null(result.AttributeMember);
        }
    }
}
