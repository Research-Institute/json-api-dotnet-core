using System;
using JsonApiDotNetCore.Configuration;
using Microsoft.AspNetCore.Http;
using Moq;

namespace UnitTests
{
    public sealed class TestScopedServiceProvider : IRequestScopedServiceProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock = new();

        public TestScopedServiceProvider(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public object GetService(Type serviceType)
        {
            if (serviceType == typeof(IHttpContextAccessor))
            {
                return _httpContextAccessorMock.Object;
            }

            return _serviceProvider.GetService(serviceType);
        }
    }
}
