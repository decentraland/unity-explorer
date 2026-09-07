using DCL.SDKComponents.LightSource;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.GoldenCapture.Tests
{
    /// <summary>
    ///     A golden capture lights every active scene light at LOD 0 wherever the avatar stands, so the
    ///     pin has to lift every cap the scene-light budget derives from the character's position.
    /// </summary>
    public class SceneLightBudgetPinShould
    {
        private const float LIGHTS_PER_PARCEL = 2f;
        private const int HARD_MAX_LIGHTS = 8;
        private const int MAX_SHADOWS = 1;
        private const float SPOT_LOD1_DISTANCE = 30f;
        private const float POINT_LOD1_DISTANCE = 45f;

        // more lights per parcel than any scene can hold, so the parcel count never bounds the budget
        private const float UNBOUNDED_LIGHTS_PER_PARCEL = 1e5f;

        private LightSourceSettings settings = null!;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<LightSourceSettings>();

            settings.ApplyQualitySettings(
                new LightSourceSettings.SceneLimitationsSettings
                {
                    LightsPerParcel = LIGHTS_PER_PARCEL,
                    HardMaxLightCount = HARD_MAX_LIGHTS,
                    MaxPointLightShadows = MAX_SHADOWS,
                    MaxSpotLightShadows = MAX_SHADOWS,
                },
                Lods(10f, SPOT_LOD1_DISTANCE),
                Lods(15f, POINT_LOD1_DISTANCE));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void LiftEverySceneLightCap()
        {
            GoldenFreeze.PinSceneLightBudget(settings);

            Assert.That(settings.SceneLimitations.LightsPerParcel, Is.GreaterThanOrEqualTo(UNBOUNDED_LIGHTS_PER_PARCEL));
            Assert.That(settings.SceneLimitations.HardMaxLightCount, Is.EqualTo(int.MaxValue));
            Assert.That(settings.SceneLimitations.MaxPointLightShadows, Is.EqualTo(int.MaxValue));
            Assert.That(settings.SceneLimitations.MaxSpotLightShadows, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void ReachAnyDistanceAtLod0AndLeaveTheOtherLodsAlone()
        {
            GoldenFreeze.PinSceneLightBudget(settings);

            Assert.That(settings.SpotLightsLods[0].Distance, Is.EqualTo(float.MaxValue));
            Assert.That(settings.PointLightsLods[0].Distance, Is.EqualTo(float.MaxValue));

            Assert.That(settings.SpotLightsLods, Has.Count.EqualTo(2));
            Assert.That(settings.PointLightsLods, Has.Count.EqualTo(2));
            Assert.That(settings.SpotLightsLods[1].Distance, Is.EqualTo(SPOT_LOD1_DISTANCE));
            Assert.That(settings.PointLightsLods[1].Distance, Is.EqualTo(POINT_LOD1_DISTANCE));
            Assert.That(settings.SpotLightsLods[0].Shadows, Is.EqualTo(LightShadows.Soft), "the pin touches only the LOD distance");
            Assert.That(settings.SpotLightsLods[0].ShadowMapResolution, Is.EqualTo(1024));
        }

        [Test]
        public void HoldTheSameValuesWhenAppliedEveryFrame()
        {
            GoldenFreeze.PinSceneLightBudget(settings);
            LightSourceSettings.SceneLimitationsSettings afterFirst = settings.SceneLimitations;
            float spotLod0 = settings.SpotLightsLods[0].Distance;

            GoldenFreeze.PinSceneLightBudget(settings);

            Assert.That(settings.SceneLimitations, Is.EqualTo(afterFirst));
            Assert.That(settings.SpotLightsLods[0].Distance, Is.EqualTo(spotLod0));
            Assert.That(settings.SpotLightsLods[1].Distance, Is.EqualTo(SPOT_LOD1_DISTANCE));
        }

        [Test]
        public void TolerateAnAssetWithoutLods()
        {
            settings.ApplyQualitySettings(settings.SceneLimitations, new List<LightSourceSettings.LodSettings>(), new List<LightSourceSettings.LodSettings>());

            Assert.That(() => GoldenFreeze.PinSceneLightBudget(settings), Throws.Nothing);
            Assert.That(settings.SceneLimitations.HardMaxLightCount, Is.EqualTo(int.MaxValue));
            Assert.That(settings.SpotLightsLods, Is.Empty);
        }

        [Test]
        public void RejectAnAssetWithoutTheBudgetShapeAndRecoverAfterwards()
        {
            var shapeless = ScriptableObject.CreateInstance<ShapelessSettings>();

            try
            {
                Assert.That(() => GoldenFreeze.PinSceneLightBudget(shapeless),
                    Throws.TypeOf<MissingFieldException>().With.Message.Contains("SceneLimitations"));

                // a rejected type leaves nothing cached, so the real asset resolves and pins again
                GoldenFreeze.PinSceneLightBudget(settings);
                Assert.That(settings.SceneLimitations.HardMaxLightCount, Is.EqualTo(int.MaxValue));
                Assert.That(settings.PointLightsLods[0].Distance, Is.EqualTo(float.MaxValue));
            }
            finally { Object.DestroyImmediate(shapeless); }
        }

        [Test]
        public void NameTheFirstFieldMissingFromAPartialShape()
        {
            var partial = ScriptableObject.CreateInstance<PartialSettings>();

            try
            {
                Assert.That(() => GoldenFreeze.PinSceneLightBudget(partial),
                    Throws.TypeOf<MissingFieldException>().With.Message.Contains("SpotLightsLods"),
                    "the rejection names the field that failed to resolve, not the first one the pin looks for");
            }
            finally { Object.DestroyImmediate(partial); }
        }

        private static List<LightSourceSettings.LodSettings> Lods(params float[] distances)
        {
            var lods = new List<LightSourceSettings.LodSettings>(distances.Length);

            foreach (float distance in distances)
                lods.Add(new LightSourceSettings.LodSettings { Distance = distance, Shadows = LightShadows.Soft, OverrideShadowMapResolution = true, ShadowMapResolution = 1024 });

            return lods;
        }

        private class ShapelessSettings : ScriptableObject { }

        // carries the scene caps but neither LOD list
        private class PartialSettings : ScriptableObject
        {
            public LightSourceSettings.SceneLimitationsSettings SceneLimitations;
        }
    }
}
