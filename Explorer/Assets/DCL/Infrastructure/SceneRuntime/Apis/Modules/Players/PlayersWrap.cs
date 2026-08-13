using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Poses;
using DCL.Profiles;
using JetBrains.Annotations;
using DCL.LiveKit.Public;
using LiveKit.Rooms.Participants;
using Newtonsoft.Json;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEngine;
using Utility;
using Avatar = DCL.Profiles.Avatar;

namespace SceneRuntime.Apis.Modules.Players
{
    public class PlayersWrap : JsApiWrapper
    {
        private readonly IRoomHub roomHub;
        private readonly IProfileRepository profileRepository;
        private readonly IRemoteMetadata remoteMetadata;

        private readonly PlayersJsonWriter playersWriter = new ();
        private readonly object writerLock = new ();

        public PlayersWrap(IRoomHub roomHub, IProfileRepository profileRepository, IRemoteMetadata remoteMetadata, CancellationTokenSource disposeCts) : base(disposeCts)
        {
            this.roomHub = roomHub;
            this.profileRepository = profileRepository;
            this.remoteMetadata = remoteMetadata;
        }

        private string BuildPlayersJson(IParticipantsHub participantsHub)
        {
            lock (writerLock)
            {
                try
                {
                    JsonTextWriter writer = playersWriter.Begin();

                    writer.WriteStartArray();

                    IReadOnlyDictionary<string, LKParticipant> identities = participantsHub.RemoteParticipantIdentities();

                    lock (identities)
                    {
                        foreach ((string identity, _) in identities)
                        {
                            writer.WriteStartObject();
                            writer.WritePropertyName("userId");
                            writer.WriteValue(participantsHub.RemoteParticipant(identity)!.Identity);
                            writer.WriteEndObject();
                        }
                    }

                    writer.WriteEndArray();

                    return playersWriter.Complete();
                }
                catch
                {
                    playersWriter.Recreate();
                    throw;
                }
            }
        }

        [UsedImplicitly]
        public object PlayerData(string walletId)
        {
            async UniTask<PlayersGetUserDataResponse> ExecuteAsync()
            {
                Profile? profile = await profileRepository.GetAsync(walletId, 0, remoteMetadata.GetLambdaDomainOrNull(walletId), disposeCts.Token,
                    batchBehaviour: IProfileRepository.FetchBehaviour.DelayUntilResolved);
                return new PlayersGetUserDataResponse(profile, walletId);
            }

            return ExecuteAsync().ToDisconnectedPromise(this);
        }

        [UsedImplicitly]
        public object ConnectedPlayers() =>
            new PlayerListResponse(BuildPlayersJson(roomHub.IslandRoom().Participants));

        [UsedImplicitly]
        public object PlayersInScene() =>
            new PlayerListResponse(BuildPlayersJson(roomHub.SceneRoom().Room().Participants));

        [Serializable]
        [PublicAPI]
        public struct PlayerListResponse
        {
            public string playersJson;

            public PlayerListResponse(string playersJson)
            {
                this.playersJson = playersJson;
            }
        }

        /// <summary>
        ///     RAII-style reused JsonTextWriter over a single StringBuilder, recreated after exceptions to avoid
        ///     corrupted depth/token state. NOT thread-safe — callers synchronize (see BuildPlayersJson).
        /// </summary>
        private sealed class PlayersJsonWriter
        {
            private readonly StringBuilder stringBuilder = new ();
            private StringWriter stringWriter;
            private JsonTextWriter writer;

            public PlayersJsonWriter()
            {
                stringWriter = new StringWriter(stringBuilder);
                writer = new JsonTextWriter(stringWriter);
            }

            public JsonTextWriter Begin()
            {
                stringBuilder.Clear();
                return writer;
            }

            public string Complete() =>
                stringWriter.ToString();

            public void Recreate()
            {
                try { writer.Close(); }
                catch
                {  }

                stringWriter.Dispose();

                stringWriter = new StringWriter(stringBuilder);
                writer = new JsonTextWriter(stringWriter);
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
                    profile.UserId.Value,
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
    }
}
