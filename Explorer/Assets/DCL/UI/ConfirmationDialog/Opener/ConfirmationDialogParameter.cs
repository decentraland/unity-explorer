#nullable enable
using DCL.Profiles;
using System;
using UnityEngine;

namespace DCL.UI.ConfirmationDialog.Opener
{
    public struct ConfirmationDialogParameter
    {
        public readonly string Text;
        public readonly string SubText;
        public readonly string CancelButtonText;
        public readonly string ConfirmButtonText;
        public readonly Sprite? Image;
        public readonly bool ShowImageRim;
        public readonly bool ShowQuitImage;
        public readonly Profile.CompactInfo UserInfo;
        public Action<ConfirmationResult>? ResultCallback;

        public ConfirmationDialogParameter(string text, string cancelButtonText, string confirmButtonText,
            Sprite? image, bool showImageRim, bool showQuitImage,
            Action<ConfirmationResult>? resultCallback = null,
            string subText = "", Profile.CompactInfo userInfo = default)
        {
            Text = text;
            CancelButtonText = cancelButtonText;
            ConfirmButtonText = confirmButtonText;
            Image = image;
            ShowImageRim = showImageRim;
            ShowQuitImage = showQuitImage;
            ResultCallback = resultCallback;
            SubText = subText;
            UserInfo = userInfo;
        }
    }

    public enum ConfirmationResult
    {
        CONFIRM,
        CANCEL,
    }
}
