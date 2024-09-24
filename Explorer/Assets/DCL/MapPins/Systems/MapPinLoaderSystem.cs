using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.FeatureFlags;
using DCL.MapPins.Components;
using DCL.SDKComponents.Utils;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
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
    public partial class MapPinLoaderSystem : BaseUnityLoopSystem
    {
        private const int ATTEMPTS_COUNT = 6;

        private readonly World globalWorld;
        private readonly IPartitionComponent partitionComponent;
        private readonly ISceneData sceneData;
        private readonly bool useCustomMapPinIcons;

        private int xRounded;
        private int yRounded;

        public MapPinLoaderSystem(World world, ISceneData sceneData, World globalWorld, IPartitionComponent partitionComponent, FeatureFlagsCache featureFlagsCache) : base(world)
        {
            this.sceneData = sceneData;
            this.globalWorld = globalWorld;
            this.partitionComponent = partitionComponent;
            useCustomMapPinIcons = featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.CUSTOM_MAP_PINS_ICONS);
        }

        protected override void Update(float t)
        {
            LoadMapPinQuery(World);
            UpdateMapPinQuery(World);
            HandleComponentRemovalQuery(World);
            HandleEntityDestructionQuery(World);
            if (useCustomMapPinIcons) { ResolvePromiseQuery(World); }
        }

        [Query]
        [None(typeof(MapPinHolderComponent))]
        private void LoadMapPin(in Entity entity, ref PBMapPin pbMapPin)
        {
            MapPinComponent mapPinComponent = new MapPinComponent
            {
                IsDirty = true,
                Position = new Vector2Int((int) pbMapPin.Position.X, (int) pbMapPin.Position.Y)
            };
            pbMapPin.IsDirty = false;

            bool hasTexturePromise = useCustomMapPinIcons;

            if (useCustomMapPinIcons)
            {
                TextureComponent? mapPinTexture = pbMapPin.Texture.CreateTextureComponent(sceneData);
                hasTexturePromise = TryCreateGetTexturePromise(in mapPinTexture, ref mapPinComponent.TexturePromise);
            }

            World.Add(entity, new MapPinHolderComponent(globalWorld.Create(pbMapPin, mapPinComponent), hasTexturePromise));
        }


        //This query is required because in the global world otherwise we cannot resolve easily
        //the promise of the texture, as it is bound to the entity in the scene world
        [Query]
        private void ResolvePromise(ref MapPinHolderComponent mapPinHolderComponent)
        {
            if (!mapPinHolderComponent.HasTexturePromise)
                return;

            ref MapPinComponent mapPinComponent = ref globalWorld.Get<MapPinComponent>(mapPinHolderComponent.GlobalWorldEntity);

            if (mapPinComponent.TexturePromise is not null && !mapPinComponent.TexturePromise.Value.IsConsumed)
            {
                if (mapPinComponent.TexturePromise.Value.TryConsume(World, out StreamableLoadingResult<Texture2DData> texture))
                {
                    mapPinComponent.ThumbnailIsDirty = true;
                    mapPinComponent.Thumbnail = texture.Asset;
                    mapPinComponent.TexturePromise = null;
                    mapPinHolderComponent.HasTexturePromise = false;
                }
            }
        }

        [Query]
        private void UpdateMapPin(ref PBMapPin pbMapPin, ref MapPinHolderComponent mapPinHolderComponent)
        {
            if (!pbMapPin.IsDirty)
                return;

            MapPinComponent mapPinComponent = globalWorld.Get<MapPinComponent>(mapPinHolderComponent.GlobalWorldEntity);

            xRounded = Mathf.RoundToInt(pbMapPin.Position.X);
            yRounded = Mathf.RoundToInt(pbMapPin.Position.Y);

            if (mapPinComponent.Position.x == xRounded && mapPinComponent.Position.y == yRounded)
                mapPinComponent.Position = new Vector2Int(xRounded, yRounded);

            mapPinComponent.IsDirty = true;

            if (useCustomMapPinIcons)
            {
                TextureComponent? mapPinTexture = pbMapPin.Texture.CreateTextureComponent(sceneData);
                mapPinHolderComponent.HasTexturePromise = TryCreateGetTexturePromise(in mapPinTexture, ref mapPinComponent.TexturePromise);
            }

            pbMapPin.IsDirty = false;

            globalWorld.Set(mapPinHolderComponent.GlobalWorldEntity, mapPinComponent, pbMapPin);
        }

        [Query]
        [None(typeof(PBMapPin), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(in Entity entity, ref MapPinHolderComponent mapPinHolderComponent)
        {
            ref MapPinComponent mapPinComponent = ref globalWorld.Get<MapPinComponent>(mapPinHolderComponent.GlobalWorldEntity);
            DereferenceTexture(ref mapPinComponent.TexturePromise);

            globalWorld.Add(mapPinHolderComponent.GlobalWorldEntity, new DeleteEntityIntention());
            World.Remove<MapPinHolderComponent>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(in Entity entity, ref MapPinHolderComponent mapPinHolderComponent)
        {
            ref MapPinComponent mapPinComponent = ref globalWorld.Get<MapPinComponent>(mapPinHolderComponent.GlobalWorldEntity);
            DereferenceTexture(ref mapPinComponent.TexturePromise);

            globalWorld.Add(mapPinHolderComponent.GlobalWorldEntity, new DeleteEntityIntention());
            World.Remove<MapPinHolderComponent, PBMapPin>(entity);
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

            promise = Promise.Create(World, new GetTextureIntention
            {
                CommonArguments = new CommonLoadingArguments(textureComponentValue.Src, attempts: ATTEMPTS_COUNT),
                WrapMode = textureComponentValue.WrapMode,
                FilterMode = textureComponentValue.FilterMode,
            }, partitionComponent);

            return true;
        }

    }
}
