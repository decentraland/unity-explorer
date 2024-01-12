using Arch.Core;
using Cysharp.Threading.Tasks;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SceneReadiness
{
    public static class SceneReadinessUtils
    {
        /// <summary>
        ///     Create a scene readiness report and add it to the world for further resolution
        /// </summary>
        /// <param name="world"></param>
        /// <param name="sceneParcel">Any parcel that belongs to the target scene</param>
        /// <returns></returns>
        public static SceneReadinessReport CreateReport(World world, Vector2Int sceneParcel)
        {
            var report = new SceneReadinessReport(new UniTaskCompletionSource());
            world.Create(report, sceneParcel);
            return report;
        }
    }
}
