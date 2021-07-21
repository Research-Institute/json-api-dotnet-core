using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Serialization.Client.Internal;
using JsonApiDotNetCore.Serialization.Objects;
using Newtonsoft.Json;
using UnitTests.TestModels;
using Xunit;

namespace UnitTests.Serialization.Client
{
    public sealed class ResponseDeserializerTests : DeserializerTestsSetup
    {
        private readonly Dictionary<string, string> _linkValues = new();
        private readonly ResponseDeserializer _deserializer;

        public ResponseDeserializerTests()
        {
            _deserializer = new ResponseDeserializer(ResourceGraph, new ResourceFactory(new ServiceContainer()));
            _linkValues.Add("self", "http://example.com/articles");
            _linkValues.Add("next", "http://example.com/articles?page[number]=2");
            _linkValues.Add("last", "http://example.com/articles?page[number]=10");
        }

        [Fact]
        public void DeserializeSingle_EmptyResponseWithMeta_CanDeserialize()
        {
            // Arrange
            var content = new Document
            {
                Meta = new Dictionary<string, object>
                {
                    ["foo"] = "bar"
                }
            };

            string body = JsonConvert.SerializeObject(content);

            // Act
            SingleResponse<TestResource> result = _deserializer.DeserializeSingle<TestResource>(body);

            // Assert
            Assert.Null(result.Data);
            Assert.NotNull(result.Meta);
            Assert.Equal("bar", result.Meta["foo"]);
        }

        [Fact]
        public void DeserializeSingle_EmptyResponseWithTopLevelLinks_CanDeserialize()
        {
            // Arrange
            var content = new Document
            {
                Links = new TopLevelLinks
                {
                    Self = _linkValues["self"],
                    Next = _linkValues["next"],
                    Last = _linkValues["last"]
                }
            };

            string body = JsonConvert.SerializeObject(content);

            // Act
            SingleResponse<TestResource> result = _deserializer.DeserializeSingle<TestResource>(body);

            // Assert
            Assert.Null(result.Data);
            Assert.NotNull(result.Links);
            TopLevelLinks links = result.Links;
            Assert.Equal(_linkValues["self"], links.Self);
            Assert.Equal(_linkValues["next"], links.Next);
            Assert.Equal(_linkValues["last"], links.Last);
        }

        [Fact]
        public void DeserializeList_EmptyResponseWithTopLevelLinks_CanDeserialize()
        {
            // Arrange
            var content = new Document
            {
                Links = new TopLevelLinks
                {
                    Self = _linkValues["self"],
                    Next = _linkValues["next"],
                    Last = _linkValues["last"]
                },
                Data = new List<ResourceObject>()
            };

            string body = JsonConvert.SerializeObject(content);

            // Act
            ManyResponse<TestResource> result = _deserializer.DeserializeMany<TestResource>(body);

            // Assert
            Assert.Empty(result.Data);
            Assert.NotNull(result.Links);
            TopLevelLinks links = result.Links;
            Assert.Equal(_linkValues["self"], links.Self);
            Assert.Equal(_linkValues["next"], links.Next);
            Assert.Equal(_linkValues["last"], links.Last);
        }

        [Fact]
        public void DeserializeSingle_ResourceWithAttributes_CanDeserialize()
        {
            // Arrange
            Document content = CreateTestResourceDocument();
            string body = JsonConvert.SerializeObject(content);

            // Act
            SingleResponse<TestResource> result = _deserializer.DeserializeSingle<TestResource>(body);
            TestResource resource = result.Data;

            // Assert
            Assert.Null(result.Links);
            Assert.Null(result.Meta);
            Assert.Equal(1, resource.Id);
            Assert.Equal(content.SingleData.Attributes["stringField"], resource.StringField);
        }

        [Fact]
        public void DeserializeSingle_MultipleDependentRelationshipsWithIncluded_CanDeserialize()
        {
            // Arrange
            Document content = CreateDocumentWithRelationships("multiPrincipals");
            content.SingleData.Relationships.Add("populatedToOne", CreateRelationshipData("oneToOneDependents"));
            content.SingleData.Relationships.Add("populatedToManies", CreateRelationshipData("oneToManyDependents", true));
            content.SingleData.Relationships.Add("emptyToOne", CreateRelationshipData());
            content.SingleData.Relationships.Add("emptyToManies", CreateRelationshipData(isToManyData: true));
            const string toOneAttributeValue = "populatedToOne member content";
            const string toManyAttributeValue = "populatedToManies member content";

            content.Included = new List<ResourceObject>
            {
                new()
                {
                    Type = "oneToOneDependents",
                    Id = "10",
                    Attributes = new Dictionary<string, object>
                    {
                        ["attributeMember"] = toOneAttributeValue
                    }
                },
                new()
                {
                    Type = "oneToManyDependents",
                    Id = "10",
                    Attributes = new Dictionary<string, object>
                    {
                        ["attributeMember"] = toManyAttributeValue
                    }
                }
            };

            string body = JsonConvert.SerializeObject(content);

            // Act
            SingleResponse<MultipleRelationshipsPrincipalPart> result = _deserializer.DeserializeSingle<MultipleRelationshipsPrincipalPart>(body);
            MultipleRelationshipsPrincipalPart resource = result.Data;

            // Assert
            Assert.Equal(1, resource.Id);
            Assert.NotNull(resource.PopulatedToOne);
            Assert.Equal(toOneAttributeValue, resource.PopulatedToOne.AttributeMember);
            Assert.Equal(toManyAttributeValue, resource.PopulatedToManies.First().AttributeMember);
            Assert.NotNull(resource.PopulatedToManies);
            Assert.NotNull(resource.EmptyToManies);
            Assert.Empty(resource.EmptyToManies);
            Assert.Null(resource.EmptyToOne);
        }

        [Fact]
        public void DeserializeSingle_MultiplePrincipalRelationshipsWithIncluded_CanDeserialize()
        {
            // Arrange
            Document content = CreateDocumentWithRelationships("multiDependents");
            content.SingleData.Relationships.Add("populatedToOne", CreateRelationshipData("oneToOnePrincipals"));
            content.SingleData.Relationships.Add("populatedToMany", CreateRelationshipData("oneToManyPrincipals"));
            content.SingleData.Relationships.Add("emptyToOne", CreateRelationshipData());
            content.SingleData.Relationships.Add("emptyToMany", CreateRelationshipData());
            const string toOneAttributeValue = "populatedToOne member content";
            const string toManyAttributeValue = "populatedToManies member content";

            content.Included = new List<ResourceObject>
            {
                new()
                {
                    Type = "oneToOnePrincipals",
                    Id = "10",
                    Attributes = new Dictionary<string, object>
                    {
                        ["attributeMember"] = toOneAttributeValue
                    }
                },
                new()
                {
                    Type = "oneToManyPrincipals",
                    Id = "10",
                    Attributes = new Dictionary<string, object>
                    {
                        ["attributeMember"] = toManyAttributeValue
                    }
                }
            };

            string body = JsonConvert.SerializeObject(content);

            // Act
            SingleResponse<MultipleRelationshipsDependentPart> result = _deserializer.DeserializeSingle<MultipleRelationshipsDependentPart>(body);
            MultipleRelationshipsDependentPart resource = result.Data;

            // Assert
            Assert.Equal(1, resource.Id);
            Assert.NotNull(resource.PopulatedToOne);
            Assert.Equal(toOneAttributeValue, resource.PopulatedToOne.AttributeMember);
            Assert.Equal(toManyAttributeValue, resource.PopulatedToMany.AttributeMember);
            Assert.NotNull(resource.PopulatedToMany);
            Assert.Null(resource.EmptyToMany);
            Assert.Null(resource.EmptyToOne);
        }

        [Fact]
        public void DeserializeSingle_NestedIncluded_CanDeserialize()
        {
            // Arrange
            Document content = CreateDocumentWithRelationships("multiPrincipals");
            content.SingleData.Relationships.Add("populatedToManies", CreateRelationshipData("oneToManyDependents", true));
            const string toManyAttributeValue = "populatedToManies member content";
            const string nestedIncludeAttributeValue = "nested include member content";

            content.Included = new List<ResourceObject>
            {
                new()
                {
                    Type = "oneToManyDependents",
                    Id = "10",
                    Attributes = new Dictionary<string, object>
                    {
                        ["attributeMember"] = toManyAttributeValue
                    },
                    Relationships = new Dictionary<string, RelationshipEntry>
                    {
                        ["principal"] = CreateRelationshipData("oneToManyPrincipals")
                    }
                },
                new()
                {
                    Type = "oneToManyPrincipals",
                    Id = "10",
                    Attributes = new Dictionary<string, object>
                    {
                        ["attributeMember"] = nestedIncludeAttributeValue
                    }
                }
            };

            string body = JsonConvert.SerializeObject(content);

            // Act
            SingleResponse<MultipleRelationshipsPrincipalPart> result = _deserializer.DeserializeSingle<MultipleRelationshipsPrincipalPart>(body);
            MultipleRelationshipsPrincipalPart resource = result.Data;

            // Assert
            Assert.Equal(1, resource.Id);
            Assert.Null(resource.PopulatedToOne);
            Assert.Null(resource.EmptyToManies);
            Assert.Null(resource.EmptyToOne);
            Assert.NotNull(resource.PopulatedToManies);
            OneToManyDependent includedResource = resource.PopulatedToManies.First();
            Assert.Equal(toManyAttributeValue, includedResource.AttributeMember);
            OneToManyPrincipal nestedIncludedResource = includedResource.Principal;
            Assert.Equal(nestedIncludeAttributeValue, nestedIncludedResource.AttributeMember);
        }

        [Fact]
        public void DeserializeSingle_DeeplyNestedIncluded_CanDeserialize()
        {
            // Arrange
            Document content = CreateDocumentWithRelationships("multiPrincipals");
            content.SingleData.Relationships.Add("multi", CreateRelationshipData("multiPrincipals"));
            const string includedAttributeValue = "multi member content";
            const string nestedIncludedAttributeValue = "nested include member content";
            const string deeplyNestedIncludedAttributeValue = "deeply nested member content";

            content.Included = new List<ResourceObject>
            {
                new()
                {
                    Type = "multiPrincipals",
                    Id = "10",
                    Attributes = new Dictionary<string, object>
                    {
                        ["attributeMember"] = includedAttributeValue
                    },
                    Relationships = new Dictionary<string, RelationshipEntry>
                    {
                        ["populatedToManies"] = CreateRelationshipData("oneToManyDependents", true)
                    }
                },
                new()
                {
                    Type = "oneToManyDependents",
                    Id = "10",
                    Attributes = new Dictionary<string, object>
                    {
                        ["attributeMember"] = nestedIncludedAttributeValue
                    },
                    Relationships = new Dictionary<string, RelationshipEntry>
                    {
                        ["principal"] = CreateRelationshipData("oneToManyPrincipals")
                    }
                },
                new()
                {
                    Type = "oneToManyPrincipals",
                    Id = "10",
                    Attributes = new Dictionary<string, object>
                    {
                        ["attributeMember"] = deeplyNestedIncludedAttributeValue
                    }
                }
            };

            string body = JsonConvert.SerializeObject(content);

            // Act
            SingleResponse<MultipleRelationshipsPrincipalPart> result = _deserializer.DeserializeSingle<MultipleRelationshipsPrincipalPart>(body);
            MultipleRelationshipsPrincipalPart resource = result.Data;

            // Assert
            Assert.Equal(1, resource.Id);
            MultipleRelationshipsPrincipalPart included = resource.Multi;
            Assert.Equal(10, included.Id);
            Assert.Equal(includedAttributeValue, included.AttributeMember);
            OneToManyDependent nestedIncluded = included.PopulatedToManies.First();
            Assert.Equal(10, nestedIncluded.Id);
            Assert.Equal(nestedIncludedAttributeValue, nestedIncluded.AttributeMember);
            OneToManyPrincipal deeplyNestedIncluded = nestedIncluded.Principal;
            Assert.Equal(10, deeplyNestedIncluded.Id);
            Assert.Equal(deeplyNestedIncludedAttributeValue, deeplyNestedIncluded.AttributeMember);
        }

        [Fact]
        public void DeserializeList_DeeplyNestedIncluded_CanDeserialize()
        {
            // Arrange
            var content = new Document
            {
                Data = new List<ResourceObject>
                {
                    CreateDocumentWithRelationships("multiPrincipals").SingleData
                }
            };

            content.ManyData[0].Relationships.Add("multi", CreateRelationshipData("multiPrincipals"));
            const string includedAttributeValue = "multi member content";
            const string nestedIncludedAttributeValue = "nested include member content";
            const string deeplyNestedIncludedAttributeValue = "deeply nested member content";

            content.Included = new List<ResourceObject>
            {
                new()
                {
                    Type = "multiPrincipals",
                    Id = "10",
                    Attributes = new Dictionary<string, object>
                    {
                        ["attributeMember"] = includedAttributeValue
                    },
                    Relationships = new Dictionary<string, RelationshipEntry>
                    {
                        ["populatedToManies"] = CreateRelationshipData("oneToManyDependents", true)
                    }
                },
                new()
                {
                    Type = "oneToManyDependents",
                    Id = "10",
                    Attributes = new Dictionary<string, object>
                    {
                        ["attributeMember"] = nestedIncludedAttributeValue
                    },
                    Relationships = new Dictionary<string, RelationshipEntry>
                    {
                        ["principal"] = CreateRelationshipData("oneToManyPrincipals")
                    }
                },
                new()
                {
                    Type = "oneToManyPrincipals",
                    Id = "10",
                    Attributes = new Dictionary<string, object>
                    {
                        ["attributeMember"] = deeplyNestedIncludedAttributeValue
                    }
                }
            };

            string body = JsonConvert.SerializeObject(content);

            // Act
            ManyResponse<MultipleRelationshipsPrincipalPart> result = _deserializer.DeserializeMany<MultipleRelationshipsPrincipalPart>(body);
            MultipleRelationshipsPrincipalPart resource = result.Data.First();

            // Assert
            Assert.Equal(1, resource.Id);
            MultipleRelationshipsPrincipalPart included = resource.Multi;
            Assert.Equal(10, included.Id);
            Assert.Equal(includedAttributeValue, included.AttributeMember);
            OneToManyDependent nestedIncluded = included.PopulatedToManies.First();
            Assert.Equal(10, nestedIncluded.Id);
            Assert.Equal(nestedIncludedAttributeValue, nestedIncluded.AttributeMember);
            OneToManyPrincipal deeplyNestedIncluded = nestedIncluded.Principal;
            Assert.Equal(10, deeplyNestedIncluded.Id);
            Assert.Equal(deeplyNestedIncludedAttributeValue, deeplyNestedIncluded.AttributeMember);
        }

        [Fact]
        public void DeserializeSingle_ResourceWithInheritanceAndInclusions_CanDeserialize()
        {
            // Arrange
            Document content = CreateDocumentWithRelationships("testResourceWithAbstractRelationships");
            content.SingleData.Relationships.Add("toMany", CreateRelationshipData("firstDerivedModels", true));
            content.SingleData.Relationships["toMany"].ManyData.Add(CreateRelationshipData("secondDerivedModels", id: "11").SingleData);
            content.SingleData.Relationships.Add("toOne", CreateRelationshipData("firstDerivedModels", id: "20"));

            content.Included = new List<ResourceObject>
            {
                new()
                {
                    Type = "firstDerivedModels",
                    Id = "10",
                    Attributes = new Dictionary<string, object>
                    {
                        ["firstProperty"] = "true"
                    }
                },
                new()
                {
                    Type = "secondDerivedModels",
                    Id = "11",
                    Attributes = new Dictionary<string, object>
                    {
                        ["secondProperty"] = "false"
                    }
                },
                new()
                {
                    Type = "firstDerivedModels",
                    Id = "20",
                    Attributes = new Dictionary<string, object>
                    {
                        ["firstProperty"] = "true"
                    }
                }
            };

            string body = JsonConvert.SerializeObject(content);

            // Act
            SingleResponse<TestResourceWithAbstractRelationship> result = _deserializer.DeserializeSingle<TestResourceWithAbstractRelationship>(body);
            TestResourceWithAbstractRelationship resource = result.Data;

            // Assert
            Assert.Equal(1, resource.Id);
            Assert.NotNull(resource.ToOne);
            Assert.True(resource.ToOne is FirstDerivedModel);
            Assert.True(((FirstDerivedModel)resource.ToOne).FirstProperty);

            Assert.NotEmpty(resource.ToMany);
            BaseModel first = resource.ToMany[0];
            Assert.True(first is FirstDerivedModel);
            Assert.True(((FirstDerivedModel)first).FirstProperty);

            BaseModel second = resource.ToMany[1];
            Assert.True(second is SecondDerivedModel);
            Assert.False(((SecondDerivedModel)second).SecondProperty);
        }
    }
}
