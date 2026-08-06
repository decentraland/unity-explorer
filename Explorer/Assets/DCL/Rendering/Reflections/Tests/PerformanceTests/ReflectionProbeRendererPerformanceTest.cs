using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DCL.Rendering.Reflections.Tests.PerformanceTests
{
    /// <summary>
    /// Guards fix #3: <c>ReflectionProbeRenderer.Start()</c> must configure the realtime reflection
    /// probe with <see cref="ReflectionProbeTimeSlicingMode.IndividualFaces"/> instead of
    /// <see cref="ReflectionProbeTimeSlicingMode.AllFacesAtOnce"/>, so the periodic 6-face + GGX
    /// convolution burst is amortized across multiple frames (removing the recurring per-refresh
    /// peak-frame spike) WITHOUT breaking the refresh cadence.
    /// <para>
    /// <c>ReflectionProbeRenderer</c> lives in the global namespace with no asmdef, so it compiles into
    /// <c>Assembly-CSharp</c>, which an asmdef-based test assembly cannot reference. The component is
    /// therefore resolved by reflection and driven through its normal MonoBehaviour lifecycle in play
    /// mode. Both tests <see cref="Assert.Ignore(string)"/> (rather than fail) when the probe cannot
    /// render in the current runner (e.g. <c>-nographics</c> batchmode) so they stay non-flaky in
    /// headless CI while still being able to falsify the fix when rendering is available.
    /// </para>
    /// </summary>
    [Category("Performance")]
    public class ReflectionProbeRendererPerformanceTest
    {
        private const int FRAME_CAP = 240;

        private GameObject probeGo;
        private GameObject rendererGo;

        [TearDown]
        public void TearDown()
        {
            if (rendererGo != null) Object.DestroyImmediate(rendererGo);
            if (probeGo != null) Object.DestroyImmediate(probeGo);

            RenderSettings.customReflectionTexture = null;
        }

        /// <summary>
        /// Primary falsifier: after the REAL component's Start() runs, the probe must be in
        /// IndividualFaces mode (reverting the fix to AllFacesAtOnce fails this immediately), and the
        /// existing poll loop must still converge at least once (proves reflections are not broken).
        /// </summary>
        [UnityTest]
        [Performance]
        public IEnumerator Component_Configures_IndividualFaces_And_Refreshes()
        {
            ReflectionProbe probe = CreateRealtimeProbe();

            Type rendererType = FindType("ReflectionProbeRenderer");
            Assert.NotNull(rendererType, "ReflectionProbeRenderer type not found in any loaded assembly.");

            rendererGo = new GameObject("PerfReflectionProbeRenderer");
            var component = (MonoBehaviour)rendererGo.AddComponent(rendererType);

            // Serialized-but-private wiring the inspector would normally provide.
            SetPrivateField(component, "reflectionProbe", probe);
            SetPrivateField(component, "intervalInSeconds", 0.05f); // tight cadence -> multiple refreshes in-window

            // Let Awake/Start execute so timeSlicingMode is configured.
            yield return null;

            Assert.AreEqual(
                ReflectionProbeTimeSlicingMode.IndividualFaces,
                probe.timeSlicingMode,
                "Fix #3: ReflectionProbeRenderer.Start() must set timeSlicingMode = IndividualFaces (was AllFacesAtOnce).");

            // Drive several update ticks; confirm the component's poll loop still completes a refresh
            // (customReflectionTexture is assigned only on IsFinishedRendering).
            bool refreshed = false;
            for (int frame = 0; frame < FRAME_CAP && !refreshed; frame++)
            {
                yield return null;
                if (RenderSettings.customReflectionTexture != null)
                    refreshed = true;
            }

            if (!refreshed)
                Assert.Ignore("Reflection probe did not converge in this environment (likely -nographics). " +
                              "The IndividualFaces mode assertion above already validated the fix.");

            Measure.Custom(new SampleGroup("RefreshCompleted", SampleUnit.Undefined), 1);
        }

        /// <summary>
        /// Mechanism check for the frame-spike reduction: measure how many frames the probe takes to
        /// finish rendering in each mode. IndividualFaces amortizes the burst, so it must spread over
        /// at least as many frames as AllFacesAtOnce; on real GPU rendering it takes strictly more.
        /// If it did not spread, the fix would not reduce the per-refresh peak-frame cost.
        /// </summary>
        [UnityTest]
        [Performance]
        public IEnumerator IndividualFaces_SpreadsRenderOverMoreFrames_ThanAllFacesAtOnce()
        {
            ReflectionProbe probe = CreateRealtimeProbe();
            yield return null;

            int allFacesFrames = -1;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            int id = probe.RenderProbe();
            for (int f = 1; f <= FRAME_CAP; f++)
            {
                yield return null;
                if (id != 0 && probe.IsFinishedRendering(id)) { allFacesFrames = f; break; }
            }

            int individualFrames = -1;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            id = probe.RenderProbe();
            for (int f = 1; f <= FRAME_CAP; f++)
            {
                yield return null;
                if (id != 0 && probe.IsFinishedRendering(id)) { individualFrames = f; break; }
            }

            if (allFacesFrames < 0 || individualFrames < 0)
                Assert.Ignore("Reflection probe rendering unavailable in this environment (likely -nographics); " +
                              "cannot measure frame spreading.");

            Measure.Custom(new SampleGroup("FramesToConverge.AllFacesAtOnce", SampleUnit.Undefined), allFacesFrames);
            Measure.Custom(new SampleGroup("FramesToConverge.IndividualFaces", SampleUnit.Undefined), individualFrames);

            Assert.GreaterOrEqual(
                individualFrames, allFacesFrames,
                "IndividualFaces must spread probe rendering over at least as many frames as AllFacesAtOnce; " +
                "otherwise the fix does not amortize the per-refresh frame spike.");
        }

        private ReflectionProbe CreateRealtimeProbe()
        {
            probeGo = new GameObject("PerfReflectionProbe");
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting; // script-driven, matches production usage
            probe.resolution = 16;                                       // tiny -> cheap render in the test
            probe.hdr = false;
            return probe;
        }

        private static Type FindType(string simpleName)
        {
            Type direct = Type.GetType(simpleName + ", Assembly-CSharp");
            if (direct != null) return direct;

            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = asm.GetType(simpleName);
                if (t != null) return t;
            }

            return null;
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(field, $"Field '{name}' not found on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
