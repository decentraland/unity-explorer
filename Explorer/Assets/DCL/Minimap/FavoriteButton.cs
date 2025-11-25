using System;
using DCL.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.Minimap
{
	public class FavoriteButton : MonoBehaviour
	{
		[SerializeField] internal Button button;
		[SerializeField] private Image image;
		[SerializeField] private Sprite interactiveSprite;
		[SerializeField] private Sprite nonInteractiveSprite;
		[SerializeField] private RectTransform imageFill;
		[SerializeField] private Color interactiveColor;
		[SerializeField] private Color nonInteractiveColor;

		[field: Header("Audio")]
		[SerializeField] private AudioClipConfig buttonPressedAudio;

		internal bool IsOn { get; private set; }
		
		public event Action<bool> OnButtonClicked; 

		public void SetButtonState(bool isOn, bool isInteractable = true)
		{
			IsOn = isOn;
			imageFill.gameObject.SetActive(isOn);
			image.sprite = isInteractable ? interactiveSprite : nonInteractiveSprite;
			image.color = isInteractable ? interactiveColor : nonInteractiveColor;
			button.interactable = isInteractable;
		}
		
		private void OnEnable() => button.onClick.AddListener(OnClick);

		private void OnDisable() => button.onClick.RemoveListener(OnClick);

		private void OnClick()
		{
			IsOn = !IsOn;
			UIAudioEventsBus.Instance.SendPlayAudioEvent(buttonPressedAudio);
			OnButtonClicked?.Invoke(IsOn);
		}
	}
}
