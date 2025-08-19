using NUnit.Framework;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build.AnalyzeRules;

namespace DCL.AssetsProvision.Tests
{
    public class DuplicateAddressablesTest
    {
        [Test, Timeout(1000000)]
        public void CheckBundleDuplicateAddressables()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            // Create rule
            var rule = new CheckBundleDupeDependencies();
            var results = rule.RefreshAnalysis(settings);


            var unexpected = results
                            .Where(r => r.severity == MessageType.Warning)
                            .Select(r => r.resultName)
                            .ToList();

            Assert.IsEmpty(unexpected,
                $"Unexpected duplicated addressable assets detected:\n{string.Join("\n", unexpected)}");
        }

        [Test]
        public void CheckResourcesDuplicateAddressables()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            // Create rule
            var rule = new CheckResourcesDupeDependencies();
            var results = rule.RefreshAnalysis(settings);


            var unexpected = results
                            .Where(r => r.severity == MessageType.Warning)
                            .Select(r => r.resultName)
                            .ToList();

            Assert.IsEmpty(unexpected,
                $"Unexpected duplicated addressable assets detected:\n{string.Join("\n", unexpected)}");
        }

        [Test]
        public void CheckSceneDuplicateAddressables()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;

            // Create rule
            var rule = new CheckSceneDupeDependencies();
            var results = rule.RefreshAnalysis(settings);


            var unexpected = results
                            .Where(r => r.severity == MessageType.Warning)
                            .Select(r => r.resultName)
                             // This one is unavoidable :(
                            .Where(p => !p.EndsWith("Packages/com.unity.shadergraph/Editor/Resources/Shaders/FallbackError.shader"))
                            .ToList();

            Assert.IsEmpty(unexpected,
                $"Unexpected duplicated addressable assets detected:\n{string.Join("\n", unexpected)}");
        }
    }
}
