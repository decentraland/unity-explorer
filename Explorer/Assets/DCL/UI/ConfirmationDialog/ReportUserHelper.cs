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
            SelfProfile selfProfile,
            UnityAppWebBrowser webBrowser,
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

                ProfileReadResult ownProfileResult = await selfProfile.ProfileAsync(ct);

                if (ownProfileResult.IsCancelled)
                    return;

                webBrowser.OpenUrlMainThreadOnly(string.Format(decentralandUrlsSource.Url(DecentralandUrl.ReportUserForm),
                    ownProfileResult.IsOk(out Profile? ownProfile) ? ownProfile.UserId : string.Empty,
                    reportedUserId));
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { ReportHub.LogException(e, reportCategory); }
        }
    }
}
