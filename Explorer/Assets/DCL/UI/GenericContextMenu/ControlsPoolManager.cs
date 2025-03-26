using DCL.UI.GenericContextMenu.Controls;
using DCL.UI.GenericContextMenu.Controls.Configs;
using MVC;
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
        private readonly IObjectPool<GenericContextMenuUserProfileView> userProfilePool;
        private readonly IObjectPool<GenericContextMenuOpenUserProfileButtonView> openUserProfileButtonPool;
        private readonly IObjectPool<GenericContextMenuMentionUserButtonView> mentionUserButtonPool;
        private readonly IObjectPool<GenericContextMenuBlockUserButtonView> blockUserButtonPool;

        private readonly List<GenericContextMenuComponentBase> currentControls = new ();

        public ControlsPoolManager(
            ViewDependencies viewDependencies,
            Transform controlsParent,
            GenericContextMenuSeparatorView separatorPrefab,
            GenericContextMenuButtonWithTextView buttonPrefab,
            GenericContextMenuToggleView togglePrefab,
            GenericContextMenuUserProfileView userProfilePrefab,
            GenericContextMenuOpenUserProfileButtonView openUserProfileButtonPrefab,
            GenericContextMenuMentionUserButtonView mentionUserButtonPrefab,
            GenericContextMenuBlockUserButtonView blockUserButtonPrefab)
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

            userProfilePool = new ObjectPool<GenericContextMenuUserProfileView>(
                createFunc: () =>
                {
                    var profileView = GameObject.Instantiate(userProfilePrefab, controlsParent);
                    profileView.InjectDependencies(viewDependencies);
                    return profileView;
                },
                actionOnGet: userProfileView => userProfileView.gameObject.SetActive(true),
                actionOnRelease: userProfileView => userProfileView.gameObject.SetActive(false),
                actionOnDestroy: userProfileView => GameObject.Destroy(userProfileView.gameObject));

            openUserProfileButtonPool = new ObjectPool<GenericContextMenuOpenUserProfileButtonView>(
                createFunc: () => GameObject.Instantiate(openUserProfileButtonPrefab, controlsParent),
                actionOnGet: buttonView => buttonView.gameObject.SetActive(true),
                actionOnRelease: buttonView => buttonView.gameObject.SetActive(false),
                actionOnDestroy: buttonView => GameObject.Destroy(buttonView.gameObject));

            blockUserButtonPool = new ObjectPool<GenericContextMenuBlockUserButtonView>(
                createFunc: () => GameObject.Instantiate(blockUserButtonPrefab, controlsParent),
                actionOnGet: buttonView => buttonView.gameObject.SetActive(true),
                actionOnRelease: buttonView => buttonView.gameObject.SetActive(false),
                actionOnDestroy: buttonView => GameObject.Destroy(buttonView.gameObject));

            mentionUserButtonPool = new ObjectPool<GenericContextMenuMentionUserButtonView>(
                createFunc: () => GameObject.Instantiate(mentionUserButtonPrefab, controlsParent),
                actionOnGet: buttonView => buttonView.gameObject.SetActive(true),
                actionOnRelease: buttonView => buttonView.gameObject.SetActive(false),
                actionOnDestroy: buttonView => GameObject.Destroy(buttonView.gameObject));
        }

        public GenericContextMenuComponentBase GetContextMenuComponent<T>(T settings, int index) where T : IContextMenuControlSettings
        {
            GenericContextMenuComponentBase component = settings switch
                                                        {
                                                            SeparatorContextMenuControlSettings separatorSettings => GetSeparator(separatorSettings),
                                                            ButtonContextMenuControlSettings buttonSettings => GetButton(buttonSettings),
                                                            ToggleContextMenuControlSettings toggleSettings => GetToggle(toggleSettings),
                                                            UserProfileContextMenuControlSettings userProfileSettings => GetUserProfile(userProfileSettings),
                                                            MentionUserButtonContextMenuControlSettings mentionUserButtonContextMenuControlSettings => GetMentionUserButton(mentionUserButtonContextMenuControlSettings),
                                                            OpenUserProfileButtonContextMenuControlSettings openUserProfileButtonContextMenuControlSettings => GetOpenUserProfileButton(openUserProfileButtonContextMenuControlSettings),
                                                            BlockUserButtonContextMenuControlSettings blockUserButtonContextMenuControlSettings => GetBlockUserButton(blockUserButtonContextMenuControlSettings),
                                                            _ => throw new ArgumentOutOfRangeException()
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

        private GenericContextMenuComponentBase GetMentionUserButton(MentionUserButtonContextMenuControlSettings settings)
        {
            GenericContextMenuMentionUserButtonView mentionUserButton = mentionUserButtonPool.Get();
            mentionUserButton.Configure(settings);

            return mentionUserButton;
        }

        private GenericContextMenuComponentBase GetOpenUserProfileButton(OpenUserProfileButtonContextMenuControlSettings settings)
        {
            GenericContextMenuOpenUserProfileButtonView userProfileView = openUserProfileButtonPool.Get();
            userProfileView.Configure(settings);

            return userProfileView;
        }

        private GenericContextMenuComponentBase GetBlockUserButton(BlockUserButtonContextMenuControlSettings settings)
        {
            GenericContextMenuBlockUserButtonView blockUserView = blockUserButtonPool.Get();
            blockUserView.Configure(settings);

            return blockUserView;
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
                    case GenericContextMenuUserProfileView userProfileView:
                        userProfilePool.Release(userProfileView);
                        break;
                    case GenericContextMenuMentionUserButtonView buttonView:
                        mentionUserButtonPool.Release(buttonView);
                        break;
                    case GenericContextMenuOpenUserProfileButtonView buttonView:
                        openUserProfileButtonPool.Release(buttonView);
                        break;
                    case GenericContextMenuBlockUserButtonView buttonView:
                        blockUserButtonPool.Release(buttonView);
                        break;
                }
            }

            currentControls.Clear();
        }
    }
}
