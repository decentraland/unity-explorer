using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.ECSComponents;
using DCL.SDKComponents.MapPins.Components;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using ECS.Unity.Groups;
using ECS.Unity.Textures.Components;
using ECS.Unity.Textures.Components.Extensions;
using SceneRunner.Scene;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace DCL.SDKComponents.MapPins.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    public partial class MapPinLoaderSystem : BaseUnityLoopSystem
    {
        private const int ATTEMPTS_COUNT = 6;
        private readonly ISceneData sceneData;

        public MapPinLoaderSystem(World world, ISceneData sceneData) : base(world)
        {
            this.sceneData = sceneData;
        }

        protected override void Update(float t)
        {
            LoadMapPinQuery(World);
            UpdateMapPinQuery(World);
        }

        [Query]
        [None(typeof(MapPinComponent))]
        private void LoadMapPin(in Entity entity, ref PBMapPin pbMapPin, ref PartitionComponent partitionComponent)
        {
            MapPinComponent mapPinComponent = new MapPinComponent
            {
                IsDirty = true,
                Position = new Vector2(pbMapPin.Position.X, pbMapPin.Position.Y),
                IconSize = pbMapPin.IconSize,
                Title = pbMapPin.Title,
                Description = pbMapPin.Description
            };
            TextureComponent? mapPinTexture = pbMapPin.Texture.CreateTextureComponent(sceneData);
            TryCreateGetTexturePromise(in mapPinTexture, ref mapPinComponent.TexturePromise, ref partitionComponent);

            World.Add(entity, mapPinComponent);
        }

        [Query]
        private void UpdateMapPin(ref PBMapPin pbMapPin, ref MapPinComponent mapPinComponent, ref PartitionComponent partitionComponent)
        {
            if (pbMapPin.IsDirty)
            {
                mapPinComponent.Position = new Vector2(pbMapPin.Position.X, pbMapPin.Position.Y);
                mapPinComponent.IconSize = pbMapPin.IconSize;
                mapPinComponent.Title = pbMapPin.Title;
                mapPinComponent.Description = pbMapPin.Description;
                TextureComponent? mapPinTexture = pbMapPin.Texture.CreateTextureComponent(sceneData);
                TryCreateGetTexturePromise(in mapPinTexture, ref mapPinComponent.TexturePromise, ref partitionComponent);
                mapPinComponent.IsDirty = true;

                pbMapPin.IsDirty = false;
            }
        }

        private void TryCreateGetTexturePromise(in TextureComponent? textureComponent, ref Promise? promise, ref PartitionComponent partitionComponent)
        {
            if (textureComponent == null)
                return;

            TextureComponent textureComponentValue = textureComponent.Value;
            promise = Promise.Create(World, new GetTextureIntention
            {
                CommonArguments = new CommonLoadingArguments(textureComponentValue.Src, attempts: ATTEMPTS_COUNT),
                WrapMode = textureComponentValue.WrapMode,
                FilterMode = textureComponentValue.FilterMode,
            }, partitionComponent);
        }
    }
}
