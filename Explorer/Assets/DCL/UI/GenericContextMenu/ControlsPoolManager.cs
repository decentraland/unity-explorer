using DCL.UI.GenericContextMenu.Controls;
using DCL.UI.GenericContextMenu.Controls.Configs;
using DCL.UI.GenericContextMenuParameter;
using DCL.UI.Profiles.Helpers;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.UI.GenericContextMenu
{
    public class ControlsPoolManager : IDisposable
    {
        private readonly IObjectPool<ControlsContainerView> controlsContainerPool;
        private readonly IObjectPool<GenericContextMenuSeparatorView> separatorPool;
        private readonly IObjectPool<GenericContextMenuButtonWithTextView> buttonPool;
        private readonly IObjectPool<GenericContextMenuToggleView> togglePool;
        private readonly IObjectPool<GenericContextMenuToggleWithIconView> toggleWithIconPool;
        private readonly IObjectPool<GenericContextMenuUserProfileView> userProfilePool;
        private readonly IObjectPool<GenericContextMenuButtonWithStringDelegateView> buttonWithStringDelegatePool;
        private readonly IObjectPool<GenericContextMenuTextView> textPool;
        private readonly IObjectPool<GenericContextMenuToggleWithCheckView> toggleWithCheckPool;
        private readonly IObjectPool<GenericContextMenuSubMenuButtonView> subMenuButtonPool;
        private readonly List<GenericContextMenuComponentBase> currentControls = new ();
        private readonly List<ControlsContainerView> currentContainers = new ();

        public ControlsPoolManager(
            ProfileRepositoryWrapper profileDataProvider,
            Transform controlsParent,
            ControlsContainerView controlsContainerPrefab,
            GenericContextMenuSeparatorView separatorPrefab,
            GenericContextMenuButtonWithTextView buttonPrefab,
            GenericContextMenuToggleView togglePrefab,
            GenericContextMenuToggleWithIconView toggleWithIconPrefab,
            GenericContextMenuUserProfileView userProfilePrefab,
            GenericContextMenuButtonWithStringDelegateView buttonWithDelegatePrefab,
            GenericContextMenuTextView textPrefab,
            GenericContextMenuToggleWithCheckView toggleWithCheckPrefab,
            GenericContextMenuSubMenuButtonView subMenuButtonPrefab)
        {
            controlsContainerPool = CreateObjectPool(controlsContainerPrefab, controlsParent);
            separatorPool = CreateObjectPool(separatorPrefab, controlsParent);
            buttonPool = CreateObjectPool(buttonPrefab, controlsParent);
            togglePool = CreateObjectPool(togglePrefab, controlsParent);
            toggleWithIconPool = CreateObjectPool(toggleWithIconPrefab, controlsParent);
            userProfilePool = CreateObjectPool(() =>
            {
                GenericContextMenuUserProfileView profileView = Object.Instantiate(userProfilePrefab, controlsParent);
                profileView.SetProfileDataProvider(profileDataProvider);
                return profileView;
            });
            buttonWithStringDelegatePool = CreateObjectPool(buttonWithDelegatePrefab, controlsParent);
            textPool = CreateObjectPool(textPrefab, controlsParent);
            toggleWithCheckPool = CreateObjectPool(toggleWithCheckPrefab, controlsParent);
            subMenuButtonPool = CreateObjectPool(subMenuButtonPrefab, controlsParent);
        }

        private static ObjectPool<T> CreateObjectPool<T>(T prefab, Transform parent) where T: MonoBehaviour =>
            CreateObjectPool(() => Object.Instantiate(prefab, parent));

        private static ObjectPool<T> CreateObjectPool<T>(Func<T> createFunc) where T: MonoBehaviour =>
            new (
                createFunc: createFunc,
                actionOnGet: component => component.gameObject.SetActive(true),
                actionOnRelease: component => component?.gameObject.SetActive(false),
                actionOnDestroy: component => Object.Destroy(component.gameObject));

        public void Dispose() =>
            ReleaseAllCurrentControls();

        public GenericContextMenuComponentBase GetContextMenuComponent<T>(T settings, int index, Transform parent) where T: IContextMenuControlSettings
        {
            GenericContextMenuComponentBase component = settings switch
                                                        {
                                                            SeparatorContextMenuControlSettings separatorSettings => GetSeparator(separatorSettings),
                                                            ButtonContextMenuControlSettings buttonSettings => GetButton(buttonSettings),
                                                            ToggleWithIconContextMenuControlSettings toggleWithIconSettings => GetToggleWithIcon(toggleWithIconSettings),
                                                            ToggleWithCheckContextMenuControlSettings toggleWithCheckSettings => GetToggleWithCheck(toggleWithCheckSettings),
                                                            ToggleContextMenuControlSettings toggleSettings => GetToggle(toggleSettings),
                                                            UserProfileContextMenuControlSettings userProfileSettings => GetUserProfile(userProfileSettings),
                                                            ButtonWithDelegateContextMenuControlSettings<string> buttonWithDelegateSettings => GetButtonWithStringDelegate(buttonWithDelegateSettings),
                                                            TextContextMenuControlSettings textSettings => GetText(textSettings),
                                                            SubMenuContextMenuButtonSettings subMenuButtonSettings => GetSubMenuButton(subMenuButtonSettings),
                                                            _ => throw new ArgumentOutOfRangeException(),
                                                        };

            component.transform.SetParent(parent);
            component!.transform.SetSiblingIndex(index);
            currentControls.Add(component);


            return component;
        }

        public ControlsContainerView GetControlsContainer(Transform parent)
        {
            ControlsContainerView container = controlsContainerPool.Get();
            currentContainers.Add(container);
            container.transform.SetParent(parent);
            container.controlsContainer.pivot = new Vector2(0f, 1f);
            return container;
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

        private GenericContextMenuComponentBase GetSubMenuButton(SubMenuContextMenuButtonSettings settings)
        {
            GenericContextMenuSubMenuButtonView subMenuButtonView = subMenuButtonPool.Get();
            subMenuButtonView.Configure(settings);

            return subMenuButtonView;
        }

        private GenericContextMenuComponentBase GetToggle(ToggleContextMenuControlSettings settings)
        {
            GenericContextMenuToggleView separatorView = togglePool.Get();
            separatorView.Configure(settings, settings.initialValue);

            return separatorView;
        }

        private GenericContextMenuComponentBase GetToggleWithCheck(ToggleWithCheckContextMenuControlSettings settings)
        {
            GenericContextMenuToggleWithCheckView toggleWithCheckView = toggleWithCheckPool.Get();
            toggleWithCheckView.Configure(settings, settings.initialValue);

            return toggleWithCheckView;
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

        private GenericContextMenuComponentBase GetText(TextContextMenuControlSettings settings)
        {
            GenericContextMenuTextView view = textPool.Get();
            view.Configure(settings);
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
                    case GenericContextMenuTextView textView:
                        textPool.Release(textView);
                        break;
                    case GenericContextMenuToggleWithCheckView toggleWithCheckView:
                        toggleWithCheckPool.Release(toggleWithCheckView);
                        break;
                    case GenericContextMenuSubMenuButtonView subMenuButtonView:
                        subMenuButtonPool.Release(subMenuButtonView);
                        break;
                }
            }

            foreach (var containerView in currentContainers)
                controlsContainerPool.Release(containerView);

            currentControls.Clear();
            currentContainers.Clear();
        }
    }
}
