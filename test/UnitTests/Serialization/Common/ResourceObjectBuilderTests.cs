using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Serialization.Building;
using JsonApiDotNetCore.Serialization.Objects;
using UnitTests.TestModels;
using Xunit;

namespace UnitTests.Serialization.Common
{
    public sealed class ResourceObjectBuilderTests : SerializerTestsSetup
    {
        private readonly ResourceObjectBuilder _builder;

        public ResourceObjectBuilderTests()
        {
            _builder = new ResourceObjectBuilder(ResourceGraph, new ResourceObjectBuilderSettings());
        }

        [Fact]
        public void ResourceToResourceObject_EmptyResource_CanBuild()
        {
            // Arrange
            var resource = new TestResource();

            // Act
            ResourceObject resourceObject = _builder.Build(resource);

            // Assert
            Assert.Null(resourceObject.Attributes);
            Assert.Null(resourceObject.Relationships);
            Assert.Null(resourceObject.Id);
            Assert.Equal("testResource", resourceObject.Type);
        }

        [Fact]
        public void ResourceToResourceObject_ResourceWithId_CanBuild()
        {
            // Arrange
            var resource = new TestResource
            {
                Id = 1
            };

            // Act
            ResourceObject resourceObject = _builder.Build(resource);

            // Assert
            Assert.Equal("1", resourceObject.Id);
            Assert.Null(resourceObject.Attributes);
            Assert.Null(resourceObject.Relationships);
            Assert.Equal("testResource", resourceObject.Type);
        }

        [Theory]
        [InlineData(null, null)]
        [InlineData("string field", 1)]
        public void ResourceToResourceObject_ResourceWithIncludedAttrs_CanBuild(string stringFieldValue, int? intFieldValue)
        {
            // Arrange
            var resource = new TestResource
            {
                StringField = stringFieldValue,
                NullableIntField = intFieldValue
            };

            IReadOnlyCollection<AttrAttribute> attrs = ResourceGraph.GetAttributes<TestResource>(tr => new
            {
                tr.StringField,
                tr.NullableIntField
            });

            // Act
            ResourceObject resourceObject = _builder.Build(resource, attrs);

            // Assert
            Assert.NotNull(resourceObject.Attributes);
            Assert.Equal(2, resourceObject.Attributes.Keys.Count);
            Assert.Equal(stringFieldValue, resourceObject.Attributes["stringField"]);
            Assert.Equal(intFieldValue, resourceObject.Attributes["nullableIntField"]);
        }

        [Fact]
        public void ResourceWithRelationshipsToResourceObject_EmptyResource_CanBuild()
        {
            // Arrange
            var resource = new MultipleRelationshipsPrincipalPart();

            // Act
            ResourceObject resourceObject = _builder.Build(resource);

            // Assert
            Assert.Null(resourceObject.Attributes);
            Assert.Null(resourceObject.Relationships);
            Assert.Null(resourceObject.Id);
            Assert.Equal("multiPrincipals", resourceObject.Type);
        }

        [Fact]
        public void ResourceWithRelationshipsToResourceObject_ResourceWithId_CanBuild()
        {
            // Arrange
            var resource = new MultipleRelationshipsPrincipalPart
            {
                PopulatedToOne = new OneToOneDependent
                {
                    Id = 10
                }
            };

            // Act
            ResourceObject resourceObject = _builder.Build(resource);

            // Assert
            Assert.Null(resourceObject.Attributes);
            Assert.Null(resourceObject.Relationships);
            Assert.Null(resourceObject.Id);
            Assert.Equal("multiPrincipals", resourceObject.Type);
        }

        [Fact]
        public void ResourceWithRelationshipsToResourceObject_WithIncludedRelationshipsAttributes_CanBuild()
        {
            // Arrange
            var resource = new MultipleRelationshipsPrincipalPart
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

            IReadOnlyCollection<RelationshipAttribute> relationships = ResourceGraph.GetRelationships<MultipleRelationshipsPrincipalPart>(tr => new
            {
                tr.PopulatedToManies,
                tr.PopulatedToOne,
                tr.EmptyToOne,
                tr.EmptyToManies
            });

            // Act
            ResourceObject resourceObject = _builder.Build(resource, relationships: relationships);

            // Assert
            Assert.Equal(4, resourceObject.Relationships.Count);
            Assert.Null(resourceObject.Relationships["emptyToOne"].Data);
            Assert.Empty((IList)resourceObject.Relationships["emptyToManies"].Data);
            var populatedToOneData = (ResourceIdentifierObject)resourceObject.Relationships["populatedToOne"].Data;
            Assert.NotNull(populatedToOneData);
            Assert.Equal("10", populatedToOneData.Id);
            Assert.Equal("oneToOneDependents", populatedToOneData.Type);
            var populatedToManiesData = (List<ResourceIdentifierObject>)resourceObject.Relationships["populatedToManies"].Data;
            Assert.Single(populatedToManiesData);
            Assert.Equal("20", populatedToManiesData.First().Id);
            Assert.Equal("oneToManyDependents", populatedToManiesData.First().Type);
        }

        [Fact]
        public void ResourceWithRelationshipsToResourceObject_DeviatingForeignKeyWhileRelationshipIncluded_IgnoresForeignKeyDuringBuild()
        {
            // Arrange
            var resource = new OneToOneDependent
            {
                Principal = new OneToOnePrincipal
                {
                    Id = 10
                },
                PrincipalId = 123
            };

            IReadOnlyCollection<RelationshipAttribute> relationships = ResourceGraph.GetRelationships<OneToOneDependent>(tr => tr.Principal);

            // Act
            ResourceObject resourceObject = _builder.Build(resource, relationships: relationships);

            // Assert
            Assert.Single(resourceObject.Relationships);
            Assert.NotNull(resourceObject.Relationships["principal"].Data);
            var ro = (ResourceIdentifierObject)resourceObject.Relationships["principal"].Data;
            Assert.Equal("10", ro.Id);
        }

        [Fact]
        public void ResourceWithRelationshipsToResourceObject_DeviatingForeignKeyAndNoNavigationWhileRelationshipIncluded_IgnoresForeignKeyDuringBuild()
        {
            // Arrange
            var resource = new OneToOneDependent
            {
                Principal = null,
                PrincipalId = 123
            };

            IReadOnlyCollection<RelationshipAttribute> relationships = ResourceGraph.GetRelationships<OneToOneDependent>(tr => tr.Principal);

            // Act
            ResourceObject resourceObject = _builder.Build(resource, relationships: relationships);

            // Assert
            Assert.Null(resourceObject.Relationships["principal"].Data);
        }

        [Fact]
        public void ResourceWithRequiredRelationshipsToResourceObject_DeviatingForeignKeyWhileRelationshipIncluded_IgnoresForeignKeyDuringBuild()
        {
            // Arrange
            var resource = new OneToOneRequiredDependent
            {
                Principal = new OneToOnePrincipal
                {
                    Id = 10
                },
                PrincipalId = 123
            };

            IReadOnlyCollection<RelationshipAttribute> relationships = ResourceGraph.GetRelationships<OneToOneRequiredDependent>(tr => tr.Principal);

            // Act
            ResourceObject resourceObject = _builder.Build(resource, relationships: relationships);

            // Assert
            Assert.Single(resourceObject.Relationships);
            Assert.NotNull(resourceObject.Relationships["principal"].Data);
            var ro = (ResourceIdentifierObject)resourceObject.Relationships["principal"].Data;
            Assert.Equal("10", ro.Id);
        }
    }
}
