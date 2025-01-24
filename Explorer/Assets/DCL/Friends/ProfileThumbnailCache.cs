using System.Collections.Generic;
using UnityEngine;

namespace DCL.Friends
{
    public class ProfileThumbnailCache : IProfileThumbnailCache
    {
        private readonly Dictionary<string, Sprite> thumbnails = new ();

        public Sprite? GetThumbnail(string userId) =>
            thumbnails.GetValueOrDefault(userId);

        public void SetThumbnail(string userId, Sprite sprite) =>
            thumbnails[userId] = sprite;
    }
}
