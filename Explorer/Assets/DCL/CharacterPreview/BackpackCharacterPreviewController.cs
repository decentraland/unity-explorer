using UnityEngine;
using UnityEngine.UI;

namespace DCL.CharacterPreview
{
    public class BackpackCharacterPreviewView : MonoBehaviour
    {
        [field: SerializeField] public RawImage RawImage { get; private set; }
    }

    public class BackpackCharacterPreviewController
    {
        private readonly BackpackCharacterPreviewView view;
        private readonly ICharacterPreviewFactory previewFactory;

        private CharacterPreviewController previewController;

        private CharacterPreviewModel previewModel;

        public BackpackCharacterPreviewController(BackpackCharacterPreviewView view, ICharacterPreviewFactory previewFactory)
        {
            this.view = view;
            this.previewFactory = previewFactory;

            // Subscribe to the event bus
        }

        public void OnShow()
        {
            previewController = previewFactory.Create((RenderTexture)view.RawImage.texture);
        }

        public void OnHide()
        {
            previewController.Dispose();
        }

        private void OnEquipped()
        {
            // Change model

            UpdateModel();
        }

        private void OnUnequipped()
        {
            // Change model

            UpdateModel();
        }

        private void UpdateModel()
        {
            previewController.UpdateAvatar(previewModel);
        }
    }
}
