using Crosstales.FB.Linux;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.TestTools;

namespace Crosstales.FB.Tests
{
    public class FileBrowserShould
    {
        private const string TITLE = "Select a screenshot";
        private static readonly string[] IMAGE_EXTENSIONS = { "png", "jpg", "jpeg" };

        private LinuxFileDialog.ProcessRunner originalProcess = null!;
        private LinuxFileDialog.PortalRunner originalPortal = null!;
        private int backendInvocations;
        private PortalResult portalResult;

        [SetUp]
        public void SetUp()
        {
            originalProcess = LinuxFileDialog.runProcess;
            originalPortal = LinuxFileDialog.runPortal;
            backendInvocations = 0;
            portalResult = PortalResult.Unavailable("no session bus address");
            LinuxFileDialog.runProcess = MissingProcess;
            LinuxFileDialog.runPortal = ScriptedPortal;
            FileBrowser.Instance.AllowSyncCalls = false;
        }

        [TearDown]
        public void TearDown()
        {
            LinuxFileDialog.runProcess = originalProcess;
            LinuxFileDialog.runPortal = originalPortal;
            FileBrowser.Instance.AllowSyncCalls = false;
        }

        [Test]
        public void RefuseABlockingPickWhileSyncCallsAreNotAllowed()
        {
            // Arrange
            LogAssert.Expect(LogType.Error, new Regex("AllowSyncCalls"));

            // Act
            string? path = FileBrowser.Instance.OpenSingleFile(TITLE, string.Empty, string.Empty, IMAGE_EXTENSIONS);

            // Assert
            Assert.IsNull(path);
            Assert.IsNull(FileBrowser.Instance.CurrentOpenSingleFileData);
            StringAssert.Contains(nameof(FileBrowser.AllowSyncCalls), FileBrowser.Instance.LastError);
            StringAssert.Contains(nameof(FileBrowser.OpenSingleFile), FileBrowser.Instance.LastError);
            Assert.AreEqual(0, backendInvocations);
        }

        [Test]
        public void RefuseEveryBlockingVariantWhileSyncCallsAreNotAllowed()
        {
            // Arrange
            LogAssert.Expect(LogType.Error, new Regex(nameof(FileBrowser.OpenFiles)));
            LogAssert.Expect(LogType.Error, new Regex(nameof(FileBrowser.OpenSingleFolder)));
            LogAssert.Expect(LogType.Error, new Regex(nameof(FileBrowser.OpenFolders)));
            LogAssert.Expect(LogType.Error, new Regex(nameof(FileBrowser.SaveFile)));

            // Act
            string[] files = FileBrowser.Instance.OpenFiles(TITLE, string.Empty, string.Empty, true, IMAGE_EXTENSIONS);
            string? folder = FileBrowser.Instance.OpenSingleFolder(TITLE, string.Empty);
            string[] folders = FileBrowser.Instance.OpenFolders(TITLE, string.Empty, true);
            string? saved = FileBrowser.Instance.SaveFile(TITLE, string.Empty, "out.png", IMAGE_EXTENSIONS);

            // Assert
            Assert.IsEmpty(files);
            Assert.IsNull(folder);
            Assert.IsEmpty(folders);
            Assert.IsNull(saved);
            Assert.AreEqual(0, backendInvocations);
        }

        [Test]
        public void RunABlockingPickOnceSyncCallsAreAllowed()
        {
            // Arrange
            AssumeLinux();
            string picked = Path.Combine(Path.GetTempPath(), "fb-pick-" + Guid.NewGuid().ToString("N") + ".png");
            byte[] payload = { 1, 2, 3, 4 };
            File.WriteAllBytes(picked, payload);
            portalResult = PortalResult.Picked(new[] { picked });
            FileBrowser.Instance.AllowSyncCalls = true;

            try
            {
                // Act
                string? path = FileBrowser.Instance.OpenSingleFile(TITLE, string.Empty, string.Empty, IMAGE_EXTENSIONS);

                // Assert
                Assert.AreEqual(picked, path);
                CollectionAssert.AreEqual(payload, FileBrowser.Instance.CurrentOpenSingleFileData);
                Assert.IsNull(FileBrowser.Instance.LastError);
                Assert.AreEqual(1, backendInvocations);
            }
            finally { File.Delete(picked); }
        }

        [Test]
        public void ClearLastErrorWhenTheUserCancels()
        {
            // Arrange
            AssumeLinux();
            LogAssert.Expect(LogType.Error, new Regex("No native file dialog could be shown"));
            FileBrowser.Instance.AllowSyncCalls = true;
            FileBrowser.Instance.OpenSingleFile(TITLE, string.Empty, string.Empty, IMAGE_EXTENSIONS);
            Assert.IsNotNull(FileBrowser.Instance.LastError);
            portalResult = PortalResult.CANCELLED;

            // Act
            string? path = FileBrowser.Instance.OpenSingleFile(TITLE, string.Empty, string.Empty, IMAGE_EXTENSIONS);

            // Assert
            Assert.IsNull(path);
            Assert.IsNull(FileBrowser.Instance.CurrentOpenSingleFileData);
            Assert.IsNull(FileBrowser.Instance.LastError);
        }

        [Test]
        public void ExposeAMissingBackendThroughLastError()
        {
            // Arrange
            AssumeLinux();
            LogAssert.Expect(LogType.Error, new Regex("No native file dialog could be shown"));
            FileBrowser.Instance.AllowSyncCalls = true;

            // Act
            string? path = FileBrowser.Instance.OpenSingleFile(TITLE, string.Empty, string.Empty, IMAGE_EXTENSIONS);

            // Assert
            Assert.IsNull(path);
            Assert.IsNull(FileBrowser.Instance.CurrentOpenSingleFileData);
            StringAssert.Contains("zenity", FileBrowser.Instance.LastError);
            StringAssert.Contains("kdialog", FileBrowser.Instance.LastError);
            StringAssert.Contains("portal", FileBrowser.Instance.LastError);
        }

        [Test]
        public void ReportAnUnreadablePickThroughLastError()
        {
            // Arrange
            AssumeLinux();
            string vanished = Path.Combine(Path.GetTempPath(), "fb-vanished-" + Guid.NewGuid().ToString("N") + ".png");
            LogAssert.Expect(LogType.Error, new Regex("Failed to read"));
            portalResult = PortalResult.Picked(new[] { vanished });
            FileBrowser.Instance.AllowSyncCalls = true;

            // Act
            string? path = FileBrowser.Instance.OpenSingleFile(TITLE, string.Empty, string.Empty, IMAGE_EXTENSIONS);

            // Assert
            Assert.AreEqual(vanished, path);
            Assert.IsNull(FileBrowser.Instance.CurrentOpenSingleFileData);
            StringAssert.Contains(vanished, FileBrowser.Instance.LastError);
        }

        [Test]
        public void BuildOneFilterGroupFromBareExtensions()
        {
            // Act
            ExtensionFilter[] grouped = FileBrowser.FiltersFromExtensions(IMAGE_EXTENSIONS);
            ExtensionFilter[] wildcard = FileBrowser.FiltersFromExtensions(new[] { "*" });
            ExtensionFilter[] anyDotAny = FileBrowser.FiltersFromExtensions(new[] { "*.*" });
            ExtensionFilter[] none = FileBrowser.FiltersFromExtensions(null);

            // Assert
            Assert.AreEqual(1, grouped.Length);
            Assert.AreEqual(NativeFileDialog.DEFAULT_FILTER_NAME, grouped[0].Name);
            CollectionAssert.AreEqual(IMAGE_EXTENSIONS, grouped[0].Extensions);
            Assert.IsEmpty(wildcard);
            Assert.IsEmpty(anyDotAny);
            Assert.IsEmpty(none);
        }

        [Test]
        public void NormalizeExtensionsIntoGlobs()
        {
            // Act & Assert
            Assert.AreEqual("*.png", NativeFileDialog.NormalizeGlob("png"));
            Assert.AreEqual("*.png", NativeFileDialog.NormalizeGlob(".png"));
            Assert.AreEqual("*.png", NativeFileDialog.NormalizeGlob(" *.png "));
            Assert.AreEqual("*", NativeFileDialog.NormalizeGlob("*.*"));
            Assert.AreEqual("*", NativeFileDialog.NormalizeGlob(".*"));
            Assert.AreEqual("*", NativeFileDialog.NormalizeGlob(string.Empty));
            Assert.AreEqual("*", NativeFileDialog.NormalizeGlob(null));
            Assert.AreEqual("*.png;*.jpg", NativeFileDialog.BuildGlobPatterns(new[] { "png", ".jpg" }, ";"));
            Assert.AreEqual(string.Empty, NativeFileDialog.BuildGlobPatterns(Array.Empty<string>(), ";"));
        }

        [Test]
        public void WidenGlobsToBothLetterCases()
        {
            // Act & Assert
            Assert.AreEqual("*.[pP][nN][gG]", NativeFileDialog.CaseInsensitiveGlob("*.png"));
            Assert.AreEqual("*.[eE][xX][eE]", NativeFileDialog.CaseInsensitiveGlob("*.EXE"));
            Assert.AreEqual("*", NativeFileDialog.CaseInsensitiveGlob("*"));
            Assert.AreEqual("*.7[zZ]", NativeFileDialog.CaseInsensitiveGlob("*.7z"));
        }

        [Test]
        public void StripExtensionsToTheirBareForm()
        {
            // Act & Assert
            Assert.AreEqual("png", NativeFileDialog.BareExtension("*.png"));
            Assert.AreEqual("png", NativeFileDialog.BareExtension(".png"));
            Assert.AreEqual("png", NativeFileDialog.BareExtension("png"));
            Assert.AreEqual(string.Empty, NativeFileDialog.BareExtension("*"));
            Assert.AreEqual(string.Empty, NativeFileDialog.BareExtension("  "));
        }

        [Test]
        public void BuildTheWin32FilterString()
        {
            // Act
            string filter = NativeFileDialog.BuildWindowsFilter(new[] { new ExtensionFilter("Images", "png", "jpg"), new ExtensionFilter("Any", "*") });
            string none = NativeFileDialog.BuildWindowsFilter(null);

            // Assert
            Assert.AreEqual("Images (*.png;*.jpg)\0*.png;*.jpg\0Any (*.*)\0*.*\0All Files (*.*)\0*.*\0\0", filter);
            Assert.AreEqual("All Files (*.*)\0*.*\0\0", none);
        }

        [Test]
        public void ReadTheWin32MultiSelectBuffer()
        {
            // Arrange
            string root = Path.Combine(Path.GetTempPath(), "pics");
            IntPtr single = Utf16Buffer(root + "\0\0");
            IntPtr multiple = Utf16Buffer(root + "\0a.png\0b.png\0\0");

            try
            {
                // Act
                string[] one = NativeFileDialog.ReadFileBuffer(single, 256);
                string[] two = NativeFileDialog.ReadFileBuffer(multiple, 256);

                // Assert
                CollectionAssert.AreEqual(new[] { root }, one);
                CollectionAssert.AreEqual(new[] { Path.Combine(root, "a.png"), Path.Combine(root, "b.png") }, two);
            }
            finally
            {
                Marshal.FreeHGlobal(single);
                Marshal.FreeHGlobal(multiple);
            }
        }

        [Test]
        public void SplitDialogOutputIntoTrimmedLines()
        {
            // Act & Assert
            CollectionAssert.AreEqual(new[] { "/a", "/b c" }, NativeFileDialog.SplitLines("/a\r\n /b c \n\n"));
            Assert.IsEmpty(NativeFileDialog.SplitLines(string.Empty));
            Assert.IsEmpty(NativeFileDialog.SplitLines(null));
        }

        [Test]
        public void ComposeTheInitialPathForDialogs()
        {
            // Act & Assert
            Assert.AreEqual(Path.Combine("/home/u", "shot.png"), NativeFileDialog.BuildInitialPath("/home/u", "shot.png"));
            Assert.AreEqual("/home/u/", NativeFileDialog.BuildInitialPath("/home/u", string.Empty));
            Assert.AreEqual("/home/u/", NativeFileDialog.BuildInitialPath("/home/u/", string.Empty));
            Assert.AreEqual("shot.png", NativeFileDialog.BuildInitialPath(string.Empty, "shot.png"));
            Assert.AreEqual(string.Empty, NativeFileDialog.BuildInitialPath(string.Empty, string.Empty));
        }

        private static void AssumeLinux() =>
            Assume.That(RuntimeInformation.IsOSPlatform(OSPlatform.Linux), "the Linux backend chain is exercised on Linux only");

        private static IntPtr Utf16Buffer(string content)
        {
            byte[] bytes = System.Text.Encoding.Unicode.GetBytes(content);
            IntPtr buffer = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, buffer, bytes.Length);
            return buffer;
        }

        private bool MissingProcess(string fileName, IReadOnlyList<string> args, out int exitCode, out string standardOutput)
        {
            backendInvocations++;
            exitCode = -1;
            standardOutput = string.Empty;
            return false;
        }

        private PortalResult ScriptedPortal(in PortalRequest request)
        {
            backendInvocations++;
            return portalResult;
        }
    }
}
