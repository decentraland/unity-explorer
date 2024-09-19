using DCL.Optimization.PerformanceBudgeting;
using UnityEngine;

namespace DCL.LOD
{
    public interface IRoadAssetPool
    {
        bool Get(string key, out Transform roadAsset);

        void Release(string key, Transform asset);

        void Unload();

        void SwitchVisibility(bool isVisible);
    }
}
