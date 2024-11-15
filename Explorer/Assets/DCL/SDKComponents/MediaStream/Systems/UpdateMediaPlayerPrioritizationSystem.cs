//#define DEBUG_VIDEO_PRIORITIES

using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.MediaStream.Settings;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Textures.Components;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.MediaStream
{
    [UpdateInGroup(typeof(SyncedPresentationSystemGroup))]
    [UpdateAfter(typeof(UpdateMediaPlayerSystem))]
    [LogCategory(ReportCategory.MEDIA_STREAM)]
    [ThrottlingEnabled]
    public partial class UpdateMediaPlayerPrioritizationSystem : BaseUnityLoopSystem
    {
        private readonly IExposedCameraData exposedCameraData;
        private readonly VideoPrioritizationSettings videoPrioritizationSettings;

        private readonly List<VideoStateByPriorityComponent> sortedVideoPriorities = new ();

        // Note: it was necessary to cache this in order to avoid a change in FOV when the character runs that ruins the computation of priorities
        private float cachedCameraVerticalFOV;
        private float cachedCameraHorizontalFOV;

        public UpdateMediaPlayerPrioritizationSystem(World world, IExposedCameraData exposedCameraData, VideoPrioritizationSettings videoPrioritizationSettings) : base(world)
        {
            this.videoPrioritizationSettings = videoPrioritizationSettings;

            this.exposedCameraData = exposedCameraData;
        }

        protected override void Update(float t)
        {
            sortedVideoPriorities.Clear();
            AddVideoStatesByPriorityQuery(World);

            if (exposedCameraData.CinemachineBrain != null)
            {
                // If camera FOV data was not cached yet...
                if (cachedCameraVerticalFOV == 0.0f)
                {
                    cachedCameraVerticalFOV = exposedCameraData.CinemachineBrain.OutputCamera.fieldOfView;
                    cachedCameraHorizontalFOV = Camera.VerticalToHorizontalFieldOfView(exposedCameraData.CinemachineBrain.OutputCamera.fieldOfView, exposedCameraData.CinemachineBrain.OutputCamera.aspect);
                }

                UpdateVideoPrioritiesQuery(World, cachedCameraVerticalFOV, cachedCameraHorizontalFOV, exposedCameraData.WorldPosition.Value, exposedCameraData.WorldRotation.Value);
                UpdateVideoStateDependingOnPriorityQuery(World, videoPrioritizationSettings.MaximumSimultaneousVideos);
            }

#if DEBUG_VIDEO_PRIORITIES

            Debug.Log("<color=cyan>" + sortedVideoPriorities.Count + "</color>");

            float maximumPlayingVideos = Mathf.Min(sortedVideoPriorities.Count, videoPrioritizationSettings.MaximumSimultaneousVideos);

            for (int i = 0; i < sortedVideoPriorities.Count; ++i)
            {
                sortedVideoPriorities[i].DebugPrioritySign.material.color = (i >= maximumPlayingVideos) ? Color.blue :
                                                                                                          new Color(i / maximumPlayingVideos, 1.0f - i / maximumPlayingVideos, 0.0f, 1.0f);
            }
#endif
        }

        [Query]
        [All(typeof(PBVideoPlayer))]
        [None(typeof(VideoStateByPriorityComponent))]
        private void AddVideoStatesByPriority(Entity entity, in MediaPlayerComponent mediaPlayer, in VideoTextureConsumer rendererComponent)
        {
            // Using the diagonal of the box instead of the height, meshes that occupy "the same" area on screen should have the same priority
            float videoMeshLocalSize = (rendererComponent.BoundsMax - rendererComponent.BoundsMin).magnitude;

            VideoStateByPriorityComponent newVideoStateByPriority = new VideoStateByPriorityComponent(){
                                                                            Entity = entity,
                                                                            WantsToPlay = mediaPlayer.IsPlaying,
                                                                            HalfSize = videoMeshLocalSize * 0.5f};

#if DEBUG_VIDEO_PRIORITIES
            GameObject prioritySign = CreateDebugPrioritySign();
            prioritySign.transform.position = rendererComponent.BoundsMax;
            newVideoStateByPriority.DebugPrioritySign = prioritySign.GetComponent<MeshRenderer>();
#endif

            World.Add(entity, newVideoStateByPriority);
        }

        [Query]
        private void UpdateVideoPriorities([Data] float cameraFov, [Data] float cameraHorizontalFov,
                                           [Data] Vector3 cameraWorldPosition, [Data] Quaternion cameraWorldRotation,
                                           in MediaPlayerComponent mediaPlayer,
                                           ref VideoStateByPriorityComponent videoStateByPriority,
                                           ref VideoTextureConsumer videoTextureConsumer)
        {

#if DEBUG_VIDEO_PRIORITIES
            videoStateByPriority.DebugPrioritySign.material.color = Color.black;
#endif
            // If the state of the video was changed manually...
            if (videoStateByPriority.IsPlaying != mediaPlayer.MediaPlayer.Control.IsPlaying())
            {
                if (mediaPlayer.MediaPlayer.Control.IsPlaying())
                {
                    videoStateByPriority.WantsToPlay = true;
                    videoStateByPriority.MediaPlayStartTime = Time.realtimeSinceStartup;

#if DEBUG_VIDEO_PRIORITIES
                    Debug.Log("Video: PLAYED MANUALLY");
#endif

                }
                else
                {
                    videoStateByPriority.WantsToPlay = false;

#if DEBUG_VIDEO_PRIORITIES
                    Debug.Log("Video: PAUSED MANUALLY");
#endif
                }
            }

            // If the video should be playing according to external state changes...
            if (videoStateByPriority.WantsToPlay)
            {
                Vector3 videoCenterPosition = (videoTextureConsumer.BoundsMax + videoTextureConsumer.BoundsMin) * 0.5f;

                // Note: It was necessary to calculate a flattened version of the dot product to prevent some videos from pausing when they were big and
                //       the character was too close, so the height of the center of the screen did not affect the result
                Vector3 cameraToVideo = (videoCenterPosition - cameraWorldPosition).normalized;
                Vector3 cameraToVideoFlattened = new Vector3(cameraToVideo.x, 0.0f, cameraToVideo.z).normalized;

                Vector3 cameraDirection = cameraWorldRotation * Vector3.forward;
                Vector3 cameraDirectionFlattened = new Vector3(cameraDirection.x, 0.0f, cameraDirection.z).normalized;

                float dotProductInXZ = Vector3.Dot(cameraToVideoFlattened, cameraDirectionFlattened);

                // Skips videos that are out of the camera
                if (Mathf.Acos(dotProductInXZ) * Mathf.Rad2Deg <= cameraHorizontalFov * 0.5f)
                {
                    float distance = (videoCenterPosition - cameraWorldPosition).magnitude;

                    // Skips videos that are too far
                    if (distance <= videoPrioritizationSettings.MaximumDistanceLimit)
                    {
                        float screenSize = Mathf.Clamp01(CalculateObjectHeightRelativeToScreenHeight(cameraFov, videoStateByPriority.HalfSize, distance));

                        // Skips videos that are too small on screen
                        if (screenSize >= videoPrioritizationSettings.MinimumSizeLimit)
                        {
                            float dotProduct = Vector3.Dot(cameraToVideo, cameraDirection);

                            // Final score
                            videoStateByPriority.Score = (videoPrioritizationSettings.MaximumDistanceLimit - distance) / videoPrioritizationSettings.MaximumDistanceLimit * videoPrioritizationSettings.DistanceWeight +
                                                         screenSize * videoPrioritizationSettings.SizeInScreenWeight +
                                                         dotProduct * videoPrioritizationSettings.AngleWeight;

#if DEBUG_VIDEO_PRIORITIES
                            Debug.Log($"VIDEO ENTITY[{videoStateByPriority.Entity.Id}] Dist: {distance} HSize:{videoStateByPriority.HalfSize} / {CalculateObjectHeightRelativeToScreenHeight(cameraFov, videoStateByPriority.HalfSize, distance)} Dot:{dotProduct} SCORE:{videoStateByPriority.Score}");
#endif

                            // Sorts the playing video list by score, on insertion
                            int i = 0;

                            for (; i < sortedVideoPriorities.Count; ++i)
                                if (sortedVideoPriorities[i].Score <= videoStateByPriority.Score)
                                    break;

                            if (i <= sortedVideoPriorities.Count)
                                sortedVideoPriorities.Insert(i, videoStateByPriority);
                        }
                    }
                }
            }
        }

        [Query]
        private void UpdateVideoStateDependingOnPriority([Data] int maxSimultaneousVideos, ref VideoStateByPriorityComponent videoStateByPriority, ref MediaPlayerComponent mediaPlayer)
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

#if DEBUG_VIDEO_PRIORITIES
                Debug.Log("VIDEO PLAYED BY PRIORITY: " + videoStateByPriority.Entity.Id + " t:" + seekTime);
#endif
            }
            else if(!mustPlay && (videoStateByPriority.IsPlaying || mediaPlayer.MediaPlayer.Control.IsPlaying()))
            {
                if (mediaPlayer.MediaPlayer.Control.IsPlaying())
                {
                    mediaPlayer.MediaPlayer.Control.Pause();
                }

                videoStateByPriority.IsPlaying = false;

#if DEBUG_VIDEO_PRIORITIES
                Debug.Log("VIDEO PAUSED BY PRIORITY: " + videoStateByPriority.Entity.Id);
#endif
            }
        }

        private float CalculateObjectHeightRelativeToScreenHeight(float cameraFOV, float halfHeight, float distance)
        {
            float halfFov = cameraFOV * 0.5f * Mathf.Deg2Rad;
            float tanValue = Mathf.Tan(halfFov);
            return halfHeight / ((distance + halfHeight) * tanValue);
        }

        // Called when the scene is unloaded
        public override void Dispose()
        {
#if DEBUG_VIDEO_PRIORITIES
            DestroyAllDebuggingSignsQuery(World);
#endif
            base.Dispose();
        }

        [Query]
        private void DestroyAllDebuggingSigns(in VideoStateByPriorityComponent videoStateByPriorityComponent)
        {
            GameObject.Destroy(videoStateByPriorityComponent.DebugPrioritySign.gameObject);
        }

#if DEBUG_VIDEO_PRIORITIES

        private static GameObject CreateDebugPrioritySign()
        {
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plane.transform.localScale = Vector3.one * 0.5f;
            plane.name = "DebugPrioritySign";

            Material cubeMaterial = new Material(Shader.Find("DCL/Unlit"));
            cubeMaterial.color = Color.white;
            plane.GetComponent<MeshRenderer>().material = cubeMaterial;

            GameObject.Destroy(plane.GetComponent<Collider>());

            return plane;
        }

#endif

    }
}
