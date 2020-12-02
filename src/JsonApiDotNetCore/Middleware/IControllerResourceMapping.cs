using System;

namespace JsonApiDotNetCore.Middleware
{
    /// <summary>
    /// Registry of which resource type is associated with which controller.
    /// </summary>
    public interface IControllerResourceMapping
    {
        /// <summary>
        /// Get the associated resource type for the provided controller name.
        /// </summary>
        Type GetResourceForEndpoint(string controllerName);
    }
}
