using DCL.UI.GenericContextMenu.Controls;
using DCL.UI.GenericContextMenu.Controls.Configs;
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
        private readonly List<GenericContextMenuComponentBase> currentControls = new ();

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

        public GenericContextMenuComponentBase GetContextMenuComponent<T>(T settings, object initialValue, int index) where T : ContextMenuControlSettings
        {
            GenericContextMenuComponentBase component = settings switch
                                                        {
                                                            SeparatorContextMenuControlSettings separatorSettings => GetSeparator(separatorSettings),
                                                            ButtonContextMenuControlSettings buttonSettings => GetButton(buttonSettings),
                                                            ToggleContextMenuControlSettings toggleSettings => GetToggle(toggleSettings, (bool)initialValue),
                                                            _ => throw new ArgumentOutOfRangeException()
                                                        };
            component!.transform.SetSiblingIndex(index);
            currentControls.Add(component);

            return component;
        }

        private GenericContextMenuComponentBase GetSeparator(SeparatorContextMenuControlSettings settings)
        {
            GenericContextMenuSeparatorView separatorView = separatorPool.Get();
            separatorView.Configure(settings);

            return separatorView;
        }

        private GenericContextMenuComponentBase GetButton(ButtonContextMenuControlSettings settings)
        {
            GenericContextMenuButtonWithTextView separatorView = buttonPool.Get();
            separatorView.Configure(settings);

            return separatorView;
        }

        private GenericContextMenuComponentBase GetToggle(ToggleContextMenuControlSettings settings, bool initialValue)
        {
            GenericContextMenuToggleView separatorView = togglePool.Get();
            separatorView.Configure(settings, initialValue);

            return separatorView;
        }

        public void Dispose() =>
            ReleaseAllCurrentControls();

        public void ReleaseAllCurrentControls()
        {
            foreach (GenericContextMenuComponentBase control in currentControls)
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
