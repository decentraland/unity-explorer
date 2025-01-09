using System;
using UnityEngine;

namespace DCL.Clipboard
{
    public class UnityClipboard : ISystemClipboard
    {
        private bool hasValue;

        public void Set(string text)
        {
            GUIUtility.systemCopyBuffer = text;
            hasValue = true;
        }

        public bool HasValue() => hasValue;
    }
}
