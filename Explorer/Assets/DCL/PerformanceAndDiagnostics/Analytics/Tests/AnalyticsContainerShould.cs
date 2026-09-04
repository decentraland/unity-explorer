using Global.AppArgs;
using NUnit.Framework;
using System.Threading;
using UnityEngine;

namespace DCL.PerformanceAndDiagnostics.Analytics.Tests
{
    public class AnalyticsContainerShould
    {
        private AnalyticsConfiguration config = null!;

        [SetUp]
        public void SetUp()
        {
            config = ScriptableObject.CreateInstance<AnalyticsConfiguration>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(config);
        }

        [Test]
        public void DiscardEventsWhenLaunchedForAutomation()
        {
            // Arrange
            IAppArgs args = new ApplicationParametersParser(false, "--alttester");

            // Act
            IAnalyticsService service = CreateServiceFor(args);

            // Assert
            Assert.AreSame(IAnalyticsService.Null, service);
        }

        [Test]
        public void UseTheConfiguredServiceWhenNotLaunchedForAutomation()
        {
            // Arrange
            IAppArgs args = new ApplicationParametersParser(false, "--skip-auth-screen", "true");

            // Act
            IAnalyticsService service = CreateServiceFor(args);

            // Assert
            Assert.AreNotSame(IAnalyticsService.Null, service);
        }

        private IAnalyticsService CreateServiceFor(IAppArgs args) =>
            AnalyticsContainer.CreateAnalyticsService(config, LauncherTraits.FromAppArgs(args), args, false, CancellationToken.None);
    }
}
