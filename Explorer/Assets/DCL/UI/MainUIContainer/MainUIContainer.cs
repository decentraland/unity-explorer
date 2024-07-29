using DCL.Chat;
using DCL.Minimap;
using DCL.UI.Sidebar;
using UnityEngine;

namespace DCL.UI.MainUI
{
    public class MainUIContainer : MonoBehaviour
    {
        [field: SerializeField]
        public ChatView ChatView { get; private set;}
        [field: SerializeField]
        public MinimapView MinimapView{ get; private set;}
        [field: SerializeField]
        public SidebarView SidebarView{ get; private set;}


    }
}
