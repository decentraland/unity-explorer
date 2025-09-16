using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Utilities;
using DCL.VoiceChat.Services;
using ECS;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.Realm;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.VoiceChat
{
    public class SceneVoiceChatTrackerService
    {
        private const string TAG = nameof(SceneVoiceChatTrackerService);

        private readonly IScenesCache scenesCache;
        private readonly IRealmNavigator realmNavigator;
        private readonly IRealmData realmData;
        private readonly IDisposable? parcelSubscription;

        private readonly Dictionary<Vector2Int, List<string>> parcelToCommunityMap = new ();
        private readonly Dictionary<string, HashSet<Vector2Int>> communityToParcelMap = new ();

        private readonly Dictionary<string, List<string>> worldToCommunityMap = new ();
        private readonly Dictionary<string, HashSet<string>> communityToWorldMap = new ();

        private readonly Dictionary<string, ActiveCommunityVoiceChat> activeCommunityVoiceChats = new ();

        public event Action<ActiveCommunityVoiceChat>? ActiveVoiceChatDetectedInScene;
        public event Action? ActiveVoiceChatStoppedInScene;

        public SceneVoiceChatTrackerService(
            IScenesCache scenesCache,
            IRealmNavigator realmNavigator,
            IRealmData realmData)
        {
            this.scenesCache = scenesCache;
            this.realmNavigator = realmNavigator;
            this.realmData = realmData;

            if (FeaturesRegistry.Instance.IsEnabled(FeatureId.COMMUNITY_VOICE_CHAT))
            {
                this.realmNavigator.NavigationExecuted += OnRealmNavigatorOperationExecuted;
                parcelSubscription = scenesCache.CurrentParcel.Subscribe(OnParcelChanged);
            }
        }

        public void RegisterCommunityInScene(string communityId, IEnumerable<string>? positions, IEnumerable<string>? worlds)
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
                OnParcelChanged(scenesCache.CurrentParcel.Value);
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

        private void OnParcelChanged(Vector2Int newParcel)
        {
            if (realmData.RealmType.Value != RealmKind.GenesisCity) return;

            if (parcelToCommunityMap.TryGetValue(newParcel, out List<string>? communities))
            {
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Parcel {newParcel} has {communities.Count} active community voice chats");

                string? communityId = communities[0];
                OnActiveVoiceChatDetectedInScene(communityId);
            }
            else
            {
                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Parcel {newParcel} has no active community voice chat");
                OnActiveVoiceChatStoppedInScene();
            }
        }

        private void OnActiveVoiceChatDetectedInScene(string communityId)
        {
            if (activeCommunityVoiceChats.TryGetValue(communityId, out ActiveCommunityVoiceChat activeChat)) { ActiveVoiceChatDetectedInScene?.Invoke(activeChat); }
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
                            ListPool<string>.Release(communities);

                            if (scenesCache.CurrentParcel.Value == parcel) { OnActiveVoiceChatStoppedInScene(); }
                        }
                        else
                        {
                            if (scenesCache.CurrentParcel.Value == parcel)
                            {
                                string remainingCommunityId = communities[0];

                                if (activeCommunityVoiceChats.TryGetValue(remainingCommunityId, out ActiveCommunityVoiceChat _)) { OnActiveVoiceChatDetectedInScene(remainingCommunityId); }
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
                            ListPool<string>.Release(communities);
                        }
                    }
                }

                communityToWorldMap.Remove(communityId);

                ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Unregistered {sceneWorlds.Count} worlds from community {communityId}");
            }
        }

        private bool TryParsePosition(string positionString, out Vector2Int parcel)
        {
            parcel = default(Vector2Int);

            if (string.IsNullOrWhiteSpace(positionString))
                return false;

            int commaIndex = positionString.IndexOf(',');
            if (commaIndex == -1 || commaIndex == 0 || commaIndex == positionString.Length - 1)
                return false;

            ReadOnlySpan<char> firstPart = positionString.AsSpan(0, commaIndex).Trim();
            ReadOnlySpan<char> secondPart = positionString.AsSpan(commaIndex + 1).Trim();

            if (!int.TryParse(firstPart, out int x) || !int.TryParse(secondPart, out int y))
                return false;

            parcel = new Vector2Int(x, y);
            return true;
        }

        private void RegisterCommunityCallInScene(string communityId, IEnumerable<string> positionStrings)
        {
            bool isNewCommunity = !communityToParcelMap.TryGetValue(communityId, out HashSet<Vector2Int>? existingParcels);

            HashSet<Vector2Int> parcels = isNewCommunity ? HashSetPool<Vector2Int>.Get() : existingParcels!;

            foreach (string positionString in positionStrings)
            {
                if (!TryParsePosition(positionString, out Vector2Int parcel))
                {
                    ReportHub.LogWarning(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Invalid position format: {positionString} for community {communityId}");
                    continue;
                }

                if (parcelToCommunityMap.TryGetValue(parcel, out List<string>? existingCommunities)) { existingCommunities.Add(communityId); }
                else
                {
                    List<string>? newList = ListPool<string>.Get();
                    newList.Add(communityId);
                    parcelToCommunityMap[parcel] = newList;
                }

                parcels.Add(parcel);

                if (scenesCache.CurrentParcel.Value == parcel)
                {
                    if (activeCommunityVoiceChats.TryGetValue(communityId, out ActiveCommunityVoiceChat _)) { OnActiveVoiceChatDetectedInScene(communityId); }
                }
            }

            if (isNewCommunity) { communityToParcelMap[communityId] = parcels; }

            ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Registered community {communityId} in {parcels.Count} parcels");
        }

        private void RegisterCommunityCallInWorlds(string communityId, IEnumerable<string> worldNames)
        {
            bool isNewCommunity = !communityToWorldMap.TryGetValue(communityId, out HashSet<string>? existingWorlds);
            HashSet<string> worlds = isNewCommunity ? HashSetPool<string>.Get() : existingWorlds!;

            foreach (string worldName in worldNames)
            {
                if (string.IsNullOrWhiteSpace(worldName))
                {
                    ReportHub.LogWarning(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Invalid world name for community {communityId}");
                    continue;
                }

                if (worldToCommunityMap.TryGetValue(worldName, out List<string>? existingCommunities)) { existingCommunities.Add(communityId); }
                else
                {
                    List<string>? newList = ListPool<string>.Get();
                    newList.Add(communityId);
                    worldToCommunityMap[worldName] = newList;
                }

                worlds.Add(worldName);
            }

            if (isNewCommunity) { communityToWorldMap[communityId] = worlds; }

            ReportHub.Log(ReportCategory.COMMUNITY_VOICE_CHAT, $"{TAG} Registered community {communityId} in {worlds.Count} worlds");
        }

        public void Dispose()
        {
            realmNavigator.NavigationExecuted -= OnRealmNavigatorOperationExecuted;
            parcelSubscription?.Dispose();

            activeCommunityVoiceChats.Clear();

            // Release pooled HashSets
            foreach (HashSet<string>? hashSet in communityToWorldMap.Values)
                HashSetPool<string>.Release(hashSet);

            communityToWorldMap.Clear();

            foreach (HashSet<Vector2Int>? hashSet in communityToParcelMap.Values)
                HashSetPool<Vector2Int>.Release(hashSet);

            communityToParcelMap.Clear();

            // Release pooled Lists
            foreach (List<string>? list in worldToCommunityMap.Values)
                ListPool<string>.Release(list);

            worldToCommunityMap.Clear();

            foreach (List<string>? list in parcelToCommunityMap.Values)
                ListPool<string>.Release(list);

            parcelToCommunityMap.Clear();
        }
    }
}
