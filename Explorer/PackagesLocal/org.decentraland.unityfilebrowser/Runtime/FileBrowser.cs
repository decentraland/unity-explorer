// Clean-room native file/folder/save dialog implementation.
//
// It reproduces only the consumer-facing surface unity-explorer relies on
// (Crosstales.FB.FileBrowser singleton + Crosstales.CTExtensions) and backs it
// with the host OS native file dialog: the xdg-desktop-portal file chooser,
// zenity or kdialog on Linux, the Win32 common dialogs on Windows, and
// NSOpenPanel/NSSavePanel (through osascript) on macOS. The platform is chosen
// at runtime so every branch is compiled on every target.

using Crosstales.FB.Linux;
using DCL.Diagnostics;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

[assembly: InternalsVisibleTo("Crosstales.FB.Tests")]

namespace Crosstales.FB
{
    /// <summary>A named group of file extensions offered as one entry in a dialog's filter dropdown.</summary>
    public struct ExtensionFilter
    {
        public string Name;
        public string[] Extensions;

        public ExtensionFilter(string filterName, params string[] filterExtensions)
        {
            Name = filterName;
            Extensions = filterExtensions;
        }
    }

    /// <summary>
    ///     Native OS file/folder/save dialog entry point. Access through <see cref="Instance" />.
    ///     Synchronous methods block until the dialog closes and return the chosen path(s); the
    ///     *Async variants run the dialog off the main thread and post the result back to the Unity
    ///     player loop.
    /// </summary>
    public class FileBrowser
    {
        private static readonly FileBrowser INSTANCE = new ();

        public static FileBrowser Instance => INSTANCE;

        /// <summary>
        ///     Permits the blocking dialog methods. Off by default: a synchronous call made while this is false opens no
        ///     dialog, records the refusal in <see cref="LastError" /> and returns no selection. The *Async variants always
        ///     run the dialog on a worker thread, whatever this flag holds.
        /// </summary>
        public bool AllowSyncCalls;

        /// <summary>Raw bytes of the file returned by the most recent <see cref="OpenSingleFile()" /> call, or null.</summary>
        public byte[]? CurrentOpenSingleFileData { get; private set; }

        /// <summary>
        ///     Why the most recent dialog call produced no selection without showing a dialog (no native backend, a refused
        ///     synchronous call, an unreadable pick), or null when a dialog was shown and the user picked or cancelled.
        /// </summary>
        public string? LastError { get; private set; }

        private FileBrowser() { }

        public string? OpenSingleFile() =>
            OpenSingleFile("Open File", string.Empty, string.Empty, Array.Empty<string>());

        public string? OpenSingleFile(string title, params string[] extensions) =>
            OpenSingleFile(title, string.Empty, string.Empty, extensions);

        public string? OpenSingleFile(string title, string directory, string defaultName, params string[] extensions) =>
            OpenSingleFile(title, directory, defaultName, FiltersFromExtensions(extensions));

        public string? OpenSingleFile(string title, string directory, string defaultName, ExtensionFilter[] extensions)
        {
            if (!SyncCallRefused(nameof(OpenSingleFile)))
                return OpenSingleFileInternal(title, directory, defaultName, extensions);

            CurrentOpenSingleFileData = null;
            return null;
        }

        public void OpenSingleFileAsync(Action<string?> callback, string title, string directory, string defaultName, params string[] extensions) =>
            RunAsync(() => OpenSingleFileInternal(title, directory, defaultName, FiltersFromExtensions(extensions)), callback, null);

        public string[] OpenFiles(string title, string directory, string defaultName, bool multiselect, params string[] extensions) =>
            OpenFiles(title, directory, defaultName, multiselect, FiltersFromExtensions(extensions));

        public string[] OpenFiles(string title, string directory, string defaultName, bool multiselect, ExtensionFilter[] extensions) =>
            SyncCallRefused(nameof(OpenFiles))
                ? Array.Empty<string>()
                : Complete(NativeFileDialog.OpenFiles(title, directory, defaultName, multiselect, extensions)).Paths;

        public void OpenFilesAsync(Action<string[]> callback, string title, string directory, string defaultName, bool multiselect, params string[] extensions) =>
            RunAsync(() => Complete(NativeFileDialog.OpenFiles(title, directory, defaultName, multiselect, FiltersFromExtensions(extensions))).Paths, callback, Array.Empty<string>());

        public string? OpenSingleFolder() =>
            OpenSingleFolder("Select Folder", string.Empty);

        public string? OpenSingleFolder(string title, string directory) =>
            SyncCallRefused(nameof(OpenSingleFolder))
                ? null
                : FirstOrNull(Complete(NativeFileDialog.OpenFolders(title, directory, false)).Paths);

        public string[] OpenFolders(string title, string directory, bool multiselect) =>
            SyncCallRefused(nameof(OpenFolders))
                ? Array.Empty<string>()
                : Complete(NativeFileDialog.OpenFolders(title, directory, multiselect)).Paths;

        public void OpenSingleFolderAsync(Action<string?> callback, string title, string directory) =>
            RunAsync(() => FirstOrNull(Complete(NativeFileDialog.OpenFolders(title, directory, false)).Paths), callback, null);

        public void OpenFoldersAsync(Action<string[]> callback, string title, string directory, bool multiselect) =>
            RunAsync(() => Complete(NativeFileDialog.OpenFolders(title, directory, multiselect)).Paths, callback, Array.Empty<string>());

        public string? SaveFile() =>
            SaveFile("Save File", string.Empty, string.Empty, Array.Empty<string>());

        public string? SaveFile(string title, string directory, string defaultName, params string[] extensions) =>
            SaveFile(title, directory, defaultName, FiltersFromExtensions(extensions));

        public string? SaveFile(string title, string directory, string defaultName, ExtensionFilter[] extensions) =>
            SyncCallRefused(nameof(SaveFile))
                ? null
                : FirstOrNull(Complete(NativeFileDialog.SaveFile(title, directory, defaultName, extensions)).Paths);

        public void SaveFileAsync(Action<string?> callback, string title, string directory, string defaultName, params string[] extensions) =>
            RunAsync(() => FirstOrNull(Complete(NativeFileDialog.SaveFile(title, directory, defaultName, FiltersFromExtensions(extensions))).Paths), callback, null);

        internal static ExtensionFilter[] FiltersFromExtensions(string[]? extensions)
        {
            if (extensions == null || extensions.Length == 0)
                return Array.Empty<ExtensionFilter>();

            // A lone wildcard means "any file" — no meaningful group to show.
            if (extensions.Length == 1 && (string.IsNullOrWhiteSpace(extensions[0]) || extensions[0] == "*" || extensions[0] == "*.*"))
                return Array.Empty<ExtensionFilter>();

            return new[] { new ExtensionFilter(NativeFileDialog.DEFAULT_FILTER_NAME, extensions) };
        }

        private string? OpenSingleFileInternal(string title, string directory, string defaultName, ExtensionFilter[] filters)
        {
            DialogOutcome outcome = Complete(NativeFileDialog.OpenFiles(title, directory, defaultName, false, filters));

            if (outcome.Paths.Length == 0)
            {
                CurrentOpenSingleFileData = null;
                return null;
            }

            string path = outcome.Paths[0];
            CurrentOpenSingleFileData = ReadBytesSafe(path);
            return path;
        }

        private bool SyncCallRefused(string method)
        {
            if (AllowSyncCalls)
                return false;

            LastError = $"{method} blocks until the dialog closes and {nameof(AllowSyncCalls)} is false; set {nameof(FileBrowser)}.{nameof(Instance)}.{nameof(AllowSyncCalls)} = true before calling it or use {method}Async.";
            NativeFileDialog.ReportFailure(LastError);
            return true;
        }

        private DialogOutcome Complete(DialogOutcome outcome)
        {
            LastError = outcome.Error;

            if (outcome.Error is { } error)
                NativeFileDialog.ReportFailure(error);

            return outcome;
        }

        private byte[]? ReadBytesSafe(string path)
        {
            try { return File.ReadAllBytes(path); }
            catch (Exception e)
            {
                LastError = $"Failed to read '{path}': {e.Message}";
                NativeFileDialog.ReportFailure(LastError);
                return null;
            }
        }

        private static string? FirstOrNull(string[] values) =>
            values.Length > 0 ? values[0] : null;

        // Runs the (blocking) dialog on a worker thread and marshals the callback back to the thread
        // that issued the call — the Unity main thread when its SynchronizationContext is installed.
        private static void RunAsync<T>(Func<T> work, Action<T> callback, T fallback)
        {
            if (SynchronizationContext.Current is { } context)
            {
                Task.Run(() =>
                {
                    T result = Guarded(work, fallback);
                    context.Post(_ => callback(result), null);
                });

                return;
            }

            Task.Run(() => callback(Guarded(work, fallback)));
        }

        private static T Guarded<T>(Func<T> work, T fallback)
        {
            try { return work(); }
            catch (Exception e)
            {
                NativeFileDialog.ReportFailure(e.ToString());
                return fallback;
            }
        }
    }

    /// <summary>
    ///     Result of one native dialog round. <see cref="Error" /> is null whenever a dialog was actually shown, whether the
    ///     user picked something or backed out; it carries the reason when no backend could show one.
    /// </summary>
    internal readonly struct DialogOutcome
    {
        public static readonly DialogOutcome CANCELLED = new (Array.Empty<string>(), null);

        public readonly string[] Paths;
        public readonly string? Error;

        private DialogOutcome(string[] paths, string? error)
        {
            Paths = paths;
            Error = error;
        }

        public static DialogOutcome From(string[] paths) =>
            paths.Length > 0 ? new DialogOutcome(paths, null) : CANCELLED;

        public static DialogOutcome Failed(string error) =>
            new (Array.Empty<string>(), error);
    }

    // Cross-platform native dialog backend. Every branch compiles on every target; the running OS,
    // resolved at runtime, decides which one executes, so the Win32 P/Invoke declarations are inert
    // on non-Windows hosts and the osascript path is inert off macOS.
    internal static class NativeFileDialog
    {
        public const string DEFAULT_FILTER_NAME = "Files";

        private const string LOG_PREFIX = "[Crosstales.FB] ";

        // Captured on first use, which the synchronous entry points make on the Unity main thread.
        private static readonly SynchronizationContext? MAIN_CONTEXT = SynchronizationContext.Current;

        private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private static bool IsMacOs => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        /// <summary>Reports a dialog failure through the report hub, from the main thread even when the dialog ran on a worker.</summary>
        internal static void ReportFailure(string message)
        {
            string prefixed = LOG_PREFIX + message;

            if (MAIN_CONTEXT == null || SynchronizationContext.Current == MAIN_CONTEXT)
                ReportHub.LogError(ReportCategory.UI, prefixed);
            else
                MAIN_CONTEXT.Post(_ => ReportHub.LogError(ReportCategory.UI, prefixed), null);
        }

        public static DialogOutcome OpenFiles(string title, string directory, string defaultName, bool multiselect, ExtensionFilter[] filters)
        {
            if (IsWindows)
                return DialogOutcome.From(WindowsOpenFiles(title, directory, defaultName, multiselect, filters));

            if (IsMacOs)
                return MacOpenFiles(title, directory, multiselect, filters);

            return LinuxFileDialog.OpenFiles(title, directory, defaultName, multiselect, filters);
        }

        public static DialogOutcome OpenFolders(string title, string directory, bool multiselect)
        {
            if (IsWindows)
            {
                string folder = WindowsOpenFolder(title);
                return DialogOutcome.From(string.IsNullOrEmpty(folder) ? Array.Empty<string>() : new[] { folder });
            }

            if (IsMacOs)
                return MacOpenFolders(title, directory, multiselect);

            return LinuxFileDialog.OpenFolders(title, directory, multiselect);
        }

        public static DialogOutcome SaveFile(string title, string directory, string defaultName, ExtensionFilter[] filters)
        {
            if (IsWindows)
            {
                string path = WindowsSaveFile(title, directory, defaultName, filters);
                return DialogOutcome.From(string.IsNullOrEmpty(path) ? Array.Empty<string>() : new[] { path });
            }

            if (IsMacOs)
                return MacSaveFile(title, directory, defaultName);

            return LinuxFileDialog.SaveFile(title, directory, defaultName, filters);
        }

        // ---- macOS (osascript / Cocoa panels) -----------------------------------------------------

        private static DialogOutcome MacOpenFiles(string title, string directory, bool multiselect, ExtensionFilter[] filters) =>
            MacChoose("file", "theFiles", "aFile", title, MacTypeClause(filters) + MacLocationClause(directory), multiselect);

        private static DialogOutcome MacOpenFolders(string title, string directory, bool multiselect) =>
            MacChoose("folder", "theFolders", "aFolder", title, MacLocationClause(directory), multiselect);

        // Shared AppleScript shape for `choose file`/`choose folder`: a single pick returns one POSIX
        // path expression, a multi-select pick collects each POSIX path from the chosen list into a
        // newline-joined "theOutput" string. `collectionVar`/`itemVar` only need to be distinct AppleScript
        // identifiers between the file and folder callers; they don't otherwise affect behavior.
        private static DialogOutcome MacChoose(string kind, string collectionVar, string itemVar, string title, string extraClause, bool multiselect)
        {
            var script = new StringBuilder();

            if (multiselect)
            {
                script.Append("set ").Append(collectionVar).Append(" to choose ").Append(kind).Append(" with prompt ").Append(AppleQuote(title));
                script.Append(extraClause).Append(" with multiple selections allowed\n");
                script.Append("set theOutput to \"\"\n");
                script.Append("repeat with ").Append(itemVar).Append(" in ").Append(collectionVar).Append('\n');
                script.Append("set theOutput to theOutput & POSIX path of ").Append(itemVar).Append(" & linefeed\n");
                script.Append("end repeat\n");
                script.Append("return theOutput");
            }
            else
            {
                script.Append("POSIX path of (choose ").Append(kind).Append(" with prompt ").Append(AppleQuote(title));
                script.Append(extraClause).Append(')');
            }

            return RunOsascript(script.ToString());
        }

        // macOS save panels take no type filter; the suggested extension travels in the default name.
        private static DialogOutcome MacSaveFile(string title, string directory, string defaultName)
        {
            var script = new StringBuilder();
            script.Append("POSIX path of (choose file name with prompt ").Append(AppleQuote(title));
            if (!string.IsNullOrEmpty(defaultName)) script.Append(" default name ").Append(AppleQuote(defaultName));
            script.Append(MacLocationClause(directory)).Append(')');

            return RunOsascript(script.ToString());
        }

        private static string MacTypeClause(ExtensionFilter[]? filters)
        {
            var bareExtensions = new List<string>();

            if (filters != null)
            {
                foreach (ExtensionFilter filter in filters)
                {
                    if (filter.Extensions == null) continue;

                    foreach (string extension in filter.Extensions)
                    {
                        string bare = BareExtension(extension);
                        if (bare.Length > 0 && !bareExtensions.Contains(bare))
                            bareExtensions.Add(bare);
                    }
                }
            }

            if (bareExtensions.Count == 0)
                return string.Empty;

            var builder = new StringBuilder(" of type {");

            for (int i = 0; i < bareExtensions.Count; i++)
            {
                if (i > 0) builder.Append(", ");
                builder.Append(AppleQuote(bareExtensions[i]));
            }

            builder.Append('}');
            return builder.ToString();
        }

        private static string MacLocationClause(string directory) =>
            string.IsNullOrEmpty(directory)
                ? string.Empty
                : " default location (POSIX file " + AppleQuote(directory) + ")";

        private static DialogOutcome RunOsascript(string script)
        {
            if (TryRun("osascript", new[] { "-e", script }, out int code, out string output))
                return code == 0 ? DialogOutcome.From(SplitLines(output)) : DialogOutcome.CANCELLED;

            return DialogOutcome.Failed("'osascript' is unavailable; cannot show a native dialog on this host.");
        }

        private static string AppleQuote(string? value)
        {
            value ??= string.Empty;
            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        // ---- Windows (Win32 common dialogs) --------------------------------------------------------

        private const int OFN_EXPLORER = 0x00080000;
        private const int OFN_FILEMUSTEXIST = 0x00001000;
        private const int OFN_PATHMUSTEXIST = 0x00000800;
        private const int OFN_ALLOWMULTISELECT = 0x00000200;
        private const int OFN_NOCHANGEDIR = 0x00000008;
        private const int OFN_OVERWRITEPROMPT = 0x00000002;

        private const uint BIF_RETURNONLYFSDIRS = 0x00000001;
        private const uint BIF_NEWDIALOGSTYLE = 0x00000040;

        private const int FILE_BUFFER_CHARS = 64 * 1024;
        private const int PATH_BUFFER_CHARS = 4096;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct OpenFileName
        {
            public int StructSize;
            public IntPtr Owner;
            public IntPtr Instance;
            public string? Filter;
            public string? CustomFilter;
            public int MaxCustomFilter;
            public int FilterIndex;
            public IntPtr File;
            public int MaxFile;
            public string? FileTitle;
            public int MaxFileTitle;
            public string? InitialDir;
            public string? Title;
            public int Flags;
            public short FileOffset;
            public short FileExtension;
            public string? DefaultExtension;
            public IntPtr CustomData;
            public IntPtr Hook;
            public string? TemplateName;
            public IntPtr Reserved1;
            public int Reserved2;
            public int FlagsEx;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct BrowseInfo
        {
            public IntPtr Owner;
            public IntPtr Root;
            public string? DisplayName;
            public string? Title;
            public uint Flags;
            public IntPtr Callback;
            public IntPtr LParam;
            public int Image;
        }

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetOpenFileNameW(ref OpenFileName ofn);

        [DllImport("comdlg32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetSaveFileNameW(ref OpenFileName ofn);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SHBrowseForFolder(ref BrowseInfo browseInfo);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern bool SHGetPathFromIDList(IntPtr pidl, IntPtr path);

        [DllImport("ole32.dll")]
        private static extern void CoTaskMemFree(IntPtr pointer);

        private static string[] WindowsOpenFiles(string title, string directory, string defaultName, bool multiselect, ExtensionFilter[] filters)
        {
            IntPtr buffer = AllocFileBuffer(defaultName);

            try
            {
                var ofn = new OpenFileName
                {
                    StructSize = Marshal.SizeOf(typeof(OpenFileName)),
                    Filter = BuildWindowsFilter(filters),
                    FilterIndex = 1,
                    File = buffer,
                    MaxFile = FILE_BUFFER_CHARS,
                    Title = string.IsNullOrEmpty(title) ? null : title,
                    InitialDir = string.IsNullOrEmpty(directory) ? null : directory,
                    Flags = OFN_EXPLORER | OFN_FILEMUSTEXIST | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR,
                };

                if (multiselect)
                    ofn.Flags |= OFN_ALLOWMULTISELECT;

                return GetOpenFileNameW(ref ofn)
                    ? ReadFileBuffer(buffer, FILE_BUFFER_CHARS)
                    : Array.Empty<string>();
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }

        private static string WindowsSaveFile(string title, string directory, string defaultName, ExtensionFilter[] filters)
        {
            IntPtr buffer = AllocFileBuffer(defaultName);

            try
            {
                var ofn = new OpenFileName
                {
                    StructSize = Marshal.SizeOf(typeof(OpenFileName)),
                    Filter = BuildWindowsFilter(filters),
                    FilterIndex = 1,
                    File = buffer,
                    MaxFile = FILE_BUFFER_CHARS,
                    Title = string.IsNullOrEmpty(title) ? null : title,
                    InitialDir = string.IsNullOrEmpty(directory) ? null : directory,
                    Flags = OFN_EXPLORER | OFN_PATHMUSTEXIST | OFN_NOCHANGEDIR | OFN_OVERWRITEPROMPT,
                    DefaultExtension = FirstBareExtension(filters),
                };

                return GetSaveFileNameW(ref ofn)
                    ? Marshal.PtrToStringUni(buffer) ?? string.Empty
                    : string.Empty;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }

        private static string WindowsOpenFolder(string title)
        {
            var browseInfo = new BrowseInfo
            {
                Title = title,
                Flags = BIF_RETURNONLYFSDIRS | BIF_NEWDIALOGSTYLE,
            };

            IntPtr pidl = SHBrowseForFolder(ref browseInfo);

            if (pidl == IntPtr.Zero)
                return string.Empty;

            IntPtr pathBuffer = Marshal.AllocHGlobal(PATH_BUFFER_CHARS * 2);

            try
            {
                return SHGetPathFromIDList(pidl, pathBuffer)
                    ? Marshal.PtrToStringUni(pathBuffer) ?? string.Empty
                    : string.Empty;
            }
            finally
            {
                Marshal.FreeHGlobal(pathBuffer);
                CoTaskMemFree(pidl);
            }
        }

        private static IntPtr AllocFileBuffer(string defaultName)
        {
            IntPtr buffer = Marshal.AllocHGlobal(FILE_BUFFER_CHARS * 2);

            if (string.IsNullOrEmpty(defaultName))
            {
                Marshal.WriteInt16(buffer, 0, 0);
                return buffer;
            }

            byte[] seed = Encoding.Unicode.GetBytes(defaultName + "\0");
            Marshal.Copy(seed, 0, buffer, Math.Min(seed.Length, FILE_BUFFER_CHARS * 2));
            return buffer;
        }

        // The multi-select buffer is a run of null-separated UTF-16 strings ending in a double null.
        // A single entry is a full path; several entries are a directory followed by bare file names.
        internal static string[] ReadFileBuffer(IntPtr buffer, int maxChars)
        {
            var parts = new List<string>();
            var current = new StringBuilder();

            for (int i = 0; i < maxChars; i++)
            {
                var character = (char)Marshal.ReadInt16(buffer, i * 2);

                if (character == '\0')
                {
                    if (current.Length == 0) break;

                    parts.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(character);
            }

            if (parts.Count <= 1)
                return parts.ToArray();

            string root = parts[0];
            var files = new string[parts.Count - 1];

            for (int i = 1; i < parts.Count; i++)
                files[i - 1] = Path.Combine(root, parts[i]);

            return files;
        }

        internal static string BuildWindowsFilter(ExtensionFilter[]? filters)
        {
            var builder = new StringBuilder();

            if (filters != null)
            {
                foreach (ExtensionFilter filter in filters)
                {
                    string patterns = ToWindowsPatterns(BuildGlobPatterns(filter.Extensions, ";"));
                    if (string.IsNullOrEmpty(patterns)) continue;

                    string name = string.IsNullOrEmpty(filter.Name) ? DEFAULT_FILTER_NAME : filter.Name;
                    builder.Append(name).Append(" (").Append(patterns).Append(')').Append('\0');
                    builder.Append(patterns).Append('\0');
                }
            }

            builder.Append("All Files (*.*)").Append('\0').Append("*.*").Append('\0').Append('\0');
            return builder.ToString();
        }

        // Win32 filters spell "any file" as "*.*"; the glob form uses a bare "*".
        private static string ToWindowsPatterns(string globPatterns)
        {
            if (string.IsNullOrEmpty(globPatterns))
                return globPatterns;

            string[] patterns = globPatterns.Split(';');

            for (int i = 0; i < patterns.Length; i++)
            {
                if (patterns[i] == "*")
                    patterns[i] = "*.*";
            }

            return string.Join(";", patterns);
        }

        private static string? FirstBareExtension(ExtensionFilter[]? filters)
        {
            if (filters == null)
                return null;

            foreach (ExtensionFilter filter in filters)
            {
                if (filter.Extensions == null) continue;

                foreach (string extension in filter.Extensions)
                {
                    string bare = BareExtension(extension);
                    if (bare.Length > 0) return bare;
                }
            }

            return null;
        }

        // ---- shared helpers ------------------------------------------------------------------------

        internal static string BuildGlobPatterns(string[]? extensions, string separator)
        {
            if (extensions == null || extensions.Length == 0)
                return string.Empty;

            var builder = new StringBuilder();
            bool first = true;

            foreach (string extension in extensions)
            {
                if (!first) builder.Append(separator);
                builder.Append(NormalizeGlob(extension));
                first = false;
            }

            return builder.ToString();
        }

        internal static string NormalizeGlob(string? extension)
        {
            if (extension is not { } raw || string.IsNullOrWhiteSpace(raw))
                return "*";

            string value = raw.Trim();

            if (value == "*" || value == "*.*" || value == ".*")
                return "*";

            if (value.StartsWith("*.", StringComparison.Ordinal))
                return value;

            if (value.StartsWith(".", StringComparison.Ordinal))
                return "*" + value;

            return "*." + value;
        }

        /// <summary>Widens every letter of a glob to a two-case bracket class, so "*.png" also matches "*.PNG" under case-sensitive matchers.</summary>
        internal static string CaseInsensitiveGlob(string glob)
        {
            var builder = new StringBuilder(glob.Length * 4);

            foreach (char character in glob)
            {
                char lower = char.ToLowerInvariant(character);
                char upper = char.ToUpperInvariant(character);

                if (lower == upper)
                    builder.Append(character);
                else
                    builder.Append('[').Append(lower).Append(upper).Append(']');
            }

            return builder.ToString();
        }

        internal static string BareExtension(string? extension)
        {
            if (extension is not { } raw || string.IsNullOrWhiteSpace(raw))
                return string.Empty;

            string value = raw.Trim();

            if (value.StartsWith("*.", StringComparison.Ordinal))
                value = value.Substring(2);
            else if (value.StartsWith(".", StringComparison.Ordinal))
                value = value.Substring(1);

            return value == "*" ? string.Empty : value;
        }

        internal static string BuildInitialPath(string directory, string defaultName)
        {
            bool hasDirectory = !string.IsNullOrEmpty(directory);
            bool hasName = !string.IsNullOrEmpty(defaultName);

            if (hasDirectory && hasName)
                return Path.Combine(directory, defaultName);

            if (hasDirectory)
                return directory.EndsWith("/", StringComparison.Ordinal) ? directory : directory + "/";

            return hasName ? defaultName : string.Empty;
        }

        internal static string[] SplitLines(string? text)
        {
            if (text is not { Length: > 0 } content)
                return Array.Empty<string>();

            string[] raw = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            var lines = new List<string>(raw.Length);

            foreach (string line in raw)
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0) lines.Add(trimmed);
            }

            return lines.ToArray();
        }

        // No shell is involved (UseShellExecute is false and each argument is passed verbatim), so
        // titles, directories and patterns need no quoting or escaping. A missing binary surfaces as a
        // Win32Exception and returns false so the caller can try the next backend.
        internal static bool TryRun(string fileName, IReadOnlyList<string> arguments, out int exitCode, out string standardOutput)
        {
            exitCode = -1;
            standardOutput = string.Empty;

            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            foreach (string argument in arguments)
                startInfo.ArgumentList.Add(argument);

            Process process;

            try { process = Process.Start(startInfo) ?? throw new InvalidOperationException($"'{fileName}' started no process."); }
            catch (Win32Exception) { return false; }
            catch (Exception e)
            {
                ReportFailure($"Failed to launch '{fileName}': {e.Message}");
                return false;
            }

            using (process)
            {
                Task<string> errorTask = process.StandardError.ReadToEndAsync();
                standardOutput = process.StandardOutput.ReadToEnd();
                process.WaitForExit();
                errorTask.Wait();
                exitCode = process.ExitCode;
            }

            return true;
        }
    }
}

namespace Crosstales
{
    /// <summary>Byte-array helpers the original package exposed for turning picked image files into Unity assets.</summary>
    public static class CTExtensions
    {
        public static Texture2D? CTToTexture(this byte[]? data)
        {
            if (data == null || data.Length == 0)
                return null;

            var texture = new Texture2D(2, 2);
            texture.LoadImage(data);
            return texture;
        }

        public static Sprite? CTToSprite(this byte[]? data, Texture2D? texture)
        {
            if (data == null || data.Length == 0 || texture == null)
                return null;

            texture.LoadImage(data);
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
    }
}
