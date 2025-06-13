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
        private const string UNKNOWN_PLACE = "Unknown place";
        private const string WORLD_PLACE_ID = "not applicable";

        private readonly SelfProfile selfProfile;
        private readonly CharacterController characterObjectController;
        private readonly RealmData realmData;
        private readonly IPlacesAPIService placesAPIService;

        private readonly List<VisiblePerson> visiblePeople = new (32);

        private Plane[]? frustumPlanes;
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

        public void Init(Vector2Int sceneParcel, Plane[] frustumPlanes)
        {
            MetadataIsReady = false;
            visiblePeople.Clear();

            this.frustumPlanes = frustumPlanes;
            this.sceneParcel = sceneParcel;

            AddProfile(selfProfile.OwnProfile, characterObjectController);
        }

        public void AddProfile(Profile? profile, Collider avatarCollider)
        {
            if (GeometryUtility.TestPlanesAABB(frustumPlanes, avatarCollider.bounds))
            {
                visiblePeople.Add(new VisiblePerson
                {
                    userName = profile?.Name ?? UNKNOWN_USER,
                    userAddress = profile?.UserId ?? UNKNOWN_USER,
                    isGuest = false,
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
            if (realmData.ScenesAreFixed)
                return (realmData.RealmName.Replace(".dcl.eth", string.Empty), WORLD_PLACE_ID);

            PlacesData.PlaceInfo? placeInfo = await placesAPIService.GetPlaceAsync(at, ct);

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

        internal void FillMetadata(Profile? profile, RealmData realm, Vector2Int playerPosition,
            string sceneName, string placeId, VisiblePerson[] visiblePeople)
        {
            if (metadata == null)
                metadata = new ScreenshotMetadata
                {
                    userName = profile?.Name ?? UNKNOWN_USER,
                    userAddress = profile?.UserId ?? UNKNOWN_USER,
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
                metadata.userAddress = profile?.UserId ?? UNKNOWN_USER;
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
