using System;
using System.Collections.Generic;

namespace BevyMigration.Tools.ApiInventory
{
    /// <summary>
    ///     Assigns every Unity member to an allowlist group on its containing type, and every group to the plan
    ///     phase that must provide it: A rendering, B shim, C UI, D content, F deferred to step 11
    ///     (BevyMigration/docs/README.md § 6 and § 7). First matching rule wins.
    /// </summary>
    internal static class SubsystemGroups
    {
        public const string EDITOR = "editor";
        public const string OTHER = "other";

        private static readonly IReadOnlyList<Rule> RULES = new Rule[]
        {
            new ("ui", "C", Namespace("UnityEngine.UI"), Namespace("UnityEngine.UIElements"), Namespace("UnityEngine.EventSystems"), Namespace("TMPro"), Namespace("UnityEngine.TextCore"),
                Prefix("UnityEngine.Canvas"), Type("UnityEngine.RectTransform"), Type("UnityEngine.RectTransformUtility"), Type("UnityEngine.RectOffset"), Type("UnityEngine.Sprite"),
                Type("UnityEngine.TextAnchor"), Type("UnityEngine.RenderMode"), Type("UnityEngine.GUI"), Type("UnityEngine.GUILayout"), Type("UnityEngine.GUIStyle"), Type("UnityEngine.GUIContent"),
                Type("UnityEngine.GUISkin"), Type("UnityEngine.GUIUtility"), Type("UnityEngine.TextGenerator"), Type("UnityEngine.FontStyle"), Type("UnityEngine.Font"), Type("UnityEngine.CharacterInfo")),
            new ("localization", "C", Namespace("UnityEngine.Localization")),
            new ("testing", "B", Namespace("UnityEngine.TestTools"), Namespace("Unity.PerformanceTesting"), Namespace("Unity.Multiplayer.PlayMode")),
            new ("input", "B", Namespace("UnityEngine.InputSystem"), Type("UnityEngine.Input"), Type("UnityEngine.Cursor"), Type("UnityEngine.KeyCode"), Type("UnityEngine.CursorLockMode"), Prefix("UnityEngine.Touch")),
            new ("addressables", "D", Namespace("UnityEngine.AddressableAssets"), Namespace("UnityEngine.ResourceManagement")),
            new ("asset_bundles", "D", Prefix("UnityEngine.AssetBundle")),
            new ("cinemachine", "B", Namespace("Unity.Cinemachine"), Namespace("Cinemachine")),
            new ("animation", "B", Namespace("UnityEngine.Animations"), Namespace("UnityEngine.Playables"), Prefix("UnityEngine.Anim"), Prefix("UnityEngine.Avatar"), Type("UnityEngine.RuntimeAnimatorController"),
                Type("UnityEngine.HumanBodyBones"), Type("UnityEngine.Motion"), Type("UnityEngine.WrapMode"), Type("UnityEngine.Keyframe"), Type("UnityEngine.PlayMode"), Type("UnityEngine.QueueMode")),
            new ("physics", "B", Prefix("UnityEngine.Physic"), Prefix("UnityEngine.Collider"), Prefix("UnityEngine.Collision"), Prefix("UnityEngine.Rigidbody"), Prefix("UnityEngine.Raycast"), Prefix("UnityEngine.Joint"),
                Suffix("Collider"), Suffix("Joint"), Type("UnityEngine.Ray"), Type("UnityEngine.CharacterController"), Type("UnityEngine.ControllerColliderHit"), Type("UnityEngine.ForceMode"),
                Type("UnityEngine.QueryTriggerInteraction"), Type("UnityEngine.LayerMask"), Type("UnityEngine.ContactPoint"), Type("UnityEngine.SimulationMode"), Type("UnityEngine.ArticulationBody")),
            new ("audio", "B", Namespace("UnityEngine.Audio"), Prefix("UnityEngine.Audio"), Type("UnityEngine.Microphone")),
            new ("video", "A", Namespace("UnityEngine.Video")),
            new ("particles", "F", Namespace("UnityEngine.VFX"), Prefix("UnityEngine.ParticleSystem"), Prefix("UnityEngine.Particle")),
            new ("landscape", "F", Namespace("UnityEngine.TerrainTools"), Prefix("UnityEngine.Terrain"), Prefix("UnityEngine.Tree"), Prefix("UnityEngine.Detail"), Type("UnityEngine.WindZone")),
            new ("navigation", "F", Namespace("UnityEngine.AI")),
            new ("rendering", "A", Namespace("UnityEngine.Rendering"), Namespace("UnityEngine.Experimental.Rendering"), Suffix("Renderer"), Prefix("UnityEngine.Render"), Prefix("UnityEngine.Material"),
                Prefix("UnityEngine.Shader"), Prefix("UnityEngine.Keyword"), Type("UnityEngine.LocalKeyword"), Type("UnityEngine.GlobalKeyword"), Prefix("UnityEngine.Compute"), Prefix("UnityEngine.Graphics"),
                Prefix("UnityEngine.Mesh"), Prefix("UnityEngine.Texture"), Prefix("UnityEngine.Cubemap"), Prefix("UnityEngine.Camera"), Prefix("UnityEngine.Light"), Prefix("UnityEngine.LOD"),
                Prefix("UnityEngine.BoneWeight"), Prefix("UnityEngine.ReflectionProbe"), Prefix("UnityEngine.FrameTiming"), Type("UnityEngine.QualitySettings"), Type("UnityEngine.ImageConversion"),
                Type("UnityEngine.ScreenCapture"), Type("UnityEngine.Gizmos"), Type("UnityEngine.ShadowCastingMode"), Type("UnityEngine.Resolution"), Type("UnityEngine.FullScreenMode"), Type("UnityEngine.SkinQuality"),
                Type("UnityEngine.Projector"), Type("UnityEngine.OcclusionPortal"), Type("UnityEngine.CombineInstance"), Type("UnityEngine.GL"), Type("UnityEngine.ColorSpace"), Type("UnityEngine.FilterMode"),
                Type("UnityEngine.PrimitiveType"), Type("UnityEngine.MotionVectorGenerationMode"), Type("UnityEngine.ReceiveGI"), Type("UnityEngine.Flare"), Type("UnityEngine.LensFlare"),
                Type("UnityEngine.SpriteMask"), Type("UnityEngine.SpriteMaskInteraction"), Type("UnityEngine.SubMeshDescriptor"), Type("UnityEngine.VertexAttribute"), Type("UnityEngine.VertexAttributeDescriptor"),
                Type("UnityEngine.VertexAttributeFormat"), Type("UnityEngine.IndexFormat"), Type("UnityEngine.TextureWrapMode"), Type("UnityEngine.HideFlags")),
            new ("math_native", "B", Namespace("Unity.Mathematics")),
            new ("collections_jobs", "B", Namespace("Unity.Collections"), Namespace("Unity.Jobs"), Namespace("Unity.Burst"), Namespace("UnityEngine.Jobs"), Namespace("Unity.IL2CPP")),
            new ("profiling", "B", Namespace("Unity.Profiling"), Namespace("UnityEngine.Profiling")),
            new ("web_requests", "B", Namespace("UnityEngine.Networking"), Type("UnityEngine.WWWForm")),
            new ("scenes", "B", Namespace("UnityEngine.SceneManagement")),
            new ("math", "B", Prefix("UnityEngine.Vector"), Type("UnityEngine.Quaternion"), Type("UnityEngine.Matrix4x4"), Type("UnityEngine.Mathf"), Prefix("UnityEngine.Color"), Prefix("UnityEngine.Rect"),
                Prefix("UnityEngine.Bounds"), Type("UnityEngine.Plane"), Type("UnityEngine.Random"), Type("UnityEngine.Hash128"), Prefix("UnityEngine.Gradient"), Type("UnityEngine.GeometryUtility"),
                Type("UnityEngine.FrustumPlanes"), Type("UnityEngine.Pose"), Type("UnityEngine.RangeInt")),
            new ("platform", "B", Type("UnityEngine.Time"), Type("UnityEngine.Application"), Type("UnityEngine.Screen"), Type("UnityEngine.SystemInfo"), Type("UnityEngine.PlayerPrefs"), Type("UnityEngine.Debug"),
                Type("UnityEngine.Resources"), Type("UnityEngine.RuntimePlatform"), Type("UnityEngine.ThreadPriority"), Type("UnityEngine.SystemLanguage"), Type("UnityEngine.OperatingSystemFamily"),
                Type("UnityEngine.DeviceType"), Type("UnityEngine.BatteryStatus"), Type("UnityEngine.NetworkReachability"), Type("UnityEngine.LogType"), Type("UnityEngine.ILogger"), Type("UnityEngine.ILogHandler"),
                Type("UnityEngine.Logger"), Type("UnityEngine.StackTraceLogType"), Type("UnityEngine.AsyncOperation"), Type("UnityEngine.Caching"), Type("UnityEngine.CrashReportHandler"),
                Type("UnityEngine.TextAsset"), Type("UnityEngine.JsonUtility"), Type("UnityEngine.ApplicationInstallMode"), Type("UnityEngine.WaitForSecondsRealtime"), Type("UnityEngine.ScreenOrientation"),
                Namespace("UnityEngine.LowLevel"), Namespace("UnityEngine.PlayerLoop"), Namespace("UnityEngine.Device"), Namespace("UnityEngine.Diagnostics"), Namespace("UnityEngine.Scripting"),
                Namespace("UnityEngine.Assertions"), Namespace("UnityEngine.Analytics"), Namespace("Unity.Services")),
            new ("object_model", "B", Type("UnityEngine.Object"), Type("UnityEngine.GameObject"), Type("UnityEngine.Component"), Type("UnityEngine.Behaviour"), Type("UnityEngine.MonoBehaviour"),
                Type("UnityEngine.ScriptableObject"), Type("UnityEngine.Transform"), Type("UnityEngine.Coroutine"), Type("UnityEngine.EntityId"), Suffix("YieldInstruction"), Prefix("UnityEngine.WaitFor"),
                Prefix("UnityEngine.WaitUntil"), Prefix("UnityEngine.WaitWhile"), Type("UnityEngine.Space"), Type("UnityEngine.SendMessageOptions"), Type("UnityEngine.FindObjectsInactive"),
                Type("UnityEngine.FindObjectsSortMode"), Suffix("Attribute"), Type("UnityEngine.SerializeField"), Type("UnityEngine.SerializeReference"), Type("UnityEngine.HideInInspector"),
                Type("UnityEngine.RequireComponent"), Type("UnityEngine.DisallowMultipleComponent"), Type("UnityEngine.ExecuteInEditMode"), Type("UnityEngine.ExecuteAlways"),
                Type("UnityEngine.AddComponentMenu"), Type("UnityEngine.DefaultExecutionOrder"), Type("UnityEngine.ContextMenu"), Type("UnityEngine.RuntimeInitializeLoadType"),
                Type("UnityEngine.PreferBinarySerialization"), Type("UnityEngine.ISerializationCallbackReceiver"), Namespace("UnityEngine.Events"), Namespace("UnityEngine.Pool"),
                Namespace("UnityEngine.Serialization")),
        };

        public static string GroupOf(string root, string containingTypeId)
        {
            if (root == "UnityEditor") return EDITOR;

            foreach (Rule rule in RULES)
                if (rule.Matches(containingTypeId))
                    return rule.Group;

            return OTHER;
        }

        /// <summary>
        ///     Anything the rules do not name still has to exist before the host flip, so it defaults to the shim phase.
        /// </summary>
        public static string PhaseOf(string group)
        {
            foreach (Rule rule in RULES)
                if (rule.Group == group)
                    return rule.Phase;

            return "B";
        }

        private static Func<string, bool> Namespace(string prefix) =>
            typeId => typeId.StartsWith(prefix + ".", StringComparison.Ordinal);

        private static Func<string, bool> Type(string typeId) =>
            candidate => candidate == typeId || candidate.StartsWith(typeId + ".", StringComparison.Ordinal) || candidate.StartsWith(typeId + "<", StringComparison.Ordinal);

        /// <summary>Matches a type name prefix: <c>UnityEngine.Audio</c> covers AudioSource, AudioClip, AudioSpeakerMode.</summary>
        private static Func<string, bool> Prefix(string typeIdPrefix) =>
            candidate => candidate.StartsWith(typeIdPrefix, StringComparison.Ordinal);

        /// <summary>Matches the outermost type name's suffix: <c>Collider</c> covers MeshCollider and BoxCollider.</summary>
        private static Func<string, bool> Suffix(string typeNameSuffix) =>
            candidate => OuterTypeName(candidate).EndsWith(typeNameSuffix, StringComparison.Ordinal);

        private static string OuterTypeName(string typeId)
        {
            int generic = typeId.IndexOf('<');
            string withoutGenerics = generic < 0 ? typeId : typeId.Substring(0, generic);
            int lastDot = withoutGenerics.LastIndexOf('.');
            return lastDot < 0 ? withoutGenerics : withoutGenerics.Substring(lastDot + 1);
        }

        private sealed class Rule
        {
            public string Group { get; }
            public string Phase { get; }
            private readonly Func<string, bool>[] predicates;

            public Rule(string group, string phase, params Func<string, bool>[] predicates)
            {
                Group = group;
                Phase = phase;
                this.predicates = predicates;
            }

            public bool Matches(string typeId)
            {
                foreach (Func<string, bool> predicate in predicates)
                    if (predicate(typeId))
                        return true;

                return false;
            }
        }
    }
}
