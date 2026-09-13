using Crosstales.FB.Linux;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace Crosstales.FB.Tests
{
    public class LinuxFileDialogShould
    {
        private const string TITLE = "Select a screenshot";
        private const string PICKED = "/tmp/pick.png";
        private static readonly ExtensionFilter[] IMAGE_FILTERS = { new ("Files", "png", "jpg") };

        private readonly List<string> attempts = new ();
        private readonly Dictionary<string, List<string>> arguments = new ();
        private readonly List<PortalRequest> portalRequests = new ();

        private LinuxFileDialog.ProcessRunner originalProcess = null!;
        private LinuxFileDialog.PortalRunner originalPortal = null!;
        private Dictionary<string, (bool available, int exitCode, string output)> processScript = new ();
        private PortalResult portalResult;

        [SetUp]
        public void SetUp()
        {
            originalProcess = LinuxFileDialog.runProcess;
            originalPortal = LinuxFileDialog.runPortal;
            attempts.Clear();
            arguments.Clear();
            portalRequests.Clear();
            processScript = new Dictionary<string, (bool, int, string)>();
            portalResult = PortalResult.Unavailable("no session bus address");
            LinuxFileDialog.runProcess = RecordingProcess;
            LinuxFileDialog.runPortal = RecordingPortal;
        }

        [TearDown]
        public void TearDown()
        {
            LinuxFileDialog.runProcess = originalProcess;
            LinuxFileDialog.runPortal = originalPortal;
        }

        [Test]
        public void PreferThePortalWhenItAnswers()
        {
            // Arrange
            portalResult = PortalResult.Picked(new[] { PICKED });

            // Act
            DialogOutcome outcome = LinuxFileDialog.OpenFiles(TITLE, string.Empty, string.Empty, false, IMAGE_FILTERS);

            // Assert
            CollectionAssert.AreEqual(new[] { PICKED }, outcome.Paths);
            Assert.IsNull(outcome.Error);
            Assert.IsEmpty(attempts);
            Assert.AreEqual(TITLE, portalRequests[0].Title);
            Assert.IsFalse(portalRequests[0].Multiple);
        }

        [Test]
        public void EndTheChainWhenThePortalDialogIsCancelled()
        {
            // Arrange
            portalResult = PortalResult.CANCELLED;

            // Act
            DialogOutcome outcome = LinuxFileDialog.OpenFiles(TITLE, string.Empty, string.Empty, false, IMAGE_FILTERS);

            // Assert
            Assert.IsEmpty(outcome.Paths);
            Assert.IsNull(outcome.Error);
            Assert.IsEmpty(attempts);
        }

        [Test]
        public void FallBackToZenityWhenThePortalIsUnavailable()
        {
            // Arrange
            processScript[LinuxFileDialog.ZENITY] = (true, 0, PICKED + "\n");

            // Act
            DialogOutcome outcome = LinuxFileDialog.OpenFiles(TITLE, "/home/tester", string.Empty, false, IMAGE_FILTERS);

            // Assert
            CollectionAssert.AreEqual(new[] { PICKED }, outcome.Paths);
            Assert.IsNull(outcome.Error);
            CollectionAssert.AreEqual(new[] { LinuxFileDialog.ZENITY }, attempts);
            List<string> zenity = arguments[LinuxFileDialog.ZENITY];
            CollectionAssert.Contains(zenity, "--file-selection");
            CollectionAssert.Contains(zenity, "--title=" + TITLE);
            CollectionAssert.Contains(zenity, "--filename=/home/tester/");
            CollectionAssert.Contains(zenity, "--file-filter=Files | *.[pP][nN][gG] *.[jJ][pP][gG]");
            CollectionAssert.Contains(zenity, "--file-filter=All files | *");
            CollectionAssert.DoesNotContain(zenity, "--multiple");
        }

        [Test]
        public void FallBackToKdialogWhenZenityIsMissing()
        {
            // Arrange
            processScript[LinuxFileDialog.ZENITY] = (false, -1, string.Empty);
            processScript[LinuxFileDialog.KDIALOG] = (true, 0, PICKED + "\n");

            // Act
            DialogOutcome outcome = LinuxFileDialog.OpenFiles(TITLE, "/home/tester", string.Empty, true, IMAGE_FILTERS);

            // Assert
            CollectionAssert.AreEqual(new[] { PICKED }, outcome.Paths);
            CollectionAssert.AreEqual(new[] { LinuxFileDialog.ZENITY, LinuxFileDialog.KDIALOG }, attempts);
            List<string> kdialog = arguments[LinuxFileDialog.KDIALOG];
            CollectionAssert.AreEqual(new[] { "--getopenfilename", "/home/tester", "*.png *.jpg|Files\n*|All Files", "--multiple", "--separate-output", "--title", TITLE }, kdialog);
        }

        [Test]
        public void TreatANonZeroExitAsCancellation()
        {
            // Arrange
            processScript[LinuxFileDialog.ZENITY] = (true, 1, string.Empty);
            processScript[LinuxFileDialog.KDIALOG] = (true, 0, PICKED);

            // Act
            DialogOutcome outcome = LinuxFileDialog.OpenFiles(TITLE, string.Empty, string.Empty, false, IMAGE_FILTERS);

            // Assert
            Assert.IsEmpty(outcome.Paths);
            Assert.IsNull(outcome.Error);
            CollectionAssert.AreEqual(new[] { LinuxFileDialog.ZENITY }, attempts);
        }

        [Test]
        public void ReportEveryBackendWhenNoneCanShowADialog()
        {
            // Act
            DialogOutcome outcome = LinuxFileDialog.OpenFiles(TITLE, string.Empty, string.Empty, false, IMAGE_FILTERS);

            // Assert
            Assert.IsEmpty(outcome.Paths);
            StringAssert.Contains("portal: no session bus address", outcome.Error);
            StringAssert.Contains("zenity: unavailable", outcome.Error);
            StringAssert.Contains("kdialog: unavailable", outcome.Error);
            CollectionAssert.AreEqual(new[] { LinuxFileDialog.ZENITY, LinuxFileDialog.KDIALOG }, attempts);
        }

        [Test]
        public void PassDirectoryModeToEveryBackend()
        {
            // Arrange
            processScript[LinuxFileDialog.ZENITY] = (false, -1, string.Empty);
            processScript[LinuxFileDialog.KDIALOG] = (true, 0, "/home/tester/dir\n");

            // Act
            DialogOutcome outcome = LinuxFileDialog.OpenFolders("Pick a folder", "/home/tester", false);

            // Assert
            Assert.IsTrue(portalRequests[0].SelectDirectory);
            Assert.IsFalse(portalRequests[0].Save);
            CollectionAssert.Contains(arguments[LinuxFileDialog.ZENITY], "--directory");
            Assert.AreEqual("--getexistingdirectory", arguments[LinuxFileDialog.KDIALOG][0]);
            CollectionAssert.AreEqual(new[] { "/home/tester/dir" }, outcome.Paths);
        }

        [Test]
        public void PassSaveModeToEveryBackend()
        {
            // Arrange
            processScript[LinuxFileDialog.ZENITY] = (false, -1, string.Empty);
            processScript[LinuxFileDialog.KDIALOG] = (true, 0, "/home/tester/out.png\n");

            // Act
            DialogOutcome outcome = LinuxFileDialog.SaveFile("Save", "/home/tester", "out.png", IMAGE_FILTERS);

            // Assert
            Assert.IsTrue(portalRequests[0].Save);
            Assert.AreEqual("out.png", portalRequests[0].DefaultName);
            CollectionAssert.Contains(arguments[LinuxFileDialog.ZENITY], "--save");
            CollectionAssert.Contains(arguments[LinuxFileDialog.ZENITY], "--filename=/home/tester/out.png");
            CollectionAssert.AreEqual(new[] { "--getsavefilename", "/home/tester/out.png", "*.png *.jpg|Files\n*|All Files", "--title", "Save" }, arguments[LinuxFileDialog.KDIALOG]);
            CollectionAssert.AreEqual(new[] { "/home/tester/out.png" }, outcome.Paths);
        }

        [Test]
        public void BuildTheKdialogFilterFromExtensionGroups()
        {
            // Act
            string grouped = LinuxFileDialog.BuildKdialogFilter(new[] { new ExtensionFilter("Images", "png", ".jpg"), new ExtensionFilter(string.Empty, "*.gif") });
            string none = LinuxFileDialog.BuildKdialogFilter(Array.Empty<ExtensionFilter>());

            // Assert
            Assert.AreEqual("*.png *.jpg|Images\n*.gif|Files\n*|All Files", grouped);
            Assert.AreEqual(string.Empty, none);
        }

        private bool RecordingProcess(string fileName, IReadOnlyList<string> args, out int exitCode, out string standardOutput)
        {
            attempts.Add(fileName);
            arguments[fileName] = new List<string>(args);

            if (processScript.TryGetValue(fileName, out (bool available, int exitCode, string output) script) && script.available)
            {
                exitCode = script.exitCode;
                standardOutput = script.output;
                return true;
            }

            exitCode = -1;
            standardOutput = string.Empty;
            return false;
        }

        private PortalResult RecordingPortal(in PortalRequest request)
        {
            portalRequests.Add(request);
            return portalResult;
        }
    }
}
