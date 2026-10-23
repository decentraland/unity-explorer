using NUnit.Framework;
using System.Reflection;

namespace UnityEngine.Shim.Tests
{
    public class ShimAssemblyShould
    {
        private const string SHIM_ASSEMBLY_NAME = "UnityEngine.Shim";

        [Test]
        public void NotReferenceAnyUnityAssembly()
        {
            // Arrange
            Assembly shim = Assembly.Load(SHIM_ASSEMBLY_NAME);

            // Act
            AssemblyName[] references = shim.GetReferencedAssemblies();

            // Assert
            foreach (AssemblyName reference in references)
                Assert.That(reference.Name, Does.Not.StartWith("UnityEngine").And.Not.StartWith("UnityEditor"),
                    $"{SHIM_ASSEMBLY_NAME} must run without Unity in the room (plan § 7, step 6)");
        }
    }
}
