using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

namespace Global.Dynamic
{
    public struct DynamicWorldDependencies
    {
        public StaticContainer StaticContainer;
        public IPluginSettingsContainer SettingsContainer;
        public UIDocument RootUIDocument;
        public UIDocument CursorUIDocument;
        public DynamicSettings DynamicSettings;
        public IWeb3VerifiedAuthenticator Web3Authenticator;
        public IWeb3IdentityCache Web3IdentityCache;
    }

    public struct DynamicWorldParams
    {
        public IReadOnlyList<int2> StaticLoadPositions { get; init; }
        public List<string> Realms { get; init; }
        public Vector2Int StartParcel { get; init; }
        public bool EnableLandscape { get; init; }
    }
}
