using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace JsonApiDotNetCore.Configuration
{
    /// <summary>
    /// Used to scan assemblies for types and cache them, to facilitate resource auto-discovery.
    /// </summary>
    internal sealed class ResourceDescriptorAssemblyCache
    {
        private readonly TypeLocator _typeLocator = new();
        private readonly Dictionary<Assembly, IReadOnlyCollection<ResourceDescriptor>> _resourceDescriptorsPerAssembly = new();

        public void RegisterAssembly(Assembly assembly)
        {
            if (!_resourceDescriptorsPerAssembly.ContainsKey(assembly))
            {
                _resourceDescriptorsPerAssembly[assembly] = null;
            }
        }

        public IReadOnlyCollection<ResourceDescriptor> GetResourceDescriptors()
        {
            EnsureAssembliesScanned();

            return _resourceDescriptorsPerAssembly.SelectMany(pair => pair.Value).ToArray();
        }

        public IReadOnlyCollection<Assembly> GetAssemblies()
        {
            EnsureAssembliesScanned();

            return _resourceDescriptorsPerAssembly.Keys;
        }

        private void EnsureAssembliesScanned()
        {
            foreach (Assembly assemblyToScan in _resourceDescriptorsPerAssembly.Where(pair => pair.Value == null).Select(pair => pair.Key).ToArray())
            {
                _resourceDescriptorsPerAssembly[assemblyToScan] = ScanForResourceDescriptors(assemblyToScan).ToArray();
            }
        }

        private IEnumerable<ResourceDescriptor> ScanForResourceDescriptors(Assembly assembly)
        {
            foreach (Type type in assembly.GetTypes())
            {
                ResourceDescriptor resourceDescriptor = _typeLocator.TryGetResourceDescriptor(type);

                if (resourceDescriptor != null)
                {
                    yield return resourceDescriptor;
                }
            }
        }
    }
}
