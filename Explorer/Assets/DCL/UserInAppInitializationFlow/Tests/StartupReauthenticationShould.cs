using DCL.AuthenticationScreenFlow;
using DCL.Utility.Types;
using NUnit.Framework;
using System;

namespace DCL.UserInAppInitializationFlow.Tests
{
    [TestFixture]
    public class StartupReauthenticationShould
    {
        [Test]
        public void RequireReauthenticationWhenTheProfileIsNotFound()
        {
            // Arrange
            var result = EnumResult<TaskError>.ErrorResult(TaskError.MessageError, "Own profile is not deployed at the catalyst", new ProfileNotFoundException());

            // Act
            bool requires = RealUserInAppInitializationFlow.RequiresReauthentication(result);

            // Assert
            Assert.That(requires, Is.True);
        }

        [Test]
        public void KeepTheIdentityWhenTheProfileFetchTimesOut()
        {
            // Arrange
            var result = EnumResult<TaskError>.ErrorResult(TaskError.Timeout, "Own profile could not be fetched from the catalyst");

            // Act
            bool requires = RealUserInAppInitializationFlow.RequiresReauthentication(result);

            // Assert
            Assert.That(requires, Is.False);
        }

        [Test]
        public void KeepTheIdentityOnAnotherFailure()
        {
            // Arrange
            var result = EnumResult<TaskError>.ErrorResult(TaskError.UnexpectedException, "boom", new InvalidOperationException("boom"));

            // Act
            bool requires = RealUserInAppInitializationFlow.RequiresReauthentication(result);

            // Assert
            Assert.That(requires, Is.False);
        }

        [Test]
        public void KeepTheIdentityOnSuccess()
        {
            // Act
            bool requires = RealUserInAppInitializationFlow.RequiresReauthentication(EnumResult<TaskError>.SuccessResult());

            // Assert
            Assert.That(requires, Is.False);
        }
    }
}
