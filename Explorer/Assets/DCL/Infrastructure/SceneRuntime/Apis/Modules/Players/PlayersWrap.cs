using Cysharp.Threading.Tasks;

#if !NO_LIVEKIT_MODE
using DCL.Multiplayer.Profiles.Poses;
#endif

using DCL.Profiles;
using JetBrains.Annotations;
using LiveKit.Rooms.Participants;
using Newtonsoft.Json;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;
using Utility;
using Avatar = DCL.Profiles.Avatar;
using DCL.LiveKit.Public;

#if !NO_LIVEKIT_MODE
using DCL.Multiplayer.Connections.RoomHubs;
#endif

namespace SceneRuntime.Apis.Modules.Players
{
    public class PlayersWrap : JsApiWrapper
    {
#if !NO_LIVEKIT_MODE
        private readonly IRoomHub roomHub;
        private readonly IProfileRepository profileRepository;
        private readonly IRemoteMetadata remoteMetadata;

        public PlayersWrap(
#if !NO_LIVEKIT_MODE
                IRoomHub roomHub, 
#endif
                IProfileRepository profileRepository, 
                IRemoteMetadata remoteMetadata, 
                CancellationTokenSource disposeCts
                ) : base(disposeCts)
        {
#if !NO_LIVEKIT_MODE
            this.roomHub = roomHub;
#endif
            this.profileRepository = profileRepository;
            this.remoteMetadata = remoteMetadata;
        }
#else
        public PlayersWrap(CancellationTokenSource disposeCts) : base(disposeCts)
        {}
#endif
        [UsedImplicitly]
        public object PlayerData(string walletId)
        {
#if NO_LIVEKIT_MODE
            return "{ data: undefined }";
#else
            async UniTask<PlayersGetUserDataResponse> ExecuteAsync()
            {
                Profile? profile = await profileRepository.GetAsync(walletId, 0, remoteMetadata.GetLambdaDomainOrNull(walletId), disposeCts.Token);
                return new PlayersGetUserDataResponse(profile, walletId);
            }

            return ExecuteAsync().ToDisconnectedPromise(this);
#endif
        }

        [UsedImplicitly]
        public object ConnectedPlayers() =>
#if NO_LIVEKIT_MODE
            "{ players: [] }";
#else
            new PlayerListResponse(roomHub.IslandRoom().Participants);
#endif

        [UsedImplicitly]
        public object PlayersInScene() =>
#if NO_LIVEKIT_MODE
            "{ players: [] }";
#else
            new PlayerListResponse(roomHub.SceneRoom().Room().Participants);
#endif

        /* from https://github.com/decentraland/js-sdk-toolchain/blob/1c2ff7242cd11eb981666a8318f670c0b302813b/packages/%40dcl/sdk-commands/src/commands/code-to-composite/scene-executor.ts#L202
        getPlayerData: async () => ({ data: undefined }),
        getPlayersInScene: async () => ({ players: [] }),
        getConnectedPlayers: async () => ({ players: [] })
        */

#if !NO_LIVEKIT_MODE
        [Serializable]
        [PublicAPI]
        public struct PlayerListResponse
        {
            public string playersJson;

            public PlayerListResponse(IParticipantsHub participantsHub)
            {
                IReadOnlyDictionary<string, LKParticipant> identities = participantsHub.RemoteParticipantIdentities();

                using PooledObject<List<Player>> pooledObj = ListPool<Player>.Get(out List<Player>? players);

                // See: https://github.com/decentraland/unity-explorer/issues/3796
                lock (identities)
                {
                    foreach ((string identity, _) in identities)
                    {
#if !UNITY_WEBGL
                        LKParticipant remote = participantsHub.RemoteParticipant(identity)!;
#else
                        LKParticipant remote = participantsHub.RemoteParticipant(identity).Value;
#endif
                        players!.Add(new Player(remote));
                    }
                }

                playersJson = JsonConvert.SerializeObject(players);
            }
        }

        [Serializable]
        [PublicAPI]
        public struct Player
        {
            public string userId;

            public Player(LKParticipant participant) : this(participant.Identity) { }

            public Player(string userId)
            {
                this.userId = userId;
            }
        }

        [Serializable]
        [PublicAPI]
        public struct PlayersGetUserDataResponse
        {
            public UserData? data;

            public PlayersGetUserDataResponse(Profile? profile, string walletId)
            {
                if (profile is null)
                {
                    data = null;
                    return;
                }

                data = new UserData(
                    new AvatarData(profile.Avatar),
                    profile.DisplayName,
                    walletId,
                    profile.UserId,
                    profile.Version
                );
            }
        }

        [Serializable]
        public class UserData
        {
            public AvatarData? avatar;

            public string displayName;

            public bool hasConnectedWeb3;

            public string? publicKey;

            public string userId;

            public int version;

            public UserData(AvatarData? avatar, string displayName, string? publicKey, string userId, int version)
            {
                this.avatar = avatar;
                this.displayName = displayName;
                this.publicKey = publicKey;
                hasConnectedWeb3 = publicKey is not null;
                this.userId = userId;
                this.version = version;
            }
        }

        [Serializable]
        public class AvatarData
        {
            public string bodyShape;

            public string eyeColor;

            public string hairColor;

            public string skinColor;

            public Snapshots? snapshots;

            public List<string> wearables;

            public AvatarData(Avatar avatar) : this(
                avatar.BodyShape.Value,
                ColorUtility.ToHtmlStringRGBA(avatar.EyesColor)!,
                ColorUtility.ToHtmlStringRGBA(avatar.HairColor)!,
                ColorUtility.ToHtmlStringRGBA(avatar.SkinColor)!,
                new Snapshots(string.Empty, string.Empty),
                avatar.Wearables.Select(e => e.ToString()).ToList()
            ) { }

            public AvatarData(string bodyShape, string eyeColor, string hairColor, string skinColor, Snapshots? snapshots,
                List<string> wearables)
            {
                this.bodyShape = bodyShape;
                this.eyeColor = eyeColor;
                this.hairColor = hairColor;
                this.skinColor = skinColor;
                this.snapshots = snapshots;
                this.wearables = wearables;
            }
        }

        [Serializable]
        public class Snapshots
        {
            public string body;
            public string face256;

            public Snapshots(string body, string face256)
            {
                this.body = body;
                this.face256 = face256;
            }
        }
#endif

    }
}
