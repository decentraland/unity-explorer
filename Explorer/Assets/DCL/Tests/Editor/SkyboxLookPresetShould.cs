using DCL.SkyBox;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DCL.Tests
{
    [TestFixture]
    public class SkyboxLookPresetShould
    {
        private const float TOLERANCE = 1e-4f;

        private SkyboxLookPreset preset;

        [SetUp]
        public void SetUp()
        {
            preset = ScriptableObject.CreateInstance<SkyboxLookPreset>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(preset);
        }

        [TestCase(0f, 1f)]
        [TestCase(0.25f, 2f)]
        [TestCase(0.5f, 3f)]
        [TestCase(0.75f, 4f)]
        [TestCase(0.875f, 2.5f)]
        [TestCase(1f, 1f)]
        public void EvaluateByPhaseInterpolatesAnchorsAndWrapsBackToNight(float phase, float expected) =>
            Assert.That(SkyboxLookPreset.EvaluateByPhase(new Vector4(1f, 2f, 3f, 4f), phase), Is.EqualTo(expected).Within(TOLERANCE));

        [Test]
        public void EvaluatePhaseIsIdentityByDefault() =>
            Assert.That(preset.EvaluatePhase(0.3f), Is.EqualTo(0.3f).Within(TOLERANCE));

        [Test]
        public void EvaluatePhaseFallsBackToIdentityWhenTheCurveHasNoKeys()
        {
            SetTimeToPhase(new AnimationCurve());

            Assert.That(preset.EvaluatePhase(0.3f), Is.EqualTo(0.3f).Within(TOLERANCE));
        }

        [Test]
        public void EvaluatePhaseClampsTheCurveToTheUnitRange()
        {
            SetTimeToPhase(AnimationCurve.Linear(0f, -1f, 1f, 2f));

            Assert.That(preset.EvaluatePhase(0f), Is.EqualTo(0f).Within(TOLERANCE));
            Assert.That(preset.EvaluatePhase(1f), Is.EqualTo(1f).Within(TOLERANCE));
        }

        [Test]
        public void ActiveLensFlareEntryPicksTheLatestStartAtOrBeforeTheTime()
        {
            List<SkyboxLookPreset.LensFlareTimeEntry> entries = Entries(0.5f, 0.2f, 0.8f);

            Assert.That(SkyboxLookPreset.ActiveLensFlareEntry(entries, 0.6f), Is.SameAs(entries[0]));
            Assert.That(SkyboxLookPreset.ActiveLensFlareEntry(entries, 0.2f), Is.SameAs(entries[1]));
            Assert.That(SkyboxLookPreset.ActiveLensFlareEntry(entries, 0.95f), Is.SameAs(entries[2]));
        }

        [Test]
        public void ActiveLensFlareEntryWrapsToTheLatestEntryBeforeTheFirstStart()
        {
            List<SkyboxLookPreset.LensFlareTimeEntry> entries = Entries(0.5f, 0.2f, 0.8f);

            Assert.That(SkyboxLookPreset.ActiveLensFlareEntry(entries, 0.1f), Is.SameAs(entries[2]));
        }

        [Test]
        public void ActiveLensFlareEntryIsNullWithoutEntries() =>
            Assert.That(SkyboxLookPreset.ActiveLensFlareEntry(new List<SkyboxLookPreset.LensFlareTimeEntry>(), 0.5f), Is.Null);

        private void SetTimeToPhase(AnimationCurve curve)
        {
            var serialized = new SerializedObject(preset);
            serialized.FindProperty("timeToPhase").animationCurveValue = curve;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static List<SkyboxLookPreset.LensFlareTimeEntry> Entries(params float[] startTimes)
        {
            var entries = new List<SkyboxLookPreset.LensFlareTimeEntry>(startTimes.Length);

            for (var i = 0; i < startTimes.Length; i++)
                entries.Add(new SkyboxLookPreset.LensFlareTimeEntry { StartTime = startTimes[i] });

            return entries;
        }
    }
}
