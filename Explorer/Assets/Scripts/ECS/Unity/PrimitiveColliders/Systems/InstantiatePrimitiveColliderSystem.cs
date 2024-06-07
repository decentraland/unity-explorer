using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components.Defaults;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.Physics;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Interaction.Utility;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Unity.Groups;
using ECS.Unity.PrimitiveColliders.Components;
using ECS.Unity.SceneBoundsChecker;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using UnityEngine;
using Utility;

namespace ECS.Unity.PrimitiveColliders.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [LogCategory(ReportCategory.PRIMITIVE_COLLIDERS)]
    public partial class InstantiatePrimitiveColliderSystem : BaseUnityLoopSystem
    {
        private static readonly Dictionary<PBMeshCollider.MeshOneofCase, ISetupCollider> SETUP_COLLIDER_LOGIC = new ()
        {
            { PBMeshCollider.MeshOneofCase.Box, new SetupBoxCollider() },
            { PBMeshCollider.MeshOneofCase.Sphere, new SetupSphereCollider() },
            { PBMeshCollider.MeshOneofCase.Cylinder, new SetupCylinderCollider() },
            { PBMeshCollider.MeshOneofCase.Plane, new SetupPlaneCollider() },
        };
        private readonly IComponentPoolsRegistry poolsRegistry;

        private readonly Dictionary<PBMeshCollider.MeshOneofCase, ISetupCollider> setupColliderCases;
        private readonly IEntityCollidersSceneCache entityCollidersCache;

        internal InstantiatePrimitiveColliderSystem(World world, IComponentPoolsRegistry poolsRegistry,
            IEntityCollidersSceneCache entityCollidersCache, Dictionary<PBMeshCollider.MeshOneofCase, ISetupCollider> setupColliderCases = null) : base(world)
        {
            this.setupColliderCases = setupColliderCases ?? SETUP_COLLIDER_LOGIC;
            this.poolsRegistry = poolsRegistry;
            this.entityCollidersCache = entityCollidersCache;
        }

        protected override void Update(float t)
        {
            InstantiateNonExistingColliderQuery(World);
            TrySetupExistingColliderQuery(World);
        }

        [Query]
        [All(typeof(PBMeshCollider), typeof(SDKTransform), typeof(TransformComponent))]
        [None(typeof(PrimitiveColliderComponent))]
        private void InstantiateNonExistingCollider(in Entity entity, ref CRDTEntity crdtEntity, ref PBMeshCollider sdkComponent, ref TransformComponent transform)
        {
            if (sdkComponent.MeshCase == PBMeshCollider.MeshOneofCase.None) return;

            var component = new PrimitiveColliderComponent();
            Instantiate(entity, crdtEntity, setupColliderCases[sdkComponent.MeshCase], ref component, ref sdkComponent, ref transform);
            World.Add(entity, component);
        }

        [Query]
        [All(typeof(PBMeshCollider), typeof(SDKTransform), typeof(TransformComponent), typeof(PrimitiveColliderComponent))]
        private void TrySetupExistingCollider(
            in Entity entity,
            ref CRDTEntity crdtEntity,
            ref PrimitiveColliderComponent primitiveColliderComponent,
            ref PBMeshCollider sdkComponent,
            ref TransformComponent transformComponent)
        {
            if (!sdkComponent.IsDirty) return;

            ISetupCollider setupCollider = setupColliderCases[sdkComponent.MeshCase];

            // Prevent calling an overloaded comparison from UnityEngine.Object
            if (ReferenceEquals(primitiveColliderComponent.Collider, null))
                Instantiate(entity, crdtEntity, setupCollider, ref primitiveColliderComponent, ref sdkComponent, ref transformComponent);
            else
                // Just a change of parameters
                SetupCollider(entity, crdtEntity, setupCollider, primitiveColliderComponent.SDKCollider, sdkComponent);

            sdkComponent.IsDirty = false;
        }

        private void SetupCollider(in Entity entity, CRDTEntity sdkEntity, ISetupCollider setupCollider, SDKCollider collider, in PBMeshCollider sdkComponent)
        {
            // Setup collider only if it's gonna be enabled, otherwise there is no reason to [re]generate a shape
            if (SetColliderLayer(entity, sdkEntity, collider, sdkComponent))
                setupCollider.Execute(collider.Collider, sdkComponent);
        }

        private bool SetColliderLayer(in Entity entity, CRDTEntity sdkEntity, SDKCollider sdkCollider, in PBMeshCollider sdkComponent)
        {
            ColliderLayer colliderLayer = sdkComponent.GetColliderLayer();

            GameObject colliderGameObject = sdkCollider.Collider.gameObject;

            bool enabled = PhysicsLayers.TryGetUnityLayerFromSDKLayer(colliderLayer, out int unityLayer);

            if (enabled)
                colliderGameObject.layer = unityLayer;

            sdkCollider.IsActiveByEntity = enabled;

            entityCollidersCache.Associate(sdkCollider.Collider, new ColliderEntityInfo(World.Reference(entity), sdkEntity, colliderLayer));

            return enabled;
        }

        /// <summary>
        ///     It is either called when there is no collider or collider was invalidated before (set to null)
        /// </summary>
        private void Instantiate(in Entity entity, CRDTEntity sdkEntity, ISetupCollider setupCollider, ref PrimitiveColliderComponent component, ref PBMeshCollider sdkComponent,
            ref TransformComponent transformComponent)
        {
            component.ColliderType = setupCollider.ColliderType;
            component.SDKType = sdkComponent.MeshCase;

            var collider = (Collider)poolsRegistry.GetPool(setupCollider.ColliderType).Rent();
            component.SDKCollider = new SDKCollider(collider);

            SetupCollider(entity, sdkEntity, setupCollider, component.SDKCollider, in sdkComponent);
            // Parent collider to the entity's transform

            Transform colliderTransform = collider.transform;

            colliderTransform.SetParent(transformComponent.Transform, false);
            colliderTransform.ResetLocalTRS();

            component.Collider = collider;
        }
    }
}
