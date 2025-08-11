using UnityEngine;
using SceneRunner.Scene;
using DCL.PerformanceAndDiagnostics.Analytics;

namespace Utility
{
    public readonly struct PlayerParcelData
    {
        public readonly Vector2Int ParcelPosition;
        public readonly string SceneHash;
        public readonly bool IsEmptyScene;
        public readonly bool HasScene;

        public PlayerParcelData(Vector2Int parcelPosition, string sceneHash, bool isEmptyScene, bool hasScene)
        {
            ParcelPosition = parcelPosition;
            SceneHash = sceneHash;
            IsEmptyScene = isEmptyScene;
            HasScene = hasScene;
        }

        public static PlayerParcelData CreateUndefined(Vector2Int parcelPosition) =>
            new(parcelPosition, IAnalyticsController.UNDEFINED, false, false);

        public static PlayerParcelData CreateWithScene(Vector2Int parcelPosition, ISceneFacade scene) =>
            new(parcelPosition, scene.Info.Name, scene.IsEmpty, true);
    }
}
