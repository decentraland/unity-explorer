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

        /// <summary>Corners of an axis-aligned box; also one past the highest axis bit of a corner index.</summary>
        private const int BOX_CORNER_COUNT = 8;

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
            AddProfile(selfProfile.OwnProfile, CalculateCharacterBounds(characterObjectController), isEmoting);

        public void AddProfile(Profile? profile, Bounds avatarBounds, bool isEmoting)
        {
            if (GeometryUtility.TestPlanesAABB(frustumPlanes, avatarBounds))
            {
                visiblePeople.Add(new VisiblePerson
                {
                    userName = profile?.Name ?? UNKNOWN_USER,
                    userAddress = AddressOf(profile),
                    isGuest = profile is { HasConnectedWeb3: false },
                    isEmoting = isEmoting,
                    screenRect = camera == null ? Rect.zero : CalculateScreenRect(camera, avatarBounds),
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

        private static string AddressOf(Profile? profile) =>
            profile?.UserId?.Value is { Length: > 0 } userId ? userId : UNKNOWN_USER_WALLET;

        private static string[] FilterNonBaseWearables(IReadOnlyCollection<URN> avatarWearables)
        {
            var wearables = new List<string>();

            foreach (URN w in avatarWearables)
                if (!w.IsBaseWearable())
                    wearables.Add(w.ToString());

            return wearables.ToArray();
        }

        /// <summary>
        /// World bounds of the character's capsule, built from its shape so they stay valid while the controller is disabled
        /// (a disabled collider reports empty <see cref="Collider.bounds" />). The capsule is upright, so only position and scale apply.
        /// </summary>
        internal static Bounds CalculateCharacterBounds(CharacterController characterController)
        {
            Transform transform = characterController.transform;
            Vector3 scale = transform.lossyScale;
            float radius = characterController.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float height = Mathf.Max(characterController.height * Mathf.Abs(scale.y), radius * 2f);

            return new Bounds(transform.TransformPoint(characterController.center), new Vector3(radius * 2f, height, radius * 2f));
        }

        /// <summary>
        /// Projects world bounds onto the saved photo: the returned rectangle is normalized to the image,
        /// with the origin at its top-left corner. Zero when the bounds do not resolve to an area of it.
        /// </summary>
        internal static Rect CalculateScreenRect(Camera camera, Bounds bounds)
        {
            Transform cameraTransform = camera.transform;
            Vector3 cameraPosition = cameraTransform.position;
            Vector3 cameraForward = cameraTransform.forward;
            float nearPlane = camera.nearClipPlane;

            var frameMin = new Vector2(float.MaxValue, float.MaxValue);
            var frameMax = new Vector2(float.MinValue, float.MinValue);
            var projectedAnyPoint = false;

            // Each bit of a corner index picks the low or the high side of an axis. The visible part of the box is
            // its corners in front of the near plane plus the points where its edges cross that plane.
            for (var corner = 0; corner < BOX_CORNER_COUNT; corner++)
            {
                Vector3 from = GetCorner(bounds, corner);
                float fromDepth = Vector3.Dot(from - cameraPosition, cameraForward);

                if (fromDepth >= nearPlane)
                    Include(from);

                for (var axisBit = 1; axisBit < BOX_CORNER_COUNT; axisBit <<= 1)
                {
                    if ((corner & axisBit) != 0) continue;

                    Vector3 to = GetCorner(bounds, corner | axisBit);
                    float toDepth = Vector3.Dot(to - cameraPosition, cameraForward);

                    if ((fromDepth >= nearPlane) != (toDepth >= nearPlane))
                        Include(Vector3.Lerp(from, to, (fromDepth - nearPlane) / (fromDepth - toDepth)));
                }
            }

            if (!projectedAnyPoint) return Rect.zero;

            frameMin = Vector2.Max(frameMin, Vector2.zero);
            frameMax = Vector2.Min(frameMax, Vector2.one);

            if (frameMax.x <= frameMin.x || frameMax.y <= frameMin.y) return Rect.zero;

            return new Rect(frameMin.x, frameMin.y, frameMax.x - frameMin.x, frameMax.y - frameMin.y);

            void Include(Vector3 world)
            {
                Vector2 framePoint = ViewportToFrame(camera.WorldToViewportPoint(world), camera.aspect);
                frameMin = Vector2.Min(frameMin, framePoint);
                frameMax = Vector2.Max(frameMax, framePoint);
                projectedAnyPoint = true;
            }
        }

        private static Vector3 GetCorner(Bounds bounds, int corner)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;

            return new Vector3(
                (corner & 1) == 0 ? min.x : max.x,
                (corner & 2) == 0 ? min.y : max.y,
                (corner & 4) == 0 ? min.z : max.z);
        }

        /// <summary>
        /// Re-bases a point of the camera's viewport on the frame the photo is cropped to, measured from the
        /// top down rather than from the bottom up.
        /// </summary>
        private static Vector2 ViewportToFrame(Vector3 viewportPoint, float screenAspectRatio)
        {
            Vector2 frameSize = ScreenRecorder.CalculateNormalizedFrameSize(screenAspectRatio);

            return new Vector2(
                (viewportPoint.x - (0.5f - (frameSize.x / 2f))) / frameSize.x,
                1f - ((viewportPoint.y - (0.5f - (frameSize.y / 2f))) / frameSize.y));
        }

        internal void FillMetadata(Profile? profile, RealmData realm, Vector2Int playerPosition,
            string sceneName, string placeId, VisiblePerson[] visiblePeople)
        {
            if (metadata == null)
                metadata = new ScreenshotMetadata
                {
                    userName = profile?.Name ?? UNKNOWN_USER,
                    userAddress = AddressOf(profile),
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
                metadata.userAddress = AddressOf(profile);
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
