using System.Collections.Generic;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Middleware;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Serialization.Objects;
using Moq;
using Newtonsoft.Json;
using Xunit;

namespace UnitTests.Serialization.Server
{
    public sealed class RequestDeserializerTests : DeserializerTestsSetup
    {
        private readonly RequestDeserializer _deserializer;
        private readonly Mock<ITargetedFields> _fieldsManagerMock = new();
        private readonly Mock<IJsonApiRequest> _requestMock = new();

        public RequestDeserializerTests()
        {
            _deserializer = new RequestDeserializer(ResourceGraph, new TestResourceFactory(), _fieldsManagerMock.Object, MockHttpContextAccessor.Object,
                _requestMock.Object, new JsonApiOptions());
        }

        [Fact]
        public void DeserializeAttributes_VariousUpdatedMembers_RegistersTargetedFields()
        {
            // Arrange
            var attributesToUpdate = new HashSet<AttrAttribute>();
            var relationshipsToUpdate = new HashSet<RelationshipAttribute>();
            SetupFieldsManager(attributesToUpdate, relationshipsToUpdate);

            Document content = CreateTestResourceDocument();
            string body = JsonConvert.SerializeObject(content);

            // Act
            _deserializer.Deserialize(body);

            // Assert
            Assert.Equal(5, attributesToUpdate.Count);
            Assert.Empty(relationshipsToUpdate);
        }

        [Fact]
        public void DeserializeRelationships_MultipleDependentRelationships_RegistersUpdatedRelationships()
        {
            // Arrange
            var attributesToUpdate = new HashSet<AttrAttribute>();
            var relationshipsToUpdate = new HashSet<RelationshipAttribute>();
            SetupFieldsManager(attributesToUpdate, relationshipsToUpdate);

            Document content = CreateDocumentWithRelationships("multiPrincipals");
            content.SingleData.Relationships.Add("populatedToOne", CreateRelationshipData("oneToOneDependents"));
            content.SingleData.Relationships.Add("emptyToOne", CreateRelationshipData());
            content.SingleData.Relationships.Add("populatedToManies", CreateRelationshipData("oneToManyDependents", true));
            content.SingleData.Relationships.Add("emptyToManies", CreateRelationshipData(isToManyData: true));
            string body = JsonConvert.SerializeObject(content);

            // Act
            _deserializer.Deserialize(body);

            // Assert
            Assert.Equal(4, relationshipsToUpdate.Count);
            Assert.Empty(attributesToUpdate);
        }

        [Fact]
        public void DeserializeRelationships_MultiplePrincipalRelationships_RegistersUpdatedRelationships()
        {
            // Arrange
            var attributesToUpdate = new HashSet<AttrAttribute>();
            var relationshipsToUpdate = new HashSet<RelationshipAttribute>();
            SetupFieldsManager(attributesToUpdate, relationshipsToUpdate);

            Document content = CreateDocumentWithRelationships("multiDependents");
            content.SingleData.Relationships.Add("populatedToOne", CreateRelationshipData("oneToOnePrincipals"));
            content.SingleData.Relationships.Add("emptyToOne", CreateRelationshipData());
            content.SingleData.Relationships.Add("populatedToMany", CreateRelationshipData("oneToManyPrincipals"));
            content.SingleData.Relationships.Add("emptyToMany", CreateRelationshipData());
            string body = JsonConvert.SerializeObject(content);

            // Act
            _deserializer.Deserialize(body);

            // Assert
            Assert.Equal(4, relationshipsToUpdate.Count);
            Assert.Empty(attributesToUpdate);
        }

        private void SetupFieldsManager(HashSet<AttrAttribute> attributesToUpdate, HashSet<RelationshipAttribute> relationshipsToUpdate)
        {
            _fieldsManagerMock.Setup(fields => fields.Attributes).Returns(attributesToUpdate);
            _fieldsManagerMock.Setup(fields => fields.Relationships).Returns(relationshipsToUpdate);
        }
    }
}
