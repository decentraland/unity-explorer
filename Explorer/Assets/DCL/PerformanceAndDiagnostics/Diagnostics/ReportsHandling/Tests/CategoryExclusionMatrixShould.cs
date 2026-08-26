using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Diagnostics.Tests
{
    public class CategoryExclusionMatrixShould
    {
        private ICategorySeverityMatrix baseMatrix;
        private CategoryExclusionMatrix matrix;

        [SetUp]
        public void SetUp()
        {
            baseMatrix = Substitute.For<ICategorySeverityMatrix>();
            matrix = new CategoryExclusionMatrix(baseMatrix, ReportCategory.JAVASCRIPT);
        }

        [Test]
        public void ReturnFalseForExcludedCategoryRegardlessOfBaseMatrix([Values(true, false)] bool baseResult, [Values(LogType.Log, LogType.Exception)] LogType severity)
        {
            baseMatrix.IsEnabled(ReportCategory.JAVASCRIPT, Arg.Any<LogType>()).Returns(baseResult);

            Assert.That(matrix.IsEnabled(ReportCategory.JAVASCRIPT, severity), Is.False);
        }

        [Test]
        public void DelegateToBaseMatrixForNonExcludedCategory_WhenEnabled()
        {
            baseMatrix.IsEnabled(ReportCategory.ENGINE, LogType.Exception).Returns(true);

            Assert.That(matrix.IsEnabled(ReportCategory.ENGINE, LogType.Exception), Is.True);
        }

        [Test]
        public void DelegateToBaseMatrixForNonExcludedCategory_WhenDisabled()
        {
            baseMatrix.IsEnabled(ReportCategory.ENGINE, LogType.Exception).Returns(false);

            Assert.That(matrix.IsEnabled(ReportCategory.ENGINE, LogType.Exception), Is.False);
        }
    }
}
