using System;

namespace DCL.InWorldCamera
{
	public class GalleryEventBus
	{
		public event Action<string, bool> ReelPublicStateChangeEvent;
		public event Action<string> ReelDeletedEvent;

		public void ReelPublicStateChanged(string reelId, bool isPublic)
		{
			ReelPublicStateChangeEvent?.Invoke(reelId, isPublic);
		}

		public void ReelDeleted(string reelId)
		{
			ReelDeletedEvent?.Invoke(reelId);
		}
	}
}