using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.FeatureFlags;
using DCL.MapPins.Bus;
using DCL.MapPins.Components;
using DCL.SDKComponents.Utils;
using ECS.Abstract;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.Unity.Groups;
using ECS.Unity.Textures.Components;
using ECS.Unity.Textures.Components.Extensions;
using SceneRunner.Scene;
using UnityEngine;
using Entity = Arch.Core.Entity;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.SDKComponents.MapPins.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    public partial class MapPinLoaderSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private const int ATTEMPTS_COUNT = 6;

        private readonly World globalWorld;
        private readonly IPartitionComponent partitionComponent;
        private readonly ISceneData sceneData;
        private readonly IMapPinsEventBus mapPinsEventBus;
        private readonly bool useCustomMapPinIcons;

        private int xRounded;
        private int yRounded;

        public MapPinLoaderSystem(World world, ISceneData sceneData, World globalWorld, IPartitionComponent partitionComponent, IMapPinsEventBus mapPinsEventBus) : base(world)
        {
            this.sceneData = sceneData;
            this.globalWorld = globalWorld;
            this.partitionComponent = partitionComponent;
            this.mapPinsEventBus = mapPinsEventBus;
            useCustomMapPinIcons = FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.CUSTOM_MAP_PINS_ICONS);
        }

        protected override void Update(float t)
        {
            LoadMapPinQuery(World);
            UpdateMapPinQuery(World);
            HandleComponentRemovalQuery(World);
            HandleEntityDestructionQuery(World);

            if (useCustomMapPinIcons) { ResolveTexturePromiseQuery(World); }
        }

        [Query]
        [None(typeof(MapPinComponent))] [All(typeof(PBMapPin))]
        private void LoadMapPin(in Entity entity)
        {
            var mapPinComponent = new MapPinComponent();
            World.Add(entity, mapPinComponent);
        }

        [Query]
        private void UpdateMapPin(in Entity entity, ref PBMapPin pbMapPin, ref MapPinComponent mapPinComponent)
        {
            if (!pbMapPin.IsDirty)
                return;

            xRounded = Mathf.RoundToInt(pbMapPin.Position.X);
            yRounded = Mathf.RoundToInt(pbMapPin.Position.Y);

            if (mapPinComponent.Position.x != xRounded || mapPinComponent.Position.y != yRounded)
                mapPinComponent.Position = new Vector2Int(xRounded, yRounded);

            if (useCustomMapPinIcons)
            {
                TextureComponent? mapPinTexture = pbMapPin.Texture.CreateTextureComponent(sceneData);
                TryCreateGetTexturePromise(in mapPinTexture, ref mapPinComponent.TexturePromise);
            }

            pbMapPin.IsDirty = false;

            mapPinsEventBus.UpdateMapPin(entity, mapPinComponent.Position, pbMapPin.Title, pbMapPin.Description);
        }

        [Query]
        private void ResolveTexturePromise(in Entity entity, ref MapPinComponent mapPinComponent)
        {
            if (mapPinComponent.TexturePromise is null || mapPinComponent.TexturePromise.Value.IsConsumed) return;

            if (mapPinComponent.TexturePromise.Value.TryConsume(World, out StreamableLoadingResult<Texture2DData> texture))
            {
                mapPinComponent.TexturePromise = null;
                mapPinsEventBus.UpdateMapPinThumbnail(entity, texture.Asset);
            }
        }

        [Query]
        [None(typeof(PBMapPin), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(in Entity entity, ref MapPinComponent mapPinComponent)
        {
            DereferenceTexture(ref mapPinComponent.TexturePromise);
            World.Remove<MapPinComponent>(entity);

            mapPinsEventBus.RemoveMapPin(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(in Entity entity, ref MapPinComponent mapPinComponent)
        {
            DereferenceTexture(ref mapPinComponent.TexturePromise);

            mapPinsEventBus.RemoveMapPin(entity);
        }

        [Query]
        private void CleanupOnFinalize(in Entity entity, ref MapPinComponent mapPinComponent)
        {
            DereferenceTexture(ref mapPinComponent.TexturePromise);

            mapPinsEventBus.RemoveMapPin(entity);
        }

        private void DereferenceTexture(ref Promise? promise)
        {
            if (promise == null)
                return;

            Promise promiseValue = promise.Value;
            promiseValue.TryDereference(World);
        }

        private bool TryCreateGetTexturePromise(in TextureComponent? textureComponent, ref Promise? promise)
        {
            if (textureComponent == null)
                return false;

            TextureComponent textureComponentValue = textureComponent.Value;

            if (TextureComponentUtils.Equals(ref textureComponentValue, ref promise))
                return false;

            DereferenceTexture(ref promise);

            promise = Promise.Create(
                World,
                new GetTextureIntention(
                    textureComponentValue.Src,
                    textureComponentValue.FileHash,
                    textureComponentValue.WrapMode,
                    textureComponentValue.FilterMode,
                    textureComponentValue.TextureType,
                    attemptsCount: ATTEMPTS_COUNT
                ),
                partitionComponent
            );

            return true;
        }

        public void FinalizeComponents(in Query query)
        {
            CleanupOnFinalizeQuery(World);
        }
    }
}
