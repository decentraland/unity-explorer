using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Browser;
using DCL.Chat;
using DCL.InWorldCamera.PassportBridge;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles;
using DCL.WebRequests;
using MVC;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class PhotoDetailPoolManager
    {
        private readonly IObjectPool<VisiblePersonController> visiblePersonPool;
        private readonly IObjectPool<EquippedWearableController> equippedWearablePool;

        public PhotoDetailPoolManager(
            VisiblePersonView visiblePersonPrefab,
            EquippedWearableView equippedWearablePrefab,
            Sprite emptyProfileImage,
            GameObject unusedVisiblePersonPoolObjectParent,
            GameObject unusedEquippedWearablePoolObjectParent,
            IWebRequestController webRequestController,
            IProfileRepository profileRepository,
            IMVCManager mvcManager,
            IWebBrowser webBrowser,
            IWearableStorage wearableStorage,
            IWearablesProvider wearablesProvider,
            IDecentralandUrlsSource decentralandUrlsSource,
            IThumbnailProvider thumbnailProvider,
            IPassportBridge passportBridge,
            NftTypeIconSO rarityBackgrounds,
            NFTColorsSO rarityColors,
            NftTypeIconSO categoryIcons,
            ChatEntryConfigurationSO chatEntryConfiguration,
            int visiblePersonDefaultCapacity,
            int visiblePersonMaxSize,
            int equippedWearableDefaultCapacity,
            int equippedWearableMaxSize)
        {
            visiblePersonPool = new ObjectPool<VisiblePersonController>(
                createFunc: () => CreateVisiblePerson(visiblePersonPrefab, webRequestController, profileRepository, mvcManager, wearableStorage, wearablesProvider, chatEntryConfiguration, passportBridge),
                actionOnGet: visiblePerson => visiblePerson.view.gameObject.SetActive(true),
                actionOnRelease: visiblePerson => VisiblePersonRelease(visiblePerson, unusedVisiblePersonPoolObjectParent, emptyProfileImage),
                actionOnDestroy: visiblePerson => GameObject.Destroy(visiblePerson.view.gameObject),
                collectionCheck: true,
                visiblePersonDefaultCapacity,
                visiblePersonMaxSize);

            equippedWearablePool = new ObjectPool<EquippedWearableController>(
                createFunc: () => CreateEquippedWearable(equippedWearablePrefab, webBrowser, decentralandUrlsSource, thumbnailProvider, rarityBackgrounds, rarityColors, categoryIcons),
                actionOnGet: equippedWearable => equippedWearable.view.gameObject.SetActive(false),
                actionOnRelease: equippedWearable => EquippedWearableRelease(equippedWearable, unusedEquippedWearablePoolObjectParent),
                actionOnDestroy: equippedWearable => GameObject.Destroy(equippedWearable.view.gameObject),
                collectionCheck: true,
                equippedWearableDefaultCapacity,
                equippedWearableMaxSize);
        }

        private void EquippedWearableRelease(EquippedWearableController equippedWearable, GameObject unusedEquippedWearablePoolObjectParent)
        {
            equippedWearable.view.transform.SetParent(unusedEquippedWearablePoolObjectParent.transform, false);
            equippedWearable.view.gameObject.SetActive(false);
        }

        private EquippedWearableController CreateEquippedWearable(
            EquippedWearableView equippedWearablePrefab,
            IWebBrowser webBrowser,
            IDecentralandUrlsSource decentralandUrlsSource,
            IThumbnailProvider thumbnailProvider,
            NftTypeIconSO rarityBackgrounds,
            NFTColorsSO rarityColors,
            NftTypeIconSO categoryIcons)
        {
            EquippedWearableView view = GameObject.Instantiate(equippedWearablePrefab);
            return new EquippedWearableController(view, webBrowser, decentralandUrlsSource, thumbnailProvider, rarityBackgrounds, rarityColors, categoryIcons);
        }

        private void VisiblePersonRelease(VisiblePersonController visiblePerson, GameObject unusedVisiblePersonPoolObjectParent, Sprite emptyProfileImage)
        {
            visiblePerson.view.transform.SetParent(unusedVisiblePersonPoolObjectParent.transform, false);
            visiblePerson.view.profileImage.SetImage(emptyProfileImage);
            visiblePerson.view.gameObject.SetActive(false);
        }

        private VisiblePersonController CreateVisiblePerson(
            VisiblePersonView visiblePersonPrefab,
            IWebRequestController webRequestController,
            IProfileRepository profileRepository,
            IMVCManager mvcManager,
            IWearableStorage wearableStorage,
            IWearablesProvider wearablesProvider,
            ChatEntryConfigurationSO chatEntryConfiguration,
            IPassportBridge passportBridge)
        {
            VisiblePersonView view = GameObject.Instantiate(visiblePersonPrefab);
            return new VisiblePersonController(view, webRequestController, profileRepository, mvcManager, wearableStorage, wearablesProvider, passportBridge, this, chatEntryConfiguration);
        }

        public VisiblePersonController GetVisiblePerson(RectTransform parent)
        {
            VisiblePersonController controller = visiblePersonPool.Get();
            controller.view.transform.SetParent(parent, false);
            return controller;
        }

        public void ReleaseVisiblePerson(VisiblePersonController controller) =>
            visiblePersonPool.Release(controller);

        public EquippedWearableController GetEquippedWearable(RectTransform parent)
        {
            EquippedWearableController controller = equippedWearablePool.Get();
            controller.view.transform.SetParent(parent, false);
            return controller;
        }

        public void ReleaseEquippedWearable(EquippedWearableController controller) =>
            equippedWearablePool.Release(controller);
    }
}
