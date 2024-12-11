using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Backpack;
using DCL.Browser;
using DCL.Chat;
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
            RectTransform visiblePersonParent,
            GameObject unusedEquippedWearablePoolObjectParent,
            IWebRequestController webRequestController,
            IProfileRepository profileRepository,
            IMVCManager mvcManager,
            IWebBrowser webBrowser,
            IWearableStorage wearableStorage,
            IWearablesProvider wearablesProvider,
            IDecentralandUrlsSource decentralandUrlsSource,
            IThumbnailProvider thumbnailProvider,
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
                createFunc: () => CreateVisiblePerson(visiblePersonPrefab, visiblePersonParent, webRequestController, profileRepository, mvcManager, wearableStorage, wearablesProvider, chatEntryConfiguration),
                actionOnGet: visiblePerson => visiblePerson.view.gameObject.SetActive(true),
                actionOnRelease: visiblePerson => VisiblePersonRelease(visiblePerson, emptyProfileImage),
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

            //Prewarm pools
            VisiblePersonController[] visiblePersonControllers = new VisiblePersonController[visiblePersonDefaultCapacity];
            for (int i = 0; i < visiblePersonDefaultCapacity; i++)
                visiblePersonControllers[i] = visiblePersonPool.Get();
            for (int i = 0; i < visiblePersonDefaultCapacity; i++)
                visiblePersonPool.Release(visiblePersonControllers[i]);

            EquippedWearableController[] equippedWearableControllers = new EquippedWearableController[equippedWearableDefaultCapacity];
            for (int i = 0; i < equippedWearableDefaultCapacity; i++)
                equippedWearableControllers[i] = equippedWearablePool.Get();
            for (int i = 0; i < equippedWearableDefaultCapacity; i++)
                equippedWearablePool.Release(equippedWearableControllers[i]);
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

        private void VisiblePersonRelease(VisiblePersonController visiblePerson, Sprite emptyProfileImage)
        {
            visiblePerson.view.profileImage.SetImage(emptyProfileImage);
            visiblePerson.view.gameObject.SetActive(false);
        }

        private VisiblePersonController CreateVisiblePerson(
            VisiblePersonView visiblePersonPrefab,
            RectTransform visiblePersonParent,
            IWebRequestController webRequestController,
            IProfileRepository profileRepository,
            IMVCManager mvcManager,
            IWearableStorage wearableStorage,
            IWearablesProvider wearablesProvider,
            ChatEntryConfigurationSO chatEntryConfiguration)
        {
            VisiblePersonView view = GameObject.Instantiate(visiblePersonPrefab, visiblePersonParent);
            return new VisiblePersonController(view, webRequestController, profileRepository, mvcManager, wearableStorage, wearablesProvider, this, chatEntryConfiguration);
        }

        public VisiblePersonController GetVisiblePerson() =>
            visiblePersonPool.Get();

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
