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
using SceneRunner.Scene;

namespace DCL.Multiplayer.SDK.Systems.SceneWorld
{
    /// <summary>
    ///     Propagates the local player's identity and avatar data to every scene world unconditionally,
    ///     similar to how <see cref="DCL.CharacterMotion.Systems.WriteMainPlayerTransformSystem" /> handles transforms.
    ///     Initialize() seeds PlayerSceneCRDTEntity and SDKProfile onto PersistentEntities.Player before the JS
    ///     runtime starts, so that <see cref="WritePlayerIdentityDataSystem" />, <see cref="WriteSDKAvatarBaseSystem" />,
    ///     and <see cref="WriteAvatarEquippedDataSystem" /> can flush CRDT messages in their own Initialize().
    ///     Update() keeps the SDKProfile in sync when Profile.IsDirty — unlike
    ///     <see cref="PlayerProfileDataPropagationSystem" /> which only targets the assigned scene
    ///     and which skips the local player entirely (filtered by PlayerComponent).
    ///     Runs in SyncedPreRenderingSystemGroup before the writer systems so SDKProfile
    ///     is up-to-date when they flush CRDT. Profile.IsDirty is guaranteed to be set
    ///     (set in PresentationSystemGroup, reset later in the same PreRenderingSystemGroup).
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
            if (!globalWorld.TryGet<Profile>(localPlayerEntity, out Profile? profile))
                return;

            Entity playerEntity = persistentEntities.Player;
            World.Add(playerEntity, new PlayerSceneCRDTEntity(new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY)));

            characterDataPropagationUtility.CopyProfileToSceneEntity(profile!, new SceneEcsExecutor(World), playerEntity);
        }

        protected override void Update(float t)
        {
            if (!globalWorld.TryGet<Profile>(localPlayerEntity, out Profile? profile))
                return;

            if (!profile!.IsDirty)
                return;

            characterDataPropagationUtility.CopyProfileToSceneEntity(profile, new SceneEcsExecutor(World), persistentEntities.Player);
        }
    }
}
