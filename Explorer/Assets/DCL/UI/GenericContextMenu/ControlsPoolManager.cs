using DCL.UI.GenericContextMenu.Controls;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.Profiles.Helpers;
using MVC;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.UI.GenericContextMenu
{
    public class ControlsPoolManager : IDisposable
    {
        private readonly IObjectPool<GenericContextMenuSeparatorView> separatorPool;
        private readonly IObjectPool<GenericContextMenuButtonWithTextView> buttonPool;
        private readonly IObjectPool<GenericContextMenuToggleView> togglePool;
        private readonly IObjectPool<GenericContextMenuToggleWithIconView> toggleWithIconPool;
        private readonly IObjectPool<GenericContextMenuUserProfileView> userProfilePool;
        private readonly IObjectPool<GenericContextMenuButtonWithStringDelegateView> buttonWithStringDelegatePool;
        private readonly List<GenericContextMenuComponentBase> currentControls = new ();

        public ControlsPoolManager(
            ViewDependencies viewDependencies,
            ProfileRepositoryWrapper profileDataProvider,
            Transform controlsParent,
            GenericContextMenuSeparatorView separatorPrefab,
            GenericContextMenuButtonWithTextView buttonPrefab,
            GenericContextMenuToggleView togglePrefab,
            GenericContextMenuToggleWithIconView toggleWithIconPrefab,
            GenericContextMenuUserProfileView userProfilePrefab,
            GenericContextMenuButtonWithStringDelegateView buttonWithDelegatePrefab)
        {
            separatorPool = new ObjectPool<GenericContextMenuSeparatorView>(
                createFunc: () => Object.Instantiate(separatorPrefab, controlsParent),
                actionOnGet: separatorView => separatorView.gameObject.SetActive(true),
                actionOnRelease: separatorView => separatorView?.gameObject.SetActive(false),
                actionOnDestroy: separatorView => Object.Destroy(separatorView.gameObject));

            buttonPool = new ObjectPool<GenericContextMenuButtonWithTextView>(
                createFunc: () => Object.Instantiate(buttonPrefab, controlsParent),
                actionOnGet: buttonView => buttonView.gameObject.SetActive(true),
                actionOnRelease: buttonView => buttonView?.gameObject.SetActive(false),
                actionOnDestroy: buttonView => Object.Destroy(buttonView.gameObject));

            togglePool = new ObjectPool<GenericContextMenuToggleView>(
                createFunc: () => Object.Instantiate(togglePrefab, controlsParent),
                actionOnGet: toggleView => toggleView.gameObject.SetActive(true),
                actionOnRelease: toggleView => toggleView?.gameObject.SetActive(false),
                actionOnDestroy: toggleView => Object.Destroy(toggleView.gameObject));

            toggleWithIconPool = new ObjectPool<GenericContextMenuToggleWithIconView>(
                createFunc: () => Object.Instantiate(toggleWithIconPrefab, controlsParent),
                actionOnGet: toggleView => toggleView.gameObject.SetActive(true),
                actionOnRelease: toggleView => toggleView?.gameObject.SetActive(false),
                actionOnDestroy: toggleView => Object.Destroy(toggleView.gameObject));

            userProfilePool = new ObjectPool<GenericContextMenuUserProfileView>(
                createFunc: () =>
                {
                    GenericContextMenuUserProfileView profileView = Object.Instantiate(userProfilePrefab, controlsParent);
                    profileView.InjectDependencies(viewDependencies);
                    profileView.SetProfileDataProvider(profileDataProvider);
                    return profileView;
                },
                actionOnGet: userProfileView => userProfileView.gameObject.SetActive(true),
                actionOnRelease: userProfileView => userProfileView?.gameObject.SetActive(false),
                actionOnDestroy: userProfileView => Object.Destroy(userProfileView.gameObject));

            buttonWithStringDelegatePool = new ObjectPool<GenericContextMenuButtonWithStringDelegateView>(
                createFunc: () => Object.Instantiate(buttonWithDelegatePrefab, controlsParent),
                actionOnGet: buttonView => buttonView.gameObject.SetActive(true),
                actionOnRelease: buttonView => buttonView?.gameObject.SetActive(false),
                actionOnDestroy: buttonView => Object.Destroy(buttonView.gameObject));

        }

        public void Dispose() =>
            ReleaseAllCurrentControls();

        public GenericContextMenuComponentBase GetContextMenuComponent<T>(T settings, int index) where T: IContextMenuControlSettings
        {
            GenericContextMenuComponentBase component = settings switch
                                                        {
                                                            SeparatorContextMenuControlSettings separatorSettings => GetSeparator(separatorSettings),
                                                            ButtonContextMenuControlSettings buttonSettings => GetButton(buttonSettings),
                                                            ToggleWithIconContextMenuControlSettings toggleWithIconSettings => GetToggleWithIcon(toggleWithIconSettings),
                                                            ToggleContextMenuControlSettings toggleSettings => GetToggle(toggleSettings),
                                                            UserProfileContextMenuControlSettings userProfileSettings => GetUserProfile(userProfileSettings),
                                                            ButtonWithDelegateContextMenuControlSettings<string> buttonWithDelegateSettings => GetButtonWithStringDelegate(buttonWithDelegateSettings),
                                                            _ => throw new ArgumentOutOfRangeException(),
                                                        };

            component!.transform.SetSiblingIndex(index);
            currentControls.Add(component);

            return component;
        }

        private GenericContextMenuComponentBase GetUserProfile(UserProfileContextMenuControlSettings settings)
        {
            GenericContextMenuUserProfileView userProfileView = userProfilePool.Get();
            userProfileView.Configure(settings);

            return userProfileView;
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

        private GenericContextMenuComponentBase GetToggle(ToggleContextMenuControlSettings settings)
        {
            GenericContextMenuToggleView separatorView = togglePool.Get();
            separatorView.Configure(settings, settings.initialValue);

            return separatorView;
        }

        private GenericContextMenuComponentBase GetButtonWithStringDelegate(ButtonWithDelegateContextMenuControlSettings<string> settings)
        {
            GenericContextMenuButtonWithStringDelegateView button = buttonWithStringDelegatePool.Get();
            button.Configure(settings);
            return button;
        }

        private GenericContextMenuComponentBase GetToggleWithIcon(ToggleWithIconContextMenuControlSettings settings)
        {
            GenericContextMenuToggleWithIconView view = toggleWithIconPool.Get();
            view.Configure(settings, settings.initialValue);

            return view;
        }

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
                    case GenericContextMenuToggleWithIconView toggleWithIconView:
                        toggleWithIconPool.Release(toggleWithIconView);
                        break;
                    case GenericContextMenuToggleView toggleView:
                        togglePool.Release(toggleView);
                        break;
                    case GenericContextMenuUserProfileView userProfileView:
                        userProfilePool.Release(userProfileView);
                        break;
                    case GenericContextMenuButtonWithStringDelegateView buttonView:
                        buttonWithStringDelegatePool.Release(buttonView);
                        break;
                }
            }

            currentControls.Clear();
        }
    }
}
