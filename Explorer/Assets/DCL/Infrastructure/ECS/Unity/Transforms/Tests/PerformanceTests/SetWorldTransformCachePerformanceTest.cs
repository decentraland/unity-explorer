using ECS.Unity.Transforms.Components;
using NUnit.Framework;
using Unity.PerformanceTesting;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ECS.Unity.Transforms.Tests.PerformanceTests
{
    /// <summary>
    /// Guards and benchmarks the world-space cache shortcut in
    /// <see cref="TransformComponent.SetWorldTransform"/>. The fix replaces the two native
    /// <c>Transform.position</c>/<c>Transform.rotation</c> readbacks (via
    /// <c>UpdateCache()</c> → <c>CachedTransform.Update(Transform)</c>) with a direct assignment of
    /// the method arguments, on the premise that <c>Transform.SetPositionAndRotation</c> is
    /// world-authoritative so the resulting world pose equals the arguments.
    /// <para>
    /// Falsification: if that premise is wrong, the arg-assigned cache diverges from the true native
    /// world pose and <see cref="SetWorldTransform_UnparentedCacheMatchesNativeWorldPose"/> fails.
    /// <see cref="SetTransform_ParentedLocalOverloadStillReadsBackWorldPose"/> guards that the sibling
    /// local-space overload is NOT given the same shortcut (its world pose does not equal its local
    /// arguments under parenting, so it genuinely needs the readback).
    /// </para>
    /// </summary>
    [Category("Performance")]
    public class SetWorldTransformCachePerformanceTest
    {
        private const float POS_EPS = 1e-4f;     // metres
        private const float ROT_EPS_DEG = 0.001f; // degrees

        private GameObject? go;
        private GameObject? parentGo;

        [TearDown]
        public void TearDown()
        {
            if (go != null)
                Object.DestroyImmediate(go);

            if (parentGo != null)
                Object.DestroyImmediate(parentGo);

            go = null;
            parentGo = null;
        }

        // FALSIFIER: the arg-assigned cache must equal the native world pose the Transform actually
        // stores. For an unparented, world-authoritative write these are identical; any divergence
        // beyond epsilon means the shortcut is unsound.
        [Test]
        public void SetWorldTransform_UnparentedCacheMatchesNativeWorldPose()
        {
            go = new GameObject("WorldTransformTarget");
            var component = new TransformComponent(go.transform);

            Random.InitState(1234);

            float maxPosDev = 0f;
            float maxRotDev = 0f;

            for (int i = 0; i < 10000; i++)
            {
                Vector3 worldPos = Random.insideUnitSphere * 500f;
                Quaternion worldRot = Random.rotationUniform;
                Vector3 localScale = new Vector3(
                    Random.Range(0.1f, 4f),
                    Random.Range(0.1f, 4f),
                    Random.Range(0.1f, 4f));

                component.SetWorldTransform(worldPos, worldRot, localScale);

                // Cached values under test vs the authoritative native readback.
                float posDev = (component.Cached.WorldPosition - go.transform.position).magnitude;
                float rotDev = Quaternion.Angle(component.Cached.WorldRotation, go.transform.rotation);

                if (posDev > maxPosDev) maxPosDev = posDev;
                if (rotDev > maxRotDev) maxRotDev = rotDev;
            }

            component.Dispose();

            Assert.That(maxPosDev, Is.LessThanOrEqualTo(POS_EPS),
                $"Cached world position drifted from native readback by {maxPosDev} m (> {POS_EPS}) — the world-authoritative arg-assignment shortcut is unsound.");
            Assert.That(maxRotDev, Is.LessThanOrEqualTo(ROT_EPS_DEG),
                $"Cached world rotation drifted from native readback by {maxRotDev} deg (> {ROT_EPS_DEG}).");
        }

        // No accumulating drift when the same pose is written repeatedly.
        [Test]
        public void SetWorldTransform_RepeatedWritesDoNotAccumulateDrift()
        {
            go = new GameObject("WorldTransformStable");
            var component = new TransformComponent(go.transform);

            var worldPos = new Vector3(123.456f, -78.9f, 42.0f);
            Quaternion worldRot = Quaternion.Euler(37f, 211f, 5f);

            for (int i = 0; i < 5000; i++)
                component.SetWorldTransform(worldPos, worldRot, Vector3.one);

            float posDev = (component.Cached.WorldPosition - go.transform.position).magnitude;
            float rotDev = Quaternion.Angle(component.Cached.WorldRotation, go.transform.rotation);

            component.Dispose();

            Assert.That(posDev, Is.LessThanOrEqualTo(POS_EPS), $"Position drift after 5000 identical writes: {posDev} m.");
            Assert.That(rotDev, Is.LessThanOrEqualTo(ROT_EPS_DEG), $"Rotation drift after 5000 identical writes: {rotDev} deg.");
        }

        // GUARD: the local-space overload must keep its readback — under a rotated + non-uniformly
        // scaled parent the resulting WORLD pose is NOT the local arguments, so the cache can only be
        // correct if it reads Transform back. Fails if the shortcut is ever wrongly copied here.
        [Test]
        public void SetTransform_ParentedLocalOverloadStillReadsBackWorldPose()
        {
            parentGo = new GameObject("Parent");
            parentGo.transform.SetPositionAndRotation(new Vector3(10f, 5f, -3f), Quaternion.Euler(20f, 45f, 10f));
            parentGo.transform.localScale = new Vector3(1f, 2f, 3f);

            go = new GameObject("Child");
            go.transform.SetParent(parentGo.transform, false);

            var component = new TransformComponent(go.transform);

            var localPos = new Vector3(4f, 1f, 2f);
            Quaternion localRot = Quaternion.Euler(15f, 90f, 0f);

            component.SetTransform(localPos, localRot, Vector3.one);

            // World pose differs from the local args under this parent; the cache must equal the
            // native world readback, proving the local overload was left on the readback path.
            float posDev = (component.Cached.WorldPosition - go.transform.position).magnitude;
            float rotDev = Quaternion.Angle(component.Cached.WorldRotation, go.transform.rotation);
            float worldVsLocal = (go.transform.position - localPos).magnitude;

            component.Dispose();

            Assert.That(worldVsLocal, Is.GreaterThan(POS_EPS),
                "Fixture invalid: parented world pose should differ from local args.");
            Assert.That(posDev, Is.LessThanOrEqualTo(POS_EPS),
                $"Local-overload cached world position off by {posDev} m — the readback was removed from SetTransform.");
            Assert.That(rotDev, Is.LessThanOrEqualTo(ROT_EPS_DEG),
                $"Local-overload cached world rotation off by {rotDev} deg.");
        }

        // Throughput of the world-space write: the metric the fix moves (2 native getters removed
        // per call). Records ns/call + GC so a regression is visible.
        [Test]
        [Performance]
        public void SetWorldTransform_Throughput()
        {
            go = new GameObject("WorldTransformBench");
            var component = new TransformComponent(go.transform);

            var worldPos = new Vector3(12f, 3.4f, -56.7f);
            Quaternion worldRot = Quaternion.Euler(11f, 22f, 33f);
            var scale = Vector3.one;

            Measure
               .Method(() =>
                {
                    for (int i = 0; i < 100000; i++)
                        component.SetWorldTransform(worldPos, worldRot, scale);
                })
               .WarmupCount(5)
               .MeasurementCount(30)
               .GC()
               .Run();

            // Observe the cache so the benchmarked calls cannot be dead-code-eliminated.
            Assert.That(float.IsFinite(component.Cached.WorldPosition.x)
                        && float.IsFinite(component.Cached.WorldRotation.w));

            component.Dispose();
        }
    }
}
