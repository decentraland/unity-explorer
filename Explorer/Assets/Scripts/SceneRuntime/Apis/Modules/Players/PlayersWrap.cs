using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Profiles;
using JetBrains.Annotations;
using LiveKit.Rooms.Participants;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;

namespace SceneRuntime.Apis.Modules.Players
{
    public class PlayersWrap : IDisposable
    {
        private readonly IRoomHub roomHub;
        private readonly IProfileRepository profileRepository;
        private readonly CancellationTokenSource cancellationTokenSource = new ();

        public PlayersWrap(IRoomHub roomHub, IProfileRepository profileRepository)
        {
            this.roomHub = roomHub;
            this.profileRepository = profileRepository;
        }

        [UsedImplicitly]
        public object PlayerData(string walletId)
        {
            async UniTask<PlayersGetUserDataResponse> ExecuteAsync()
            {
                var profile = await profileRepository.GetAsync(walletId, 0, cancellationTokenSource.Token);
                return new PlayersGetUserDataResponse(profile);
            }

            return ExecuteAsync().ToPromise();
        }

        [UsedImplicitly]
        public object ConnectedPlayers() =>
            new PlayerListResponse(roomHub.IslandRoom().Participants);

        [UsedImplicitly]
        public object PlayersInScene() =>
            new PlayerListResponse(roomHub.SceneRoom().Participants);

        public void Dispose()
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
        }

        [Serializable]
        public struct PlayerListResponse
        {
            public List<Player> players;

            public PlayerListResponse(IParticipantsHub participantsHub)
            {
                var sids = participantsHub.RemoteParticipantSids();
                players = new List<Player>();

                foreach (string sid in sids)
                {
                    var remote = participantsHub.RemoteParticipant(sid)!;
                    players.Add(new Player(remote));
                }
            }

            public PlayerListResponse(List<Player> players)
            {
                this.players = players;
            }
        }

        [Serializable]
        public struct Player
        {
            public string userId;

            public Player(Participant participant) : this(participant.Identity) { }

            public Player(string userId)
            {
                this.userId = userId;
            }
        }

        [Serializable]
        public struct PlayersGetUserDataResponse
        {
            public UserData? data;

            public PlayersGetUserDataResponse(Profile? profile)
            {
                if (profile is null)
                {
                    data = null;
                    return;
                }

                data = new UserData(
                    new AvatarData(profile.Avatar),
                    profile.DisplayName,
                    profile.UserId,
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
                avatar.BodyShape.Value!,
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
