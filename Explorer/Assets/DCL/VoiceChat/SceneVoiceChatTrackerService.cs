using DCL.Diagnostics;
using DCL.Utilities;
using DCL.VoiceChat.Services;
using ECS;
using ECS.SceneLifeCycle.Realm;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.VoiceChat
{
    public class SceneVoiceChatTrackerService
    {
        private const string TAG = nameof(SceneVoiceChatTrackerService);

        private readonly PlayerParcelTrackerService parcelTrackerService;
        private readonly IRealmNavigator realmNavigator;
        private readonly IRealmData realmData;
        private readonly IDisposable? parcelSubscription;

        private readonly Dictionary<Vector2Int, List<string>> parcelToCommunityMap = new ();
        private readonly Dictionary<string, HashSet<Vector2Int>> communityToParcelMap = new ();
        private readonly HashSet<Vector2Int> reusableParcelSet = new ();

        private readonly Dictionary<string, List<string>> worldToCommunityMap = new ();
        private readonly Dictionary<string, HashSet<string>> communityToWorldMap = new ();
        private readonly HashSet<string> reusableWorldSet = new ();

        private readonly Dictionary<string, ActiveCommunityVoiceChat> activeCommunityVoiceChats = new ();

        public event Action<ActiveCommunityVoiceChat>? ActiveVoiceChatDetectedInScene;
        public event Action? ActiveVoiceChatStoppedInScene;

        public SceneVoiceChatTrackerService(
            PlayerParcelTrackerService parcelTrackerService,
            IRealmNavigator realmNavigator,
            IRealmData realmData)
        {
            this.parcelTrackerService = parcelTrackerService;
            this.realmNavigator = realmNavigator;
            this.realmData = realmData;

            this.realmNavigator.NavigationExecuted += OnRealmNavigatorOperationExecuted;
            parcelSubscription = parcelTrackerService.CurrentParcelData.Subscribe(OnParcelChanged);
        }

        public void RegisterCommunityInScene(string communityId, IEnumerable<string> positions, IEnumerable<string> worlds)
        {
            if (positions != null)
                RegisterCommunityCallInScene(communityId, positions);

            if (worlds != null)
                RegisterCommunityCallInWorlds(communityId, worlds);
        }

        public void UnregisterCommunityFromScene(string communityId)
        {
            UnregisterCommunityCallFromScene(communityId);
            UnregisterCommunityCallFromWorlds(communityId);
        }

        public void SetActiveCommunityVoiceChat(string communityId, ActiveCommunityVoiceChat activeChat)
        {
            activeCommunityVoiceChats[communityId] = activeChat;
        }

        public void RemoveActiveCommunityVoiceChat(string communityId)
        {
            activeCommunityVoiceChats.Remove(communityId);
        }

        private void OnRealmNavigatorOperationExecuted(Vector2Int _)
        {
            if (realmData.RealmType.Value == RealmKind.GenesisCity)
            {
                OnParcelChanged(parcelTrackerService.CurrentParcelData.Value);
                return;
            }

            string currentRealm = realmData.RealmName;
            if (string.IsNullOrEmpty(currentRealm)) return;

            if (worldToCommunityMap.TryGetValue(currentRealm, out List<string>? communities))
            {
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} World {currentRealm} has {communities.Count} active community voice chats");

                string? communityId = communities[0];
                OnActiveVoiceChatDetectedInScene(communityId);
            }
            else
            {
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} World {currentRealm} has no active community voice chat");
                OnActiveVoiceChatStoppedInScene();
            }
        }

        private void OnParcelChanged(PlayerParcelData playerParcelData)
        {
            if (realmData.RealmType.Value != RealmKind.GenesisCity) return;

            if (parcelToCommunityMap.TryGetValue(playerParcelData.ParcelPosition, out List<string>? communities))
            {
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Parcel {playerParcelData.ParcelPosition} has {communities.Count} active community voice chats");

                string? communityId = communities[0];
                OnActiveVoiceChatDetectedInScene(communityId);
            }
            else
            {
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Parcel {playerParcelData.ParcelPosition} has no active community voice chat");
                OnActiveVoiceChatStoppedInScene();
            }
        }

        private void OnActiveVoiceChatDetectedInScene(string communityId)
        {
            if (activeCommunityVoiceChats.TryGetValue(communityId, out ActiveCommunityVoiceChat activeChat))
            {
                ActiveVoiceChatDetectedInScene?.Invoke(activeChat);
            }
        }

        private void OnActiveVoiceChatStoppedInScene()
        {
            ActiveVoiceChatStoppedInScene?.Invoke();
        }

        private void UnregisterCommunityCallFromScene(string communityId)
        {
            if (communityToParcelMap.TryGetValue(communityId, out HashSet<Vector2Int>? sceneParcels))
            {
                foreach (Vector2Int parcel in sceneParcels)
                {
                    if (parcelToCommunityMap.TryGetValue(parcel, out List<string>? communities))
                    {
                        communities.Remove(communityId);

                        if (communities.Count == 0)
                        {
                            parcelToCommunityMap.Remove(parcel);

                            if (parcelTrackerService.CurrentParcelData.Value.ParcelPosition == parcel)
                            {
                                OnActiveVoiceChatStoppedInScene();
                            }
                        }
                        else
                        {
                            if (parcelTrackerService.CurrentParcelData.Value.ParcelPosition == parcel)
                            {
                                string remainingCommunityId = communities[0];
                                if (activeCommunityVoiceChats.TryGetValue(remainingCommunityId, out ActiveCommunityVoiceChat _))
                                {
                                    OnActiveVoiceChatDetectedInScene(remainingCommunityId);
                                }
                            }
                        }
                    }
                }

                communityToParcelMap.Remove(communityId);

                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Unregistered {sceneParcels.Count} scene parcels from community {communityId}");
            }
        }

        private void UnregisterCommunityCallFromWorlds(string communityId)
        {
            if (communityToWorldMap.TryGetValue(communityId, out HashSet<string>? sceneWorlds))
            {
                foreach (string worldName in sceneWorlds)
                {
                    if (worldToCommunityMap.TryGetValue(worldName, out List<string>? communities))
                    {
                        communities.Remove(communityId);

                        if (communities.Count == 0)
                        {
                            worldToCommunityMap.Remove(worldName);
                        }
                    }
                }

                communityToWorldMap.Remove(communityId);

                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Unregistered {sceneWorlds.Count} worlds from community {communityId}");
            }
        }

        private bool TryParsePosition(string positionString, out Vector2Int parcel)
        {
            parcel = default;

            if (string.IsNullOrWhiteSpace(positionString))
                return false;

            string[] coords = positionString.Split(',');
            if (coords.Length != 2)
                return false;

            if (!int.TryParse(coords[0].Trim(), out int x) || !int.TryParse(coords[1].Trim(), out int y))
                return false;

            parcel = new Vector2Int(x, y);
            return true;
        }

        private void RegisterCommunityCallInScene(string communityId, IEnumerable<string> positionStrings)
        {
            bool isNewCommunity = !communityToParcelMap.TryGetValue(communityId, out HashSet<Vector2Int>? existingParcels);
            HashSet<Vector2Int> parcels = isNewCommunity ? reusableParcelSet : existingParcels!;

            if (isNewCommunity)
                reusableParcelSet.Clear();

            foreach (string positionString in positionStrings)
            {
                if (!TryParsePosition(positionString, out Vector2Int parcel))
                {
                    ReportHub.LogWarning(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Invalid position format: {positionString} for community {communityId}");
                    continue;
                }

                if (parcelToCommunityMap.TryGetValue(parcel, out List<string>? existingCommunities)) { existingCommunities.Add(communityId); }
                else { parcelToCommunityMap[parcel] = new List<string> { communityId }; }

                parcels.Add(parcel);

                if (parcelTrackerService.CurrentParcelData.Value.ParcelPosition == parcel)
                {
                    if (activeCommunityVoiceChats.TryGetValue(communityId, out ActiveCommunityVoiceChat _))
                    {
                        OnActiveVoiceChatDetectedInScene(communityId);
                    }
                }
            }

            if (isNewCommunity)
            {
                communityToParcelMap[communityId] = new HashSet<Vector2Int>(reusableParcelSet);
            }

            ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Registered community {communityId} in {parcels.Count} parcels");
        }

        private void RegisterCommunityCallInWorlds(string communityId, IEnumerable<string> worldNames)
        {
            bool isNewCommunity = !communityToWorldMap.TryGetValue(communityId, out HashSet<string>? existingWorlds);
            HashSet<string> worlds = isNewCommunity ? reusableWorldSet : existingWorlds!;

            if (isNewCommunity)
                reusableWorldSet.Clear();

            foreach (string worldName in worldNames)
            {
                if (string.IsNullOrWhiteSpace(worldName))
                {
                    ReportHub.LogWarning(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Invalid world name for community {communityId}");
                    continue;
                }

                if (worldToCommunityMap.TryGetValue(worldName, out List<string>? existingCommunities)) { existingCommunities.Add(communityId); }
                else { worldToCommunityMap[worldName] = new List<string> { communityId }; }

                worlds.Add(worldName);
            }

            if (isNewCommunity)
            {
                communityToWorldMap[communityId] = new HashSet<string>(reusableWorldSet);
            }

            ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Registered community {communityId} in {worlds.Count} worlds");
        }

        public void Dispose()
        {
            realmNavigator.NavigationExecuted -= OnRealmNavigatorOperationExecuted;
            parcelSubscription?.Dispose();

            activeCommunityVoiceChats.Clear();
            worldToCommunityMap.Clear();
            communityToWorldMap.Clear();
            parcelToCommunityMap.Clear();
            communityToParcelMap.Clear();
        }
    }
}
