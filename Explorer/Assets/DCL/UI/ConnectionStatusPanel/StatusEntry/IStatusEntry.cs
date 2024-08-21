using ECS.SceneLifeCycle.CurrentScene;
using System;
using System.Collections.Generic;

namespace DCL.UI.ConnectionStatusPanel.StatusEntry
{
    public interface IStatusEntry
    {
        enum Status
        {
            Lost,
            Poor,
            Good,
            Excellent,
        }

        public void ShowReloadButton(Action onClick);

        public void ShowStatus(string status);

        public void HideStatus();
    }

    public static class StatusEntryExtensions
    {
        private static readonly IReadOnlyDictionary<IStatusEntry.Status, string> CACHE_ROOM_STATUS = new Dictionary<IStatusEntry.Status, string>
        {
            [IStatusEntry.Status.Lost] = "Lost",
            [IStatusEntry.Status.Poor] = "Poor",
            [IStatusEntry.Status.Good] = "Good",
            [IStatusEntry.Status.Excellent] = "Excellent",
        };

        private static readonly IReadOnlyDictionary<ICurrentSceneInfo.Status, string> CACHE_SCENE_STATUS = new Dictionary<ICurrentSceneInfo.Status, string>
        {
            [ICurrentSceneInfo.Status.Good] = "Good",
            [ICurrentSceneInfo.Status.Crashed] = "Crashed",
        };

        public static void ShowStatus(this IStatusEntry statusEntry, IStatusEntry.Status status)
        {
            statusEntry.ShowStatus(CACHE_ROOM_STATUS[status]!);
        }

        public static void ShowStatus(this IStatusEntry statusEntry, ICurrentSceneInfo.Status status)
        {
            statusEntry.ShowStatus(CACHE_SCENE_STATUS[status]!);
        }
    }
}
