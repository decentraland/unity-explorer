using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.Prioritization.Components;
using ECS.Unity.PrimitiveRenderer.Components;
using ECS.Unity.Transforms.Components;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [UpdateAfter(typeof(UpdateMediaPlayerSystem))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    [ThrottlingEnabled]
    public partial class UpdateMediaPlayerVisibilitySystem : BaseUnityLoopSystem
    {
        private readonly IExposedCameraData exposedCameraData;
        private readonly Utility.Storage.PersistentSetting<float> maxSimultaneousVideosSetting;

        private readonly List<VideoStateByPriorityComponent> sortedVideoPriorities = new List<VideoStateByPriorityComponent>();
        private const float MAX_DISTANCE = 1000.0f;

        public UpdateMediaPlayerVisibilitySystem(World world, IExposedCameraData exposedCameraData) : base(world)
        {
            this.exposedCameraData = exposedCameraData;
            maxSimultaneousVideosSetting = Utility.Storage.PersistentSetting.CreateFloat("Settings_MaxSimultaneousVideos", 1);
        }

        protected override void Update(float t)
        {
            sortedVideoPriorities.Clear();
            AddVideoStatesByPriorityQuery(World);
            UpdateVideoPrioritiesQuery(World);
            UpdateVideoVisibilityDependingOnPriorityQuery(World, (int)maxSimultaneousVideosSetting.Value);

            Debug.Log("<color=cyan>" + sortedVideoPriorities.Count + "</color>");
        }

        [Query]
        [None(typeof(VideoStateByPriorityComponent))]
        private void AddVideoStatesByPriority(Entity entity, in MediaPlayerComponent mediaPlayer, in PrimitiveMeshRendererComponent rendererComponent)
        {
            World.Add(entity, new VideoStateByPriorityComponent(){ Entity = entity, WantsToPlay = mediaPlayer.State == VideoState.VsPlaying, Size = CalculateSizeFromMeshRenderer(rendererComponent.MeshRenderer)});
        }

        private float CalculateSizeFromMeshRenderer(MeshRenderer renderer)
        {
            return renderer.bounds.size.y;
        }

        private float CalculateCullRelativeHeight(float defaultFOV, float size, float cullDistance)
        {
            //The cull distance is at loading distance - 1 parcel for some space buffer
            //(It should first load, and then cull in)
            float halfFov = defaultFOV / 2.0f * Mathf.Deg2Rad;
            float tanValue = Mathf.Tan(halfFov);
            return CalculateScreenRelativeCullHeight(tanValue, cullDistance + size / 2, size / 2, 0);
        }

        //This will give us the percent of the screen in which the object will be culled when being at (unloadingDistance - 1) parcel
        public static float CalculateScreenRelativeCullHeight(float tanValue, float distanceToCenter, float objectExtents, float defaultLODBias)
        {
            return objectExtents / (distanceToCenter * tanValue) * defaultLODBias;
        }

        [Query]
        private void UpdateVideoPriorities(Entity entity, in MediaPlayerComponent mediaPlayer, ref VideoStateByPriorityComponent videoStateByPriority, in PartitionComponent partitionComponent, in TransformComponent transform)
        {
            if(!videoStateByPriority.WantsToPlay)
                return;

            float dotProduct = Vector3.Dot((transform.Transform.position - exposedCameraData.WorldPosition.Value).normalized, exposedCameraData.WorldRotation.Value * Vector3.forward);

            // Skips videos that are behind the camera
            if (dotProduct > 0.0f)
            {
                float sqrDistance = Mathf.Clamp((transform.Transform.position - exposedCameraData.WorldPosition.Value).sqrMagnitude, 0.0f, MAX_DISTANCE);
                float screenSize = CalculateCullRelativeHeight(90.0f, videoStateByPriority.Size, Mathf.Sqrt(sqrDistance));

                const float DISTANCE_WEIGHT = 2.0f;
                const float ANGLE_WEIGHT = 4.0f;
                const float SCREEN_SIZE_WEIGHT = 1.0f;

                videoStateByPriority.Score = (MAX_DISTANCE - sqrDistance) / MAX_DISTANCE * DISTANCE_WEIGHT +
                                             screenSize * SCREEN_SIZE_WEIGHT +
                                             dotProduct * ANGLE_WEIGHT;

                Debug.Log($"[{entity.Id}] Dist: {sqrDistance} Size:{videoStateByPriority.Size} Dot:{dotProduct} SCORE:{videoStateByPriority.Score}");

                // Sorts the playing video list by score
                int i = 0;

                for (; i < sortedVideoPriorities.Count; ++i)
                {
                    if (sortedVideoPriorities[i].Score <= videoStateByPriority.Score) { break; }
                }

                if (i <= sortedVideoPriorities.Count) { sortedVideoPriorities.Insert(i, videoStateByPriority); }
            }
        }

        [Query]
        private void UpdateVideoVisibilityDependingOnPriority([Data] int maxSimultaneousVideos, ref VideoStateByPriorityComponent videoStateByPriority, ref MediaPlayerComponent mediaPlayer)
        {
            bool mustPlay = false;
            int playingVideoCount = Mathf.Min(maxSimultaneousVideos, sortedVideoPriorities.Count);
            double pauseDuration = 0.0f;

            // Is the current video player in the list of playing video players? And is it in the first N elements that are allowed to play at maximum? And should it be playing?
            for (int i = 0; i < playingVideoCount; ++i)
            {
                if (sortedVideoPriorities[i].Entity == videoStateByPriority.Entity &&
                    sortedVideoPriorities[i].WantsToPlay)
                {
                    mustPlay = true;
                    pauseDuration = Time.realtimeSinceStartup - videoStateByPriority.MediaPlayStartTime;

                    break;
                }
            }

            if (mustPlay && !videoStateByPriority.IsPlaying)
            {
                double seekTime = pauseDuration % mediaPlayer.Duration;

                if (!mediaPlayer.MediaPlayer.Control.IsPlaying())
                {
                    mediaPlayer.MediaPlayer.Control.Play();
                    mediaPlayer.MediaPlayer.Control.Seek(seekTime);
                }

                videoStateByPriority.IsPlaying = true;
                Debug.Log("xxx: ----PLAY: " + videoStateByPriority.Entity.Id + " t:" + seekTime);
            }
            else if(!mustPlay && (videoStateByPriority.IsPlaying || mediaPlayer.MediaPlayer.Control.IsPlaying()))
            {
                if (mediaPlayer.MediaPlayer.Control.IsPlaying())
                {
                    mediaPlayer.MediaPlayer.Control.Pause();
                }

                videoStateByPriority.LastTimePaused = Time.realtimeSinceStartup;
                videoStateByPriority.IsPlaying = false;

                Debug.Log("xxx: ----PAUSE: " + videoStateByPriority.Entity.Id);
            }
        }

    }
}
