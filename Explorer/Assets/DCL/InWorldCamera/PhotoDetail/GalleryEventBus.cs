using System;

namespace DCL.InWorldCamera
{
	public class GalleryEventBus
	{
		public event Action<string, bool> ReelPublicStateChangeEvent;

		public void ReelPublicStateChanged(string reelId, bool isPublic)
		{
			ReelPublicStateChangeEvent?.Invoke(reelId, isPublic);
		}
	}
}