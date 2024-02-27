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
        public DynamicSettings DynamicSettings;
        public IWeb3VerifiedAuthenticator Web3Authenticator;
        public IWeb3IdentityCache Web3IdentityCache;
    }

    public struct DynamicWorldParams
    {
        public IReadOnlyList<int2> StaticLoadPositions;
        [Obsolete] public int SceneLoadRadius;
        public List<string> Realms;
        public Vector2Int StartParcel;
        public bool EnableLandscape;
    }
}
