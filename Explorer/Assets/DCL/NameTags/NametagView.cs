using Sentry;
using System;
using TMPro;
using UnityEngine;

namespace DCL.Nametags
{
    public class NametagView : MonoBehaviour
    {
        [field: SerializeField]
        public TMP_Text Username { get; private set; }

        [field: SerializeField]
        public SpriteRenderer Background { get; private set; }

        [field: SerializeField]
        public GameObject VerifiedIcon { get; private set; }

        private Vector3 backgroundLocalScale;

        public void Start()
        {
            backgroundLocalScale = Background.transform.localScale;
        }

        public void SetUsername(string username)
        {
            Username.text = username;
            Username.rectTransform.sizeDelta = new Vector2(Username.preferredWidth, Username.preferredHeight);
            Background.transform.localScale = new Vector3(Username.preferredWidth + 0.2f, 0.2f, backgroundLocalScale.z);
        }

    }
}
