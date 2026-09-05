using Cysharp.Threading.Tasks;
using System.Threading;
using UnityEngine;

namespace DCL.UI.ProfileElements
{
    public static class UserInfoHelper
    {
        public static void CopyToClipboard(string text) =>
            GUIUtility.systemCopyBuffer = text;

        public static async UniTaskVoid ShowCopyWarningAsync(WarningNotificationView notificationView, CancellationToken ct)
        {
            notificationView.Show();
            await UniTask.Delay(1000, cancellationToken: ct);
            notificationView.Hide();
        }
    }
}
