using DCL.Diagnostics;
using SceneRunner.Debugging.Hub;
using UnityEngine;

namespace SceneRunner.Debugging
{
    /// <summary>
    ///     Helps to view information when it requires to save the state during pause
    /// </summary>
    public class WorldInfoTool : MonoBehaviour
    {
        [SerializeField] private Vector2Int sceneCoordinates;
        [SerializeField] private int entityId;
        [Space]
        [TextArea]
        [SerializeField] private string message = string.Empty;



        private IWorldInfoHub? worldInfoHub;
        private readonly ReportData category = ReportCategory.DEBUG;

        public void Initialize(IWorldInfoHub hub)
        {
            worldInfoHub = hub;
        }

        [ContextMenu(nameof(PrintInfo))]
        public void PrintInfo()
        {
            if (worldInfoHub == null)
            {
                ReportHub.LogError(category, "WorldInfoHub is not initialized");
                return;
            }

            message = worldInfoHub.WorldInfo(sceneCoordinates)?.EntityComponentsInfo(entityId)
                             ?? $"World {sceneCoordinates} not found";

            ReportHub.Log(category, message);
        }
    }
}
