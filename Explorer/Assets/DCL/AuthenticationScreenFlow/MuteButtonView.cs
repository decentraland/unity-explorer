using DCL.UI;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.AuthenticationScreenFlow
{
	public class MuteButtonView : ButtonWithAnimationView
	{
		[SerializeField] private Image icon;
		[SerializeField] private Sprite mutedSprite;
		[SerializeField] private Sprite unmutedSprite;

		public void SetIcon(bool isMuted)
		{
			icon.sprite = isMuted ? mutedSprite : unmutedSprite;
		}
	}
}
