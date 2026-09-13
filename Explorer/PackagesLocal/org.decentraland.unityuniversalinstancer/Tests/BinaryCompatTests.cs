using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using GPUInstancerPro;

namespace GPUInstancerPro.Tests
{
    /// <summary>
    /// REQ-029..032: binary-compatibility hardening. The .asset and .prefab
    /// files under Assets/DCL/Landscape/Assets/GPUI/ reference these types by
    /// `m_Script` GUID; the C# call sites reference them by namespace-qualified
    /// type name. If either drifts, scenes/prefabs get "missing script" errors
    /// on import.
    ///
    /// REQ-029 — Public types live in the GPUInstancerPro namespace.
    /// REQ-030 — GPUICoreAPI exposes the static method shapes the DCL call
    ///            sites (LandscapePlugin, TreeData) are written against.
    /// REQ-031 — GPUIProfile has a public Vector2 minMaxDistance field + void
    ///            SetParameterBufferData() instance method.
    /// REQ-032 — `.cs.meta` GUIDs equal the m_Script GUIDs those .asset and
    ///            .prefab files carry, so their references resolve cleanly.
    /// </summary>
    [TestFixture]
    public class BinaryCompatTests
    {
        // Each GUID is the m_Script reference the live .asset / .prefab files
        // in this repo use for the type, and must equal the type's
        // Runtime/*.cs.meta guid so those references resolve on import:
        //   GPUIProfile        → Decentraland_TreeProfile.asset:12 + siblings
        //                        (Decentraland_DetailProfile.asset,
        //                        Decentraland_DetailTextureProfile.asset)
        //   GPUIRuntimeSettings→ GPUIRuntimeSettings.asset:12
        //   GPUIShaderBindings → GPUIShaderBindings.asset:12
        //   GPUITreeManager    → GPUIPRO_TreeManager.prefab
        //   GPUIDebuggerCanvas → GPUIDebuggerCanvas.prefab (m_Script on the
        //                        canvas-controller MonoBehaviour; the
        //                        0cd44c10... entry on the same prefab belongs
        //                        to a UI CanvasScaler, not us).
        //   GPUIDetailManager  → GPUIPRO_DetailsManager.prefab:44, the only
        //                        asset in this repo referencing that GUID.
        //                        GPUIPrefabManager is referenced by no asset,
        //                        so it carries its own generated GUID and is
        //                        not pinned here.
        private static readonly (Type type, string guid)[] PINNED_GUIDS =
        {
            (typeof(GPUIProfile),         "1ec256bd8e4c92f48b9b9221cc99020f"),
            (typeof(GPUIRuntimeSettings), "951c13b4dce928945a2e8bd7f5fd7928"),
            (typeof(GPUIShaderBindings),  "f01694a6d0ad91f4985ea0689f262c1b"),
            (typeof(GPUITreeManager),     "bc071a606fd17964d988019d4f90155e"),
            (typeof(GPUIDebuggerCanvas),  "a9f8d1ff44dcb5c4b944b77a33d6283e"),
            (typeof(GPUIDetailManager),   "0293089e577d0ef41bf2ac4c44548d66"),
        };

        // ---- REQ-029 ----
        [Test]
        public void AllPublicTypes_LiveInGPUInstancerProNamespace()
        {
            foreach (Type t in new[]
            {
                typeof(GPUICoreAPI),
                typeof(GPUIProfile),
                typeof(GPUIRuntimeSettings),
                typeof(GPUIShaderBindings),
                typeof(GPUIDebuggerCanvas),
                typeof(GPUIPrefabManager),
                typeof(GPUIDetailManager),
                typeof(GPUITreeManager),
            })
            {
                Assert.AreEqual("GPUInstancerPro", t.Namespace,
                    $"{t.Name} must live in GPUInstancerPro namespace (REQ-029)");
            }
        }

        // ---- REQ-030: GPUICoreAPI surface shape ----
        [Test]
        public void GPUICoreAPI_RegisterRenderer_HasExactSignature()
        {
            // Two public static RegisterRenderer overloads exist (the out-int
            // form used by the smoke runner and the awaitable form
            // LandscapePlugin awaits), so select by parameter types rather than
            // by name alone (which would be ambiguous).
            var syncOverload = typeof(GPUICoreAPI).GetMethod("RegisterRenderer",
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(Transform), typeof(GameObject), typeof(GPUIProfile), typeof(int).MakeByRefType() },
                null);
            Assert.NotNull(syncOverload, "RegisterRenderer(Transform, GameObject, GPUIProfile, out int) must be public static");
            var p = syncOverload.GetParameters();
            Assert.AreEqual(4, p.Length, "This RegisterRenderer overload takes 4 parameters");
            Assert.AreEqual(typeof(Transform), p[0].ParameterType, "param 0: Transform");
            Assert.AreEqual(typeof(GameObject), p[1].ParameterType, "param 1: GameObject");
            Assert.AreEqual(typeof(GPUIProfile), p[2].ParameterType, "param 2: GPUIProfile");
            Assert.IsTrue(p[3].IsOut, "param 3 must be out int");
            Assert.AreEqual(typeof(int).MakeByRefType(), p[3].ParameterType, "param 3 type");

            // The awaitable overload LandscapePlugin uses:
            //   await GPUICoreAPI.RegisterRenderer(root, prototype, profile)
            //     .rendererKey
            var asyncOverload = typeof(GPUICoreAPI).GetMethod("RegisterRenderer",
                BindingFlags.Public | BindingFlags.Static, null,
                new[] { typeof(Transform), typeof(GameObject), typeof(GPUIProfile) },
                null);
            Assert.NotNull(asyncOverload, "RegisterRenderer(Transform, GameObject, GPUIProfile) must be public static");
            Assert.AreEqual("rendererKey",
                asyncOverload.ReturnType.GetGenericArguments()[0].GetField("rendererKey")?.Name,
                "awaitable overload returns a result exposing an int rendererKey field");
        }

        [Test]
        public void GPUICoreAPI_SetInstanceCount_HasExactSignature()
        {
            var m = typeof(GPUICoreAPI).GetMethod("SetInstanceCount",
                BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(m);
            var p = m.GetParameters();
            Assert.AreEqual(2, p.Length);
            Assert.AreEqual(typeof(int), p[0].ParameterType);
            Assert.AreEqual(typeof(int), p[1].ParameterType);
            Assert.AreEqual(typeof(void), m.ReturnType);
        }

        [Test]
        public void GPUICoreAPI_SetTransformBufferData_HasExactSignature()
        {
            var m = typeof(GPUICoreAPI).GetMethod("SetTransformBufferData",
                BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(m);
            var p = m.GetParameters();
            Assert.AreEqual(5, p.Length);
            Assert.AreEqual(typeof(int), p[0].ParameterType);
            Assert.AreEqual(typeof(System.Collections.Generic.List<Matrix4x4>), p[1].ParameterType);
            Assert.AreEqual(typeof(int), p[2].ParameterType);
            Assert.AreEqual(typeof(int), p[3].ParameterType);
            Assert.AreEqual(typeof(int), p[4].ParameterType);
        }

        // ---- REQ-031: GPUIProfile surface ----
        [Test]
        public void GPUIProfile_minMaxDistance_IsPublicVector2Field()
        {
            var f = typeof(GPUIProfile).GetField("minMaxDistance");
            Assert.NotNull(f, "minMaxDistance must be a public field");
            Assert.AreEqual(typeof(Vector2), f.FieldType);
        }

        [Test]
        public void GPUIProfile_SetParameterBufferData_IsPublicVoidInstanceMethod()
        {
            var m = typeof(GPUIProfile).GetMethod("SetParameterBufferData",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(m);
            Assert.AreEqual(typeof(void), m.ReturnType);
            Assert.AreEqual(0, m.GetParameters().Length);
        }

        [Test]
        public void GPUIProfile_InheritsScriptableObject()
        {
            Assert.IsTrue(typeof(ScriptableObject).IsAssignableFrom(typeof(GPUIProfile)));
        }

        // ---- REQ-032: strict script-GUID pinning ----
        //
        // Every type's Runtime/*.cs.meta guid must equal the m_Script guid the
        // consuming .asset/.prefab files reference; otherwise those references
        // import as "missing script". Resolve each type's script path and
        // assert the asset DB maps it to the pinned guid.
        private static readonly (Type type, string path)[] SCRIPT_PATHS =
        {
            (typeof(GPUIProfile),         "Packages/org.decentraland.unityuniversalinstancer/Runtime/GPUIProfile.cs"),
            (typeof(GPUIRuntimeSettings), "Packages/org.decentraland.unityuniversalinstancer/Runtime/GPUIRuntimeSettings.cs"),
            (typeof(GPUIShaderBindings),  "Packages/org.decentraland.unityuniversalinstancer/Runtime/GPUIShaderBindings.cs"),
            (typeof(GPUITreeManager),     "Packages/org.decentraland.unityuniversalinstancer/Runtime/GPUITreeManager.cs"),
            (typeof(GPUIDebuggerCanvas),  "Packages/org.decentraland.unityuniversalinstancer/Runtime/GPUIDebuggerCanvas.cs"),
            // The file name must equal the type name or Unity emits no
            // MonoScript for it and the prefab's m_Script imports as missing,
            // whatever the .meta guid says.
            (typeof(GPUIDetailManager),   "Packages/org.decentraland.unityuniversalinstancer/Runtime/GPUIDetailManager.cs"),
        };

        [Test]
        public void ScriptMetaGuids_MatchPinnedValues()
        {
            foreach ((Type type, string path) in SCRIPT_PATHS)
            {
                string expected = System.Array.Find(PINNED_GUIDS, e => e.type == type).guid;
                string actual = AssetDatabase.AssetPathToGUID(path);
                Assert.AreEqual(expected, actual,
                    $"{type.Name} .cs.meta guid must stay pinned so .asset/.prefab m_Script references resolve (REQ-032)");
            }
        }

        // ---- REQ-032: end-to-end deserialization ----
        //
        // The user-visible contract behind the pinned GUIDs: the .asset files
        // the original package serviced still deserialize to a non-null
        // instance of our replacement type with the authored fields intact.
        [Test]
        public void DecentralandTreeProfile_DeserializesToGPUIProfile()
        {
            const string PATH = "Assets/DCL/Landscape/Assets/GPUI/Profiles/Decentraland_TreeProfile.asset";
            var loaded = AssetDatabase.LoadAssetAtPath<GPUIProfile>(PATH);
            Assert.IsNotNull(loaded, $"{PATH} must deserialize to GPUIProfile (script GUID must stay pinned)");
            // The field LandscapeData reads must carry the authored value.
            Assert.Greater(loaded.minMaxDistance.y, 0f,
                "Authored minMaxDistance.y should be the production cull distance (1000)");
        }

        [Test]
        public void DecentralandDetailProfile_DeserializesToGPUIProfile()
        {
            const string PATH = "Assets/DCL/Landscape/Assets/GPUI/Profiles/Decentraland_DetailProfile.asset";
            var loaded = AssetDatabase.LoadAssetAtPath<GPUIProfile>(PATH);
            Assert.IsNotNull(loaded, $"{PATH} must deserialize to GPUIProfile");
        }

        [Test]
        public void DecentralandDetailTextureProfile_DeserializesToGPUIProfile()
        {
            const string PATH = "Assets/DCL/Landscape/Assets/GPUI/Profiles/Decentraland_DetailTextureProfile.asset";
            var loaded = AssetDatabase.LoadAssetAtPath<GPUIProfile>(PATH);
            Assert.IsNotNull(loaded, $"{PATH} must deserialize to GPUIProfile");
        }
    }
}
