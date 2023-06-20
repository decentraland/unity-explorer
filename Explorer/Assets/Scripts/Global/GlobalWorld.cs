using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Special;
using ECS.Global.Systems;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Systems;
using ECS.Unity.Transforms.Components;
using Ipfs;
using JetBrains.Annotations;
using SceneRunner;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Global
{
    public class GlobalWorld : IDisposable
    {
        private readonly CancellationTokenSource destroyCancellationSource = new ();

        private ProcessRealmChangeSystem processRealmChangeSystem;
        private SceneLifeCycleState state;

        private World world;

        private SystemGroupWorld worldSystems;

        public void Dispose()
        {
            destroyCancellationSource.Cancel();
            worldSystems.Dispose();
            world.Dispose();
        }

        public void Initialize(ISceneFactory sceneFactory, Camera unityCamera, int sceneLoadRadius, [CanBeNull] List<Vector2Int> staticLoadPositions = null)
        {
            world = World.Create();

            var builder = new ArchSystemsWorldBuilder<World>(world);

            state = new SceneLifeCycleState
            {
                PlayerEntity = world.Create(new PlayerComponent(), new TransformComponent()),
                SceneLoadRadius = sceneLoadRadius,
            };

            processRealmChangeSystem = ProcessRealmChangeSystem.InjectToWorld(ref builder, state);
            LoadScenesDynamicallySystem.InjectToWorld(ref builder, state, staticLoadPositions);
            LoadSceneMetadataSystem.InjectToWorld(ref builder, state);
            LoadSceneSystem.InjectToWorld(ref builder, state);
            StartSceneSystem.InjectToWorld(ref builder, state, sceneFactory, destroyCancellationSource.Token);
            DestroySceneSystem.InjectToWorld(ref builder);

            DebugCameraTransformToPlayerTransformSystem.InjectToWorld(ref builder, state.PlayerEntity, unityCamera);

            worldSystems = builder.Finish();
            worldSystems.Initialize();
        }

        public void SetRealm(string realm)
        {
            processRealmChangeSystem.ChangeRealm(realm);
        }
    }
}
