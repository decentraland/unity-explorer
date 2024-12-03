using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.ECSToCRDTWriter;

// using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.GltfNode.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.GltfNode.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ParentingTransformSystem))]
    // [LogCategory(ReportCategory.LogCategory)]
    public partial class GltfNodeSystem : BaseUnityLoopSystem
    {
        private const string GLTF_ROOT_GO_NAME = "Scene/";
        private IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap;
        private readonly IECSToCRDTWriter ecsToCrdtWriter;
        private readonly IComponentPool<SDKTransform> sdkTransformPool;
        private readonly ISceneData sceneData;

        public GltfNodeSystem(World world, IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap, IECSToCRDTWriter ecsToCrdtWriter,
          IComponentPool<SDKTransform> sdkTransformPool, ISceneData sceneData) : base(world)
        {
            this.entitiesMap = entitiesMap;
            this.ecsToCrdtWriter = ecsToCrdtWriter;
            this.sceneData = sceneData;
            this.sdkTransformPool = sdkTransformPool;
        }

        protected override void Update(float t)
        {
            SetupGltfNodeQuery(World);
        }

        [Query]
        [None(typeof(GltfNodeComponent), typeof(DeleteEntityIntention))]
        private void SetupGltfNode(Entity entity, in CRDTEntity crdtEntity, in PBGltfNode pbComponent)
        {
            // Debug.Log($"PRAVS - SetupGltfNode() - 1 - gltfEntity: {pbComponent.GltfContainerEntity} / path: {pbComponent.NodePath}");
            var gltfCRDTEntity = new CRDTEntity((int)pbComponent.GltfContainerEntity);
            if (!entitiesMap.TryGetValue(gltfCRDTEntity, out var gltfEntity)
                || !World.Has<PBGltfContainer>(gltfEntity) // Maybe this check can even be removed...
                || !World.TryGet(gltfEntity, out TransformComponent gltfEntityTransform))
                return;

            // TODO: Find standard patch for the 'pbComponent.NodePath' against the path processed by GLTFast...
            // Non-skeleton GLTFs seem to work OK, do the skeleton GLTFs have a path inconsistency (e.g. GLTFast vs BabylonSandbox vs Blender) 100% ???

            Transform? nodeTransform = null;
            // if ((nodeTransform = gltfEntityTransform.Transform.Find("Scene/Scene/"+pbComponent.NodePath)) == null)
            if ((nodeTransform = gltfEntityTransform.Transform.Find(GLTF_ROOT_GO_NAME+pbComponent.NodePath)) == null)
                return;

            Debug.Log($"PRAVS - SetupGltfNode() - 2 - gltfEntity: {pbComponent.GltfContainerEntity} / path: {pbComponent.NodePath}", nodeTransform);

            // Duplicate node and hide the original one (to be able to reset the node)
            nodeTransform.gameObject.SetActive(false);
            GameObject nodeClone = GameObject.Instantiate(nodeTransform.gameObject, nodeTransform.parent);
            var nodeCloneTransform = nodeClone.transform;

            // TODO: This works but ends up putting the entity transform values using the unity values,
            // very far away from the scene, can we really escape using the relative transform values?
            nodeCloneTransform.localPosition = nodeTransform.localPosition;
            nodeCloneTransform.localRotation = nodeTransform.localRotation;
            nodeCloneTransform.localScale = nodeTransform.localScale;
            nodeClone.SetActive(true);

            // Populate GLTFNode entity with detected components
            var sdkTransform = sdkTransformPool.Get();
            sdkTransform.Position.Value = nodeCloneTransform.localPosition;
            sdkTransform.Rotation.Value = nodeCloneTransform.localRotation;
            sdkTransform.Scale = nodeCloneTransform.localScale;
            // sdkTransform.ParentId = pbComponent.GltfContainerEntity;
            sdkTransform.ParentId = gltfCRDTEntity;
            World.Add(entity, sdkTransform, new TransformComponent(nodeCloneTransform));
            ExposedTransformUtils.Put(
                ecsToCrdtWriter,
                sdkTransform,
                crdtEntity,
                sceneData.Geometry.BaseParcelPosition,
                false);

            var component = new GltfNodeComponent();
            // TODO: Cache stuff in GltfNodeComponent...

            World.Add(entity, component);
        }
    }
}
