using Crosstales.FB.Linux;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Crosstales.FB.Tests
{
    public class PortalFileChooserShould
    {
        private const string TITLE = "Select a screenshot";
        private const string DIRECTORY = "/home/tester/Pictures";
        private static readonly ExtensionFilter[] IMAGE_FILTERS = { new ("Images", "png", "jpg") };

        private FakeDBusDaemon daemon = null!;

        [SetUp]
        public void SetUp()
        {
            Assume.That(RuntimeInformation.IsOSPlatform(OSPlatform.Linux), "the portal transport is exercised on Linux only");
            daemon = new FakeDBusDaemon();
        }

        [TearDown]
        public void TearDown()
        {
            daemon?.Dispose();
        }

        [Test]
        public void OpenAFileThroughThePortalAndReturnTheDecodedPaths()
        {
            // Arrange
            daemon.ResponseUris = new[] { "file:///home/tester/Pictures/shot%201.png" };

            // Act
            PortalResult result = PortalFileChooser.Run(OpenRequest(false), daemon.Address);

            // Assert
            Assert.IsNull(daemon.Failure);
            Assert.IsTrue(result.Answered);
            Assert.IsNull(result.Reason);
            CollectionAssert.AreEqual(new[] { "/home/tester/Pictures/shot 1.png" }, result.Paths);
        }

        [Test]
        public void MarshalTheOpenFileOptionsTheChooserUnderstands()
        {
            // Arrange
            daemon.ResponseUris = new[] { "file:///tmp/a.png" };

            // Act
            PortalFileChooser.Run(OpenRequest(false), daemon.Address);
            Dictionary<string, object> options = daemon.ChooserOptions!;

            // Assert
            Assert.AreEqual(PortalFileChooser.OPEN_FILE, daemon.ChooserMember);
            Assert.AreEqual(string.Empty, daemon.ChooserParentWindow);
            Assert.AreEqual(TITLE, daemon.ChooserTitle);
            StringAssert.StartsWith("dcl_fb_", (string)options["handle_token"]);
            Assert.AreEqual(true, options["modal"]);
            Assert.AreEqual(false, options["multiple"]);
            Assert.IsFalse(options.ContainsKey("directory"));
            Assert.IsFalse(options.ContainsKey("current_name"));
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(DIRECTORY + "\0"), (byte[])options["current_folder"]);

            var filters = (object[])options["filters"];
            Assert.AreEqual(2, filters.Length);
            var images = (object[])filters[0];
            Assert.AreEqual("Images", images[0]);
            var patterns = (object[])images[1];
            CollectionAssert.AreEqual(new object[] { PortalFileChooser.FILTER_GLOB, "*.[pP][nN][gG]" }, (object[])patterns[0]);
            CollectionAssert.AreEqual(new object[] { PortalFileChooser.FILTER_GLOB, "*.[jJ][pP][gG]" }, (object[])patterns[1]);
            var all = (object[])filters[1];
            Assert.AreEqual(PortalFileChooser.ALL_FILES_LABEL, all[0]);
            CollectionAssert.AreEqual(new object[] { PortalFileChooser.FILTER_GLOB, "*" }, (object[])((object[])all[1])[0]);
        }

        [Test]
        public void SubscribeToTheResponseSignalBeforeCalling()
        {
            // Arrange
            daemon.ResponseUris = new[] { "file:///tmp/a.png" };

            // Act
            PortalFileChooser.Run(OpenRequest(false), daemon.Address);
            string token = (string)daemon.ChooserOptions!["handle_token"];

            // Assert
            string expectedHandle = PortalFileChooser.PredictHandle(FakeDBusDaemon.CLIENT_NAME, token);
            CollectionAssert.Contains(daemon.MatchRules, PortalFileChooser.ResponseMatchRule(expectedHandle));
            CollectionAssert.Contains(daemon.MatchRules, PortalFileChooser.PortalOwnerMatchRule());
        }

        [Test]
        public void TreatUserCancellationAsAnAnsweredRequest()
        {
            // Arrange
            daemon.ResponseCode = 1;

            // Act
            PortalResult result = PortalFileChooser.Run(OpenRequest(false), daemon.Address);

            // Assert
            Assert.IsTrue(result.Answered);
            Assert.IsEmpty(result.Paths);
            Assert.IsNull(result.Reason);
        }

        [Test]
        public void FollowTheHandleTheServiceReturnsWhenItDiffersFromThePrediction()
        {
            // Arrange
            const string SERVICE_HANDLE = "/org/freedesktop/portal/desktop/request/1_42/service_chosen";
            daemon.ReturnedHandle = SERVICE_HANDLE;
            daemon.ResponseUris = new[] { "file:///tmp/b.png" };

            // Act
            PortalResult result = PortalFileChooser.Run(OpenRequest(false), daemon.Address);

            // Assert
            Assert.IsTrue(result.Answered);
            CollectionAssert.AreEqual(new[] { "/tmp/b.png" }, result.Paths);
            CollectionAssert.Contains(daemon.MatchRules, PortalFileChooser.ResponseMatchRule(SERVICE_HANDLE));
        }

        [Test]
        public void NotLoseAResponseThatArrivesBeforeTheHandle()
        {
            // Arrange
            daemon.RespondBeforeReturningHandle = true;
            daemon.ResponseUris = new[] { "file:///tmp/early.png" };

            // Act
            PortalResult result = PortalFileChooser.Run(OpenRequest(false), daemon.Address);

            // Assert
            Assert.IsTrue(result.Answered);
            CollectionAssert.AreEqual(new[] { "/tmp/early.png" }, result.Paths);
        }

        [Test]
        public void ReportUnavailableWhenNoServiceOwnsThePortalName()
        {
            // Arrange
            daemon.PortalErrorName = "org.freedesktop.DBus.Error.ServiceUnknown";

            // Act
            PortalResult result = PortalFileChooser.Run(OpenRequest(false), daemon.Address);

            // Assert
            Assert.IsFalse(result.Answered);
            Assert.IsEmpty(result.Paths);
            StringAssert.Contains("ServiceUnknown", result.Reason);
        }

        [Test]
        public void ReportUnavailableWhenTheBusSocketDoesNotExist()
        {
            // Arrange
            string missing = "unix:path=" + Path.Combine(Path.GetTempPath(), "fb-no-such-bus-" + Guid.NewGuid().ToString("N"));

            // Act
            PortalResult result = PortalFileChooser.Run(OpenRequest(false), missing);

            // Assert
            Assert.IsFalse(result.Answered);
            StringAssert.Contains("bus connection failed", result.Reason);
        }

        [Test]
        public void ReportUnavailableWithoutABusAddress()
        {
            // Act
            PortalResult result = PortalFileChooser.Run(OpenRequest(false), null);

            // Assert
            Assert.IsFalse(result.Answered);
            Assert.AreEqual("no session bus address", result.Reason);
        }

        [Test]
        public void RequestDirectoryModeForFolderPicks()
        {
            // Arrange
            daemon.ResponseUris = new[] { "file:///home/tester" };

            // Act
            PortalResult result = PortalFileChooser.Run(new PortalRequest(TITLE, string.Empty, string.Empty, true, true, false, IMAGE_FILTERS), daemon.Address);
            Dictionary<string, object> options = daemon.ChooserOptions!;

            // Assert
            Assert.AreEqual(PortalFileChooser.OPEN_FILE, daemon.ChooserMember);
            Assert.AreEqual(true, options["directory"]);
            Assert.AreEqual(true, options["multiple"]);
            Assert.IsFalse(options.ContainsKey("filters"));
            Assert.IsFalse(options.ContainsKey("current_folder"));
            CollectionAssert.AreEqual(new[] { "/home/tester" }, result.Paths);
        }

        [Test]
        public void SendTheSuggestedNameForSaveDialogs()
        {
            // Arrange
            daemon.ResponseUris = new[] { "file:///home/tester/report.png" };

            // Act
            PortalResult result = PortalFileChooser.Run(new PortalRequest(TITLE, DIRECTORY, "report.png", false, false, true, IMAGE_FILTERS), daemon.Address);
            Dictionary<string, object> options = daemon.ChooserOptions!;

            // Assert
            Assert.AreEqual(PortalFileChooser.SAVE_FILE, daemon.ChooserMember);
            Assert.AreEqual("report.png", options["current_name"]);
            Assert.IsFalse(options.ContainsKey("multiple"));
            Assert.IsTrue(options.ContainsKey("filters"));
            CollectionAssert.AreEqual(new[] { "/home/tester/report.png" }, result.Paths);
        }

        [Test]
        public void ParseResponseResultsIntoPaths()
        {
            // Arrange
            DBusMessage picked = Response(FakeDBusDaemon.ResponseBody(0, new[] { "file:///tmp/%C3%A9.png", "https://example.invalid/x" }));
            DBusMessage cancelled = Response(FakeDBusDaemon.ResponseBody(1, Array.Empty<string>()));
            DBusMessage emptySuccess = Response(FakeDBusDaemon.ResponseBody(0, Array.Empty<string>()));

            // Act
            PortalResult pickedResult = PortalFileChooser.ParseResponse(picked);
            PortalResult cancelledResult = PortalFileChooser.ParseResponse(cancelled);
            PortalResult emptyResult = PortalFileChooser.ParseResponse(emptySuccess);

            // Assert
            CollectionAssert.AreEqual(new[] { "/tmp/é.png" }, pickedResult.Paths);
            Assert.IsTrue(cancelledResult.Answered);
            Assert.IsEmpty(cancelledResult.Paths);
            Assert.IsTrue(emptyResult.Answered);
            Assert.IsEmpty(emptyResult.Paths);
        }

        [Test]
        public void PredictTheRequestHandleFromTheUniqueName()
        {
            // Act
            string handle = PortalFileChooser.PredictHandle(":1.42", "tok_1");

            // Assert
            Assert.AreEqual("/org/freedesktop/portal/desktop/request/1_42/tok_1", handle);
        }

        [Test]
        public void ConvertFileUrisToPaths()
        {
            // Act & Assert
            Assert.AreEqual("/home/u/a b.png", PortalFileChooser.UriToPath("file:///home/u/a%20b.png"));
            Assert.AreEqual("/x/y", PortalFileChooser.UriToPath("file://localhost/x/y"));
            Assert.AreEqual("/run/user/1001/doc/ab12/c.png", PortalFileChooser.UriToPath("FILE:///run/user/1001/doc/ab12/c.png"));
            Assert.IsNull(PortalFileChooser.UriToPath("https://example.invalid/a.png"));
            Assert.IsNull(PortalFileChooser.UriToPath("file://hostonly"));
        }

        private static PortalRequest OpenRequest(bool multiple) =>
            new (TITLE, DIRECTORY, string.Empty, multiple, false, false, IMAGE_FILTERS);

        private static DBusMessage Response(byte[] body) =>
            new ()
            {
                Type = DBusMessageType.Signal,
                Interface = PortalFileChooser.REQUEST_INTERFACE,
                Member = PortalFileChooser.RESPONSE,
                Signature = PortalFileChooser.RESPONSE_SIGNATURE,
                Body = body,
            };
    }
}
