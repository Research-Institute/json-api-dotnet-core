﻿using System.Collections.Generic;
using System.Linq;
using JsonApiDotNetCore.Internal.Contracts;
using JsonApiDotNetCore.Managers.Contracts;
using JsonApiDotNetCore.Models;
using JsonApiDotNetCore.Query;
using JsonApiDotNetCore.Serialization.Server.Builders;

namespace JsonApiDotNetCore.Serialization.Server
{
    public class ResponseResourceObjectBuilder : BaseResourceObjectBuilder, IResourceObjectBuilder
    {
        private RelationshipAttribute _requestRelationship;
        private readonly IIncludedResourceObjectBuilder _includedBuilder;
        private readonly IIncludeService _includeService;
        private readonly ILinkBuilder _linkBuilder;

        public ResponseResourceObjectBuilder(ILinkBuilder linkBuilder,
                                             IIncludedResourceObjectBuilder includedBuilder,
                                             IIncludeService includeService,
                                             IResourceGraph resourceGraph,
                                             ICurrentRequest currentRequest,
                                             IContextEntityProvider provider,
                                             IResourceObjectBuilderSettingsProvider settingsProvider)
            : base(resourceGraph, provider, settingsProvider.Get())
        {
            _linkBuilder = linkBuilder;
            _includedBuilder = includedBuilder;
            _includeService = includeService;
            if (currentRequest?.RequestRelationship != null && currentRequest.IsRelationshipPath)
                _requestRelationship = currentRequest.RequestRelationship;
        }

        /// <summary>
        /// Builds the values of the relationships object on a resource object.
        /// The server serializer only populates the "data" member when the relationship is included,
        /// and adds links unless these are turned off. This means that if a relationship is not included
        /// and links are turned off, the entry would be completely empty, ie { }, which is not conform
        /// json:api spec. In that case we return null which will omit the entry from the output.
        /// </summary>
        protected override RelationshipEntry GetRelationshipData(RelationshipAttribute relationship, IIdentifiable entity)
        {
            RelationshipEntry relationshipData = new RelationshipEntry();
            if (relationship == _requestRelationship)
            {   // if serializing a request with a requestRelationship, always populate the data field.
                relationshipData.Data = GetRelatedResourceLinkage(relationship, entity);
            }
            else if (ShouldInclude(relationship, out var relationshipChains))
            {   // if the relationship is included, populate the "data" field.
                relationshipData.Data = GetRelatedResourceLinkage(relationship, entity);
                if (relationshipData.HasResource)
                    foreach (var chain in relationshipChains)
                        // traverses (recursively)  and extracts all (nested) related entities for the current inclusion chain.
                        _includedBuilder.IncludeRelationshipChain(chain, entity);
            }

            var links = _linkBuilder.GetRelationshipLinks(relationship, entity);
            if (links != null)
                // if links relationshiplinks should be built for this entry, populate the "links" field.
                relationshipData.Links = links;

            /// if neither "links" nor "data" was popupated, return null, which will omit this entry from the output.
            /// (see the NullValueHandling settings on <see cref="ResourceObject"/>)
            return relationshipData.IsPopulated ? relationshipData : null;
        }

        /// <summary>
        /// Inspects the included relationship chains (see <see cref="IIncludeService"/>
        /// to see if <paramref name="relationship"/> should be included or not.
        /// </summary>
        private bool ShouldInclude(RelationshipAttribute relationship, out List<List<RelationshipAttribute>> inclusionChain)
        {
            inclusionChain = _includeService.Get()?.Where(l => l.First().Equals(relationship)).ToList();
            if (inclusionChain == null || !inclusionChain.Any())
                return false;
            return true;
        }
    }
}
