using Arch.Core;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Utility;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Cache;
using ECS.Unity.GLTFContainer.Asset.Components;
using System;
using System.Collections.Generic;
using UnityEngine;
using AssetBundlePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundles.AssetBundleData, ECS.StreamableLoading.AssetBundles.GetAssetBundleIntention>;

namespace ECS.StreamableLoading.AssetBundles.InitialSceneState
{
    public class InitialSceneStateDescriptor
    {
        public static InitialSceneStateDescriptor CreateUnsupported(string sceneID)
        {
            InitialSceneStateDescriptor unsuportedStaticSceneAB = new InitialSceneStateDescriptor();
            return unsuportedStaticSceneAB;
        }

        public static InitialSceneStateDescriptor CreateSupported(World world, IGltfContainerAssetsCache assetsCache, EntityDefinitionBase entityDefinition)
        {
            InitialSceneStateDescriptor suportedStaticSceneAB = new InitialSceneStateDescriptor();
            return suportedStaticSceneAB;
        }

    }
}
