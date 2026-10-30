using Arch.Core;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.Diagnostics;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems.GlobalWorld;
using DCL.PluginSystem.World;
using DCL.Profiles;
using ECS.Abstract;
using ECS.Groups;

namespace DCL.Multiplayer.SDK.Systems.SceneWorld
{
    /// <summary>
    ///     Propagates the local player's identity and avatar data to every scene world unconditionally,
    ///     similar to how <see cref="DCL.CharacterMotion.Systems.WriteMainPlayerTransformSystem" /> handles transforms.
    ///     Initialize() seeds PlayerSceneCRDTEntity and SDKProfile onto PersistentEntities.Player before the JS
    ///     runtime starts, so that <see cref="WritePlayerIdentityDataSystem" />, <see cref="WriteSDKAvatarBaseSystem" />,
    ///     and <see cref="WriteAvatarEquippedDataSystem" /> can flush CRDT messages in their own Initialize().
    ///     Update() keeps the SDKProfile in sync when Profile.IsDirty or the Profile is replaced — unlike
    ///     <see cref="PlayerProfileDataPropagationSystem" /> which only targets the assigned scene
    ///     and which skips the local player entirely (filtered by PlayerComponent).
    ///     It also mirrors <see cref="TalkingComponent" /> into <see cref="SDKTalking" />,
    ///     adding <see cref="SDKTalking" /> only once <see cref="TalkingComponent" /> exists.
    ///     Runs in SyncedPreRenderingSystemGroup before the writer systems so SDKProfile
    ///     is up-to-date when they flush CRDT.
    /// </summary>
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [UpdateBefore(typeof(WritePlayerIdentityDataSystem))]
    [UpdateBefore(typeof(WriteSDKAvatarBaseSystem))]
    [UpdateBefore(typeof(WriteAvatarEquippedDataSystem))]
    [LogCategory(ReportCategory.PLAYER_SDK_DATA)]
    public partial class LocalPlayerCRDTEntityHandlerSystem : BaseUnityLoopSystem
    {
        private readonly World globalWorld;
        private readonly Entity localPlayerEntity;
        private readonly CharacterDataPropagationUtility characterDataPropagationUtility;
        private readonly PersistentEntities persistentEntities;

        internal LocalPlayerCRDTEntityHandlerSystem(
            World world,
            World globalWorld,
            Entity localPlayerEntity,
            CharacterDataPropagationUtility characterDataPropagationUtility,
            PersistentEntities persistentEntities) : base(world)
        {
            this.globalWorld = globalWorld;
            this.localPlayerEntity = localPlayerEntity;
            this.characterDataPropagationUtility = characterDataPropagationUtility;
            this.persistentEntities = persistentEntities;
        }

        public override void Initialize()
        {
            Profile profile = globalWorld.TryGetRef<Profile>(localPlayerEntity, out bool exists);

            if (exists)
                Seed(profile);
        }

        protected override void Update(float t)
        {
            Profile profile = globalWorld.TryGetRef<Profile>(localPlayerEntity, out bool profileExists);

            if (!profileExists)
                return;

            // No SDKProfile means Initialize ran before the local profile existed.
            // A replaced Profile instance is caught even when its IsDirty is already reset.
            SDKProfile sdkProfile = World.TryGetRef<SDKProfile>(persistentEntities.Player, out bool seeded);

            if (!seeded)
                Seed(profile);
            else if (profile.IsDirty || !sdkProfile.IsCopyOf(profile))
                characterDataPropagationUtility.CopyProfileToSceneEntity(profile, World, persistentEntities.Player);

            if (!globalWorld.TryGet(localPlayerEntity, out TalkingComponent talking))
                return;

            ref SDKTalking sdkTalking = ref World.TryGetRef<SDKTalking>(persistentEntities.Player, out bool exists);

            if (exists)
                sdkTalking.Set(talking.IsTalking);
            else
                World.Add(persistentEntities.Player, new SDKTalking(talking.IsTalking));
        }

        private void Seed(Profile profile)
        {
            Entity playerEntity = persistentEntities.Player;
            World.Add(playerEntity, new PlayerSceneCRDTEntity(new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY), localPlayerEntity));

            characterDataPropagationUtility.CopyProfileToSceneEntity(profile, World, playerEntity);
        }
    }
}
