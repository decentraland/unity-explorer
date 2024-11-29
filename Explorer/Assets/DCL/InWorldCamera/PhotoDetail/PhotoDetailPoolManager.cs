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
                () =>
                {
                    VisiblePersonView view = GameObject.Instantiate(visiblePersonPrefab);
                    return new VisiblePersonController(view, webRequestController, profileRepository, mvcManager, wearableStorage, wearablesProvider, this, chatEntryConfiguration);
                },
                visiblePerson => visiblePerson.view.gameObject.SetActive(true),
                visiblePerson =>
                {
                    visiblePerson.view.transform.SetParent(unusedVisiblePersonPoolObjectParent.transform, false);
                    visiblePerson.view.profileImage.SetImage(emptyProfileImage);
                    visiblePerson.view.gameObject.SetActive(false);
                },
                visiblePerson =>
                {
                    GameObject.Destroy(visiblePerson.view.gameObject);
                },
                true,
                visiblePersonDefaultCapacity,
                visiblePersonMaxSize);

            equippedWearablePool = new ObjectPool<EquippedWearableController>(
                () =>
                {
                    EquippedWearableView view = GameObject.Instantiate(equippedWearablePrefab);
                    return new EquippedWearableController(view, webBrowser, decentralandUrlsSource, thumbnailProvider, rarityBackgrounds, rarityColors, categoryIcons);
                },
                equippedWearable => equippedWearable.view.gameObject.SetActive(false),
                equippedWearable =>
                {
                    equippedWearable.view.transform.SetParent(unusedEquippedWearablePoolObjectParent.transform, false);
                    equippedWearable.view.gameObject.SetActive(false);
                },
                equippedWearable =>
                {
                    GameObject.Destroy(equippedWearable.view.gameObject);
                },
                true,
                equippedWearableDefaultCapacity,
                equippedWearableMaxSize);
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
