using System;
using UnityEngine;
using UnityEngine.UI;

namespace DCL.CharacterPreview
{
    public class CharacterPreviewCursorContainer : MonoBehaviour
    {
        [field: SerializeField] public Image CursorOverrideImage { get; private set; }
    }
}
