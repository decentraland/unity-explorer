using System;
using UnityEngine.InputSystem;

namespace DCL.ExplorePanel.Components
{
    public interface IExplorePanelEscapeAction
    {
        void RegisterEscapeAction(Action<InputAction.CallbackContext> action);

        void RemoveEscapeAction(Action<InputAction.CallbackContext> action);
    }
}
