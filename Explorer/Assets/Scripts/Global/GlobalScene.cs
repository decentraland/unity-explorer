using Arch.Core;
using Arch.SystemGroups;
using CrdtEcsBridge.Components.Special;
using ECS.Global.Systems;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Systems;
using ECS.Unity.Transforms.Components;
using SceneRunner;
using System;
using UnityEngine;

namespace Global
{
    public class GlobalScene : IDisposable
    {
        private SceneLifeCycleState state = new SceneLifeCycleState();

        private SystemGroupWorld worldSystems;

        private World world;

        public void Initialize(ISceneFactory sceneFactory, Camera unityCamera, int sceneLoadRadius)
        {
            world = World.Create();

            var builder = new ArchSystemsWorldBuilder<World>(world);

            state.PlayerEntity = world.Create(new PlayerComponent(), new TransformComponent());
            state.SceneLoadRadius = sceneLoadRadius;

            SceneDynamicLoaderSystem.InjectToWorld(ref builder, state);
            SceneLifeCycleSystem.InjectToWorld(ref builder, state);
            SceneLoadingSystem.InjectToWorld(ref builder, sceneFactory);
            DestroySceneSystem.InjectToWorld(ref builder);

            DebugCameraTransformToPlayerTransformSystem.InjectToWorld(ref builder, state.PlayerEntity, unityCamera);

            worldSystems = builder.Finish();
            worldSystems.Initialize();

        }

        public void Dispose()
        {
            worldSystems.Dispose();
            world.Dispose();
        }
    }
}
