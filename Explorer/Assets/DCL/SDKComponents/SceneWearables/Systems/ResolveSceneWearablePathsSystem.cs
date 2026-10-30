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
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.AvatarShape.Systems;
using Google.Protobuf.Collections;
using SceneRunner.Scene;
using System;

namespace DCL.SDKComponents.SceneWearables.Systems
{
    [UpdateInGroup(typeof(ComponentInstantiationGroup))]
    [UpdateBefore(typeof(AvatarShapeHandlerSystem))]
    [LogCategory(ReportCategory.WEARABLE)]
    [ThrottlingEnabled]
    public partial class ResolveSceneWearablePathsSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly ISceneData sceneData;
        private readonly IWearableStorage wearableStorage;
        private readonly bool loadFromLocalScene;
        private readonly string sceneWearableUrnPrefix;

        internal ResolveSceneWearablePathsSystem(World world, ISceneData sceneData, IWearableStorage wearableStorage, bool loadFromLocalScene) : base(world)
        {
            this.sceneData = sceneData;
            this.wearableStorage = wearableStorage;
            this.loadFromLocalScene = loadFromLocalScene;
            sceneWearableUrnPrefix = SceneWearableDTOFactory.GetUrnPrefix(sceneData.SceneEntityDefinition.id);
        }

        public void FinalizeComponents(in Query query) =>
            wearableStorage.QualifyForUnloading(sceneWearableUrnPrefix);

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
                string wearable = wearables[i];

                if (!SceneWearableDTOFactory.IsSceneWearablePath(wearable))
                {
                    if (!wearable.StartsWith("urn", StringComparison.OrdinalIgnoreCase))
                    {
                        ReportHub.LogWarning(GetReportData(), $"AvatarShape wearable '{wearable}' skipped: it is neither a URN nor a '.glb' path");
                        wearables.RemoveAt(i);
                    }

                    continue;
                }

                if (SceneWearableDTOFactory.TryGetUrn(sceneData, wearable, loadFromLocalScene, out URN urn, out string? category, out string? hash))
                {
                    Seed(urn, wearable, category, hash);
                    wearables[i] = urn;
                }
                else
                    wearables.RemoveAt(i);
            }
        }

        private void Seed(URN urn, string path, string category, string hash)
        {
            if (!wearableStorage.TryGetElement(urn, out IWearable existing))
            {
                wearableStorage.GetOrAddByDTO(SceneWearableDTOFactory.Create(sceneData, path, urn, category, hash, loadFromLocalScene), qualifiedForUnloading: false);
                return;
            }

            if (!existing.Model.Succeeded)
                wearableStorage.Set(urn, new Wearable(new StreamableLoadingResult<WearableDTO>(SceneWearableDTOFactory.Create(sceneData, path, urn, category, hash, loadFromLocalScene))));

            wearableStorage.ExcludeFromUnloading(urn);
        }
    }
}
