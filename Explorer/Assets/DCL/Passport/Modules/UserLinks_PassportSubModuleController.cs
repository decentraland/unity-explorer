using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.ExternalUrlPrompt;
using DCL.Passport.Fields;
using DCL.Passport.Modals;
using DCL.Profiles;
using MVC;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.Pool;
using UnityEngine.UI;
using Utility;

namespace DCL.Passport.Modules
{
    public class UserLinks_PassportSubModuleController
    {
        private const string NO_LINKS_TEXT = "No links.";
        private const int LINKS_MAX_AMOUNT = 5;

        private readonly UserDetailedInfo_PassportModuleView view;
        private readonly AddLink_PassportModal addLinkModal;
        private readonly IMVCManager mvcManager;
        private readonly IProfileRepository profileRepository;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly PassportErrorsController passportErrorsController;

        private Profile currentProfile;
        private readonly IObjectPool<Link_PassportFieldView> linksPool;
        private readonly List<Link_PassportFieldView> instantiatedLinks = new();
        private readonly IObjectPool<Link_PassportFieldView> linksPoolForEdition;
        private readonly List<Link_PassportFieldView> instantiatedLinksForEdition = new();

        private CancellationTokenSource saveLinksCts;

        public UserLinks_PassportSubModuleController(
            UserDetailedInfo_PassportModuleView view,
            AddLink_PassportModal addLinkModal,
            IMVCManager mvcManager,
            IProfileRepository profileRepository,
            World world,
            Entity playerEntity,
            PassportErrorsController passportErrorsController)
        {
            this.view = view;
            this.addLinkModal = addLinkModal;
            this.mvcManager = mvcManager;
            this.profileRepository = profileRepository;
            this.world = world;
            this.playerEntity = playerEntity;
            this.passportErrorsController = passportErrorsController;

            linksPool = new ObjectPool<Link_PassportFieldView>(
                InstantiateLinkPrefab,
                defaultCapacity: LINKS_MAX_AMOUNT,
                actionOnGet: buttonView => buttonView.gameObject.SetActive(true),
                actionOnRelease: buttonView =>
                {
                    buttonView.LinkButton.onClick.RemoveAllListeners();
                    buttonView.RemoveLinkButton.onClick.RemoveAllListeners();
                    buttonView.gameObject.SetActive(false);
                }
            );

            linksPoolForEdition = new ObjectPool<Link_PassportFieldView>(
                InstantiateLinkForEditionPrefab,
                defaultCapacity: LINKS_MAX_AMOUNT,
                actionOnGet: buttonView => buttonView.gameObject.SetActive(true),
                actionOnRelease: buttonView =>
                {
                    buttonView.LinkButton.onClick.RemoveAllListeners();
                    buttonView.RemoveLinkButton.onClick.RemoveAllListeners();
                    buttonView.gameObject.SetActive(false);
                }
            );

            view.NoLinksLabel.text = NO_LINKS_TEXT;
            view.LinksEditionButton.onClick.AddListener(() => SetLinksSectionAsEditionMode(true));
            view.CancelLinksButton.onClick.AddListener(() => SetLinksSectionAsEditionMode(false));
            view.SaveLinksButton.onClick.AddListener(SaveLinksSection);
            view.AddNewLinkButton.onClick.AddListener(addLinkModal.Show);
            addLinkModal.OnSave += CreateNewLink;
        }

        public void Setup(Profile profile) =>
            this.currentProfile = profile;

        public void Dispose()
        {
            view.LinksEditionButton.onClick.RemoveAllListeners();
            view.CancelLinksButton.onClick.RemoveAllListeners();
            view.SaveLinksButton.onClick.RemoveAllListeners();
            view.AddNewLinkButton.onClick.RemoveAllListeners();
            addLinkModal.OnSave -= CreateNewLink;
            saveLinksCts.SafeCancelAndDispose();
        }

        private Link_PassportFieldView InstantiateLinkPrefab()
        {
            Link_PassportFieldView linkView = UnityEngine.Object.Instantiate(view.LinkPrefab, view.LinksContainer);
            return linkView;
        }

        private Link_PassportFieldView InstantiateLinkForEditionPrefab()
        {
            Link_PassportFieldView linkView = UnityEngine.Object.Instantiate(view.LinkPrefab, view.LinksContainerForEditMode);
            return linkView;
        }

        public void ClearAllLinks()
        {
            ClearLinks();
            ClearLinksForEdition();
        }

        private void ClearLinks()
        {
            foreach (Link_PassportFieldView link in instantiatedLinks)
                linksPool.Release(link);

            instantiatedLinks.Clear();
        }

        private void ClearLinksForEdition()
        {
            foreach (Link_PassportFieldView linkForEdition in instantiatedLinksForEdition)
                linksPoolForEdition.Release(linkForEdition);

            instantiatedLinksForEdition.Clear();
        }

        public void LoadLinks()
        {
            view.NoLinksLabel.gameObject.SetActive(currentProfile.Links == null || currentProfile.Links.Count == 0);
            view.LinksContainer.gameObject.SetActive(currentProfile.Links is { Count: > 0 });

            if (currentProfile.Links == null)
                return;

            foreach (var link in currentProfile.Links)
                AddLink(Guid.NewGuid().ToString(), link.title, link.url, false);
        }

        private void AddLink(string id, string title, string url, bool isEditMode)
        {
            var newLink = !isEditMode ? linksPool.Get() : linksPoolForEdition.Get();
            newLink.transform.SetAsLastSibling();
            newLink.Id = id;
            newLink.Url = url;
            newLink.Title.text = title;
            newLink.LinkButton.onClick.AddListener(() =>
            {
                if (newLink.IsInEditMode) return;
                OpenUrlAsync(url).Forget();
            });
            newLink.SetAsEditable(isEditMode);

            if (!isEditMode)
                instantiatedLinks.Add(newLink);
            else
            {
                newLink.RemoveLinkButton.onClick.AddListener(() => RemoveLink(newLink));
                instantiatedLinksForEdition.Add(newLink);
                SetNewLinkButtonActive(instantiatedLinksForEdition.Count < LINKS_MAX_AMOUNT);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(newLink.Container);
        }

        private async UniTask OpenUrlAsync(string url) =>
            await mvcManager.ShowAsync(ExternalUrlPromptController.IssueCommand(new ExternalUrlPromptController.Params(url)));

        public void SetLinksEditionButtonAsActive(bool isActive) =>
            view.LinksEditionButton.gameObject.SetActive(isActive);

        public void SetSaveButtonAsInteractable(bool isInteractable) =>
            view.SaveLinksButton.interactable = isInteractable;

        public void SetLinksSectionAsEditionMode(bool isEditMode)
        {
            SetLinksSectionAsSavingStatus(false);

            foreach (var editionObj in view.LinksEditionObjects)
                editionObj.SetActive(isEditMode);

            foreach (var readOnlyObj in view.LinksReadOnlyObjects)
                readOnlyObj.SetActive(!isEditMode);

            if (isEditMode)
            {
                ClearLinksForEdition();
                foreach (var link in instantiatedLinks)
                    AddLink(link.Id, link.Title.text, link.Url, true);

                SetNewLinkButtonActive(instantiatedLinksForEdition.Count < LINKS_MAX_AMOUNT);
            }
            else
            {
                view.LinksContainer.gameObject.SetActive(currentProfile.Links is { Count: > 0 });
                saveLinksCts.SafeCancelAndDispose();
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.MainContainer);
        }

        private void CreateNewLink(string title, string url)
        {
            AddLink(Guid.NewGuid().ToString(), title, url, true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(view.MainContainer);
        }

        private void RemoveLink(Link_PassportFieldView linkToRemove)
        {
            var indexToRemove = 0;
            foreach (var link in instantiatedLinksForEdition)
            {
                if (link.Id == linkToRemove.Id)
                    break;

                indexToRemove++;
            }

            if (indexToRemove >= instantiatedLinksForEdition.Count)
                return;

            linksPoolForEdition.Release(linkToRemove);
            instantiatedLinksForEdition.RemoveAt(indexToRemove);
            SetNewLinkButtonActive(true);
        }

        private void SetNewLinkButtonActive(bool isActive)
        {
            view.AddNewLinkButton.gameObject.SetActive(isActive);

            if (isActive)
                view.AddNewLinkButton.transform.SetAsLastSibling();
        }

        private void SaveLinksSection()
        {
            saveLinksCts = saveLinksCts.SafeRestart();
            SaveLinksAsync(saveLinksCts.Token).Forget();
            return;

            async UniTaskVoid SaveLinksAsync(CancellationToken ct)
            {
                SetLinksSectionAsSavingStatus(true);
                currentProfile.ClearLinks();

                foreach (var link in instantiatedLinksForEdition)
                {
                    currentProfile.Links!.Add(new LinkJsonDto
                    {
                        title = link.Title.text,
                        url = link.Url,
                    });
                }

                await UpdateProfileAsync(ct);
                ClearAllLinks();
                LoadLinks();
                SetLinksSectionAsEditionMode(false);
            }
        }

        private void SetLinksSectionAsSavingStatus(bool isSaving)
        {
            view.SaveLinksButtonLoading.SetActive(isSaving);
            view.CancelLinksButton.gameObject.SetActive(!isSaving);
            view.SaveLinksButton.gameObject.SetActive(!isSaving);
            view.SaveInfoButton.interactable = !isSaving;

            foreach (var buttonToDisable in view.ButtonsToDisableWhileSaving)
                buttonToDisable.interactable = !isSaving;

            foreach (var link in instantiatedLinks)
                link.SetAsInteractable(!isSaving);
        }

        private async UniTask UpdateProfileAsync(CancellationToken ct)
        {
            try
            {
                // Update profile data
                await profileRepository.SetAsync(currentProfile, ct);

                // Update player entity in world
                currentProfile.IsDirty = true;
                world.Set(playerEntity, currentProfile);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                const string ERROR_MESSAGE = "There was an error while trying to update your profile links. Please try again!";
                passportErrorsController.Show();
                ReportHub.LogError(ReportCategory.PROFILE, $"{ERROR_MESSAGE} ERROR: {e.Message}");
            }
            finally
            {
                if (currentProfile != null)
                    currentProfile = await profileRepository.GetAsync(currentProfile.UserId, 0, ct);
            }
        }
    }
}
