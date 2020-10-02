using System;
using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance.Models
{
    public class Cat : Animal
    {
        public Cat()
        {
            Feline = true;
        }

        [Attr]
        public bool ScaredOfDogs { get; set; }
    }
}
