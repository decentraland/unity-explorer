using DCL.Diagnostics.Sentry;
using NUnit.Framework;
using Sentry;
using System;
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
            var exception = new Exception("ReferenceError: applyPartMaterial is not defined\n    at childPart (Script [19]:65573:5)");

            SentryReportHandler.AddSceneJsFingerprint(scope, reportData, exception);

            CollectionAssert.AreEqual(new[] { "scene-js", "kingdom-of-antrom", "ReferenceError: applyPartMaterial is not defined" }, scope.Fingerprint);
        }

        [Test]
        public void TrimCarriageReturnFromFirstLineOfWindowsLineEndingMessage()
        {
            Scope scope = NewScope();
            var reportData = new ReportData(ReportCategory.JAVASCRIPT, sceneShortInfo: SCENE_INFO);
            var exception = new Exception("TypeError: Vector33.Create is not a function\r\n    at updateWarpBall (Script [19]:40371:51)");

            SentryReportHandler.AddSceneJsFingerprint(scope, reportData, exception);

            CollectionAssert.AreEqual(new[] { "scene-js", "kingdom-of-antrom", "TypeError: Vector33.Create is not a function" }, scope.Fingerprint);
        }

        [Test]
        public void NotSetFingerprintWhenExceptionIsNull()
        {
            Scope scope = NewScope();
            var reportData = new ReportData(ReportCategory.JAVASCRIPT, sceneShortInfo: SCENE_INFO);

            SentryReportHandler.AddSceneJsFingerprint(scope, reportData, null);

            CollectionAssert.IsEmpty(scope.Fingerprint);
        }

        [Test]
        public void NotSetFingerprintWhenExceptionMessageIsEmpty()
        {
            Scope scope = NewScope();
            var reportData = new ReportData(ReportCategory.JAVASCRIPT, sceneShortInfo: SCENE_INFO);

            SentryReportHandler.AddSceneJsFingerprint(scope, reportData, new Exception(string.Empty));

            CollectionAssert.IsEmpty(scope.Fingerprint);
        }

        [Test]
        public void NotSetFingerprintForNonJavaScriptCategory()
        {
            Scope scope = NewScope();
            var reportData = new ReportData(ReportCategory.ENGINE, sceneShortInfo: SCENE_INFO);

            SentryReportHandler.AddSceneJsFingerprint(scope, reportData, new Exception("Error: boom"));

            CollectionAssert.IsEmpty(scope.Fingerprint);
        }

        [Test]
        public void SetFingerprintWithFallbackSceneNameWhenSceneShortInfoIsMissing()
        {
            // No sceneShortInfo supplied -> default(SceneShortInfo), Name == null.
            Scope scope = NewScope();
            var reportData = new ReportData(ReportCategory.JAVASCRIPT);

            SentryReportHandler.AddSceneJsFingerprint(scope, reportData, new Exception("Error: boom"));

            CollectionAssert.AreEqual(new[] { "scene-js", "unknown-scene", "Error: boom" }, scope.Fingerprint);
        }
    }
}
