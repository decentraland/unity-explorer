using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.InWorldCamera.CameraReelStorageService.Schemas;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using ECS;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace DCL.InWorldCamera
{
    public class ScreenshotMetadataBuilder
    {
        private const string UNKNOWN_USER = "Unknown";
        private const string UNKNOWN_USER_WALLET = "0x000000000000000000000000000000000000000";
        private const string UNKNOWN_PLACE = "Unknown place";

        private readonly SelfProfile selfProfile;
        private readonly CharacterController characterObjectController;
        private readonly RealmData realmData;
        private readonly IPlacesAPIService placesAPIService;

        private readonly List<VisiblePerson> visiblePeople = new (32);

        private Plane[]? frustumPlanes;
        private Camera? camera;
        private Vector2Int sceneParcel;

        private ScreenshotMetadata? metadata;

        public bool MetadataIsReady { get; private set; }

        public ScreenshotMetadataBuilder(SelfProfile selfProfile, CharacterController characterObjectController, RealmData realmData, IPlacesAPIService placesAPIService)
        {
            this.selfProfile = selfProfile;
            this.characterObjectController = characterObjectController;
            this.realmData = realmData;
            this.placesAPIService = placesAPIService;
        }

        public ScreenshotMetadata GetMetadataAndReset()
        {
            MetadataIsReady = false;
            return metadata;
        }

        public void Init(Vector2Int sceneParcel, Plane[] frustumPlanes, Camera camera)
        {
            MetadataIsReady = false;
            visiblePeople.Clear();

            this.frustumPlanes = frustumPlanes;
            this.camera = camera;
            this.sceneParcel = sceneParcel;
        }

        public void AddSelfProfile(bool isEmoting) =>
            AddProfile(selfProfile.OwnProfile, characterObjectController, isEmoting);

        public void AddProfile(Profile? profile, Collider avatarCollider, bool isEmoting)
        {
            if (GeometryUtility.TestPlanesAABB(frustumPlanes, avatarCollider.bounds))
            {
                visiblePeople.Add(new VisiblePerson
                {
                    userName = profile?.Name ?? UNKNOWN_USER,
                    userAddress = string.IsNullOrEmpty(profile?.UserId) ? UNKNOWN_USER_WALLET : profile!.UserId,
                    isGuest = profile is { HasConnectedWeb3: false },
                    isEmoting = isEmoting,
                    screenRect = camera is null ? Rect.zero : CalculateScreenRect(camera, avatarCollider.bounds),
                    wearables = FilterNonBaseWearables(profile?.Avatar.Wearables ?? Array.Empty<URN>()),
                });
            }
        }

        public async UniTask BuildAsync(CancellationToken ct)
        {
            (string sceneName, string placeId) = await GetSceneInfoAsync(sceneParcel, ct);

            FillMetadata(selfProfile.OwnProfile, realmData, sceneParcel, sceneName, placeId, visiblePeople.ToArray());

            MetadataIsReady = true;
        }

        private async UniTask<(string, string)> GetSceneInfoAsync(Vector2Int at, CancellationToken ct)
        {
            PlacesData.PlaceInfo? placeInfo;

            if (realmData.ScenesAreFixed)
                placeInfo = await placesAPIService.GetWorldAsync(at, realmData.RealmName, ct);
            else
                placeInfo = await placesAPIService.GetPlaceAsync(at, ct);

            return placeInfo == null ? (UNKNOWN_PLACE, UNKNOWN_PLACE) : (placeInfo.title, placeInfo.id);
        }

        private static string[] FilterNonBaseWearables(IReadOnlyCollection<URN> avatarWearables)
        {
            var wearables = new List<string>();

            foreach (URN w in avatarWearables)
                if (!w.IsBaseWearable())
                    wearables.Add(w.ToString());

            return wearables.ToArray();
        }

        /// <summary>
        /// Projects world bounds onto the saved photo: the returned rectangle is normalized to the image,
        /// with the origin at its top-left corner. Zero when the bounds do not resolve to an area of it.
        /// </summary>
        internal static Rect CalculateScreenRect(Camera camera, Bounds bounds)
        {
            Vector3 min = bounds.min;
            Vector3 size = bounds.size;

            var frameMin = new Vector2(float.MaxValue, float.MaxValue);
            var frameMax = new Vector2(float.MinValue, float.MinValue);
            var projectedAnyCorner = false;

            // The eight corners of the box, each bit of the index picking the low or the high side of an axis.
            for (var corner = 0; corner < 8; corner++)
            {
                var world = new Vector3(
                    min.x + ((corner & 1) == 0 ? 0f : size.x),
                    min.y + ((corner & 2) == 0 ? 0f : size.y),
                    min.z + ((corner & 4) == 0 ? 0f : size.z));

                Vector3 viewportPoint = camera.WorldToViewportPoint(world);

                // Behind the camera the projection mirrors, which would stretch the rectangle across the
                // whole photo. The corners in front of it still describe the visible part of the box.
                if (viewportPoint.z <= 0f) continue;

                projectedAnyCorner = true;

                Vector2 framePoint = ViewportToFrame(viewportPoint, camera.aspect);
                frameMin = Vector2.Min(frameMin, framePoint);
                frameMax = Vector2.Max(frameMax, framePoint);
            }

            if (!projectedAnyCorner) return Rect.zero;

            frameMin = Vector2.Max(frameMin, Vector2.zero);
            frameMax = Vector2.Min(frameMax, Vector2.one);

            if (frameMax.x <= frameMin.x || frameMax.y <= frameMin.y) return Rect.zero;

            return new Rect(frameMin.x, frameMin.y, frameMax.x - frameMin.x, frameMax.y - frameMin.y);
        }

        /// <summary>
        /// Re-bases a point of the camera's viewport on the frame the photo is cropped to: a centred box of
        /// the saved image's aspect ratio, scaled by <see cref="ScreenRecorder.FRAME_SCALE" /> on the
        /// limiting side, and measured from the top down rather than from the bottom up.
        /// </summary>
        private static Vector2 ViewportToFrame(Vector3 viewportPoint, float screenAspectRatio)
        {
            float frameWidth;
            float frameHeight;

            if (screenAspectRatio > ScreenRecorder.TARGET_ASPECT_RATIO)
            {
                frameHeight = ScreenRecorder.FRAME_SCALE;
                frameWidth = frameHeight * ScreenRecorder.TARGET_ASPECT_RATIO / screenAspectRatio;
            }
            else
            {
                frameWidth = ScreenRecorder.FRAME_SCALE;
                frameHeight = frameWidth * screenAspectRatio / ScreenRecorder.TARGET_ASPECT_RATIO;
            }

            return new Vector2(
                (viewportPoint.x - (0.5f - (frameWidth / 2f))) / frameWidth,
                1f - ((viewportPoint.y - (0.5f - (frameHeight / 2f))) / frameHeight));
        }

        internal void FillMetadata(Profile? profile, RealmData realm, Vector2Int playerPosition,
            string sceneName, string placeId, VisiblePerson[] visiblePeople)
        {
            if (metadata == null)
                metadata = new ScreenshotMetadata
                {
                    userName = profile?.Name ?? UNKNOWN_USER,
                    userAddress = string.IsNullOrEmpty(profile?.UserId) ? UNKNOWN_USER_WALLET : profile!.UserId,
                    dateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                    realm = realm?.RealmName,
                    placeId = placeId,
                    scene = new Scene
                    {
                        name = sceneName,
                        location = new Location(playerPosition),
                    },
                    visiblePeople = visiblePeople,
                };
            else
            {
                metadata.userName = profile?.Name ?? UNKNOWN_USER;
                metadata.userAddress = string.IsNullOrEmpty(profile?.UserId) ? UNKNOWN_USER_WALLET : profile!.UserId;
                metadata.dateTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
                metadata.realm = realm?.RealmName;
                metadata.placeId = placeId;
                metadata.scene.name = sceneName;
                metadata.scene.location = new Location(playerPosition);
                metadata.visiblePeople = visiblePeople;
            }
        }
    }
}
