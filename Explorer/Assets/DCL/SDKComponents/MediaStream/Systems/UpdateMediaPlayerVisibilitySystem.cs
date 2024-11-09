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
using System;
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
        private const float MAX_DISTANCE = 100.0f;
        private const float DISTANCE_WEIGHT = 2.0f;
        private const float ANGLE_WEIGHT = 4.0f;
        private const float SCREEN_SIZE_WEIGHT = 1.0f;

        public UpdateMediaPlayerVisibilitySystem(World world, IExposedCameraData exposedCameraData) : base(world)
        {
            this.exposedCameraData = exposedCameraData;
            maxSimultaneousVideosSetting = Utility.Storage.PersistentSetting.CreateFloat("Settings_MaxSimultaneousVideos", 1);
        }

        protected override void Update(float t)
        {
            sortedVideoPriorities.Clear();
            AddVideoStatesByPriorityQuery(World);

            if (exposedCameraData.CinemachineBrain != null)
            {
                float horizontalFOV = Camera.VerticalToHorizontalFieldOfView(exposedCameraData.CinemachineBrain.OutputCamera.fieldOfView, exposedCameraData.CinemachineBrain.OutputCamera.aspect);
                UpdateVideoPrioritiesQuery(World, exposedCameraData.CinemachineBrain.OutputCamera.fieldOfView, horizontalFOV, exposedCameraData.WorldPosition.Value, exposedCameraData.WorldRotation.Value);
                UpdateVideoVisibilityDependingOnPriorityQuery(World, (int)maxSimultaneousVideosSetting.Value);
            }

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

        private float CalculateCullRelativeHeight(float cameraFOV, float size, float distance)
        {
            float halfFov = cameraFOV / 2.0f * Mathf.Deg2Rad;
            float tanValue = Mathf.Tan(halfFov);
            return size * 0.5f / (distance * tanValue);
        }

        [Query]
        private void UpdateVideoPriorities([Data] float cameraFov, [Data] float cameraHorizontalFov,
                                            [Data] Vector3 cameraWorldPosition, [Data] Quaternion cameraWorldRotation,
                                            ref VideoStateByPriorityComponent videoStateByPriority, in TransformComponent transform)
        {
            if(!videoStateByPriority.WantsToPlay)
                return;

            float dotProduct = Vector3.Dot((transform.Transform.position - cameraWorldPosition).normalized, cameraWorldRotation * Vector3.forward);

            // Skips videos that are out of the camera
            if (Mathf.Acos(dotProduct) * Mathf.Rad2Deg <= cameraHorizontalFov * 0.5f)
            {
                float distance = Mathf.Clamp((transform.Transform.position - cameraWorldPosition).magnitude, 0.0f, MAX_DISTANCE);
                float screenSize = Mathf.Clamp01(CalculateCullRelativeHeight(cameraFov, videoStateByPriority.Size, Mathf.Sqrt(distance)));

                videoStateByPriority.Score = (MAX_DISTANCE - distance) / MAX_DISTANCE * DISTANCE_WEIGHT +
                                             screenSize * SCREEN_SIZE_WEIGHT +
                                             dotProduct * ANGLE_WEIGHT;

                Debug.Log($"[{videoStateByPriority.Entity.Id}] Dist: {distance} Size:{videoStateByPriority.Size} / {screenSize} Dot:{dotProduct} SCORE:{videoStateByPriority.Score}");
//                Debug.Log(Mathf.Acos(dotProduct) * Mathf.Rad2Deg + " --- " + horizontalFOV * 0.5f);

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
