using DCL.UI.GenericContextMenu.Controls;
using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.UI.GenericContextMenu
{
    public class ControlsPoolManager : IDisposable
    {

        public ControlsPoolManager()
        {

        }

        public GameObject GetSeparator()
        {
            throw new NotImplementedException();
        }

        public Button GetButton(ButtonContextMenuControlSettings settings)
        {
            throw new NotImplementedException();
        }

        public Toggle GetToggle(ToggleContextMenuControlSettings settings)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {

        }

        public void ReleaseAllCurrentControls()
        {
            throw new NotImplementedException();
        }
    }
}
