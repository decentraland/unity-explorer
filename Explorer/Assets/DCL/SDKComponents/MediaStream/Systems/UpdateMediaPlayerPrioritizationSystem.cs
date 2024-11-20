#define DEBUG_VIDEO_PRIORITIES
// When the definition is enabled, a colored cube will be created next to each video's mesh renderer. Its color corresponds to the current priority of the video.
// Green means higher priority, red means lower priority. Blue means that it was prioritized but it is not allowed to play due to the maximum limit.
// Black means it has been discarded from prioritization.

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
    /// <summary>
    /// A system that limits the amount of video streams that can be playing at the same time on camera.
    /// Videos are streamed using MediaPlayers. MediaPlayers can be played or paused manually outside this system. Those that are played manually will be
    /// prioritized. Depending on the position and priority of the renderer every the video, it will keep playing or will be "culled", which means paused
    /// until its priority/position allows it to resume the stream at the current time (not at the time it was paused, as if it was never paused at all).
    /// </summary>
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
        private float cachedCameraTanValue; // A pre-calculated value of the tangent of half the FOV, used to calculate the size on screen

        public UpdateMediaPlayerPrioritizationSystem(World world, IExposedCameraData exposedCameraData, VideoPrioritizationSettings videoPrioritizationSettings) : base(world)
        {
            this.videoPrioritizationSettings = videoPrioritizationSettings;
            this.exposedCameraData = exposedCameraData;
        }

        public override void Initialize()
        {
            base.Initialize();

            cachedCameraVerticalFOV = exposedCameraData.CinemachineBrain!.OutputCamera.fieldOfView;
            cachedCameraHorizontalFOV = Camera.VerticalToHorizontalFieldOfView(exposedCameraData.CinemachineBrain.OutputCamera.fieldOfView, exposedCameraData.CinemachineBrain.OutputCamera.aspect);
            float cameraHalfFov = cachedCameraVerticalFOV * 0.5f * Mathf.Deg2Rad;
            cachedCameraTanValue = Mathf.Tan(cameraHalfFov);
        }

        protected override void Update(float t)
        {
            sortedVideoPriorities.Clear();
            AddVideoStatesByPriorityQuery(World);
            UpdateVideoPrioritiesQuery(World, cachedCameraVerticalFOV, cachedCameraHorizontalFOV, exposedCameraData.WorldPosition.Value, exposedCameraData.WorldRotation.Value);
            UpdateVideoStateDependingOnPriorityQuery(World, videoPrioritizationSettings.MaximumSimultaneousVideos);

#if DEBUG_VIDEO_PRIORITIES

            ReportHub.Log(GetReportData(), "<color=cyan>" + sortedVideoPriorities.Count + "</color>");

            float maximumPlayingVideos = Mathf.Min(sortedVideoPriorities.Count, videoPrioritizationSettings.MaximumSimultaneousVideos);

            for (int i = 0; i < sortedVideoPriorities.Count; ++i)
            {
                sortedVideoPriorities[i].DebugPrioritySign.material.color = (i >= maximumPlayingVideos) ? Color.blue :
                                                                                                          new Color(i / maximumPlayingVideos, 1.0f - i / maximumPlayingVideos, 0.0f, 1.0f);
            }
#endif
        }

        /// <summary>
        /// Adds the VideoStateByPriorityComponent to all entities streaming a video with a MediaPlayer.
        /// </summary>
        [Query]
        [All(typeof(PBVideoPlayer))]
        [None(typeof(VideoStateByPriorityComponent))]
        private void AddVideoStatesByPriority(Entity entity, in MediaPlayerComponent mediaPlayer, in VideoTextureConsumer videoTextureConsumer)
        {
            // Using the diagonal of the box instead of the height, meshes that occupy "the same" area on screen should have the same priority
            float videoMeshLocalSize = (videoTextureConsumer.BoundsMax - videoTextureConsumer.BoundsMin).magnitude;

            VideoStateByPriorityComponent newVideoStateByPriority = new VideoStateByPriorityComponent(
                                                                            entity,
                                                                            videoMeshLocalSize * 0.5f,
                                                                            mediaPlayer.IsPlaying);

#if DEBUG_VIDEO_PRIORITIES
            // Adds a colored cube to a corner of the video mesh renderer which shows the priority of the video
            GameObject prioritySign = CreateDebugPrioritySign();
            prioritySign.transform.position = videoTextureConsumer.BoundsMax;
            newVideoStateByPriority.DebugPrioritySign = prioritySign.GetComponent<MeshRenderer>();
#endif

            World.Add(entity, newVideoStateByPriority);
        }

        /// <summary>
        /// Calculates the priority of each video.
        /// First, videos whose renderer's position is not on camera (imagine a cone in the XZ plane) will be culled; in the same way, those that are too far
        /// from the camera will also be culled. The rest of the videos will be prioritized calculating a score that depends on their positions and sizes.
        /// The formula of the score is: S * w0 + D * w1 + A * w2, were S, D and A are values in [0, 1] and wX are arbitrary multipliers.
        /// S = The size of the video mesh renderer relative to the screen.
        /// D = The distance from the camera to the video mesh renderer, with a maximum.
        /// A = The angle of the camera with respect to the video mesh renderer (the nearer to the center of the screen, the higher).
        /// The first M videos with the highest score will resume / keep playing (were M is the maximum amount of videos allowed), the rest will be culled.
        /// Every video is stored in a list sorted by score.
        /// </summary>
        [Query]
        private void UpdateVideoPriorities([Data] float cameraFov, [Data] float cameraHorizontalFov,
                                           [Data] Vector3 cameraWorldPosition, [Data] Quaternion cameraWorldRotation,
                                           in MediaPlayerComponent mediaPlayer,
                                           ref VideoStateByPriorityComponent videoStateByPriority,
                                           ref VideoTextureConsumer videoTextureConsumer)
        {

#if DEBUG_VIDEO_PRIORITIES
            videoStateByPriority.DebugPrioritySign.transform.position = videoTextureConsumer.BoundsMax;
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
                    ReportHub.Log(GetReportData(),"Video: PLAYED MANUALLY");
#endif
                }
                else
                {
                    videoStateByPriority.WantsToPlay = false;

#if DEBUG_VIDEO_PRIORITIES
                    ReportHub.Log(GetReportData(),"Video: PAUSED MANUALLY");
#endif
                }
            }

            // If the video should be playing according to external state changes...
            if (videoStateByPriority.WantsToPlay)
            {
                Vector3 boundsMin = videoTextureConsumer.BoundsMin;
                Vector3 boundsMax = videoTextureConsumer.BoundsMax;
                Vector3 videoCenterPosition = (boundsMax + boundsMin) * 0.5f;

                bool isCameraInVideoBoundingBox = cameraWorldPosition.x >= boundsMin.x && cameraWorldPosition.y >= boundsMin.y && cameraWorldPosition.z >= boundsMin.z &&
                                                  cameraWorldPosition.x <= boundsMax.x && cameraWorldPosition.y <= boundsMax.y && cameraWorldPosition.z <= boundsMax.z;

                Vector3 cameraToVideo = (videoCenterPosition - cameraWorldPosition).normalized;
                Vector3 cameraDirection = cameraWorldRotation * Vector3.forward;
                bool isVideoInCameraFrustum = false;

                if (!isCameraInVideoBoundingBox) // If the camera is inside the BB, it's not necessary to calculate anything else, the video is considered in camera
                {
                    // Note: It was necessary to calculate a flattened version of the dot product to prevent some videos from pausing when they were big and
                    //       the character was too close, so the height of the center of the screen did not affect the result
                    Vector3 cameraToVideoFlattened = new Vector3(cameraToVideo.x, 0.0f, cameraToVideo.z).normalized;
                    Vector3 cameraDirectionFlattened = new Vector3(cameraDirection.x, 0.0f, cameraDirection.z).normalized;

                    float dotProductInXZ = Vector3.Dot(cameraToVideoFlattened, cameraDirectionFlattened);
                    isVideoInCameraFrustum = Mathf.Acos(dotProductInXZ) * Mathf.Rad2Deg <= cameraHorizontalFov * 0.5f;
                }

                // Skips videos that are out of the camera frustum in XZ
                if (isCameraInVideoBoundingBox || isVideoInCameraFrustum)
                {
                    float distance = (videoCenterPosition - cameraWorldPosition).magnitude;

                    // Skips videos that are too far
                    if (distance <= videoPrioritizationSettings.MaximumDistanceLimit)
                    {
                        float screenSize = Mathf.Clamp01(CalculateObjectHeightRelativeToScreenHeight(videoStateByPriority.HalfSize, distance));

                        // Skips videos that are too small on screen
                        if (screenSize >= videoPrioritizationSettings.MinimumSizeLimit)
                        {
                            float dotProduct = Vector3.Dot(cameraToVideo, cameraDirection);

                            // Final score
                            videoStateByPriority.Score = (videoPrioritizationSettings.MaximumDistanceLimit - distance) / videoPrioritizationSettings.MaximumDistanceLimit * videoPrioritizationSettings.DistanceWeight +
                                                         screenSize * videoPrioritizationSettings.SizeInScreenWeight +
                                                         dotProduct * videoPrioritizationSettings.AngleWeight;

#if DEBUG_VIDEO_PRIORITIES
                            ReportHub.Log(GetReportData(),$"VIDEO ENTITY[{videoStateByPriority.Entity.Id}] Dist: {distance} HSize:{videoStateByPriority.HalfSize} / {CalculateObjectHeightRelativeToScreenHeight(videoStateByPriority.HalfSize, distance)} Dot:{dotProduct} SCORE:{videoStateByPriority.Score}");
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

        /// <summary>
        /// Resumes or pauses every video depending on its priority.
        /// </summary>
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
                    // Videos are resumed at the current time, not at the time it was paused
                    mediaPlayer.MediaPlayer.Control.Play();
                    mediaPlayer.MediaPlayer.Control.Seek(seekTime);
                }

                videoStateByPriority.IsPlaying = true;

#if DEBUG_VIDEO_PRIORITIES
                ReportHub.Log(GetReportData(),"VIDEO RESUMED BY PRIORITY: " + videoStateByPriority.Entity.Id + " t:" + seekTime);
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
                ReportHub.Log(GetReportData(),"VIDEO CULLED BY PRIORITY: " + videoStateByPriority.Entity.Id);
#endif
            }
        }

        /// <summary>
        /// Given the height of an object, it calculates how much screen it covers in vertical.
        /// </summary>
        /// <param name="halfHeight">A half of the height of the object.</param>
        /// <param name="distance">The distance from the camera to the object.</param>
        /// <returns>The amount of screen covered in vertical, from 0 to 1 (total coverage).</returns>
        private float CalculateObjectHeightRelativeToScreenHeight(float halfHeight, float distance)
        {
            return halfHeight / ((distance + halfHeight) * cachedCameraTanValue);
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
            GameObject.Destroy(videoStateByPriorityComponent.DebugPrioritySign?.gameObject);
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
