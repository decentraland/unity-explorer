using Arch.Core;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.SDKComponents.AssetLoad.Components;
using SceneRunner.Scene;

namespace DCL.SDKComponents.AssetLoad
{
    public class AssetLoadUtils
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly ISceneStateProvider sceneStateProvider;

        public AssetLoadUtils(IECSToCRDTWriter ecsToCRDTWriter,
            ISceneStateProvider sceneStateProvider)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.sceneStateProvider = sceneStateProvider;
        }

        public static void RemoveAssetLoading(World world, Entity loadingEntity, string assetPath, ref AssetLoadComponent existingComponent)
        {
            //TODO: stop each loading properly and then destroy
            world.Destroy(loadingEntity);
            existingComponent.LoadingEntities.Remove(assetPath);
        }

        public void AppendAssetLoadingMessage(CRDTEntity crdtEntity, LoadingState loadingState, string assetPath)
        {
            ecsToCRDTWriter.AppendMessage<PBAssetLoadLoadingState, (LoadingState loadingState, string assetPath)>(
                static (component, data) =>
                {
                    component.CurrentState = data.loadingState;
                    component.Asset = data.assetPath;
                },
                crdtEntity,
                (int)sceneStateProvider.TickNumber,
                (loadingState, assetPath)
            );
        }
    }
}
