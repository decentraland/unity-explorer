using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Defaults;
using CrdtEcsBridge.Components.Transform;
using CrdtEcsBridge.Physics;
using DCL.ECSComponents;
using Diagnostics.ReportsHandling;
using ECS.Abstract;
using ECS.ComponentsPooling;
using ECS.Unity.Groups;
using ECS.Unity.PrimitiveColliders.Components;
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
        private readonly IComponentPoolsRegistry poolsRegistry;

        private static readonly Dictionary<PBMeshCollider.MeshOneofCase, ISetupCollider> SETUP_COLLIDER_LOGIC = new ()
        {
            { PBMeshCollider.MeshOneofCase.Box, new SetupBoxCollider() },
            { PBMeshCollider.MeshOneofCase.Sphere, new SetupSphereCollider() },
            { PBMeshCollider.MeshOneofCase.Cylinder, new SetupCylinderCollider() },
            { PBMeshCollider.MeshOneofCase.Plane, new SetupPlaneCollider() },
        };

        private readonly Dictionary<PBMeshCollider.MeshOneofCase, ISetupCollider> setupColliderCases;

        internal InstantiatePrimitiveColliderSystem(World world, IComponentPoolsRegistry poolsRegistry,
            Dictionary<PBMeshCollider.MeshOneofCase, ISetupCollider> setupColliderCases = null) : base(world)
        {
            this.setupColliderCases = setupColliderCases ?? SETUP_COLLIDER_LOGIC;
            this.poolsRegistry = poolsRegistry;
        }

        protected override void Update(float t)
        {
            InstantiateNonExistingColliderQuery(World);
            TrySetupExistingColliderQuery(World);
        }

        [Query]
        [All(typeof(PBMeshCollider), typeof(SDKTransform), typeof(TransformComponent))]
        [None(typeof(PrimitiveColliderComponent))]
        private void InstantiateNonExistingCollider(in Entity entity, ref PBMeshCollider sdkComponent, ref TransformComponent transform)
        {
            var component = new PrimitiveColliderComponent();
            Instantiate(setupColliderCases[sdkComponent.MeshCase], ref component, ref sdkComponent, ref transform);
            World.Add(entity, component);
        }

        [Query]
        [All(typeof(PBMeshCollider), typeof(SDKTransform), typeof(TransformComponent), typeof(PrimitiveColliderComponent))]
        private void TrySetupExistingCollider(
            ref PrimitiveColliderComponent primitiveColliderComponent,
            ref PBMeshCollider sdkComponent,
            ref TransformComponent transformComponent)
        {
            if (!sdkComponent.IsDirty) return;

            ISetupCollider setupCollider = setupColliderCases[sdkComponent.MeshCase];

            // Prevent calling an overloaded comparison from UnityEngine.Object
            if (ReferenceEquals(primitiveColliderComponent.Collider, null))
                Instantiate(setupCollider, ref primitiveColliderComponent, ref sdkComponent, ref transformComponent);
            else

                // Just a change of parameters
                SetupCollider(setupCollider, primitiveColliderComponent.Collider, sdkComponent);

            sdkComponent.IsDirty = false;
        }

        private void SetupCollider(ISetupCollider setupCollider, Collider collider, in PBMeshCollider sdkComponent)
        {
            // Setup collider only if it's gonna be enabled, otherwise there is no reason to [re]generate a shape
            if (SetColliderLayer(collider, sdkComponent))
                setupCollider.Execute(collider, sdkComponent);
        }

        private bool SetColliderLayer(Collider collider, in PBMeshCollider sdkComponent)
        {
            ColliderLayer colliderLayer = sdkComponent.GetColliderLayer();

            GameObject colliderGameObject = collider.gameObject;

            bool enabled = PhysicsLayers.TryGetUnityLayerFromSDKLayer(colliderLayer, out int unityLayer);

            if (enabled)
                colliderGameObject.layer = unityLayer;

            collider.enabled = enabled;
            return enabled;
        }

        /// <summary>
        ///     It is either called when there is no collider or collider was invalidated before (set to null)
        /// </summary>
        private void Instantiate(ISetupCollider setupCollider, ref PrimitiveColliderComponent component, ref PBMeshCollider sdkComponent, ref TransformComponent transformComponent)
        {
            component.ColliderType = setupCollider.ColliderType;
            component.SDKType = sdkComponent.MeshCase;

            var collider = (Collider)poolsRegistry.GetPool(setupCollider.ColliderType).Rent();

            SetupCollider(setupCollider, collider, in sdkComponent);

            // Parent collider to the entity's transform

            Transform colliderTransform = collider.transform;

            colliderTransform.SetParent(transformComponent.Transform, false);
            colliderTransform.ResetLocalTRS();

            component.Collider = collider;
        }
    }
}
