using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.MapPins.Components;
using DCL.SDKComponents.Utils;
using DCL.Utilities;
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
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.SDKComponents.MapPins.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    public partial class MapPinLoaderSystem : BaseUnityLoopSystem
    {
        private readonly ObjectProxy<World> globalWorldProxy;
        private readonly IPartitionComponent partitionComponent;
        private const int ATTEMPTS_COUNT = 6;
        private readonly ISceneData sceneData;
        private int xRounded;
        private int yRounded;

        public MapPinLoaderSystem(World world, ISceneData sceneData, ObjectProxy<World> globalWorldProxy, IPartitionComponent partitionComponent) : base(world)
        {
            this.sceneData = sceneData;
            this.globalWorldProxy = globalWorldProxy;
            this.partitionComponent = partitionComponent;
        }

        protected override void Update(float t)
        {
            LoadMapPinQuery(World);
            UpdateMapPinQuery(World);
            HandleComponentRemovalQuery(World);
            HandleEntityDestructionQuery(World);
            ResolvePromiseQuery(World);
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
            TextureComponent? mapPinTexture = pbMapPin.Texture.CreateTextureComponent(sceneData);
            TryCreateGetTexturePromise(in mapPinTexture, ref mapPinComponent.TexturePromise);

            World.Add(entity, new MapPinHolderComponent(globalWorldProxy.Object!.Create(pbMapPin, mapPinComponent)));
        }


        //This query is required because in the global world otherwise we cannot resolve easily
        //the promise of the texture, as it is bound to the entity in the scene world
        [Query]
        private void ResolvePromise(ref MapPinHolderComponent mapPinHolderComponent)
        {
            MapPinComponent mapPinComponent = (MapPinComponent) globalWorldProxy.Object!.Get(mapPinHolderComponent.GlobalWorldEntity, typeof(MapPinComponent))!;

            if (mapPinComponent.TexturePromise is not null && !mapPinComponent.TexturePromise.Value.IsConsumed)
            {
                if (mapPinComponent.TexturePromise.Value.TryConsume(World, out StreamableLoadingResult<Texture2D> texture))
                {
                    mapPinComponent.ThumbnailIsDirty = true;
                    mapPinComponent.Thumbnail = texture.Asset;
                    mapPinComponent.TexturePromise = null;
                    globalWorldProxy.Object!.Set(mapPinHolderComponent.GlobalWorldEntity, mapPinComponent);
                }
            }
        }

        [Query]
        private void UpdateMapPin(ref PBMapPin pbMapPin, ref MapPinHolderComponent mapPinHolderComponent)
        {
            if (!pbMapPin.IsDirty)
                return;

            MapPinComponent mapPinComponent = (MapPinComponent) globalWorldProxy.Object!.Get(mapPinHolderComponent.GlobalWorldEntity, typeof(MapPinComponent))!;

            xRounded = Mathf.RoundToInt(pbMapPin.Position.X);
            yRounded = Mathf.RoundToInt(pbMapPin.Position.Y);

            if (mapPinComponent.Position.x == xRounded && mapPinComponent.Position.y == yRounded)
                mapPinComponent.Position = new Vector2Int(xRounded, yRounded);

            mapPinComponent.IsDirty = true;

            TextureComponent? mapPinTexture = pbMapPin.Texture.CreateTextureComponent(sceneData);
            TryCreateGetTexturePromise(in mapPinTexture, ref mapPinComponent.TexturePromise);
            pbMapPin.IsDirty = false;

            globalWorldProxy.Object!.Set(mapPinHolderComponent.GlobalWorldEntity, mapPinComponent, pbMapPin);
        }

        [Query]
        [None(typeof(PBMapPin), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(in Entity entity, ref MapPinHolderComponent mapPinHolderComponent)
        {
            globalWorldProxy.Object!.Add(mapPinHolderComponent.GlobalWorldEntity, new DeleteEntityIntention());
            World.Remove<MapPinHolderComponent>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(in Entity entity, ref MapPinHolderComponent mapPinHolderComponent)
        {
            globalWorldProxy.Object!.Add(mapPinHolderComponent.GlobalWorldEntity, new DeleteEntityIntention());
            World.Remove<MapPinHolderComponent, PBMapPin>(entity);
        }

        private void TryCreateGetTexturePromise(in TextureComponent? textureComponent, ref Promise? promise)
        {
            if (textureComponent == null)
                return;

            TextureComponent textureComponentValue = textureComponent.Value;

            if (TextureComponentUtils.Equals(ref textureComponentValue, ref promise))
                return;

            promise = Promise.Create(World, new GetTextureIntention
            {
                CommonArguments = new CommonLoadingArguments(textureComponentValue.Src, attempts: ATTEMPTS_COUNT),
                WrapMode = textureComponentValue.WrapMode,
                FilterMode = textureComponentValue.FilterMode,
            }, partitionComponent);
        }

    }
}
