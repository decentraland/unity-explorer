using DCL.Diagnostics;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Diagnostics.ReportsHandling.Tests
{
    public class ReportsHandlingSettingsWithOverrideShould
    {
        private IReportsHandlingSettings baseSettings;
        private ICategorySeverityMatrix baseDebugLogMatrix;
        private ICategorySeverityMatrix baseSentryMatrix;

        [SetUp]
        public void SetUp()
        {
            baseDebugLogMatrix = Substitute.For<ICategorySeverityMatrix>();
            baseSentryMatrix = Substitute.For<ICategorySeverityMatrix>();

            // nothing is enabled by the base settings, so every enabled entry comes from the override
            baseDebugLogMatrix.IsEnabled(Arg.Any<string>(), Arg.Any<LogType>()).Returns(false);
            baseSentryMatrix.IsEnabled(Arg.Any<string>(), Arg.Any<LogType>()).Returns(false);

            baseSettings = Substitute.For<IReportsHandlingSettings>();
            baseSettings.GetMatrix(ReportHandler.DebugLog).Returns(baseDebugLogMatrix);
            baseSettings.GetMatrix(ReportHandler.Sentry).Returns(baseSentryMatrix);
        }

        [Test]
        public void EnableEveryCategoryInDebugLogOnAllOverride([Values(LogType.Log, LogType.Warning, LogType.Error, LogType.Exception, LogType.Assert)] LogType logType)
        {
            var settings = new ReportsHandlingSettingsWithOverride(baseSettings, new CategorySeverityMatrixDto { allOverride = true });

            ICategorySeverityMatrix matrix = settings.GetMatrix(ReportHandler.DebugLog);

            Assert.That(matrix.IsEnabled(ReportCategory.ENGINE, logType), Is.True);
            Assert.That(matrix.IsEnabled("NOT_A_DECLARED_CATEGORY", logType), Is.True);
        }

        [Test]
        public void KeepSentryMatrixOnAllOverride()
        {
            var settings = new ReportsHandlingSettingsWithOverride(baseSettings, new CategorySeverityMatrixDto { allOverride = true });

            Assert.That(settings.GetMatrix(ReportHandler.Sentry), Is.SameAs(baseSentryMatrix));
        }

        [Test]
        public void PrioritizeAllOverrideOverListedEntries()
        {
            var dto = new CategorySeverityMatrixDto
            {
                allOverride = true,
                isOverride = true,
                debugLogMatrix = new List<CategorySeverityMatrixDto.MatrixEntryDto>
                {
                    new (ReportCategory.ENGINE, LogType.Error),
                },
            };

            var settings = new ReportsHandlingSettingsWithOverride(baseSettings, dto);

            // the listed entry would be the only enabled one without allOverride
            Assert.That(settings.GetMatrix(ReportHandler.DebugLog).IsEnabled(ReportCategory.AVATAR, LogType.Log), Is.True);
        }

        [Test]
        public void FallBackToListedEntriesWhenAllOverrideIsDisabled()
        {
            var dto = new CategorySeverityMatrixDto
            {
                isOverride = true,
                debugLogMatrix = new List<CategorySeverityMatrixDto.MatrixEntryDto>
                {
                    new (ReportCategory.ENGINE, LogType.Error),
                },
            };

            var settings = new ReportsHandlingSettingsWithOverride(baseSettings, dto);
            ICategorySeverityMatrix matrix = settings.GetMatrix(ReportHandler.DebugLog);

            Assert.That(matrix.IsEnabled(ReportCategory.ENGINE, LogType.Error), Is.True);
            Assert.That(matrix.IsEnabled(ReportCategory.AVATAR, LogType.Log), Is.False);
        }
    }
}
