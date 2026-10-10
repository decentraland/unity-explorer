using CRDT;
using DCL.ECSComponents;

namespace ECS.Unity.AssetLoad.Components
{
    public struct AssetPreLoadLoadingStateComponent
    {
        public readonly CRDTEntity MainCRDTEntity;
        public readonly string AssetPath;

        public string AssetHash;
        public LoadingState LoadingState;
        public int LastUpdatedTick;
        public bool IsDirty;

        public AssetPreLoadLoadingStateComponent(CRDTEntity mainCRDTEntity, string assetPath)
        {
            MainCRDTEntity = mainCRDTEntity;
            AssetHash = string.Empty;
            AssetPath = assetPath;
            LoadingState = LoadingState.Unknown;
            LastUpdatedTick = -1;
            IsDirty = false;
        }
    }
}
