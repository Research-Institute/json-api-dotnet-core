using System.Collections.Generic;
using JsonApiDotNetCore;
using JsonApiDotNetCore.Configuration;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using JsonApiDotNetCore.Serialization;
using JsonApiDotNetCore.Serialization.Objects;
using Microsoft.AspNetCore.Http;
using Moq;

namespace UnitTests.Serialization
{
    public class DeserializerTestsSetup : SerializationTestsSetupBase
    {
        protected Mock<IHttpContextAccessor> MockHttpContextAccessor { get; }

        protected DeserializerTestsSetup()
        {
            MockHttpContextAccessor = new Mock<IHttpContextAccessor>();
            MockHttpContextAccessor.Setup(mock => mock.HttpContext).Returns(new DefaultHttpContext());
        }

        protected Document CreateDocumentWithRelationships(string primaryType, string relationshipMemberName, string relatedType = null,
            bool isToManyData = false)
        {
            Document content = CreateDocumentWithRelationships(primaryType);
            content.SingleData.Relationships.Add(relationshipMemberName, CreateRelationshipData(relatedType, isToManyData));
            return content;
        }

        protected Document CreateDocumentWithRelationships(string primaryType)
        {
            return new()
            {
                Data = new ResourceObject
                {
                    Id = "1",
                    Type = primaryType,
                    Relationships = new Dictionary<string, RelationshipEntry>()
                }
            };
        }

        protected RelationshipEntry CreateRelationshipData(string relatedType = null, bool isToManyData = false, string id = "10")
        {
            var entry = new RelationshipEntry();

            ResourceIdentifierObject rio = relatedType == null
                ? null
                : new ResourceIdentifierObject
                {
                    Id = id,
                    Type = relatedType
                };

            if (isToManyData)
            {
                entry.Data = relatedType != null ? rio.AsList() : new List<ResourceIdentifierObject>();
            }
            else
            {
                entry.Data = rio;
            }

            return entry;
        }

        protected Document CreateTestResourceDocument()
        {
            return new()
            {
                Data = new ResourceObject
                {
                    Type = "testResource",
                    Id = "1",
                    Attributes = new Dictionary<string, object>
                    {
                        ["stringField"] = "some string",
                        ["intField"] = 1,
                        ["nullableIntField"] = null,
                        ["guidField"] = "1a68be43-cc84-4924-a421-7f4d614b7781",
                        ["dateTimeField"] = "9/11/2019 11:41:40 AM"
                    }
                }
            };
        }

        protected sealed class TestDeserializer : BaseDeserializer
        {
            public TestDeserializer(IResourceGraph resourceGraph, IResourceFactory resourceFactory)
                : base(resourceGraph, resourceFactory)
            {
            }

            public object Deserialize(string body)
            {
                return DeserializeBody(body);
            }

            protected override void AfterProcessField(IIdentifiable resource, ResourceFieldAttribute field, RelationshipEntry data = null)
            {
            }
        }
    }
}
