using Cysharp.Threading.Tasks;
using DCL.Browser;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.UI.ConfirmationDialog.Opener;
using System;
using System.Threading;
using UnityEngine;

namespace DCL.UI.ConfirmationDialog
{
    public static class ReportUserHelper
    {
        public static async UniTask ShowConfirmAndReportAsync(
            IConfirmationDialogOpener confirmationDialogOpener,
            Sprite? reportSprite,
            string reportCategory,
            string reportedUserId,
            ISelfProfile selfProfile,
            IWebBrowser webBrowser,
            IDecentralandUrlsSource decentralandUrlsSource,
            CancellationToken ct)
        {
            try
            {
                bool confirmed = await ReportUserConfirmationDialog.ShowAsync(
                    confirmationDialogOpener,
                    reportSprite,
                    reportCategory,
                    ct);

                if (!confirmed)
                    return;

                Profile? ownProfile = await selfProfile.ProfileAsync(ct);

                webBrowser.OpenUrl(string.Format(decentralandUrlsSource.Url(DecentralandUrl.ReportUserForm),
                    ownProfile != null ? ownProfile.UserId : string.Empty,
                    reportedUserId));
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, reportCategory); }
        }
    }
}
