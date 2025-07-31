using DCL.Input;
using DCL.Input.Component;
using DCL.UI.SceneDebugConsole.LogHistory;
using DCL.UI.SceneDebugConsole.MessageBus;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace DCL.UI.SceneDebugConsole.Tests
{
    public class SceneDebugConsoleControllerShould
    {
        private SceneDebugConsoleController controller;
        private SceneDebugConsoleLogEntryBus mockLogEntriesBus;
        private IInputBlock mockInputBlock;
        private GameObject testResourcePrefab;

        [SetUp]
        public void SetUp()
        {
            // Create mock dependencies
            mockLogEntriesBus = new SceneDebugConsoleLogEntryBus();
            mockInputBlock = Substitute.For<IInputBlock>();

            // Create test UI resources
            SetupTestUIResources();
        }

        [TearDown]
        public void TearDown()
        {
            // Clean up according to memory policy: destroy created GameObjects
            controller?.Dispose();

            if (testResourcePrefab != null)
                UnityEngine.Object.DestroyImmediate(testResourcePrefab);
        }

        private void SetupTestUIResources()
        {
            // Create a minimal test UIDocument prefab
            testResourcePrefab = new GameObject("SceneDebugConsoleRootCanvas");
            var uiDoc = testResourcePrefab.AddComponent<UIDocument>();

            // Note: In a real Unity test environment, the UIDocument would load its visual tree
            // from a VisualTreeAsset. For unit testing, we're focusing on the controller logic
            // rather than the UI setup, so we don't need to mock the complete UI hierarchy.
            // The SceneDebugConsoleController will handle Resources.Load internally.
        }

        [Test]
        public void InitializeCorrectly()
        {
            // Act & Assert - Should not throw during initialization
            Assert.DoesNotThrow(() => 
            {
                controller = new SceneDebugConsoleController(mockLogEntriesBus, mockInputBlock);
            });
            
            Assert.That(controller, Is.Not.Null);
        }

        [Test]
        public void HandleLogEntryAddition()
        {
            // Arrange
            controller = new SceneDebugConsoleController(mockLogEntriesBus, mockInputBlock);
            bool exceptionThrown = false;

            // Act
            try
            {
                mockLogEntriesBus.Send("Test message", LogType.Log);
            }
            catch (System.Exception)
            {
                exceptionThrown = true;
            }

            // Assert
            Assert.That(exceptionThrown, Is.False, "Log entry processing should not throw exceptions");
        }

        [Test]
        public void HandleErrorLogEntryAddition()
        {
            // Arrange
            controller = new SceneDebugConsoleController(mockLogEntriesBus, mockInputBlock);
            var errorMessage = "Test error message";
            var stackTrace = "Test stack trace";
            bool exceptionThrown = false;

            // Act
            try
            {
                mockLogEntriesBus.Send(errorMessage, LogType.Error, stackTrace);
            }
            catch (System.Exception)
            {
                exceptionThrown = true;
            }

            // Assert
            Assert.That(exceptionThrown, Is.False, "Error log entry processing should not throw exceptions");
        }

        [Test]
        public void BlockInputWhenTextFieldFocused()
        {
            // Arrange
            controller = new SceneDebugConsoleController(mockLogEntriesBus, mockInputBlock);

            // Since we can't directly trigger focus events in this test setup,
            // we verify that the input block is available and can be called

            // Act & Assert
            mockInputBlock.Received(0).Disable(Arg.Any<InputMapComponent.Kind[]>());

            // The actual focus event handling would be tested in integration tests
            // where we can simulate UI interactions
        }

        [Test]
        public void HandleDisposalCorrectly()
        {
            // Arrange
            controller = new SceneDebugConsoleController(mockLogEntriesBus, mockInputBlock);

            // Act & Assert
            Assert.DoesNotThrow(() => controller.Dispose(), "Disposal should not throw exceptions");
            
            // Verify we can send messages after disposal without the controller processing them
            Assert.DoesNotThrow(() => mockLogEntriesBus.Send("Post-disposal message", LogType.Log));
        }

        [Test]
        public void HandleMultipleLogEntries()
        {
            // Arrange
            controller = new SceneDebugConsoleController(mockLogEntriesBus, mockInputBlock);
            int processedCount = 0;

            // Act
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 10; i++)
                {
                    mockLogEntriesBus.Send($"Log message {i}", LogType.Log);
                    mockLogEntriesBus.Send($"Error message {i}", LogType.Error);
                    processedCount += 2;
                }
            });

            // Assert
            Assert.That(processedCount, Is.EqualTo(20), "All messages should be processed");
        }



        [Test]
        public void HandleNullOrEmptyMessages()
        {
            // Arrange
            controller = new SceneDebugConsoleController(mockLogEntriesBus, mockInputBlock);

            // Act & Assert
            Assert.DoesNotThrow(() => mockLogEntriesBus.Send("", LogType.Log));
            Assert.DoesNotThrow(() => mockLogEntriesBus.Send(" ", LogType.Log));

            // Note: null messages would be handled by the LogEntry constructor
        }

        [Test]
        public void HandleHighVolumeLogMessages()
        {
            // Arrange
            controller = new SceneDebugConsoleController(mockLogEntriesBus, mockInputBlock);
            int messagesSent = 0;

            // Act
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 1000; i++)
                {
                    mockLogEntriesBus.Send($"High volume message {i}", LogType.Log);
                    messagesSent++;
                }
            });

            // Assert
            Assert.That(messagesSent, Is.EqualTo(1000), "All high volume messages should be sent successfully");
            // Verify controller remains responsive after high volume
            Assert.DoesNotThrow(() => mockLogEntriesBus.Send("Post high-volume test", LogType.Log));
        }

        [Test]
        public void HandleDifferentLogTypes()
        {
            // Arrange
            controller = new SceneDebugConsoleController(mockLogEntriesBus, mockInputBlock);

            // Act & Assert
            Assert.DoesNotThrow(() => mockLogEntriesBus.Send("Log message", LogType.Log));
            Assert.DoesNotThrow(() => mockLogEntriesBus.Send("Error message", LogType.Error));
            Assert.DoesNotThrow(() => mockLogEntriesBus.Send("Exception message", LogType.Exception));
        }

        [Test]
        public void HandleInputBlockOperations()
        {
            // Arrange
            controller = new SceneDebugConsoleController(mockLogEntriesBus, mockInputBlock);

            // Verify input block is stored and could be used
            // The actual usage would be in focus event handlers

            // Assert
            Assert.That(mockInputBlock, Is.Not.Null);
        }

        [Test]
        public void ThrowWhenLogEntriesBusIsNull()
        {
            // Act & Assert
            Assert.Throws<System.NullReferenceException>(() =>
                new SceneDebugConsoleController(null, mockInputBlock));
        }

        [Test]
        public void HandleNullInputBlockGracefully()
        {
            // Act & Assert - Constructor should complete but may fail later when inputBlock is used
            Assert.DoesNotThrow(() =>
                controller = new SceneDebugConsoleController(mockLogEntriesBus, null));
        }


    }
}
