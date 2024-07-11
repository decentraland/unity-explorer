using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using SceneRunner.Debugging;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Global.Dynamic
{
    public struct DynamicWorldDependencies
    {
        public IDebugContainerBuilder DebugContainerBuilder;
        public IAssetsProvisioner AssetsProvisioner;
        public StaticContainer StaticContainer;
        public IPluginSettingsContainer SettingsContainer;
        public UIDocument RootUIDocument;
        public UIDocument CursorUIDocument;
        public DynamicSettings DynamicSettings;
        public IWeb3VerifiedAuthenticator Web3Authenticator;
        public IWeb3IdentityCache Web3IdentityCache;
        public Animator SplashAnimator;
        public WorldInfoTool WorldInfoTool;
    }

    public struct DynamicWorldParams
    {
        public IReadOnlyList<int2> StaticLoadPositions { get; init; }
        public List<string> Realms { get; init; }
        public Vector2Int StartParcel { get; init; }
        public bool EnableLandscape { get; init; }
        public bool EnableLOD { get; init; }
        public bool EnableAnalytics { get; init; }
        public HybridSceneParams HybridSceneParams { get; init; }

    }

    public struct HybridSceneParams
    {
        public bool EnableHybridScene { get; set; }
        public string HybridSceneID { get; set; }
        public string HybridSceneContent { get; set; }
    }
}
