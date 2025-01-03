using DCL.UI.GenericContextMenu.Controls;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.UI.GenericContextMenu
{
    public class ControlsPoolManager : IDisposable
    {
        private readonly IObjectPool<GenericContextMenuSeparatorView> separatorPool;
        private readonly IObjectPool<GenericContextMenuButtonWithTextView> buttonPool;
        private readonly IObjectPool<GenericContextMenuToggleView> togglePool;
        private readonly List<IGenericContextMenuComponent> currentControls = new ();

        public ControlsPoolManager(
            Transform controlsParent,
            GenericContextMenuSeparatorView separatorPrefab,
            GenericContextMenuButtonWithTextView buttonPrefab,
            GenericContextMenuToggleView togglePrefab)
        {
            separatorPool = new ObjectPool<GenericContextMenuSeparatorView>(
                createFunc: () => GameObject.Instantiate(separatorPrefab, controlsParent),
                actionOnGet: separatorView => separatorView.gameObject.SetActive(true),
                actionOnRelease: separatorView => separatorView.gameObject.SetActive(false),
                actionOnDestroy: separatorView => GameObject.Destroy(separatorView.gameObject));

            buttonPool = new ObjectPool<GenericContextMenuButtonWithTextView>(
                createFunc: () => GameObject.Instantiate(buttonPrefab, controlsParent),
                actionOnGet: buttonView => buttonView.gameObject.SetActive(true),
                actionOnRelease: buttonView => buttonView.gameObject.SetActive(false),
                actionOnDestroy: buttonView => GameObject.Destroy(buttonView.gameObject));

            togglePool = new ObjectPool<GenericContextMenuToggleView>(
                createFunc: () => GameObject.Instantiate(togglePrefab, controlsParent),
                actionOnGet: toggleView => toggleView.gameObject.SetActive(true),
                actionOnRelease: toggleView => toggleView.gameObject.SetActive(false),
                actionOnDestroy: toggleView => GameObject.Destroy(toggleView.gameObject));
        }

        public GenericContextMenuSeparatorView GetSeparator(SeparatorContextMenuControlSettings settings)
        {
            GenericContextMenuSeparatorView separatorView = separatorPool.Get();
            separatorView.Configure(settings);
            currentControls.Add(separatorView);
            return separatorView;
        }

        public GenericContextMenuButtonWithTextView GetButton(ButtonContextMenuControlSettings settings)
        {
            GenericContextMenuButtonWithTextView buttonView = buttonPool.Get();
            buttonView.Configure(settings);
            currentControls.Add(buttonView);
            return buttonView;
        }

        public GenericContextMenuToggleView GetToggle(ToggleContextMenuControlSettings settings)
        {
            GenericContextMenuToggleView toggleView = togglePool.Get();
            toggleView.Configure(settings);
            currentControls.Add(toggleView);
            return toggleView;
        }

        public void Dispose() =>
            ReleaseAllCurrentControls();

        public void ReleaseAllCurrentControls()
        {
            foreach (IGenericContextMenuComponent control in currentControls)
            {
                control.UnregisterListeners();

                switch (control)
                {
                    case GenericContextMenuSeparatorView separatorView:
                        separatorPool.Release(separatorView);
                        break;
                    case GenericContextMenuButtonWithTextView buttonView:
                        buttonPool.Release(buttonView);
                        break;
                    case GenericContextMenuToggleView toggleView:
                        togglePool.Release(toggleView);
                        break;
                }
            }

            currentControls.Clear();
        }
    }
}
