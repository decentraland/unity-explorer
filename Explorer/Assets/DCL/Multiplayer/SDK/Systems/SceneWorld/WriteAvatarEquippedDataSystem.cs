using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Profiles;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.LifeCycle.Systems;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.Multiplayer.SDK.Systems.SceneWorld
{
    /// <summary>
    ///     Writes the profile outfit, never the scene's AvatarShape override.
    /// </summary>
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [UpdateAfter(typeof(WritePlayerIdentityDataSystem))]
    [UpdateBefore(typeof(ResetDirtyFlagSystem<SDKProfile>))]
    [LogCategory(ReportCategory.PLAYER_SDK_DATA)]
    public partial class WriteAvatarEquippedDataSystem : BaseUnityLoopSystem
    {
        private const float MAX_PENDING_SECONDS = 60f;

        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly IWearableStorage wearableStorage;

        internal WriteAvatarEquippedDataSystem(World world, IECSToCRDTWriter ecsToCRDTWriter, IWearableStorage wearableStorage) : base(world)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.wearableStorage = wearableStorage;
        }

        public override void Initialize()
        {
            UpdateAvatarEquippedDataQuery(World, true);
        }

        protected override void Update(float t)
        {
            HandleComponentRemovalQuery(World);
            RetryWearablesVisibilityQuery(World, t);
            UpdateAvatarEquippedDataQuery(World, false);
        }

        [Query]
        [None(typeof(WearablesVisibilityPending), typeof(DeleteEntityIntention))]
        private void UpdateAvatarEquippedData([Data] bool force, Entity entity, PlayerSceneCRDTEntity crdtEntity, SDKProfile profile)
        {
            if (!force && !profile.IsDirty) return;

            if (!TryWrite(crdtEntity.CRDTEntity, profile, true))
                World.Add(entity, new WearablesVisibilityPending());
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void RetryWearablesVisibility([Data] float dt, Entity entity, ref WearablesVisibilityPending pending, PlayerSceneCRDTEntity crdtEntity, SDKProfile profile)
        {
            // A dirty profile gets a full write instead of the retry.
            if (profile.IsDirty || TryWrite(crdtEntity.CRDTEntity, profile, false))
            {
                World.Remove<WearablesVisibilityPending>(entity);
                return;
            }

            pending.ElapsedSeconds += dt;

            if (pending.ElapsedSeconds < MAX_PENDING_SECONDS) return;

            ReportHub.LogWarning(GetReportData(), $"Wearable visibility of player entity {crdtEntity.CRDTEntity.Id} is still unresolved after {MAX_PENDING_SECONDS}s, left unset");
            World.Remove<WearablesVisibilityPending>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleComponentRemoval(PlayerSceneCRDTEntity playerCRDTEntity)
        {
            ecsToCRDTWriter.DeleteMessage<PBAvatarEquippedData>(playerCRDTEntity.CRDTEntity);
        }

        /// <returns>Whether the visibility was resolved and written.</returns>
        private bool TryWrite(CRDTEntity crdtEntity, SDKProfile profile, bool writeUnresolved)
        {
            using PooledObject<List<bool>> _ = ListPool<bool>.Get(out List<bool> visibility);
            bool resolved = TryResolveVisibility(profile.Avatar, visibility);

            if (resolved || writeUnresolved)
                ecsToCRDTWriter.PutMessage<PBAvatarEquippedData, (SDKProfile profile, List<bool>? visibility)>(static (pbComponent, data) =>
                    Fill(pbComponent, data.profile, data.visibility), crdtEntity, (profile, resolved ? visibility : null));

            return resolved;
        }

        private bool TryResolveVisibility(SDKProfile.SDKAvatar avatar, List<bool> visibility)
        {
            IReadOnlyList<URN> pointers = avatar.ShortenedWearables;
            using PooledObject<List<IWearable>> byPointerScope = WearableComponentsUtils.WEARABLES_POOL.Get(out List<IWearable> byPointer);
            using PooledObject<List<IWearable>> loadedScope = WearableComponentsUtils.WEARABLES_POOL.Get(out List<IWearable> loaded);

            for (var i = 0; i < pointers.Count; i++)
            {
                if (!wearableStorage.TryGetElement(pointers[i], out IWearable wearable)
                    || wearable.Model is { Succeeded: false, Exception: null })
                    return false;

                byPointer.Add(wearable);

                // A wearable that failed to load is not rendered and hides nothing.
                if (wearable.Model.Succeeded)
                    loaded.Add(wearable);
            }

            var resolution = new HideWearablesResolution(avatar.ForceRender);
            WearableComponentsUtils.ExtractVisibleWearables(avatar.BodyShape, loaded, ref resolution);

            if (resolution.VisibleWearables is { } visibleWearables)
                for (var i = 0; i < byPointer.Count; i++)
                    visibility.Add(IsVisible(byPointer, i, visibleWearables));

            resolution.Release();
            return true;
        }

        /// <summary>
        ///     Two pointers can share one stored wearable, for example two tokens of the same item, so the later pointer of a category wins by index.
        /// </summary>
        private static bool IsVisible(List<IWearable> byPointer, int index, List<IWearable> visibleWearables)
        {
            IWearable wearable = byPointer[index];

            if (!wearable.Model.Succeeded || !visibleWearables.Contains(wearable)) return false;

            string category = wearable.GetCategory();

            for (int i = index + 1; i < byPointer.Count; i++)
                if (byPointer[i].Model.Succeeded && byPointer[i].GetCategory() == category)
                    return false;

            return true;
        }

        private static void Fill(PBAvatarEquippedData pbComponent, SDKProfile profile, List<bool>? visibility)
        {
            IReadOnlyList<URN> wearables = profile.Avatar.Wearables;

            for (var i = 0; i < wearables.Count; i++)
            {
                pbComponent.WearableUrns.Add(wearables[i]);

                var equipped = new PBAvatarEquippedData.Types.EquippedWearable { Urn = wearables[i] };

                if (visibility != null)
                    equipped.Visible = visibility[i];

                pbComponent.Wearables.Add(equipped);
            }

            foreach (URN urn in profile.Avatar.Emotes)
            {
                if (!urn.IsNullOrEmpty())
                    pbComponent.EmoteUrns.Add(urn);
            }
        }

        private struct WearablesVisibilityPending
        {
            public float ElapsedSeconds;
        }
    }
}
