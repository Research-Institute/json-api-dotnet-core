using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace JsonApiDotNetCore.Configuration
{
    /// <summary>
    /// Service that allows customization of the dynamic controller generation. Register a
    /// custom implementation before calling <see cref="ServiceCollectionExtensions.AddJsonApi"/>
    /// in order to override the default controller generation behaviour.
    /// </summary>
    public interface IJsonApiControllerGenerator : IApplicationFeatureProvider<ControllerFeature> { }
}
