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
                             // Resolving these two (Removing the default font from TMP Settings) makes development a pain, we can live with it for now
                            .Where(p => !(p.EndsWith("Assets/TextMesh Pro/Fonts & Materials/Inter-Regular SDF.asset") ||
                                          p.EndsWith("Assets/TextMesh Pro/Shaders/TMP_SDF.shader")))
                            .ToList();

            Assert.IsEmpty(unexpected,
                $"Unexpected duplicated addressable assets detected:\n{string.Join("\n", unexpected)}");
        }

        // issues reported related to transparencies:
        // https://github.com/decentraland/unity-explorer/issues/5286
        // https://github.com/decentraland/unity-explorer/issues/5247
        // TODO: enable this test once we properly solve the material references as addressables at MaterialsPlugin
        // [Test]
        // public void CheckSceneDuplicateAddressables()
        // {
        //     var settings = AddressableAssetSettingsDefaultObject.Settings;
        //
        //     // Create rule
        //     var rule = new CheckSceneDupeDependencies();
        //     var results = rule.RefreshAnalysis(settings);
        //
        //
        //     var unexpected = results
        //                     .Where(r => r.severity == MessageType.Warning)
        //                     .Select(r => r.resultName)
        //                      // This one is unavoidable :(
        //                     .Where(p => !p.EndsWith("Packages/com.unity.shadergraph/Editor/Resources/Shaders/FallbackError.shader"))
        //                     .ToList();
        //
        //     Assert.IsEmpty(unexpected,
        //         $"Unexpected duplicated addressable assets detected:\n{string.Join("\n", unexpected)}");
        // }
    }
}
