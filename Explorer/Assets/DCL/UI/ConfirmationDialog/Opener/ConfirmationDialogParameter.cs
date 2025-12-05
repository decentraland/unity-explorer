#nullable enable
using System;
using UnityEngine;

namespace DCL.UI.ConfirmationDialog.Opener
{
    public struct ConfirmationDialogParameter
    {
        public struct UserData
        {
            public readonly string Address;
            public readonly string ThumbnailUrl;
            public readonly Color Color;

            public UserData(string address, string thumbnailUrl, Color color)
            {
                Address = address;
                ThumbnailUrl = thumbnailUrl;
                Color = color;
            }
        }

        public readonly string Text;
        public readonly string SubText;
        public readonly string CancelButtonText;
        public readonly string ConfirmButtonText;
        public readonly Sprite? Image;
        public readonly bool ShowImageRim;
        public readonly bool ShowQuitImage;
        public readonly UserData UserInfo;
        public Action<ConfirmationResult>? ResultCallback;
        public readonly string LinkText;
        public readonly Action<string>? OnLinkClickCallback;

        public ConfirmationDialogParameter(string text, string cancelButtonText, string confirmButtonText,
            Sprite? image, bool showImageRim, bool showQuitImage,
            Action<ConfirmationResult>? resultCallback = null,
            string subText = "", UserData userInfo = default, string linkText = "", Action<string>? onLinkClickCallback = null)
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
            LinkText = linkText;
            OnLinkClickCallback = onLinkClickCallback;
        }
    }

    public enum ConfirmationResult
    {
        CONFIRM,
        CANCEL,
    }
}
