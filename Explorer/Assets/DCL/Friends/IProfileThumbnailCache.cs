using UnityEngine;

namespace DCL.Friends
{
    public interface IProfileThumbnailCache
    {
        Sprite? GetThumbnail(string userId);
        void SetThumbnail(string userId, Sprite sprite);
    }
}
