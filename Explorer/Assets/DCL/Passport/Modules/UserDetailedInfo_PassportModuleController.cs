using Cysharp.Threading.Tasks;
using DCL.ExternalUrlPrompt;
using DCL.Passport.Fields;
using DCL.Profiles;
using MVC;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;
using UnityEngine.UI;

namespace DCL.Passport.Modules
{
    public class UserDetailedInfo_PassportModuleController : IPassportModuleController
    {
        private const string NO_INTRO_TEXT = "No intro.";
        private const string NO_LINKS_TEXT = "No links.";
        private const int ADDITIONAL_FIELDS_POOL_DEFAULT_CAPACITY = 11;
        private const int LINK_POOL_DEFAULT_CAPACITY = 5;

        private readonly UserDetailedInfo_PassportModuleView view;
        private readonly IMVCManager mvcManager;
        private readonly IObjectPool<AdditionalField_PassportFieldView> additionalFieldsPool;
        private readonly List<AdditionalField_PassportFieldView> instantiatedAdditionalFields = new();
        private readonly IObjectPool<Link_PassportFieldView> linksPool;
        private readonly List<Link_PassportFieldView> instantiatedLinks = new();

        private Profile currentProfile;

        public UserDetailedInfo_PassportModuleController(
            UserDetailedInfo_PassportModuleView view,
            IMVCManager mvcManager)
        {
            this.view = view;
            this.mvcManager = mvcManager;

            additionalFieldsPool = new ObjectPool<AdditionalField_PassportFieldView>(
                InstantiateAdditionalFieldPrefab,
                defaultCapacity: ADDITIONAL_FIELDS_POOL_DEFAULT_CAPACITY,
                actionOnGet: buttonView => { buttonView.gameObject.SetActive(true); },
                actionOnRelease: buttonView => { buttonView.gameObject.SetActive(false); }
            );

            linksPool = new ObjectPool<Link_PassportFieldView>(
                InstantiateLinkPrefab,
                defaultCapacity: LINK_POOL_DEFAULT_CAPACITY,
                actionOnGet: buttonView => { buttonView.gameObject.SetActive(true); },
                actionOnRelease: buttonView =>
                {
                    buttonView.LinkButton.onClick.RemoveAllListeners();
                    buttonView.gameObject.SetActive(false);
                }
            );

            view.NoLinksLabel.text = NO_LINKS_TEXT;
        }

        public void Setup(Profile profile)
        {
            currentProfile = profile;

            LoadAdditionalFields();
            LoadLinks();

            view.Description.text = !string.IsNullOrEmpty(profile.Description) || instantiatedAdditionalFields.Count > 0 ? profile.Description : NO_INTRO_TEXT;

            LayoutRebuilder.ForceRebuildLayoutImmediate(view.MainContainer);
        }

        public void Clear()
        {
            foreach (AdditionalField_PassportFieldView additionalField in instantiatedAdditionalFields)
                additionalFieldsPool.Release(additionalField);

            instantiatedAdditionalFields.Clear();

            foreach (Link_PassportFieldView link in instantiatedLinks)
                linksPool.Release(link);

            instantiatedLinks.Clear();
        }

        public void Dispose() =>
            Clear();

        private AdditionalField_PassportFieldView InstantiateAdditionalFieldPrefab()
        {
            AdditionalField_PassportFieldView additionalFieldView = UnityEngine.Object.Instantiate(view.AdditionalFieldsConfiguration.additionalInfoFieldPrefab, view.AdditionalInfoContainer);
            return additionalFieldView;
        }

        private Link_PassportFieldView InstantiateLinkPrefab()
        {
            Link_PassportFieldView linkView = UnityEngine.Object.Instantiate(view.LinkPrefab, view.LinksContainer);
            return linkView;
        }

        private void LoadAdditionalFields()
        {
            if (!string.IsNullOrEmpty(currentProfile.Gender))
                AddAdditionalField(AdditionalFieldType.GENDER, currentProfile.Gender);

            if (!string.IsNullOrEmpty(currentProfile.Country))
                AddAdditionalField(AdditionalFieldType.COUNTRY, currentProfile.Country);

            if (currentProfile.Birthdate != null && currentProfile.Birthdate.Value != new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc))
                AddAdditionalField(AdditionalFieldType.BIRTH_DATE, currentProfile.Birthdate.Value.ToString("dd/MM/yyyy"));

            if (!string.IsNullOrEmpty(currentProfile.Pronouns))
                AddAdditionalField(AdditionalFieldType.PRONOUNS, currentProfile.Pronouns);

            if (!string.IsNullOrEmpty(currentProfile.RelationshipStatus))
                AddAdditionalField(AdditionalFieldType.RELATIONSHIP_STATUS, currentProfile.RelationshipStatus);

            if (!string.IsNullOrEmpty(currentProfile.SexualOrientation))
                AddAdditionalField(AdditionalFieldType.SEXUAL_ORIENTATION, currentProfile.SexualOrientation);

            if (!string.IsNullOrEmpty(currentProfile.Language))
                AddAdditionalField(AdditionalFieldType.LANGUAGE, currentProfile.Language);

            if (!string.IsNullOrEmpty(currentProfile.Profession))
                AddAdditionalField(AdditionalFieldType.PROFESSION, currentProfile.Profession);

            if (!string.IsNullOrEmpty(currentProfile.EmploymentStatus))
                AddAdditionalField(AdditionalFieldType.EMPLOYMENT_STATUS, currentProfile.EmploymentStatus);

            if (!string.IsNullOrEmpty(currentProfile.Hobbies))
                AddAdditionalField(AdditionalFieldType.HOBBIES, currentProfile.Hobbies);

            if (!string.IsNullOrEmpty(currentProfile.RealName))
                AddAdditionalField(AdditionalFieldType.REAL_NAME, currentProfile.RealName);

            view.AdditionalInfoContainer.gameObject.SetActive(instantiatedAdditionalFields.Count > 0);
        }

        private void AddAdditionalField(AdditionalFieldType type, string value)
        {
            var newAdditionalField = additionalFieldsPool.Get();
            newAdditionalField.transform.SetAsLastSibling();
            newAdditionalField.Value.text = value;
            newAdditionalField.Title.text = type.ToString();
            newAdditionalField.Logo.sprite = null;

            foreach (AdditionalFieldConfiguration additionalFieldConfig in view.AdditionalFieldsConfiguration.additionalFields)
            {
                if (additionalFieldConfig.type != type)
                    continue;

                newAdditionalField.Title.text = additionalFieldConfig.title;
                newAdditionalField.Logo.sprite = additionalFieldConfig.logo;
            }

            instantiatedAdditionalFields.Add(newAdditionalField);
        }

        private void LoadLinks()
        {
            view.NoLinksLabel.gameObject.SetActive(currentProfile.Links == null || currentProfile.Links.Count == 0);
            view.LinksContainer.gameObject.SetActive(currentProfile.Links is { Count: > 0 });

            if (currentProfile.Links == null)
                return;

            foreach (var link in currentProfile.Links)
                AddLink(link.title, link.url);
        }

        private void AddLink(string title, string url)
        {
            var newLink = linksPool.Get();
            newLink.transform.SetAsLastSibling();
            newLink.Title.text = title;
            newLink.Link = url;
            newLink.LinkButton.onClick.AddListener(() => OpenUrlAsync(url).Forget());
            instantiatedLinks.Add(newLink);
            LayoutRebuilder.ForceRebuildLayoutImmediate(newLink.Container);
        }

        private async UniTask OpenUrlAsync(string url)
        {
            await UniTask.SwitchToMainThread();
            await mvcManager.ShowAsync(ExternalUrlPromptController.IssueCommand(new ExternalUrlPromptController.Params(url)));
        }
    }
}
