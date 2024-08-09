using DCL.UI.ConnectionStatusPanel.StatusEntry;
using DCL.Utilities.Extensions;
using MVC;
using UnityEngine;

namespace DCL.UI.ConnectionStatusPanel
{
    public class ConnectionStatusPanelView : ViewBase, IView
    {
        [SerializeField] private StatusEntryView? scene;
        [SerializeField] private StatusEntryView? sceneRoom;
        [SerializeField] private StatusEntryView? globalRoom;

        public IStatusEntry Scene => scene.EnsureNotNull();
        public IStatusEntry SceneRoom => sceneRoom.EnsureNotNull();
        public IStatusEntry GlobalRoom => globalRoom.EnsureNotNull();
    }
}
