// Editor-time smoke runner that exercises the full registration → cull/LOD →
// DrawMeshInstanced → camera-render → framebuffer-capture path with the real
// production tree prototype + profile. Designed to be invoked via
//   Unity -batchmode -projectPath ... -executeMethod \
//     GPUInstancerPro.Editor.RenderingSmokeRunner.RunHeadlessSmoke
// so the rendering can be verified end-to-end without VNC interaction. The
// resulting framebuffer is encoded to a PNG at /tmp/gpui-smoke.png and the
// stats are logged to the run log so a downstream agent can read both
// programmatically.

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using GPUInstancerPro;

namespace GPUInstancerPro.Editor
{
    public static class RenderingSmokeRunner
    {
        private const string PROFILE_PATH = "Assets/DCL/Landscape/Assets/GPUI/Profiles/Decentraland_TreeProfile.asset";
        private const string PROTOTYPE_PATH = "Assets/DCL/Landscape/Assets/TreeOptimisation/TreeOptimizationPrefabs/TreeOptimized_v01.prefab";
        private const string OUTPUT_PNG = "/tmp/gpui-smoke.png";

        // Menu hook so a developer can also fire it from the GUI.
        [MenuItem("GPUInstancerPro/Run Rendering Smoke (off-screen capture)")]
        public static void RunFromMenu() => RunHeadlessSmoke();

        public static void RunHeadlessSmoke()
        {
            try
            {
                Debug.Log("[GPUI Smoke] start");
                Debug.Log($"[GPUI Smoke] graphicsDeviceType={SystemInfo.graphicsDeviceType} graphicsDeviceName={SystemInfo.graphicsDeviceName}");
                Debug.Log($"[GPUI Smoke] activeRP={UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline?.GetType().FullName ?? "Built-in"}");
                Debug.Log($"[GPUI Smoke] qualityRP={UnityEngine.QualitySettings.renderPipeline?.GetType().FullName ?? "(QS:none)"}");
                var profile = AssetDatabase.LoadAssetAtPath<GPUIProfile>(PROFILE_PATH);
                var prototype = AssetDatabase.LoadAssetAtPath<GameObject>(PROTOTYPE_PATH);
                if (profile == null) { Fail($"profile not found: {PROFILE_PATH}"); return; }
                if (prototype == null) { Fail($"prototype not found: {PROTOTYPE_PATH}"); return; }

                // Build a tiny scene: directional light + sky-blue camera +
                // 36 trees in a 6×6 grid centred at origin.
                var lightGO = new GameObject("SmokeLight");
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                lightGO.transform.rotation = Quaternion.Euler(50, 30, 0);

                var camGO = new GameObject("SmokeCamera");
                var cam = camGO.AddComponent<Camera>();
                cam.transform.position = new Vector3(0, 6, -18);
                cam.transform.LookAt(new Vector3(0, 2, 0));
                cam.fieldOfView = 60f;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 2000f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.36f, 0.55f, 0.78f); // distinguishable sky

                var rootGO = new GameObject("LandscapeSmokeRoot");

                GPUICoreAPI.RegisterRenderer(rootGO.transform, prototype, profile, out int key);
                var matrices = new List<Matrix4x4>(36);
                for (int z = 0; z < 6; z++)
                for (int x = 0; x < 6; x++)
                    matrices.Add(Matrix4x4.TRS(new Vector3((x - 2.5f) * 3f, 0, (z - 2.5f) * 3f), Quaternion.identity, Vector3.one));
                GPUICoreAPI.SetTransformBufferData(key, matrices, 0, 0, matrices.Count);
                GPUICoreAPI.SetInstanceCount(key, matrices.Count);

                // Render to a fresh RenderTexture so we can ReadPixels() it.
                const int W = 1280, H = 720;
                var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { name = "GPUI_Smoke_RT" };
                rt.Create();
                cam.targetTexture = rt;

                // Drive our renderer for this camera and then submit Camera.Render().
                // The bootstrap also subscribes to Camera.onPreCull, but call
                // explicitly here so the order is deterministic regardless of
                // whether the bootstrap has fired (Editor menu invocation can
                // predate RuntimeInitializeOnLoad).
                // Render twice: SRP submission queues consumed during BeginCameraRendering,
                // and Graphics.RenderMeshInstanced is documented as effective for the
                // *next* render. Belt-and-braces — call our tick before each.
                GPUICoreAPI.TickForTests(cam);
                cam.Render();
                GPUICoreAPI.TickForTests(cam);
                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply(false);
                RenderTexture.active = prev;

                byte[] png = tex.EncodeToPNG();
                File.WriteAllBytes(OUTPUT_PNG, png);

                var stats = GPUICoreAPI.GetLastFrameStatsForTests();
                Debug.Log($"[GPUI Smoke] saved {OUTPUT_PNG} ({png.Length} bytes); " +
                          $"drawn={stats.totalInstancesDrawn} drawCalls={stats.drawCallCount} " +
                          $"distCull={stats.culledByDistance} frustumCull={stats.culledByFrustum} " +
                          $"lodSizeCull={stats.culledByLODSize} " +
                          $"lodHist=[{string.Join(",", stats.lodHistogram)}]");

                // Sample the centre pixel & report colour. If the centre is the sky
                // colour we drew nothing in front of the camera; if it's anything
                // else, instances were rasterised over the clear.
                Color centre = tex.GetPixel(W / 2, H / 2);
                Debug.Log($"[GPUI Smoke] centre pixel = ({centre.r:F2},{centre.g:F2},{centre.b:F2})  " +
                          $"sky was (0.36,0.55,0.78); difference = {ColorDistance(centre, cam.backgroundColor):F3}");

                // Multi-pixel sample: scan a horizontal strip across the frame
                // and report how many pixels deviate from the sky colour. If
                // anything renders we expect a sizable count.
                int nonSky = 0;
                int sampled = 0;
                for (int x = 0; x < W; x += 8)
                for (int y = H / 4; y <= 3 * H / 4; y += 16)
                {
                    var c = tex.GetPixel(x, y);
                    if (ColorDistance(c, cam.backgroundColor) > 0.05f) nonSky++;
                    sampled++;
                }
                Debug.Log($"[GPUI Smoke] non-sky pixels in mid-band = {nonSky} / {sampled}");

                cam.targetTexture = null;
                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(camGO);
                Object.DestroyImmediate(lightGO);
                Object.DestroyImmediate(rootGO);

                Debug.Log("[GPUI Smoke] ok");

                if (Application.isBatchMode) EditorApplication.Exit(0);
            }
            catch (System.Exception e)
            {
                Fail($"exception: {e}");
            }
        }

        private static float ColorDistance(Color a, Color b) =>
            Mathf.Sqrt((a.r - b.r) * (a.r - b.r) + (a.g - b.g) * (a.g - b.g) + (a.b - b.b) * (a.b - b.b));

        private static void Fail(string msg)
        {
            Debug.LogError($"[GPUI Smoke] FAIL: {msg}");
            if (Application.isBatchMode) EditorApplication.Exit(2);
        }
    }
}
