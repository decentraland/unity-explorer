using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DCL.CharacterPreview
{
    public class BackpackCharacterPreviewView : MonoBehaviour, IDisposable
    {
        [field: SerializeField]
        public RawImage RawImage { get; private set; }

        [field: SerializeField]
        public CharacterPreviewContainer CharacterPreviewContainer { get; private set;}

        [field: SerializeField]
        internal CharacterPreviewInputDetector characterPreviewInputDetector { get; private set; }

        [field: SerializeField]
        internal Transform cameraTarget { get; private set; }

        public void Initialize()
        {
            CharacterPreviewContainer.Initialize((RenderTexture)RawImage.texture);
            characterPreviewInputDetector.OnScrollEvent += OnScrollEvent;
            characterPreviewInputDetector.OnDragging += OnDragEvent;
        }

        private void OnScrollEvent(PointerEventData pointerEventData)
        {
            var transform1 = cameraTarget.transform;
            //this should be in a system probably?
            var position = transform1.position;
            position.z -= pointerEventData.scrollDelta.y * Time.deltaTime;
            if (position.z < -7) position.z = -7;
            transform1.position = position;
        }

        private void OnDragEvent(PointerEventData pointerEventData)
        {
            //this should be in a system probably?

            if (pointerEventData.button == PointerEventData.InputButton.Right)
            {
                var transform1 = cameraTarget.transform;

                var position = transform1.position;
                position.x += pointerEventData.delta.x * Time.deltaTime;
                position.y -= pointerEventData.delta.y * Time.deltaTime; //Input in Y is inverted

                //Apply boundaries for limiting panning after this step
                transform1.position = position;

            }
            else if (pointerEventData.button == PointerEventData.InputButton.Left)
            {
                var transform1 = CharacterPreviewContainer.transform;
                var rotation = transform1.rotation.eulerAngles;
                rotation.y += pointerEventData.delta.x * Time.deltaTime;
                var quaternion = new Quaternion
                    {
                        eulerAngles = rotation,
                    };

                transform1.rotation = quaternion;
            }
        }

        public void Dispose()
        {
            characterPreviewInputDetector.OnScrollEvent -= OnScrollEvent;

        }
    }
}
