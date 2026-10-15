using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.SDK.Components;
using DCL.Profiles;
using DCL.VoiceChat.Nearby;
using DCL.VoiceChat.Nearby.Audio;
using ECS.LifeCycle.Components;
using ECS.SceneLifeCycle;
using LiveKit.Rooms;
using LiveKit.Rooms.Participants;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     Temporary diagnostic of the Nearby voice -> PBAvatarBase.talking pipeline. Reads the global world, the island
    ///     LiveKit room and the scene worlds. Scene worlds are read without the scene sync lock, as a snapshot, the same
    ///     way <see cref="ListSceneEntitiesTool" /> does. To be deleted before the PR is undrafted.
    /// </summary>
    public class GetVoiceDebugStateTool : McpTool
    {
        private static readonly QueryDescription REMOTE_PLAYERS = new QueryDescription().WithAll<PlayerCRDTEntity>().WithNone<PlayerComponent>();
        private static readonly QueryDescription SCENE_PLAYERS = new QueryDescription().WithAll<PlayerSceneCRDTEntity>();

        private readonly World world;
        private readonly Entity playerEntity;
        private readonly IScenesCache scenesCache;
        private readonly IRoomHub roomHub;
        private readonly NearbyVoiceChatStateModel? nearbyStateModel;
        private readonly Func<INearbyAudioStreamRegistry?> nearbyRegistryProvider;

        public override string Name => "get_voice_debug_state";

        public override string Description =>
            "Temporary diagnostic of the Nearby voice -> PBAvatarBase.talking pipeline: local talking state, island LiveKit room, "
            + "Nearby audio stream registry, every remote player's global/scene-side components, and the current scene's player entities.";

        public override McpToolAnnotations Annotations => McpToolAnnotations.ReadOnly();

        public GetVoiceDebugStateTool(
            World world,
            Entity playerEntity,
            IScenesCache scenesCache,
            IRoomHub roomHub,
            NearbyVoiceChatStateModel? nearbyStateModel,
            Func<INearbyAudioStreamRegistry?> nearbyRegistryProvider)
        {
            this.world = world;
            this.playerEntity = playerEntity;
            this.scenesCache = scenesCache;
            this.roomHub = roomHub;
            this.nearbyStateModel = nearbyStateModel;
            this.nearbyRegistryProvider = nearbyRegistryProvider;
        }

        public override UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            var knownWallets = new HashSet<string>();

            JToken livekit = BuildLiveKit(knownWallets);
            JArray remotePlayers = BuildRemotePlayers(knownWallets);

            var state = new JObject
            {
                ["local"] = BuildLocal(),
                ["livekit"] = livekit,
                ["registry"] = BuildRegistry(knownWallets),
                ["remotePlayers"] = remotePlayers,
                ["currentScene"] = BuildCurrentScene(),
            };

            return UniTask.FromResult(McpToolResult.JsonWithStructured(state));
        }

        private JObject BuildLocal()
        {
            world.TryGet(playerEntity, out Profile? profile);
            bool hasTransform = world.TryGet(playerEntity, out CharacterTransform transform);
            bool hasTalking = world.TryGet(playerEntity, out TalkingComponent talking);

            return new JObject
            {
                ["address"] = NullableString(profile?.UserId.Value),
                ["parcel"] = hasTransform ? transform.Position.ToParcel().ToParcelJson() : JValue.CreateNull(),
                ["localPlayerTalkingComponent"] = new JObject
                {
                    ["present"] = hasTalking,
                    ["isTalking"] = hasTalking ? new JValue(talking.IsTalking) : JValue.CreateNull(),
                },
                ["nearbyStateModel"] = nearbyStateModel == null
                    ? JValue.CreateNull()
                    : new JObject
                    {
                        ["state"] = nearbyStateModel.State.Value.ToString(),
                        ["isLocalSpeaking"] = nearbyStateModel.IsLocalSpeaking,
                        ["currentActivation"] = nearbyStateModel.CurrentActivation.ToString(),
                        ["activeSuppression"] = NullableString(nearbyStateModel.ActiveSuppression.Value?.ToString()),
                    },
            };
        }

        private JToken BuildLiveKit(HashSet<string> knownWallets)
        {
            try
            {
                IRoom room = roomHub.IslandRoom();
                LKParticipant local = room.Participants.LocalParticipant();

                var participants = new JArray();

                foreach (KeyValuePair<string, LKParticipant> remote in room.Participants.RemoteParticipantIdentities())
                {
                    participants.Add(remote.Key);
                    knownWallets.Add(remote.Key);
                }

                var activeSpeakers = new JArray();

                foreach (string identity in room.ActiveSpeakers)
                    activeSpeakers.Add(identity);

                return new JObject
                {
                    ["connectionState"] = room.Info.ConnectionState.ToString(),
                    ["localIdentity"] = local != null ? new JValue(local.Identity) : JValue.CreateNull(),
                    ["participants"] = participants,
                    ["activeSpeakers"] = activeSpeakers,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }

        private JToken BuildRegistry(HashSet<string> knownWallets)
        {
            INearbyAudioStreamRegistry? registry = nearbyRegistryProvider();

            if (registry == null)
                return JValue.CreateNull();

            var byWallet = new JArray();

            foreach (string walletId in knownWallets)
            {
                string[]? sids = registry.GetAudioSidsArray(walletId);
                JToken sidsJson = JValue.CreateNull();

                if (sids != null)
                {
                    var array = new JArray();

                    foreach (string sid in sids)
                        array.Add(sid);

                    sidsJson = array;
                }

                byWallet.Add(new JObject
                {
                    ["walletId"] = walletId,
                    ["hasAudioStream"] = registry.HasAudioStream(walletId),
                    ["audioSids"] = sidsJson,
                    ["isActiveSpeaker"] = registry.IsActiveSpeaker(walletId),
                });
            }

            return new JObject
            {
                ["rebuildEpoch"] = registry.RebuildEpoch,
                ["byWallet"] = byWallet,
            };
        }

        private JArray BuildRemotePlayers(HashSet<string> knownWallets)
        {
            var players = new JArray();

            world.Query(in REMOTE_PLAYERS, (Entity entity, ref PlayerCRDTEntity crdt) =>
            {
                var item = new JObject { ["globalEntity"] = entity.Id };

                world.TryGet(entity, out Profile? profile);
                item["hasProfile"] = profile != null;
                item["address"] = NullableString(profile?.UserId.Value);
                item["name"] = NullableString(profile?.Name);

                if (profile != null)
                    knownWallets.Add(profile.UserId.Value);

                bool hasTransform = world.TryGet(entity, out CharacterTransform transform);
                item["position"] = hasTransform ? transform.Position.ToVector() : JValue.CreateNull();
                item["parcel"] = hasTransform ? transform.Position.ToParcel().ToParcelJson() : JValue.CreateNull();

                ISceneFacade? facade = crdt.SceneFacade;

                item["playerCRDTEntity"] = new JObject
                {
                    ["crdtEntityId"] = crdt.CRDTEntity.Id,
                    ["isDirty"] = crdt.IsDirty,
                    ["assignedToScene"] = crdt.AssignedToScene,
                    ["sceneWorldEntity"] = crdt.SceneWorldEntity == Entity.Null ? JValue.CreateNull() : new JValue(crdt.SceneWorldEntity.Id),
                    ["scene"] = facade == null
                        ? JValue.CreateNull()
                        : new JObject
                        {
                            ["name"] = facade.Info.Name,
                            ["baseParcel"] = facade.Info.BaseParcel.ToParcelJson(),
                        },
                };

                item["isActivelySpeakingTag"] = world.Has<IsActivelySpeakingTag>(entity);
                item["inAudibleRangeTag"] = world.Has<InAudibleRangeTag>(entity);
                item["hasNearbyAudioStreamer"] = world.Has<NearbyAudioStreamerComponent>(entity);
                item["hasDeleteEntityIntention"] = world.Has<DeleteEntityIntention>(entity);

                item["sceneSide"] = facade == null ? JValue.CreateNull() : BuildSceneSide(facade, crdt.SceneWorldEntity);

                players.Add(item);
            });

            return players;
        }

        private JToken BuildCurrentScene()
        {
            ISceneFacade? scene = scenesCache.CurrentScene.Value;

            if (scene == null)
                return JValue.CreateNull();

            World sceneWorld = scene.EcsExecutor.World;
            var playerSceneEntities = new JArray();

            sceneWorld.Query(in SCENE_PLAYERS, (Entity entity, ref PlayerSceneCRDTEntity crdt) =>
            {
                bool hasProfile = sceneWorld.TryGet(entity, out SDKProfile? sdkProfile) && sdkProfile != null;
                bool hasTalking = sceneWorld.TryGet(entity, out SDKTalking talking);

                playerSceneEntities.Add(new JObject
                {
                    ["entity"] = entity.Id,
                    ["crdtId"] = crdt.CRDTEntity.Id,
                    ["hasSDKProfile"] = hasProfile,
                    ["sdkProfileName"] = NullableString(hasProfile ? sdkProfile?.Name : null),
                    ["hasSDKTalking"] = hasTalking,
                    ["sdkTalkingValue"] = hasTalking ? NullableBool(talking.Value) : JValue.CreateNull(),
                });
            });

            return new JObject
            {
                ["name"] = scene.Info.Name,
                ["baseParcel"] = scene.Info.BaseParcel.ToParcelJson(),
                ["playerSceneEntities"] = playerSceneEntities,
            };
        }

        private static JObject BuildSceneSide(ISceneFacade facade, Entity sceneEntity)
        {
            World sceneWorld = facade.EcsExecutor.World;
            bool alive = sceneEntity != Entity.Null && sceneWorld.IsAlive(sceneEntity);

            var result = new JObject { ["alive"] = alive };

            if (!alive)
                return result;

            bool hasCrdt = sceneWorld.TryGet(sceneEntity, out PlayerSceneCRDTEntity sceneCrdt);
            bool hasProfile = sceneWorld.TryGet(sceneEntity, out SDKProfile? sdkProfile) && sdkProfile != null;
            bool hasTalking = sceneWorld.TryGet(sceneEntity, out SDKTalking talking);

            result["hasPlayerSceneCRDTEntity"] = hasCrdt;
            result["playerSceneCrdtId"] = hasCrdt ? new JValue(sceneCrdt.CRDTEntity.Id) : JValue.CreateNull();
            result["hasSDKProfile"] = hasProfile;
            result["sdkProfileIsDirty"] = hasProfile && sdkProfile?.IsDirty == true;
            result["sdkProfileName"] = NullableString(hasProfile ? sdkProfile?.Name : null);
            result["hasSDKTalking"] = hasTalking;
            result["sdkTalkingValue"] = hasTalking ? NullableBool(talking.Value) : JValue.CreateNull();
            result["sdkTalkingIsDirty"] = hasTalking && talking.IsDirty;

            return result;
        }

        private static JToken NullableString(string? value) =>
            value == null ? JValue.CreateNull() : new JValue(value);

        private static JToken NullableBool(bool? value) =>
            value.HasValue ? new JValue(value.Value) : JValue.CreateNull();
    }
}
