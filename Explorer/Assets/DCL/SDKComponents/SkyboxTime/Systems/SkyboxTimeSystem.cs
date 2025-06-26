using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CrdtEcsBridge.Components;
using DCL.ECSComponents;
using DCL.StylizedSkybox.Scripts;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using SceneRunner.Scene;
using System;
using UnityEngine;

namespace DCL.SDKComponents.SkyboxTime.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    public partial class SkyboxTimeSystem : BaseUnityLoopSystem, ISceneIsCurrentListener
    {
        private readonly StylizedSkyboxSettingsAsset skyboxSettings;
        private readonly Entity rootEntity;
        private readonly ISceneStateProvider sceneStateProvider;

        public SkyboxTimeSystem(World world, StylizedSkyboxSettingsAsset skyboxSettings, Entity rootEntity, ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.skyboxSettings = skyboxSettings;
            this.rootEntity = rootEntity;
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float t)
        {
            CreateComponentQuery(World);

            //TODO: Change these queries to world.get/has since only the root entity can have the component
            //if (!World.Has<SkyboxTimeComponent>(rootEntity)) return;
            //var skyboxTimeComponent = World.Get<SkyboxTimeComponent>(rootEntity);

            UpdateSkyboxTimeQuery(World);
            HandleEntityDestructionQuery(World);
        }

        [Query]
        [All(typeof(PBSkyboxTime))]
        [None(typeof(DeleteEntityIntention), typeof(SkyboxTimeComponent))]
        private void CreateComponent(in Entity entity)
        {
            if (entity.Id != SpecialEntitiesID.SCENE_ROOT_ENTITY)
                World.Remove<PBSkyboxTime>(entity);

            World.Add<SkyboxTimeComponent>(entity);
        }

        [Query]
        [All(typeof(SkyboxTimeComponent))]
        [None(typeof(DeleteEntityIntention))]
        private void UpdateSkyboxTime(ref PBSkyboxTime pbSkyboxTime)
        {
            if(pbSkyboxTime.IsDirty == false) return;

            skyboxSettings.IsDayCycleEnabled = false;
            skyboxSettings.SkyboxTimeSource = SkyboxTimeSource.SDK_SKYBOX_COMPONENT_FIXED;
            skyboxSettings.TimeOfDayNormalized = pbSkyboxTime.FixedTimeOfDay;

            pbSkyboxTime.IsDirty = false;
        }

        [Query]
        [None(typeof(PBSkyboxTime))]
        [All(typeof(SkyboxTimeComponent))]
        private void HandleEntityDestruction(in Entity entity)
        {
            World.Remove<SkyboxTimeComponent>(entity);
            skyboxSettings.IsDayCycleEnabled = true;
            skyboxSettings.SkyboxTimeSource = SkyboxTimeSource.GLOBAL;
        }

        public void OnSceneIsCurrentChanged(bool value)
        {
            Debug.Log($"ALE- OnSceneIsCurrentChanged: {value} isCurrent: {sceneStateProvider.IsCurrent}");
        }
    }
}

public struct SkyboxTimeComponent{}
