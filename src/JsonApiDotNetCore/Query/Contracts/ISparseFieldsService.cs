﻿using System.Collections.Generic;
using JsonApiDotNetCore.Models;

namespace JsonApiDotNetCore.Query
{
    /// <summary>
    /// Query service to access sparse field selection.
    /// </summary>
    public interface ISparseFieldsService
    {
        /// <summary>
        /// Gets the list of targeted fields. In a relationship is supplied,
        /// gets the list of targeted fields for that relationship.
        /// </summary>
        /// <param name="relationship"></param>
        /// <returns></returns>
        List<AttrAttribute> Get(RelationshipAttribute relationship = null);
        void Register(AttrAttribute selected, RelationshipAttribute relationship = null);
    }
}