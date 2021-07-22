using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using JetBrains.Annotations;
using JsonApiDotNetCore.Resources;
using JsonApiDotNetCore.Resources.Annotations;
using Microsoft.AspNetCore.Authentication;

namespace JsonApiDotNetCoreExampleTests.IntegrationTests.ResourceConstructorInjection
{
    [UsedImplicitly(ImplicitUseTargetFlags.Members)]
    public sealed class PostOffice : Identifiable
    {
        private readonly ISystemClock _systemClock;

        [Attr]
        public string Address { get; set; }

        [Attr(Capabilities = AttrCapabilities.AllowView)]
        [NotMapped]
        public bool IsOpen => IsWithinOperatingHours();

        [HasMany]
        public IList<GiftCertificate> GiftCertificates { get; set; }

        public PostOffice(InjectionDbContext injectionDbContext)
        {
            _systemClock = injectionDbContext.SystemClock;
        }

        private bool IsWithinOperatingHours()
        {
            DateTimeOffset currentTime = _systemClock.UtcNow;
            return currentTime.DayOfWeek is >= DayOfWeek.Monday and <= DayOfWeek.Friday && currentTime.Hour is >= 9 and <= 17;
        }
    }
}
