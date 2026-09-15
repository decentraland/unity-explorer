using System;
using UnityEngine;
using UnityEngine.Rendering;
using Utility;

namespace DCL.Chat.Teleport
{
    /// <summary>
    /// Reusable ribbons that spiral around the avatar and accelerate into the sky.
    /// </summary>
    internal sealed class GotoTeleportTrails : IDisposable
    {
        private const int TRAIL_COUNT = 24;
        private const int POINT_COUNT = 12;
        private readonly GameObject root;
        private readonly Material material;
        private readonly LineRenderer[] trails = new LineRenderer[TRAIL_COUNT];

        public GotoTeleportTrails(Shader shader)
        {
            root = new GameObject("Goto teleport trails");
            material = new Material(shader);

            for (var i = 0; i < TRAIL_COUNT; i++)
            {
                var trail = new GameObject($"Energy ribbon {i}");
                trail.transform.SetParent(root.transform, false);
                var line = trail.AddComponent<LineRenderer>();
                line.sharedMaterial = material;
                line.useWorldSpace = false;
                line.positionCount = POINT_COUNT;
                line.widthMultiplier = i % 3 == 0 ? 0.045f : 0.018f;
                line.startColor = new Color(0.05f, 0.6f, 1f, 0f);
                line.endColor = new Color(0.3f, 1f, 1f, 1f);
                line.shadowCastingMode = ShadowCastingMode.Off;
                line.receiveShadows = false;
                line.lightProbeUsage = LightProbeUsage.Off;
                line.reflectionProbeUsage = ReflectionProbeUsage.Off;
                trails[i] = line;
            }

            root.SetActive(false);
        }

        public void Dispose()
        {
            UnityObjectUtils.SafeDestroy(root);
            UnityObjectUtils.SafeDestroy(material);
        }

        public void Hide() =>
            root.SetActive(false);

        public void Apply(Vector3 origin, float elapsed)
        {
            root.SetActive(true);
            root.transform.position = origin;
            float launch = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.35f, 2.6f, elapsed));
            float alpha = Mathf.SmoothStep(0f, 1f, elapsed / 0.6f) * (1f - Mathf.InverseLerp(2.5f, 2.8f, elapsed));
            material.SetFloat("_Intensity", alpha);

            for (var i = 0; i < TRAIL_COUNT; i++)
            {
                float seed = i * 0.618034f;
                float head = Mathf.Repeat(seed + (elapsed * 0.55f), 1f);
                float radius = Mathf.Lerp(0.65f + (Mathf.Repeat(seed, 0.4f)), 0.12f, launch);

                for (var j = 0; j < POINT_COUNT; j++)
                {
                    float along = j / (float)(POINT_COUNT - 1);
                    float height = (head * 2.5f) + (along * Mathf.Lerp(0.5f, 5f, launch)) + (launch * launch * 12f);
                    float angle = (seed * Mathf.PI * 2f) + (elapsed * 2f) - (along * (1f - launch));
                    trails[i].SetPosition(j, new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius));
                }
            }
        }
    }
}
