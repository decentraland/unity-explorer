using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.ExternalUrlPrompt;
using DCL.Passport.Configuration;
using DCL.Passport.Fields;
using DCL.Passport.Modals;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI;
using MVC;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine.Pool;
using UnityEngine.UI;
using Utility;

namespace DCL.Passport.Modules
{
    public class UserDetailedInfo_PassportModuleController : IPassportModuleController
    {
        private const string NO_INTRO_TEXT = "No intro.";
        private const string NO_LINKS_TEXT = "No links.";
        private const string EDITION_DROPDOWN_DEFAULT_OPTION = "-Select an option-";
        private const string EDITION_PLACE_HOLDER = "Write here";
        private const string EDITION_PLACE_HOLDER_FOR_DATES = "DD/MM/YYYY";
        private const int ADDITIONAL_FIELDS_POOL_DEFAULT_CAPACITY = 11;
        private const int LINKS_MAX_AMOUNT = 5;

        private readonly UserDetailedInfo_PassportModuleView view;
        private readonly IMVCManager mvcManager;
        private readonly ISelfProfile selfProfile;
        private readonly IProfileRepository profileRepository;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly AddLink_PassportModal addLinkModal;
        private readonly WarningNotificationView errorNotification;
        private readonly IObjectPool<AdditionalField_PassportFieldView> additionalFieldsPool;
        private readonly List<AdditionalField_PassportFieldView> instantiatedAdditionalFields = new();
        private readonly IObjectPool<AdditionalField_PassportFieldView> additionalFieldsPoolForEdition;
        private readonly List<AdditionalField_PassportFieldView> instantiatedAdditionalFieldsForEdition = new();
        private readonly IObjectPool<Link_PassportFieldView> linksPool;
        private readonly List<Link_PassportFieldView> instantiatedLinks = new();
        private readonly IObjectPool<Link_PassportFieldView> linksPoolForEdition;
        private readonly List<Link_PassportFieldView> instantiatedLinksForEdition = new();

        private Profile currentProfile;
        private CancellationTokenSource checkEditionAvailabilityCts;
        private CancellationTokenSource saveInfoCts;
        private CancellationTokenSource saveLinksCts;

        public UserDetailedInfo_PassportModuleController(
            UserDetailedInfo_PassportModuleView view,
            IMVCManager mvcManager,
            ISelfProfile selfProfile,
            IProfileRepository profileRepository,
            World world,
            Entity playerEntity,
            AddLink_PassportModal addLinkModal,
            WarningNotificationView errorNotification)
        {
            this.view = view;
            this.mvcManager = mvcManager;
            this.selfProfile = selfProfile;
            this.profileRepository = profileRepository;
            this.world = world;
            this.playerEntity = playerEntity;
            this.addLinkModal = addLinkModal;
            this.errorNotification = errorNotification;

            additionalFieldsPool = new ObjectPool<AdditionalField_PassportFieldView>(
                InstantiateAdditionalFieldPrefab,
                defaultCapacity: ADDITIONAL_FIELDS_POOL_DEFAULT_CAPACITY,
                actionOnGet: buttonView => buttonView.gameObject.SetActive(true),
                actionOnRelease: buttonView => buttonView.gameObject.SetActive(false));

            additionalFieldsPoolForEdition = new ObjectPool<AdditionalField_PassportFieldView>(
                InstantiateAdditionalFieldForEditionPrefab,
                defaultCapacity: ADDITIONAL_FIELDS_POOL_DEFAULT_CAPACITY,
                actionOnGet: buttonView => buttonView.gameObject.SetActive(true),
                actionOnRelease: buttonView => buttonView.gameObject.SetActive(false));

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

            view.InfoEditionButton.onClick.AddListener(() => SetInfoSectionAsEditionMode(true));
            view.CancelInfoButton.onClick.AddListener(() => SetInfoSectionAsEditionMode(false));
            view.SaveInfoButton.onClick.AddListener(SaveInfoSection);

            view.NoLinksLabel.text = NO_LINKS_TEXT;
            view.LinksEditionButton.onClick.AddListener(() => SetLinksSectionAsEditionMode(true));
            view.CancelLinksButton.onClick.AddListener(() => SetLinksSectionAsEditionMode(false));
            view.SaveLinksButton.onClick.AddListener(SaveLinksSection);
            view.AddNewLinkButton.onClick.AddListener(addLinkModal.Show);
            addLinkModal.OnSave += CreateNewLink;
        }

        public void Setup(Profile profile)
        {
            currentProfile = profile;

            SetInfoSectionAsEditionMode(false);
            SetLinksSectionAsEditionMode(false);
            LoadAdditionalFields();
            LoadLinks();
            LoadDescription();

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.MainContainer);

            checkEditionAvailabilityCts = checkEditionAvailabilityCts.SafeRestart();
            CheckForEditionAvailabilityAsync(checkEditionAvailabilityCts.Token).Forget();
        }

        public void Clear()
        {
            ClearAllAdditionalInfoFields();
            ClearAllLinks();
        }

        private void ClearAllAdditionalInfoFields()
        {
            ClearAdditionalInfoFields();
            ClearAdditionalInfoFieldsForEdition();
        }

        private void ClearAdditionalInfoFields()
        {
            foreach (AdditionalField_PassportFieldView additionalField in instantiatedAdditionalFields)
                additionalFieldsPool.Release(additionalField);

            instantiatedAdditionalFields.Clear();
        }

        private void ClearAdditionalInfoFieldsForEdition()
        {
            foreach (AdditionalField_PassportFieldView additionalFieldForEdition in instantiatedAdditionalFieldsForEdition)
                additionalFieldsPoolForEdition.Release(additionalFieldForEdition);

            instantiatedAdditionalFieldsForEdition.Clear();
        }

        private void ClearAllLinks()
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

        public void Dispose()
        {
            view.InfoEditionButton.onClick.RemoveAllListeners();
            view.CancelInfoButton.onClick.RemoveAllListeners();
            view.SaveInfoButton.onClick.RemoveAllListeners();
            view.LinksEditionButton.onClick.RemoveAllListeners();
            view.CancelLinksButton.onClick.RemoveAllListeners();
            view.SaveLinksButton.onClick.RemoveAllListeners();
            view.AddNewLinkButton.onClick.RemoveAllListeners();
            addLinkModal.OnSave -= CreateNewLink;
            checkEditionAvailabilityCts.SafeCancelAndDispose();
            saveInfoCts.SafeCancelAndDispose();
            saveLinksCts.SafeCancelAndDispose();
            Clear();
        }

        private AdditionalField_PassportFieldView InstantiateAdditionalFieldPrefab()
        {
            AdditionalField_PassportFieldView additionalFieldView = UnityEngine.Object.Instantiate(view.AdditionalFieldsConfiguration.additionalInfoFieldPrefab, view.AdditionalInfoContainer);
            return additionalFieldView;
        }

        private AdditionalField_PassportFieldView InstantiateAdditionalFieldForEditionPrefab()
        {
            AdditionalField_PassportFieldView additionalFieldView = UnityEngine.Object.Instantiate(view.AdditionalFieldsConfiguration.additionalInfoFieldPrefab, view.AdditionalInfoContainerForEditMode);
            return additionalFieldView;
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

        private void LoadDescription() =>
            view.Description.text = !string.IsNullOrEmpty(currentProfile.Description) || instantiatedAdditionalFields.Count > 0 ? currentProfile.Description : NO_INTRO_TEXT;

        private void LoadAdditionalFields()
        {
            if (!string.IsNullOrEmpty(currentProfile.Gender))
            {
                AddAdditionalField(AdditionalFieldType.GENDER, currentProfile.Gender, false);
                AddAdditionalField(AdditionalFieldType.GENDER, currentProfile.Gender, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.GENDER, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.Country))
            {
                AddAdditionalField(AdditionalFieldType.COUNTRY, currentProfile.Country, false);
                AddAdditionalField(AdditionalFieldType.COUNTRY, currentProfile.Country, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.COUNTRY, string.Empty, true);

            if (currentProfile.Birthdate != null && currentProfile.Birthdate.Value != new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
            {
                AddAdditionalField(AdditionalFieldType.BIRTH_DATE, currentProfile.Birthdate.Value.ToString("dd/MM/yyyy"), false);
                AddAdditionalField(AdditionalFieldType.BIRTH_DATE, currentProfile.Birthdate.Value.ToString("dd/MM/yyyy"), true);
            }
            else
                AddAdditionalField(AdditionalFieldType.BIRTH_DATE, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.Pronouns))
            {
                AddAdditionalField(AdditionalFieldType.PRONOUNS, currentProfile.Pronouns, false);
                AddAdditionalField(AdditionalFieldType.PRONOUNS, currentProfile.Pronouns, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.PRONOUNS, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.RelationshipStatus))
            {
                AddAdditionalField(AdditionalFieldType.RELATIONSHIP_STATUS, currentProfile.RelationshipStatus, false);
                AddAdditionalField(AdditionalFieldType.RELATIONSHIP_STATUS, currentProfile.RelationshipStatus, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.RELATIONSHIP_STATUS, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.SexualOrientation))
            {
                AddAdditionalField(AdditionalFieldType.SEXUAL_ORIENTATION, currentProfile.SexualOrientation, false);
                AddAdditionalField(AdditionalFieldType.SEXUAL_ORIENTATION, currentProfile.SexualOrientation, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.SEXUAL_ORIENTATION, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.Language))
            {
                AddAdditionalField(AdditionalFieldType.LANGUAGE, currentProfile.Language, false);
                AddAdditionalField(AdditionalFieldType.LANGUAGE, currentProfile.Language, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.LANGUAGE, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.Profession))
            {
                AddAdditionalField(AdditionalFieldType.PROFESSION, currentProfile.Profession, false);
                AddAdditionalField(AdditionalFieldType.PROFESSION, currentProfile.Profession, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.PROFESSION, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.EmploymentStatus))
            {
                AddAdditionalField(AdditionalFieldType.EMPLOYMENT_STATUS, currentProfile.EmploymentStatus, false);
                AddAdditionalField(AdditionalFieldType.EMPLOYMENT_STATUS, currentProfile.EmploymentStatus, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.EMPLOYMENT_STATUS, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.Hobbies))
            {
                AddAdditionalField(AdditionalFieldType.HOBBIES, currentProfile.Hobbies, false);
                AddAdditionalField(AdditionalFieldType.HOBBIES, currentProfile.Hobbies, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.HOBBIES, string.Empty, true);

            if (!string.IsNullOrEmpty(currentProfile.RealName))
            {
                AddAdditionalField(AdditionalFieldType.REAL_NAME, currentProfile.RealName, false);
                AddAdditionalField(AdditionalFieldType.REAL_NAME, currentProfile.RealName, true);
            }
            else
                AddAdditionalField(AdditionalFieldType.REAL_NAME, string.Empty, true);

            view.AdditionalInfoContainer.gameObject.SetActive(instantiatedAdditionalFields.Count > 0);
        }

        private void AddAdditionalField(AdditionalFieldType type, string value, bool isEditMode)
        {
            var newAdditionalField = !isEditMode ? additionalFieldsPool.Get() : additionalFieldsPoolForEdition.Get();
            newAdditionalField.transform.SetAsLastSibling();
            newAdditionalField.Value.text = value;
            newAdditionalField.Type = type;
            newAdditionalField.Title.text = type.ToString();
            newAdditionalField.Logo.sprite = null;
            newAdditionalField.EditionDropdown.options.Clear();
            newAdditionalField.EditionDropdown.options.Add(new TMP_Dropdown.OptionData { text = EDITION_DROPDOWN_DEFAULT_OPTION });
            newAdditionalField.EditionTextInput.text = string.Empty;
            newAdditionalField.EditionTextInputPlaceholder.text = type == AdditionalFieldType.BIRTH_DATE ? EDITION_PLACE_HOLDER_FOR_DATES : EDITION_PLACE_HOLDER;

            foreach (AdditionalFieldConfiguration additionalFieldConfig in view.AdditionalFieldsConfiguration.additionalFields)
            {
                if (additionalFieldConfig.type != type)
                    continue;

                newAdditionalField.Title.text = additionalFieldConfig.title;
                newAdditionalField.Logo.sprite = additionalFieldConfig.logo;
                newAdditionalField.IsEditableWithDropdown = additionalFieldConfig.editionValues != null;

                if (additionalFieldConfig.editionValues != null)
                    foreach (string option in additionalFieldConfig.editionValues.values)
                        newAdditionalField.EditionDropdown.options.Add(new TMP_Dropdown.OptionData { text = option });
            }

            newAdditionalField.SetAsEditable(isEditMode);

            if (!isEditMode)
                instantiatedAdditionalFields.Add(newAdditionalField);
            else
                instantiatedAdditionalFieldsForEdition.Add(newAdditionalField);
        }

        private void LoadLinks()
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

        private async UniTask OpenUrlAsync(string url)
        {
            await UniTask.SwitchToMainThread();
            await mvcManager.ShowAsync(ExternalUrlPromptController.IssueCommand(new ExternalUrlPromptController.Params(url)));
        }

        private async UniTaskVoid CheckForEditionAvailabilityAsync(CancellationToken ct)
        {
            view.InfoEditionButton.gameObject.SetActive(false);
            view.LinksEditionButton.gameObject.SetActive(false);
            var ownProfile = await selfProfile.ProfileAsync(ct);
            if (ownProfile?.UserId == currentProfile.UserId)
            {
                view.InfoEditionButton.gameObject.SetActive(true);
                view.LinksEditionButton.gameObject.SetActive(true);
            }
        }

        private void SetInfoSectionAsEditionMode(bool isEditMode)
        {
            SetInfoSectionAsSavingStatus(false);

            foreach (var editionObj in view.InfoEditionObjects)
                editionObj.SetActive(isEditMode);

            foreach (var readOnlyObj in view.InfoReadOnlyObjects)
                readOnlyObj.SetActive(!isEditMode);

            foreach (var additionalFieldForEdition in instantiatedAdditionalFieldsForEdition)
            {
                additionalFieldForEdition.SetEditionValue(string.Empty);
                foreach (var additionalField in instantiatedAdditionalFields)
                {
                    if (additionalFieldForEdition.Type != additionalField.Type)
                        continue;

                    additionalFieldForEdition.SetEditionValue(additionalField.Value.text);
                    break;
                }
            }

            if (isEditMode)
                view.DescriptionForEditMode.text = view.Description.text;
            else
            {
                view.AdditionalInfoContainer.gameObject.SetActive(instantiatedAdditionalFields.Count > 0);
                saveInfoCts.SafeCancelAndDispose();
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.MainContainer);
        }

        private void SaveInfoSection()
        {
            saveInfoCts = saveInfoCts.SafeRestart();
            SaveInfoAsync(saveInfoCts.Token).Forget();
            return;

            async UniTaskVoid SaveInfoAsync(CancellationToken ct)
            {
                SetInfoSectionAsSavingStatus(true);
                currentProfile.Description = view.DescriptionForEditMode.text;

                foreach (var additionalFieldForEdition in instantiatedAdditionalFieldsForEdition)
                {
                    string valueToSave = !string.IsNullOrEmpty(additionalFieldForEdition.EditionTextInput.text) ? additionalFieldForEdition.EditionTextInput.text : null;
                    switch (additionalFieldForEdition.Type)
                    {
                        case AdditionalFieldType.GENDER:
                            currentProfile.Gender = valueToSave;
                            break;
                        case AdditionalFieldType.COUNTRY:
                            currentProfile.Country = valueToSave;
                            break;
                        case AdditionalFieldType.BIRTH_DATE:
                            if (valueToSave != null)
                                currentProfile.Birthdate = DateTime.ParseExact(valueToSave, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                            else
                                currentProfile.Birthdate = null;
                            break;
                        case AdditionalFieldType.PRONOUNS:
                            currentProfile.Pronouns = valueToSave;
                            break;
                        case AdditionalFieldType.RELATIONSHIP_STATUS:
                            currentProfile.RelationshipStatus = valueToSave;
                            break;
                        case AdditionalFieldType.SEXUAL_ORIENTATION:
                            currentProfile.SexualOrientation = valueToSave;
                            break;
                        case AdditionalFieldType.LANGUAGE:
                            currentProfile.Language = valueToSave;
                            break;
                        case AdditionalFieldType.PROFESSION:
                            currentProfile.Profession = valueToSave;
                            break;
                        case AdditionalFieldType.EMPLOYMENT_STATUS:
                            currentProfile.EmploymentStatus = valueToSave;
                            break;
                        case AdditionalFieldType.HOBBIES:
                            currentProfile.Hobbies = valueToSave;
                            break;
                        case AdditionalFieldType.REAL_NAME:
                            currentProfile.RealName = valueToSave;
                            break;
                    }
                }

                await UpdateProfileAsync(ct);
                ClearAllAdditionalInfoFields();
                LoadAdditionalFields();
                LoadDescription();
                SetInfoSectionAsEditionMode(false);
                LayoutRebuilder.ForceRebuildLayoutImmediate(view.MainContainer);
            }
        }

        private void SetInfoSectionAsSavingStatus(bool isSaving)
        {
            view.SaveInfoButtonLoading.SetActive(isSaving);
            view.CancelInfoButton.gameObject.SetActive(!isSaving);
            view.SaveInfoButton.gameObject.SetActive(!isSaving);
            view.DescriptionForEditMode.interactable = !isSaving;
            view.SaveLinksButton.interactable = !isSaving;

            foreach (var additionalInfoForEdition in instantiatedAdditionalFieldsForEdition)
                additionalInfoForEdition.SetAsInteractable(!isSaving);
        }

        private void SetLinksSectionAsEditionMode(bool isEditMode)
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

                if (currentProfile.Links == null)
                    currentProfile.Links = new List<LinkJsonDto>();
                else
                    currentProfile.Links.Clear();

                List<LinkJsonDto> linksToSave = new ();
                foreach (var link in instantiatedLinksForEdition)
                {
                    currentProfile.Links.Add(new LinkJsonDto
                    {
                        title = link.Title.text,
                        url = link.Url,
                    });
                }

                await UpdateProfileAsync(ct);
                ClearAllLinks();
                LoadLinks();
                SetLinksSectionAsEditionMode(false);
                LayoutRebuilder.ForceRebuildLayoutImmediate(view.MainContainer);
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
                ShowErrorNotificationAsync(errorNotification, CancellationToken.None).Forget();
                ReportHub.LogError(ReportCategory.PROFILE, $"Error updating profile from passport: {e.Message}");
            }
            finally
            {
                if (currentProfile != null)
                    currentProfile = await profileRepository.GetAsync(currentProfile.UserId, 0, ct);
            }
        }

        private async UniTaskVoid ShowErrorNotificationAsync(WarningNotificationView notificationView, CancellationToken ct)
        {
            notificationView.Show();
            await UniTask.Delay(3000, cancellationToken: ct);
            notificationView.Hide();
        }
    }
}
