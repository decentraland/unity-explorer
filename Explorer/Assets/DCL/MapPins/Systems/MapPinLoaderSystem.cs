using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.SDKComponents.MapPins.Components;
using DCL.SDKComponents.VideoPlayer.Utils;
using DCL.Utilities;
using Decentraland.Common;
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
using Vector2 = UnityEngine.Vector2;

namespace DCL.SDKComponents.MapPins.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    public partial class MapPinLoaderSystem : BaseUnityLoopSystem
    {
        private readonly ObjectProxy<World> globalWorldProxy;
        private const int ATTEMPTS_COUNT = 6;
        private readonly ISceneData sceneData;

        public MapPinLoaderSystem(World world, ISceneData sceneData, ObjectProxy<World> globalWorldProxy) : base(world)
        {
            this.sceneData = sceneData;
            this.globalWorldProxy = globalWorldProxy;
        }

        protected override void Update(float t)
        {
            LoadMapPinQuery(World);
            UpdateMapPinQuery(World);
            HandleComponentRemovalQuery(World);
            HandleEntityDestructionQuery(World);
        }

        [Query]
        [None(typeof(MapPinHolderComponent))]
        [All(typeof(PBMapPin))]
        private void LoadMapPin(in Entity entity, ref PBMapPin pbMapPin, ref PartitionComponent partitionComponent)
        {
            MapPinComponent mapPinComponent = new MapPinComponent
            {
                IsDirty = true,
                Position = new Vector2(pbMapPin.Position.X, pbMapPin.Position.Y)
            };
            pbMapPin.IsDirty = false;
            TextureComponent? mapPinTexture = pbMapPin.Texture.CreateTextureComponent(sceneData);
            TryCreateGetTexturePromise(in mapPinTexture, ref mapPinComponent.TexturePromise, ref partitionComponent);

            World.Add(entity, new MapPinHolderComponent(globalWorldProxy.Object!.Create(pbMapPin, mapPinComponent, partitionComponent)));
        }

        [Query]
        private void UpdateMapPin(ref PBMapPin pbMapPin, ref MapPinHolderComponent mapPinHolderComponent, ref PartitionComponent partitionComponent)
        {
            if (!pbMapPin.IsDirty)
                return;

            MapPinComponent mapPinComponent = new MapPinComponent
            {
                IsDirty = true,
                Position = new Vector2(pbMapPin.Position.X, pbMapPin.Position.Y)
            };

            TextureComponent? mapPinTexture = pbMapPin.Texture.CreateTextureComponent(sceneData);
            TryCreateGetTexturePromise(in mapPinTexture, ref mapPinComponent.TexturePromise, ref partitionComponent);
            pbMapPin.IsDirty = false;

            globalWorldProxy.Object!.Set(mapPinHolderComponent.GlobalWorldEntity, mapPinComponent);
            globalWorldProxy.Object!.Set(mapPinHolderComponent.GlobalWorldEntity, pbMapPin);
        }

        [Query]
        [None(typeof(PBMapPin), typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(in Entity entity, ref MapPinHolderComponent sdkAvatarShapeComponent)
        {
            globalWorldProxy.Object!.Add(sdkAvatarShapeComponent.GlobalWorldEntity, new DeleteEntityIntention());
            World.Remove<MapPinHolderComponent>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleEntityDestruction(in Entity entity, ref MapPinHolderComponent sdkAvatarShapeComponent)
        {
            World.Remove<MapPinHolderComponent>(entity);
            World.Remove<PBMapPin>(entity);
            globalWorldProxy.Object!.Add(sdkAvatarShapeComponent.GlobalWorldEntity, new DeleteEntityIntention());
        }

        private void TryCreateGetTexturePromise(in TextureComponent? textureComponent, ref Promise? promise, ref PartitionComponent partitionComponent)
        {
            if (textureComponent == null)
            {
                return;
            }

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
