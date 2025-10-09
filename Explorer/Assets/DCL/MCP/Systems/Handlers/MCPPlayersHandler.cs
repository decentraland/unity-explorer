using System;
using System.Collections.Generic;
using System.Linq;
using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.Multiplayer.Movement;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Profiles;
using DCL.Utilities;
using Newtonsoft.Json.Linq;
using System.Threading;
using UnityEngine;
using Avatar = DCL.Profiles.Avatar;

namespace DCL.MCP.Handlers
{
    /// <summary>
    ///     Возвращает максимально возможный срез информации оNearby игроках (на основе имеющихся кэшей/таблиц).
    ///     Грязный прототип: используется MCPGlobalLocator и доступ через ECS к компонентам.
    /// </summary>
    public class MCPPlayersHandler
    {
        private readonly World globalWorld;
        private readonly IProfileRepository profileRepository;

        public MCPPlayersHandler(World globalWorld, IProfileRepository profileRepository)
        {
            this.globalWorld = globalWorld;
            this.profileRepository = profileRepository;
        }

        private static object SerializeColor(Color c) =>
            new
            {
                c.r,
                c.g,
                c.b,
                c.a,
            };

        public async UniTask<object> HandleGetNearbyPlayersAsync(JObject parameters)
        {
            if (!MCPGlobalLocator.HasEntityParticipantTable)
                return new { success = false, error = "EntityParticipantTable not available" };

            var table = (IReadOnlyEntityParticipantTable)MCPGlobalLocator.EntityParticipantTable;

            var result = new List<object>(Mathf.Max(1, table.Count));

            foreach (string wallet in table.Wallets())
            {
                IReadOnlyEntityParticipantTable.Entry entry = table.Get(wallet);

                Vector3? position = null;
                float? rotationY = null;
                object? lastMovement = null;
                float? distanceToCamera = null;

                if (globalWorld.TryGet(entry.Entity, out CharacterTransform characterTransform))
                {
                    position = characterTransform.Position;
                    rotationY = characterTransform.Rotation.eulerAngles.y;
                }

                if (position.HasValue && Camera.main != null) { distanceToCamera = Vector3.Distance(position.Value, Camera.main.transform.position); }

                if (globalWorld.TryGet(entry.Entity, out RemotePlayerMovementComponent movementComp))
                {
                    NetworkMovementMessage m = movementComp.PastMessage;

                    lastMovement = new
                    {
                        m.timestamp,
                        position = new { m.position.x, m.position.y, m.position.z },
                        m.rotationY,
                        velocity = new { m.velocity.x, m.velocity.y, m.velocity.z },
                        speed = Mathf.Sqrt(m.velocitySqrMagnitude),
                        movementKind = m.movementKind.ToString(),
                        anim = new
                        {
                            grounded = m.animState.IsGrounded,
                            jumping = m.animState.IsJumping,
                            longJump = m.animState.IsLongJump,
                            falling = m.animState.IsFalling,
                            longFall = m.animState.IsLongFall,
                            movementBlend = m.animState.MovementBlendValue,
                            slideBlend = m.animState.SlideBlendValue,
                        },
                        m.isStunned,
                        m.isInstant,
                        m.isEmoting,
                    };
                }

                // Предпочтительно получить профиль через репозиторий, используя кэш, чтобы не вызывать сеть лишний раз
                Profile? profile = null;

                try { profile = await profileRepository.GetAsync(wallet, default(CancellationToken)); }
                catch
                { /* ignore */
                }

                object? avatarObj = null;

                if (profile != null && profile.Avatar != null)
                {
                    Avatar av = profile.Avatar;
                    string[]? wearables = av.Wearables?.Select(w => w.ToString()).ToArray();
                    string?[]? emotes = av.Emotes?.Select(e => string.IsNullOrEmpty(e) ? null : e.ToString()).ToArray();
                    string[]? forceRender = av.ForceRender?.ToArray();

                    avatarObj = new
                    {
                        bodyShape = av.BodyShape.ToString(),
                        wearables,
                        forceRender,
                        emotes,
                        faceSnapshotUrl = av.FaceSnapshotUrl.ToString(),
                        bodySnapshotUrl = av.BodySnapshotUrl.ToString(),
                        eyesColor = SerializeColor(av.EyesColor),
                        hairColor = SerializeColor(av.HairColor),
                        skinColor = SerializeColor(av.SkinColor),
                    };
                }

                bool connectedIsland = (entry.ConnectedTo & RoomSource.ISLAND) != 0;
                bool connectedScene = (entry.ConnectedTo & RoomSource.GATEKEEPER) != 0;
                bool connectedChat = (entry.ConnectedTo & RoomSource.CHAT) != 0;

                result.Add(new
                {
                    walletId = wallet,
                    connectedTo = entry.ConnectedTo.ToString(),
                    connectedToFlags = new { island = connectedIsland, scene = connectedScene, chat = connectedChat },
                    position = position.HasValue
                        ? new
                        {
                            position.Value.x,
                            position.Value.y,
                            position.Value.z,
                        }
                        : null,
                    rotationY,
                    distanceToCamera,
                    profile = profile == null
                        ? null
                        : new
                        {
                            userId = profile.UserId,
                            name = profile.Name,
                            displayName = profile.DisplayName,
                            validatedName = profile.ValidatedName,
                            mentionName = profile.MentionName,
                            userNameColor = SerializeColor(profile.UserNameColor),
                            hasClaimedName = profile.HasClaimedName,
                            walletSuffix = profile.WalletId,
                            hasConnectedWeb3 = profile.HasConnectedWeb3,
                            version = profile.Version,
                            description = profile.Description,
                            country = profile.Country,
                            language = profile.Language,
                            pronouns = profile.Pronouns,
                            profession = profile.Profession,
                            realName = profile.RealName,
                            hobbies = profile.Hobbies,
                            birthdate = profile.Birthdate?.ToString("o"),
                            unclaimedName = profile.UnclaimedName,
                            tutorialStep = profile.TutorialStep,
                            email = profile.Email,
                            employmentStatus = profile.EmploymentStatus,
                            gender = profile.Gender,
                            relationshipStatus = profile.RelationshipStatus,
                            sexualOrientation = profile.SexualOrientation,
                            interests = profile.Interests?.ToArray(),
                            links = profile.Links?.Select(l => new { l.title, l.url }).ToArray(),
                            blocked = profile.Blocked?.ToArray(),
                            blockedCount = profile.Blocked?.Count,
                            wearablesCount = profile.Avatar?.Wearables?.Count,
                            emotesEquippedCount = profile.Avatar?.Emotes?.Count(e => !string.IsNullOrEmpty(e)),
                            avatar = avatarObj,
                        },
                    movement = lastMovement,
                });
            }

            return new
            {
                success = true,
                count = result.Count,
                players = result,
            };
        }
    }
}
