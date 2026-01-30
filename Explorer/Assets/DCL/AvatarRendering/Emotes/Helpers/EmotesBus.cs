using System;

namespace DCL.AvatarRendering.Emotes
{
	public class EmotesBus
	{
		public event Action QuickActionEmotePlayed;

		/// <summary>
		/// Fired when an emote was triggered from a shortcut (B+number).
		/// </summary>
		public event Action EmotePlayedFromShortcut;

		public void OnQuickActionEmotePlayed()
		{
			QuickActionEmotePlayed?.Invoke();
		}

		public void OnEmotePlayedFromShortcut()
		{
			EmotePlayedFromShortcut?.Invoke();
		}
	}
}
