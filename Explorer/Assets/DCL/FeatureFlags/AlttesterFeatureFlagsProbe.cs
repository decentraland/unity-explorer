#if ALTTESTER
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace DCL.FeatureFlags
{
    /// <summary>
    ///     Feature flag state exposed to the AltTester suite via <c>AltDriver.CallStaticMethod</c>, so UI
    ///     tests read the values the client itself gates on instead of re-fetching the remote document and
    ///     re-deriving the evaluation rules. Compiles into the <c>DCL.Network</c> assembly.
    ///     Gated by the <c>ALTTESTER</c> compile define (stripped from release builds by <c>CloudBuild.cs</c>
    ///     when <c>IS_RELEASE_BUILD=true</c>), so the type is absent from shipping binaries.
    /// </summary>
    public static class AlttesterFeatureFlagsProbe
    {
        /// <summary>
        ///     Raw remote flag state, keyed without the <c>explorer-</c> prefix the server carries
        ///     (e.g. <c>alfa-marketplace-credits</c>).
        /// </summary>
        public static bool IsFlagEnabled(string flagId) =>
            FeatureFlagsConfiguration.Instance.IsEnabled(flagId);

        /// <summary>
        ///     Resolved <see cref="FeatureId"/> state, with the remote flag, app arguments and editor
        ///     overrides already folded together — this is what the UI gates on.
        /// </summary>
        /// <param name="featureId"><see cref="FeatureId"/> member name, case-insensitive.</param>
        /// <exception cref="ArgumentException">The name is not a <see cref="FeatureId"/> member.</exception>
        public static bool IsFeatureEnabled(string featureId) =>
            FeaturesRegistry.Instance.IsEnabled(ParseFeatureId(featureId));

        /// <summary>
        ///     The flag's variant and payload, for allowlist-style gating.
        ///     Shape: <c>{"present":true,"name":"wallets","enabled":true,"payloadType":"string","payloadValue":"0x1,0x2"}</c>.
        /// </summary>
        public static string GetFlagVariantJson(string flagId)
        {
            if (!FeatureFlagsConfiguration.Instance.TryGetVariant(flagId, out FeatureFlagVariantDto variant))
                return JsonConvert.SerializeObject(new { present = false });

            return JsonConvert.SerializeObject(new
            {
                present = true,
                name = variant.name,
                enabled = variant.enabled,
                payloadType = variant.payload.type,
                payloadValue = variant.payload.value,
            });
        }

        /// <summary>
        ///     Snapshot for failure diagnostics. Shape:
        ///     <c>{"flagsLoaded":true,"registryLoaded":true,"enabledFlags":["..."],"enabledFeatures":["..."]}</c>.
        ///     Never throws — a test calls this when something already went wrong.
        /// </summary>
        public static string GetStatusJson()
        {
            var enabledFlags = new List<string>();
            var flagsLoaded = true;

            try { enabledFlags.AddRange(FeatureFlagsConfiguration.Instance.AllEnabledFlags); }
            catch (Exception) { flagsLoaded = false; }

            var enabledFeatures = new List<string>();
            var registryLoaded = true;

            try
            {
                FeaturesRegistry registry = FeaturesRegistry.Instance;

                foreach (FeatureId id in Enum.GetValues(typeof(FeatureId)))
                {
                    if (id != FeatureId.None && registry.IsEnabled(id))
                        enabledFeatures.Add(id.ToString());
                }
            }
            catch (Exception) { registryLoaded = false; }

            return JsonConvert.SerializeObject(new
            {
                flagsLoaded,
                registryLoaded,
                enabledFlags,
                enabledFeatures,
            });
        }

        private static FeatureId ParseFeatureId(string featureId)
        {
            if (!Enum.TryParse(featureId, true, out FeatureId parsed) || !Enum.IsDefined(typeof(FeatureId), parsed))
                throw new ArgumentException($"'{featureId}' is not a {nameof(FeatureId)} member", nameof(featureId));

            return parsed;
        }
    }
}
#endif
