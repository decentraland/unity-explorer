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
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using SceneRunner.Scene;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace DCL.SDKComponents.GltfNode.Systems
{
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateAfter(typeof(ParentingTransformSystem))]
    // [LogCategory(ReportCategory.LogCategory)]
    public partial class GltfNodeSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private const string GLTF_ROOT_GO_NAME = "Scene/";
        private IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap;
        private readonly IECSToCRDTWriter ecsToCrdtWriter;
        private readonly IComponentPool<SDKTransform> sdkTransformPool;
        private readonly ISceneData sceneData;

        Stopwatch stopWatch = new Stopwatch();

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

            // stopWatch.Start();

            Transform? nodeTransform = null;
            // if ((nodeTransform = gltfEntityTransform.Transform.Find("Scene/Scene/"+pbComponent.NodePath)) == null)
            if ((nodeTransform = gltfEntityTransform.Transform.Find(GLTF_ROOT_GO_NAME+pbComponent.NodePath)) == null)
                return;

            // Debug.Log($"PRAVS - SetupGltfNode() - 2 - gltfEntity: {pbComponent.GltfContainerEntity} / path: {pbComponent.NodePath}", nodeTransform);

            // Duplicate node and hide the original one (to be able to reset the node)

            GameObject nodeClone = GameObject.Instantiate(nodeTransform.gameObject, nodeTransform.parent);
            var nodeCloneTransform = nodeClone.transform;
            nodeTransform.gameObject.SetActive(false);

            nodeCloneTransform.localPosition = nodeTransform.localPosition;
            nodeCloneTransform.localRotation = nodeTransform.localRotation;
            nodeCloneTransform.localScale = nodeTransform.localScale;
            nodeClone.SetActive(true);

            // Populate GLTFNode entity with detected components
            var sdkTransform = sdkTransformPool.Get();
            sdkTransform.Position.Value = nodeCloneTransform.localPosition;
            sdkTransform.Rotation.Value = nodeCloneTransform.localRotation;
            sdkTransform.Scale = nodeCloneTransform.localScale;

            // if SDKTransform is marked as Dirty, the transform system will re-parent its GO to the scene root
            sdkTransform.IsDirty = false;

            var gltfNodeComponent = new GltfNodeComponent()
            {
                originalNodeGameObject = nodeTransform.gameObject,
                clonedNodeTransform = nodeCloneTransform
            };
            World.Add(entity, gltfNodeComponent, new TransformComponent(nodeCloneTransform), sdkTransform);

            // Put transform on SDK entity so that the scene can read it
            ExposedTransformUtils.Put(
                ecsToCrdtWriter,
                sdkTransform,
                crdtEntity,
                sceneData.Geometry.BaseParcelPosition,
                false);

            // stopWatch.Stop();
            // Debug.Log($"PRAVS - SetupGltfNode() - milliseconds: {stopWatch.ElapsedMilliseconds}");
        }

        // TODO: Add query for component deletion

        [Query]
        public void FinalizeComponents(Entity entity, ref GltfNodeComponent gltfNodeComponent, ref TransformComponent transformComponent, ref SDKTransform sdkTransform)
        {
            // stopWatch.Restart();

            // Clean GltfNode entity
            /*Transform[] children = new Transform[gltfNodeComponent.clonedNodeTransform.childCount];
            int index = 0;
            foreach (Transform child in gltfNodeComponent.clonedNodeTransform)
            {
                children[index] = child;
                index++;
            }
            foreach (Transform child in children)
            {
                child.parent.SetParent(null);
                // TODO: update every child entity transform parent to be the root scene ON CRDT AS WELL...
            }*/

            GameObject.Destroy(gltfNodeComponent.clonedNodeTransform!.gameObject);

            // Reset original GO
            gltfNodeComponent.originalNodeGameObject!.SetActive(true);

            // stopWatch.Stop();
            // Debug.Log($"PRAVS - FinalizeComponents() - milliseconds: {stopWatch.ElapsedMilliseconds}");

            // TODO: Remove SDKTransform from CRDT Entity as well ???

            sdkTransformPool.Release(sdkTransform);
            World.Remove<TransformComponent, SDKTransform, GltfNodeComponent>(entity);
        }

        public void FinalizeComponents(in Query query) =>
            FinalizeComponentsQuery(World);
    }
}