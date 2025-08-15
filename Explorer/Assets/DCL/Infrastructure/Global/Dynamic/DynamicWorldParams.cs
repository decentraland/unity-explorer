﻿using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.RealmNavigation;
using DCL.SceneLoadingScreens.SplashScreen;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using Global.AppArgs;
using SceneRunner.Debugging;
using System.Collections.Generic;
using SceneRunner.Scene;
using Unity.Mathematics;
using UnityEngine.UIElements;

namespace Global.Dynamic
{
    public readonly struct DynamicWorldDependencies
    {
        public readonly IDebugContainerBuilder DebugContainerBuilder;
        public readonly IAppArgs CommandLineArgs;
        public readonly IAssetsProvisioner AssetsProvisioner;
        public readonly StaticContainer StaticContainer;
        public readonly IPluginSettingsContainer SettingsContainer;
        public readonly UIDocument RootUIDocument;
        public readonly UIDocument ScenesUIDocument;
        public readonly UIDocument CursorUIDocument;
        public readonly DynamicSettings DynamicSettings;
        public readonly IWeb3VerifiedAuthenticator Web3Authenticator;
        public readonly IWeb3IdentityCache Web3IdentityCache;
        public readonly SplashScreen SplashScreen;
        public readonly WorldInfoTool WorldInfoTool;

        public DynamicWorldDependencies(
            IDebugContainerBuilder debugContainerBuilder,
            IAppArgs commandLineArgs,
            IAssetsProvisioner assetsProvisioner,
            StaticContainer staticContainer,
            IPluginSettingsContainer settingsContainer,
            UIDocument rootUIDocument,
            UIDocument scenesUIRoot,
            UIDocument cursorUIDocument,
            DynamicSettings dynamicSettings,
            IWeb3VerifiedAuthenticator web3Authenticator,
            IWeb3IdentityCache web3IdentityCache,
            SplashScreen splashScreen,
            WorldInfoTool worldInfoTool
        )
        {
            DebugContainerBuilder = debugContainerBuilder;
            CommandLineArgs = commandLineArgs;
            AssetsProvisioner = assetsProvisioner;
            StaticContainer = staticContainer;
            SettingsContainer = settingsContainer;
            RootUIDocument = rootUIDocument;
            ScenesUIDocument = scenesUIRoot;
            CursorUIDocument = cursorUIDocument;
            DynamicSettings = dynamicSettings;
            Web3Authenticator = web3Authenticator;
            Web3IdentityCache = web3IdentityCache;
            SplashScreen = splashScreen;
            WorldInfoTool = worldInfoTool;
        }
    }

    public struct DynamicWorldParams
    {
        public IReadOnlyList<int2> StaticLoadPositions { get; init; }
        public List<string> Realms { get; init; }
        public StartParcel StartParcel { get; init; }
        public bool IsolateScenesCommunication { get; init; }
        public bool EnableLandscape { get; init; }
        public bool EnableLOD { get; init; }
        public bool EnableAnalytics { get; init; }
        public HybridSceneParams HybridSceneParams { get; init; }
        public string LocalSceneDevelopmentRealm { get; init; }
    }


    public struct HybridSceneParams
    {
        public bool EnableHybridScene { get; set; }
        public HybridSceneContentServer HybridSceneContentServer { get; set; }
        public string World { get; init; }
    }
}
