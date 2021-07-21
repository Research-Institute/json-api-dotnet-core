using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Serialization.Building;
using JsonApiDotNetCore.Serialization.Objects;
using UnitTests.TestModels;
using Xunit;

namespace UnitTests.Serialization.Server
{
    public sealed class ResponseResourceObjectBuilderTests : SerializerTestsSetup
    {
        private const string RelationshipName = "dependents";
        private readonly List<RelationshipAttribute> _relationshipsForBuild;

        public ResponseResourceObjectBuilderTests()
        {
            _relationshipsForBuild = ResourceGraph.GetRelationships<OneToManyPrincipal>(relationship => new
            {
                relationship.Dependents
            }).ToList();
        }

        [Fact]
        public void Build_RelationshipNotIncludedAndLinksEnabled_RelationshipEntryWithLinks()
        {
            // Arrange
            var resource = new OneToManyPrincipal
            {
                Id = 10
            };

            ResponseResourceObjectBuilder builder = GetResponseResourceObjectBuilder(relationshipLinks: DummyRelationshipLinks);

            // Act
            ResourceObject resourceObject = builder.Build(resource, relationships: _relationshipsForBuild);

            // Assert
            Assert.True(resourceObject.Relationships.TryGetValue(RelationshipName, out RelationshipEntry entry));
            Assert.Equal("http://www.dummy.com/dummy-relationship-self-link", entry.Links.Self);
            Assert.Equal("http://www.dummy.com/dummy-relationship-related-link", entry.Links.Related);
            Assert.False(entry.IsPopulated);
        }

        [Fact]
        public void Build_RelationshipNotIncludedAndLinksDisabled_NoRelationshipObject()
        {
            // Arrange
            var resource = new OneToManyPrincipal
            {
                Id = 10
            };

            ResponseResourceObjectBuilder builder = GetResponseResourceObjectBuilder();

            // Act
            ResourceObject resourceObject = builder.Build(resource, relationships: _relationshipsForBuild);

            // Assert
            Assert.Null(resourceObject.Relationships);
        }

        [Fact]
        public void Build_RelationshipIncludedAndLinksDisabled_RelationshipEntryWithData()
        {
            // Arrange
            var resource = new OneToManyPrincipal
            {
                Id = 10,
                Dependents = new HashSet<OneToManyDependent>
                {
                    new()
                    {
                        Id = 20
                    }
                }
            };

            ResponseResourceObjectBuilder builder = GetResponseResourceObjectBuilder(_relationshipsForBuild.AsEnumerable());

            // Act
            ResourceObject resourceObject = builder.Build(resource, relationships: _relationshipsForBuild);

            // Assert
            Assert.True(resourceObject.Relationships.TryGetValue(RelationshipName, out RelationshipEntry entry));
            Assert.Null(entry.Links);
            Assert.True(entry.IsPopulated);
            Assert.Equal("20", entry.ManyData.Single().Id);
        }

        [Fact]
        public void Build_RelationshipIncludedAndLinksEnabled_RelationshipEntryWithDataAndLinks()
        {
            // Arrange
            var resource = new OneToManyPrincipal
            {
                Id = 10,
                Dependents = new HashSet<OneToManyDependent>
                {
                    new()
                    {
                        Id = 20
                    }
                }
            };

            ResponseResourceObjectBuilder builder =
                GetResponseResourceObjectBuilder(_relationshipsForBuild.AsEnumerable(), relationshipLinks: DummyRelationshipLinks);

            // Act
            ResourceObject resourceObject = builder.Build(resource, relationships: _relationshipsForBuild);

            // Assert
            Assert.True(resourceObject.Relationships.TryGetValue(RelationshipName, out RelationshipEntry entry));
            Assert.Equal("http://www.dummy.com/dummy-relationship-self-link", entry.Links.Self);
            Assert.Equal("http://www.dummy.com/dummy-relationship-related-link", entry.Links.Related);
            Assert.True(entry.IsPopulated);
            Assert.Equal("20", entry.ManyData.Single().Id);
        }
    }
}
