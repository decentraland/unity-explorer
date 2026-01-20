using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using SceneRunner.Scene;

namespace ECS.Unity.AssetLoad
{
    public class AssetPreLoadUtils
    {
        private readonly IECSToCRDTWriter ecsToCRDTWriter;
        private readonly ISceneStateProvider sceneStateProvider;

        public AssetPreLoadUtils(IECSToCRDTWriter ecsToCRDTWriter,
            ISceneStateProvider sceneStateProvider)
        {
            this.ecsToCRDTWriter = ecsToCRDTWriter;
            this.sceneStateProvider = sceneStateProvider;
        }

        public void AppendAssetLoadingMessage(CRDTEntity crdtEntity, LoadingState loadingState, string assetPath)
        {
            ecsToCRDTWriter.AppendMessage<PBAssetLoadLoadingState, (LoadingState loadingState, string assetPath, uint timestamp)>(
                static (component, data) =>
                {
                    component.CurrentState = data.loadingState;
                    component.Asset = data.assetPath;
                    component.Timestamp = data.timestamp;
                },
                crdtEntity,
                (int)sceneStateProvider.TickNumber,
                (loadingState, assetPath, sceneStateProvider.TickNumber)
            );
        }
    }
}
