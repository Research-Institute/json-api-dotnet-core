using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceInheritance
{
    public abstract class Person : Identifiable
    {
        [Attr]
        public bool Retired { get; set; }
        
        [HasOne]
        public Animal Pet { get; set; }
        
        [HasMany]
        public List<Person> Parents { get; set; }
        
        [NotMapped]
        [HasManyThrough(nameof(ContentPersons))]
        public List<Content> FavoriteContent { get; set; }
        
        public List<ContentPerson> ContentPersons { get; set; }
    }
}
    

    

