using DCL.Diagnostics.Sentry;
using NUnit.Framework;
using Sentry;
using UnityEngine;

namespace DCL.Diagnostics.Tests
{
    public class SentryReportHandlerShould
    {
        private static readonly SceneShortInfo SCENE_INFO = new (Vector2Int.zero, "kingdom-of-antrom");

        private static Scope NewScope() =>
            new (new SentryOptions());

        [Test]
        public void SetFingerprintForJavaScriptExceptionWithSceneAndMultilineMessage()
        {
            Scope scope = NewScope();
            var reportData = new ReportData(ReportCategory.JAVASCRIPT, sceneShortInfo: SCENE_INFO);
            const string MESSAGE = "ReferenceError: applyPartMaterial is not defined\n    at childPart (Script [19]:65573:5)";

            SentryReportHandler.AddSceneJsFingerprint(scope, reportData, MESSAGE);

            CollectionAssert.AreEqual(new[] { "scene-js", "kingdom-of-antrom", "ReferenceError: applyPartMaterial is not defined" }, scope.Fingerprint);
        }

        [TestCase(null)]
        [TestCase("")]
        public void NotSetFingerprintWhenExceptionMessageIsNullOrEmpty(string message)
        {
            Scope scope = NewScope();
            var reportData = new ReportData(ReportCategory.JAVASCRIPT, sceneShortInfo: SCENE_INFO);

            SentryReportHandler.AddSceneJsFingerprint(scope, reportData, message);

            CollectionAssert.IsEmpty(scope.Fingerprint);
        }

        [Test]
        public void NotSetFingerprintForNonJavaScriptCategory()
        {
            Scope scope = NewScope();
            var reportData = new ReportData(ReportCategory.ENGINE, sceneShortInfo: SCENE_INFO);

            SentryReportHandler.AddSceneJsFingerprint(scope, reportData, "Error: boom");

            CollectionAssert.IsEmpty(scope.Fingerprint);
        }

        [Test]
        public void SetFingerprintWithNullSceneNameWhenSceneShortInfoIsMissing()
        {
            // No sceneShortInfo supplied -> default(SceneShortInfo), Name == null.
            // The patch does not special-case a missing scene: the null flows straight
            // into the fingerprint array (documented residual risk in review.md #4).
            Scope scope = NewScope();
            var reportData = new ReportData(ReportCategory.JAVASCRIPT);

            SentryReportHandler.AddSceneJsFingerprint(scope, reportData, "Error: boom");

            CollectionAssert.AreEqual(new[] { "scene-js", null, "Error: boom" }, scope.Fingerprint);
        }
    }
}
