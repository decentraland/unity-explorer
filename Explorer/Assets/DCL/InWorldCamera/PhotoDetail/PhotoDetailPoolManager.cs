using DCL.Profiles;
using DCL.WebRequests;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.InWorldCamera.PhotoDetail
{
    public class PhotoDetailPoolManager
    {
        private readonly IObjectPool<VisiblePersonController> visiblePersonPool;

        public PhotoDetailPoolManager(
            VisiblePersonView visiblePersonPrefab,
            GameObject unusedVisiblePersonPoolObjectParent,
            IWebRequestController webRequestController,
            IProfileRepository profileRepository,
            int visiblePersonDefaultCapacity,
            int visiblePersonMaxSize)
        {
            visiblePersonPool = new ObjectPool<VisiblePersonController>(
                () =>
                {
                    VisiblePersonView view = GameObject.Instantiate(visiblePersonPrefab);
                    return new VisiblePersonController(view, webRequestController, profileRepository);
                },
                visiblePerson => visiblePerson.view.gameObject.SetActive(true),
                visiblePerson =>
                {
                    visiblePerson.view.transform.SetParent(unusedVisiblePersonPoolObjectParent.transform, false);
                    visiblePerson.view.gameObject.SetActive(false);
                },
                visiblePerson =>
                {
                    GameObject.Destroy(visiblePerson.view.gameObject);
                },
                true,
                visiblePersonDefaultCapacity,
                visiblePersonMaxSize);
        }

        public VisiblePersonController GetVisiblePerson(RectTransform parent)
        {
            VisiblePersonController controller = visiblePersonPool.Get();
            controller.view.transform.SetParent(parent, false);
            return controller;
        }

        public void ReleaseVisiblePerson(VisiblePersonController controller) =>
            visiblePersonPool.Release(controller);
    }
}
