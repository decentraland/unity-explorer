using System;

namespace DCL.AvatarRendering.Emotes
{
	public class EmotesBus
	{
		public event Action QuickActionEmotePlayed;

		public void OnQuickActionEmotePlayed()
		{
			QuickActionEmotePlayed?.Invoke();
		}
	}
}