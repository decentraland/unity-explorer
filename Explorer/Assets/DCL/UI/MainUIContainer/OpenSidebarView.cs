using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DCL.UI.MainUI
{
    public class OpenSidebarView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action EnterOpenArea;
        public event Action ExitOpenArea;

        public void OnPointerEnter(PointerEventData eventData)
        {
            EnterOpenArea?.Invoke();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            ExitOpenArea?.Invoke();
        }
    }
}
