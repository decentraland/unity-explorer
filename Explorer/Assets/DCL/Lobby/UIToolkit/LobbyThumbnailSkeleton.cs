using DCL.UIToolkit.Elements;
using UnityEngine.UIElements;

namespace DCL.Lobby
{
    /// <summary>
    ///     The shimmer a card's picture wears while loading: a one-bone skeleton inside the picture's element, so it shares
    ///     its box, clipping and hover zoom.
    /// </summary>
    public static class LobbyThumbnailSkeleton
    {
        public const string NAME = "Skeleton";
        public const string USS_BLOCK = "lobby-thumbnail-skeleton";
        public const string USS_SHOWN = USS_BLOCK + "--shown";

        public static SkeletonLoadingElement AddTo(VisualElement picture)
        {
            var skeleton = new SkeletonLoadingElement { name = NAME, BoneCount = 1, pickingMode = PickingMode.Ignore };
            skeleton.AddToClassList(USS_BLOCK);
            picture.Add(skeleton);
            return skeleton;
        }

        public static void SetLoading(SkeletonLoadingElement skeleton, bool loading)
        {
            skeleton.EnableInClassList(USS_SHOWN, loading);
            skeleton.IsAnimating = loading;
        }
    }
}
