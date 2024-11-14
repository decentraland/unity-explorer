#define DEBUG_VIDEO_PRIORITIES

using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.SDKComponents.MediaStream.Settings;
using ECS.Abstract;
using ECS.Groups;
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
        private readonly VideoPrioritizationSettings videoPrioritizationSettings;

        private readonly List<VideoStateByPriorityComponent> sortedVideoPriorities = new ();
        private const float MAX_DISTANCE = 100.0f;

        public UpdateMediaPlayerVisibilitySystem(World world, IExposedCameraData exposedCameraData, VideoPrioritizationSettings videoPrioritizationSettings) : base(world)
        {
            this.videoPrioritizationSettings = videoPrioritizationSettings;

            this.exposedCameraData = exposedCameraData;

            // TODO: This should be done in a better way:
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
                UpdateVideoStateDependingOnPriorityQuery(World, (int)maxSimultaneousVideosSetting.Value);
            }

#if DEBUG_VIDEO_PRIORITIES

            Debug.Log("<color=cyan>" + sortedVideoPriorities.Count + "</color>");

            float maximumPlayingVideos = Mathf.Min(sortedVideoPriorities.Count, maxSimultaneousVideosSetting.Value);

            for (int i = 0; i < sortedVideoPriorities.Count; ++i)
            {
                sortedVideoPriorities[i].DebugPrioritySign.material.color = (i >= maximumPlayingVideos) ? Color.blue :
                                                                                                          new Color(i / maximumPlayingVideos, 1.0f - i / maximumPlayingVideos, 0.0f, 1.0f);
            }
#endif
        }

        [Query]
        [None(typeof(VideoStateByPriorityComponent))]
        private void AddVideoStatesByPriority(Entity entity, in MediaPlayerComponent mediaPlayer, in PrimitiveMeshRendererComponent rendererComponent)
        {
            float videoMeshLocalVerticalSize = rendererComponent.MeshRenderer.bounds.size.y;

            VideoStateByPriorityComponent newVideoStateByPriority = new VideoStateByPriorityComponent(){
                                                                            Entity = entity,
                                                                            WantsToPlay = mediaPlayer.IsPlaying,
                                                                            Size = videoMeshLocalVerticalSize};

#if DEBUG_VIDEO_PRIORITIES
            GameObject prioritySign = CreateDebugPrioritySign();
            prioritySign.transform.position = rendererComponent.MeshRenderer.bounds.max;
            newVideoStateByPriority.DebugPrioritySign = prioritySign.GetComponent<MeshRenderer>();
#endif

            World.Add(entity, newVideoStateByPriority);
        }

        [Query]
        private void UpdateVideoPriorities([Data] float cameraFov, [Data] float cameraHorizontalFov,
                                            [Data] Vector3 cameraWorldPosition, [Data] Quaternion cameraWorldRotation,
                                            in MediaPlayerComponent mediaPlayer,
                                            ref VideoStateByPriorityComponent videoStateByPriority, in TransformComponent transform)
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

            if (videoStateByPriority.WantsToPlay)
            {
                float dotProduct = Vector3.Dot((transform.Transform.position - cameraWorldPosition).normalized, cameraWorldRotation * Vector3.forward);

                // Skips videos that are out of the camera
                if (Mathf.Acos(dotProduct) * Mathf.Rad2Deg <= cameraHorizontalFov * 0.5f)
                {
                    float distance = Mathf.Clamp((transform.Transform.position - cameraWorldPosition).magnitude, 0.0f, MAX_DISTANCE);
                    float screenSize = Mathf.Clamp01(CalculateCullRelativeHeight(cameraFov, videoStateByPriority.Size, Mathf.Sqrt(distance)));

                    videoStateByPriority.Score = (MAX_DISTANCE - distance) / MAX_DISTANCE * videoPrioritizationSettings.DistanceWeight +
                                                 screenSize * videoPrioritizationSettings.SizeInScreenWeight +
                                                 dotProduct * videoPrioritizationSettings.AngleWeight;

#if DEBUG_VIDEO_PRIORITIES
                    Debug.Log($"VIDEO ENTITY[{videoStateByPriority.Entity.Id}] Dist: {distance} Size:{videoStateByPriority.Size} / {screenSize} Dot:{dotProduct} SCORE:{videoStateByPriority.Score}");
#endif

                    // Sorts the playing video list by score
                    int i = 0;

                    for (; i < sortedVideoPriorities.Count; ++i)
                    {
                        if (sortedVideoPriorities[i].Score <= videoStateByPriority.Score) { break; }
                    }

                    if (i <= sortedVideoPriorities.Count) { sortedVideoPriorities.Insert(i, videoStateByPriority); }
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

        private float CalculateCullRelativeHeight(float cameraFOV, float size, float distance)
        {
            float halfFov = cameraFOV / 2.0f * Mathf.Deg2Rad;
            float tanValue = Mathf.Tan(halfFov);
            return size * 0.5f / (distance * tanValue);
        }

    }
}
