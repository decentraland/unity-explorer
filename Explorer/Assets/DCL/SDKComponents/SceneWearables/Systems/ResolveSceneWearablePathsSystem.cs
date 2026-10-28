using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.ECSComponents;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.AvatarShape.Systems;
using Google.Protobuf.Collections;
using SceneRunner.Scene;

namespace DCL.SDKComponents.SceneWearables.Systems
{
    /// <summary>
    ///     Replaces scene content <c>*.glb</c> paths in <see cref="PBAvatarShape.Wearables" /> with scene wearable URNs seeded into
    ///     <see cref="IWearableStorage" />; unresolvable paths and items that are neither a URN nor a path are dropped.
    /// </summary>
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateBefore(typeof(AvatarShapeHandlerSystem))]
    [LogCategory(ReportCategory.WEARABLE)]
    [ThrottlingEnabled]
    public partial class ResolveSceneWearablePathsSystem : BaseUnityLoopSystem
    {
        private readonly ISceneData sceneData;
        private readonly IWearableStorage wearableStorage;
        private readonly bool loadFromLocalScene;

        internal ResolveSceneWearablePathsSystem(World world, ISceneData sceneData, IWearableStorage wearableStorage, bool loadFromLocalScene) : base(world)
        {
            this.sceneData = sceneData;
            this.wearableStorage = wearableStorage;
            this.loadFromLocalScene = loadFromLocalScene;
        }

        protected override void Update(float t)
        {
            ResolveScenePathsQuery(World);
        }

        [Query]
        [None(typeof(DeleteEntityIntention))]
        private void ResolveScenePaths(ref PBAvatarShape pbAvatarShape)
        {
            if (!pbAvatarShape.IsDirty) return;

            RepeatedField<string> wearables = pbAvatarShape.Wearables;

            for (int i = wearables.Count - 1; i >= 0; i--)
            {
                if (!SceneWearableDTOFactory.IsSceneWearablePath(wearables[i]))
                {
                    if (!new URN(wearables[i]).IsValid())
                    {
                        ReportHub.LogWarning(GetReportData(), $"Scene wearable '{wearables[i]}' skipped: it is neither a URN nor a '.glb' path");
                        wearables.RemoveAt(i);
                    }

                    continue;
                }

                if (SceneWearableDTOFactory.TryCreate(sceneData, wearables[i], loadFromLocalScene, out URN urn, out WearableDTO? dto))
                {
                    Seed(urn, dto);
                    wearables[i] = urn;
                }
                else
                    wearables.RemoveAt(i);
            }
        }

        private void Seed(URN urn, WearableDTO dto)
        {
            if (!wearableStorage.TryGetElement(urn, out IWearable existing))
                wearableStorage.GetOrAddByDTO(dto, qualifiedForUnloading: false);
            else if (!existing.Model.Succeeded)
                wearableStorage.Set(urn, new Wearable(new StreamableLoadingResult<WearableDTO>(dto)));
        }
    }
}
