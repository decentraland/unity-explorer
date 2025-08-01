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
        private readonly List<GenericContextMenuComponentBase> currentControls = new ();
        private readonly List<ControlsContainerView> currentContainers = new ();
        private readonly Transform controlsParent;

        private readonly Dictionary<Type, (object Pool, Action<object> ReleaseAction)> poolRegistry = new ();

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
            GenericContextMenuSubMenuButtonView subMenuButtonPrefab,
            GenericContextMenuSimpleButtonView simpleButtonPrefab,
            GenericContextMenuScrollableButtonListView buttonListPrefab)
        {
            this.controlsParent = controlsParent;

            CreateObjectPool(controlsContainerPrefab);
            CreateObjectPool(separatorPrefab);
            CreateObjectPool(buttonPrefab);
            CreateObjectPool(togglePrefab);
            CreateObjectPool(toggleWithIconPrefab);
            CreateObjectPool(() =>
            {
                GenericContextMenuUserProfileView profileView = Object.Instantiate(userProfilePrefab, controlsParent);
                profileView.SetProfileDataProvider(profileDataProvider);
                return profileView;
            });
            CreateObjectPool(buttonWithDelegatePrefab);
            CreateObjectPool(textPrefab);
            CreateObjectPool(toggleWithCheckPrefab);
            CreateObjectPool(subMenuButtonPrefab);
            CreateObjectPool(simpleButtonPrefab);
            CreateObjectPool(buttonListPrefab);
        }

        private void CreateObjectPool<T>(T prefab) where T: MonoBehaviour =>
            CreateObjectPool(() => Object.Instantiate(prefab, controlsParent));

        private void CreateObjectPool<T>(Func<T> createFunc) where T: MonoBehaviour
        {
            ObjectPool<T> pool = new (
                createFunc: createFunc,
                actionOnGet: component => component.gameObject.SetActive(true),
                actionOnRelease: component => component?.gameObject.SetActive(false),
                actionOnDestroy: component => Object.Destroy(component.gameObject));

            Type type = typeof(T);
            poolRegistry[type] = (pool, obj => pool.Release((T)obj));
        }

        private ObjectPool<T> GetPoolFromRegistry<T>() where T: MonoBehaviour
        {
            if (poolRegistry.TryGetValue(typeof(T), out (object Pool, Action<object> ReleaseAction) pool))
                return (ObjectPool<T>) pool.Pool;

            throw new Exception($"No pool for type {typeof(T)} found. Did you forget to create it?");
        }

        public void Dispose() =>
            ReleaseAllCurrentControls();

        public GenericContextMenuComponentBase GetContextMenuComponent(IContextMenuControlSettings settings, int index, Transform parent)
        {
            GenericContextMenuComponentBase component = settings switch
                                                        {
                                                            SeparatorContextMenuControlSettings separatorSettings => GetSeparator(separatorSettings),
                                                            ButtonContextMenuControlSettings buttonSettings => GetButton(buttonSettings),
                                                            SimpleButtonContextMenuControlSettings simpleButtonSettings => GetSimpleButton(simpleButtonSettings),
                                                            ToggleWithIconContextMenuControlSettings toggleWithIconSettings => GetToggleWithIcon(toggleWithIconSettings),
                                                            ToggleWithCheckContextMenuControlSettings toggleWithCheckSettings => GetToggleWithCheck(toggleWithCheckSettings),
                                                            ToggleContextMenuControlSettings toggleSettings => GetToggle(toggleSettings),
                                                            UserProfileContextMenuControlSettings userProfileSettings => GetUserProfile(userProfileSettings),
                                                            ButtonWithDelegateContextMenuControlSettings<string> buttonWithDelegateSettings => GetButtonWithStringDelegate(buttonWithDelegateSettings),
                                                            TextContextMenuControlSettings textSettings => GetText(textSettings),
                                                            SubMenuContextMenuButtonSettings subMenuButtonSettings => GetSubMenuButton(subMenuButtonSettings),
                                                            ScrollableButtonListControlSettings scrollableButtonList => GetScrollableButtonList(scrollableButtonList),
                                                            _ => throw new ArgumentOutOfRangeException(),
                                                        };

            component.transform.SetParent(parent);
            component!.transform.SetSiblingIndex(index);
            currentControls.Add(component);

            return component;
        }

        private GenericContextMenuScrollableButtonListView GetScrollableButtonList(ScrollableButtonListControlSettings settings)
        {
            GenericContextMenuScrollableButtonListView buttonListView = GetPoolFromRegistry<GenericContextMenuScrollableButtonListView>().Get();
            buttonListView.Configure(settings, this);

            return buttonListView;
        }

        public ControlsContainerView GetControlsContainer(Transform parent)
        {
            ControlsContainerView container = GetPoolFromRegistry<ControlsContainerView>().Get();
            currentContainers.Add(container);
            container.transform.SetParent(parent);
            container.controlsContainer.pivot = new Vector2(0f, 1f);
            return container;
        }

        private GenericContextMenuComponentBase GetUserProfile(UserProfileContextMenuControlSettings settings)
        {
            GenericContextMenuUserProfileView userProfileView = GetPoolFromRegistry<GenericContextMenuUserProfileView>().Get();
            userProfileView.Configure(settings);

            return userProfileView;
        }

        private GenericContextMenuComponentBase GetSeparator(SeparatorContextMenuControlSettings settings)
        {
            GenericContextMenuSeparatorView separatorView = GetPoolFromRegistry<GenericContextMenuSeparatorView>().Get();
            separatorView.Configure(settings);

            return separatorView;
        }

        private GenericContextMenuComponentBase GetButton(ButtonContextMenuControlSettings settings)
        {
            GenericContextMenuButtonWithTextView separatorView = GetPoolFromRegistry<GenericContextMenuButtonWithTextView>().Get();
            separatorView.Configure(settings);

            return separatorView;
        }

        private GenericContextMenuComponentBase GetSimpleButton(SimpleButtonContextMenuControlSettings settings)
        {
            GenericContextMenuSimpleButtonView buttonView = GetPoolFromRegistry<GenericContextMenuSimpleButtonView>().Get();
            buttonView.Configure(settings);

            return buttonView;
        }

        private GenericContextMenuComponentBase GetSubMenuButton(SubMenuContextMenuButtonSettings settings)
        {
            GenericContextMenuSubMenuButtonView subMenuButtonView = GetPoolFromRegistry<GenericContextMenuSubMenuButtonView>().Get();
            subMenuButtonView.Configure(settings);

            return subMenuButtonView;
        }

        private GenericContextMenuComponentBase GetToggle(ToggleContextMenuControlSettings settings)
        {
            GenericContextMenuToggleView separatorView = GetPoolFromRegistry<GenericContextMenuToggleView>().Get();
            separatorView.Configure(settings, settings.initialValue);

            return separatorView;
        }

        private GenericContextMenuComponentBase GetToggleWithCheck(ToggleWithCheckContextMenuControlSettings settings)
        {
            GenericContextMenuToggleWithCheckView toggleWithCheckView = GetPoolFromRegistry<GenericContextMenuToggleWithCheckView>().Get();
            toggleWithCheckView.Configure(settings, settings.initialValue);

            return toggleWithCheckView;
        }

        private GenericContextMenuComponentBase GetButtonWithStringDelegate(ButtonWithDelegateContextMenuControlSettings<string> settings)
        {
            GenericContextMenuButtonWithStringDelegateView button = GetPoolFromRegistry<GenericContextMenuButtonWithStringDelegateView>().Get();
            button.Configure(settings);
            return button;
        }

        private GenericContextMenuComponentBase GetToggleWithIcon(ToggleWithIconContextMenuControlSettings settings)
        {
            GenericContextMenuToggleWithIconView view = GetPoolFromRegistry<GenericContextMenuToggleWithIconView>().Get();
            view.Configure(settings, settings.initialValue);

            return view;
        }

        private GenericContextMenuComponentBase GetText(TextContextMenuControlSettings settings)
        {
            GenericContextMenuTextView view = GetPoolFromRegistry<GenericContextMenuTextView>().Get();
            view.Configure(settings);
            return view;
        }

        public void ReleaseAllCurrentControls()
        {
            foreach (GenericContextMenuComponentBase control in currentControls)
            {
                control.UnregisterListeners();
                poolRegistry[control.GetType()].ReleaseAction.Invoke(control);
            }

            foreach (var containerView in currentContainers)
                poolRegistry[typeof(ControlsContainerView)].ReleaseAction.Invoke(containerView);

            currentControls.Clear();
            currentContainers.Clear();
        }
    }
}
