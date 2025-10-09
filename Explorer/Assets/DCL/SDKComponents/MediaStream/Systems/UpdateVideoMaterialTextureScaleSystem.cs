using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.GLTFContainer;
using ECS.Unity.Materials;
using ECS.Unity.Textures.Components;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [UpdateAfter(typeof(MaterialLoadingGroup))]
    [UpdateAfter(typeof(GltfContainerGroup))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    public partial class UpdateVideoMaterialTextureScaleSystem : BaseUnityLoopSystem
    {
        private readonly IPerformanceBudget capFrameBudget;

        public UpdateVideoMaterialTextureScaleSystem(World world, IPerformanceBudget capFrameBudget) : base(world)
        {
            this.capFrameBudget = capFrameBudget;
        }

        protected override void Update(float t)
        {
            UpdateMaterialQuery(World);
        }

        [Query]
        public void UpdateMaterial(in MediaPlayerComponent mediaPlayerComponent, ref VideoTextureConsumer videoTextureConsumer)
        {
            if (!capFrameBudget.TrySpendBudget())
                return;

            bool needsUpdate = videoTextureConsumer.IsDirty || ShouldReapplyScale(mediaPlayerComponent, ref videoTextureConsumer);

            if (!needsUpdate)
                return;

            if (!IsReady(mediaPlayerComponent, ref videoTextureConsumer))
                return;

            if (!TryHandleRequest(mediaPlayerComponent, out var texScale))
                return;

            videoTextureConsumer.SetTextureScale(texScale);
            videoTextureConsumer.IsDirty = false;
        }

        private bool TryHandleRequest(in MediaPlayerComponent mediaPlayerComponent, out Vector2 texScale)
        {
            texScale = Vector2.zero;

            if (!mediaPlayerComponent.MediaPlayer.IsReady)
            {
                if (mediaPlayerComponent.MediaPlayer.IsAvProPlayer(out AvProPlayer? _))
                {
                    // AV Pro player not initialized yet (should not happen, but we can just wait)
                    ReportHub.LogWarning(GetReportCategory(), $"Handling {nameof(UpdateVideoMaterialTextureScaleSystem)} before the AV Pro player was initialized");
                }
                return false;
            }
            texScale = mediaPlayerComponent.MediaPlayer.GetTexureScale;
            return true;
        }

        private bool IsReady(in MediaPlayerComponent mediaPlayerComponent, ref VideoTextureConsumer videoTextureConsumer)
        {
            if (!mediaPlayerComponent.MediaPlayer.IsReady)
                return false;

            if (!mediaPlayerComponent.MediaPlayer.MediaOpened)
                return false;

            if (mediaPlayerComponent.MediaPlayer.LastTexture() == null)
                return false;

            if (videoTextureConsumer.renderers.Count == 0)
                return false;

            foreach (var renderer in videoTextureConsumer.renderers)
            {
                if (renderer.sharedMaterial == null)
                    return false;
            }

            return true;
        }

        private bool ShouldReapplyScale(in MediaPlayerComponent mediaPlayerComponent, ref VideoTextureConsumer videoTextureConsumer)
        {
            if (!mediaPlayerComponent.MediaPlayer.IsReady)
                return false;

            return videoTextureConsumer.NeedsScaleReapplication(mediaPlayerComponent.MediaPlayer.GetTexureScale);
        }
    }
}
