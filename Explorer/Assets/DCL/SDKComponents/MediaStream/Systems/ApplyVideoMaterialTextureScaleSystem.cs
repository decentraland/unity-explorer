using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using DCL.Diagnostics;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Materials;
using ECS.Unity.Materials.Components;
using ECS.Unity.Textures.Components;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     System that handles video material texture scaling.
    ///     This system runs a query on entities with MaterialScaleRequestComponent flag
    ///     and applies texture scaling to video materials when the media player is ready.
    /// </summary>
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [UpdateAfter(typeof(MaterialLoadingGroup))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    public partial class ApplyVideoMaterialTextureScaleSystem : BaseUnityLoopSystem
    {
        private readonly IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap;

        public ApplyVideoMaterialTextureScaleSystem(World world, IReadOnlyDictionary<CRDTEntity, Entity> entitiesMap) : base(world)
        {
            this.entitiesMap = entitiesMap;
        }

        protected override void Update(float t)
        {
            UpdateVideoMaterialQuery(World);
        }

        [Query]
        [All(typeof(MaterialScaleRequestComponent), typeof(MaterialComponent))]
        public void UpdateVideoMaterial(Entity entity, ref MaterialComponent materialComponent)
        {
            if (!TryGetVideoPlayerEntity(materialComponent, out var videoPlayerEntity))
                return;

            if (!World.TryGet<VideoTextureConsumer>(videoPlayerEntity, out var videoTextureConsumer))
                return;

            if (!TryGetTextureScale(videoPlayerEntity, out var textureScale))
                return;

            ApplyTextureScaleToMaterial(entity, videoTextureConsumer, textureScale);
        }

        private bool TryGetVideoPlayerEntity(MaterialComponent materialComponent, out Entity videoPlayerEntity)
        {
            videoPlayerEntity = default;

            // Check albedo texture for video
            if (materialComponent.Data.Textures.AlbedoTexture is { IsVideoTexture: true } || materialComponent.Data.Textures.AlphaTexture is { IsVideoTexture: true })
            {
                if(entitiesMap.TryGetValue(materialComponent.Data.Textures.AlbedoTexture.Value.VideoPlayerEntity, out videoPlayerEntity))
                    return true;
            }

            return false;
        }

        private bool TryGetTextureScale(Entity videoPlayerEntity, out Vector2 textureScale)
        {
            textureScale = Vector2.zero;

            if (!World.TryGet<MediaPlayerComponent>(videoPlayerEntity, out var mediaPlayerComponent))
                return false;

            if (!mediaPlayerComponent.MediaPlayer.IsReady)
                return false;

            textureScale = mediaPlayerComponent.MediaPlayer.GetTexureScale;
            return true;
        }

        private void ApplyTextureScaleToMaterial(Entity e, VideoTextureConsumer videoTextureConsumer, Vector2 textureScale)
        {
            videoTextureConsumer.SetTextureScale(textureScale);
            World.Remove<MaterialScaleRequestComponent>(e);
        }
    }
}
