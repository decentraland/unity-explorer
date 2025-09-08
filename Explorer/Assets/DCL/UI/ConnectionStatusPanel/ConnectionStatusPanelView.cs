using DCL.UI.ConnectionStatusPanel.StatusEntry;
using DCL.Utilities.Extensions;
using MVC;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.ConnectionStatusPanel
{
    public class ConnectionStatusPanelView : ViewBase, IView
    {
        [SerializeField] private bool openByDefault;
        [Space]
        [SerializeField] private GameObject panel = null!;
        [SerializeField] private Button showButton = null!;
        [SerializeField] private StatusEntryView? scene;
        [SerializeField] private StatusEntryView? sceneRoom;
        [SerializeField] private StatusEntryView? globalRoom;
        [SerializeField] private StatusEntryView? assetBundleStatus;


        public IStatusEntry Scene => scene.EnsureNotNull();
        public IStatusEntry SceneRoom => sceneRoom.EnsureNotNull();
        public IStatusEntry GlobalRoom => globalRoom.EnsureNotNull();
        public IStatusEntry AssetBundle => assetBundleStatus.EnsureNotNull();


        private void Awake()
        {
            showButton.onClick!.AddListener(() =>
            {
                bool isShown = panel.activeSelf;
                panel.SetActive(isShown == false);
            });

            panel.SetActive(openByDefault);
        }
    }
}
