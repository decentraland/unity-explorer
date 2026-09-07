// Clean-room replacement for the `com.decentraland.filebrowserpro` package.
//
// It reproduces only the consumer-facing surface unity-explorer relies on
// (Crosstales.FB.FileBrowser singleton + Crosstales.CTExtensions) and backs it
// with the host OS native file dialog: zenity/kdialog on Linux, the Win32
// common dialogs on Windows, and NSOpenPanel/NSSavePanel (through osascript) on
// macOS. The platform is chosen at runtime so every branch is compiled on every
// target; on Linux the zenity/kdialog path is the one exercised in practice.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

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
        ///     Permits blocking (synchronous) dialog calls. Desktop platforms can always block, so the
        ///     synchronous methods honor the request there; the flag exists to preserve the original
        ///     package's opt-in contract for callers that toggle it before a synchronous pick.
        /// </summary>
        public bool AllowSyncCalls;

        /// <summary>Raw bytes of the file returned by the most recent <see cref="OpenSingleFile()" /> call, or null.</summary>
        public byte[] CurrentOpenSingleFileData { get; private set; }

        private FileBrowser() { }

        public string OpenSingleFile() =>
            OpenSingleFile("Open File", string.Empty, string.Empty, Array.Empty<string>());

        public string OpenSingleFile(string title, params string[] extensions) =>
            OpenSingleFile(title, string.Empty, string.Empty, extensions);

        public string OpenSingleFile(string title, string directory, string defaultName, params string[] extensions) =>
            OpenSingleFileInternal(title, directory, defaultName, FiltersFromExtensions(extensions));

        public string OpenSingleFile(string title, string directory, string defaultName, ExtensionFilter[] extensions) =>
            OpenSingleFileInternal(title, directory, defaultName, extensions);

        public void OpenSingleFileAsync(Action<string> callback, string title, string directory, string defaultName, params string[] extensions) =>
            RunAsync(() => OpenSingleFileInternal(title, directory, defaultName, FiltersFromExtensions(extensions)), callback);

        public string[] OpenFiles(string title, string directory, string defaultName, bool multiselect, params string[] extensions) =>
            NativeFileDialog.OpenFiles(title, directory, defaultName, multiselect, FiltersFromExtensions(extensions));

        public string[] OpenFiles(string title, string directory, string defaultName, bool multiselect, ExtensionFilter[] extensions) =>
            NativeFileDialog.OpenFiles(title, directory, defaultName, multiselect, extensions);

        public void OpenFilesAsync(Action<string[]> callback, string title, string directory, string defaultName, bool multiselect, params string[] extensions) =>
            RunAsync(() => NativeFileDialog.OpenFiles(title, directory, defaultName, multiselect, FiltersFromExtensions(extensions)), callback);

        public string OpenSingleFolder() =>
            OpenSingleFolder("Select Folder", string.Empty);

        public string OpenSingleFolder(string title, string directory)
        {
            string[] folders = NativeFileDialog.OpenFolders(title, directory, false);
            return folders.Length > 0 ? folders[0] : null;
        }

        public string[] OpenFolders(string title, string directory, bool multiselect) =>
            NativeFileDialog.OpenFolders(title, directory, multiselect);

        public void OpenSingleFolderAsync(Action<string> callback, string title, string directory) =>
            RunAsync(() =>
            {
                string[] folders = NativeFileDialog.OpenFolders(title, directory, false);
                return folders.Length > 0 ? folders[0] : null;
            }, callback);

        public void OpenFoldersAsync(Action<string[]> callback, string title, string directory, bool multiselect) =>
            RunAsync(() => NativeFileDialog.OpenFolders(title, directory, multiselect), callback);

        public string SaveFile() =>
            SaveFile("Save File", string.Empty, string.Empty, Array.Empty<string>());

        public string SaveFile(string title, string directory, string defaultName, params string[] extensions) =>
            NativeFileDialog.SaveFile(title, directory, defaultName, FiltersFromExtensions(extensions));

        public string SaveFile(string title, string directory, string defaultName, ExtensionFilter[] extensions) =>
            NativeFileDialog.SaveFile(title, directory, defaultName, extensions);

        public void SaveFileAsync(Action<string> callback, string title, string directory, string defaultName, params string[] extensions) =>
            RunAsync(() => NativeFileDialog.SaveFile(title, directory, defaultName, FiltersFromExtensions(extensions)), callback);

        private string OpenSingleFileInternal(string title, string directory, string defaultName, ExtensionFilter[] filters)
        {
            string[] chosen = NativeFileDialog.OpenFiles(title, directory, defaultName, false, filters);
            string path = chosen.Length > 0 ? chosen[0] : null;
            CurrentOpenSingleFileData = ReadBytesSafe(path);
            return path;
        }

        private static ExtensionFilter[] FiltersFromExtensions(string[] extensions)
        {
            if (extensions == null || extensions.Length == 0)
                return Array.Empty<ExtensionFilter>();

            // A lone wildcard means "any file" — no meaningful group to show.
            if (extensions.Length == 1 && (string.IsNullOrWhiteSpace(extensions[0]) || extensions[0] == "*" || extensions[0] == "*.*"))
                return Array.Empty<ExtensionFilter>();

            return new[] { new ExtensionFilter("Files", extensions) };
        }

        private static byte[] ReadBytesSafe(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            try { return File.ReadAllBytes(path); }
            catch (Exception e)
            {
                Debug.LogError($"[Crosstales.FB] Failed to read '{path}': {e.Message}");
                return null;
            }
        }

        // Runs the (blocking) dialog on a worker thread and marshals the callback back to the thread
        // that issued the call — the Unity main thread when its SynchronizationContext is installed.
        private static void RunAsync<T>(Func<T> work, Action<T> callback)
        {
            if (callback == null)
                return;

            SynchronizationContext context = SynchronizationContext.Current;

            Task.Run(() =>
            {
                T result;

                try { result = work(); }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    result = default;
                }

                if (context != null)
                    context.Post(_ => callback(result), null);
                else
                    callback(result);
            });
        }
    }

    // Cross-platform native dialog backend. Every branch compiles on every target; the running OS,
    // resolved at runtime, decides which one executes, so the Win32 P/Invoke declarations are inert
    // on non-Windows hosts and the osascript path is inert off macOS.
    internal static class NativeFileDialog
    {
        private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        private static bool IsMacOs => RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

        public static string[] OpenFiles(string title, string directory, string defaultName, bool multiselect, ExtensionFilter[] filters)
        {
            if (IsWindows)
                return WindowsOpenFiles(title, directory, defaultName, multiselect, filters);

            if (IsMacOs)
                return MacOpenFiles(title, directory, multiselect, filters);

            return LinuxOpenFiles(title, directory, defaultName, multiselect, filters);
        }

        public static string[] OpenFolders(string title, string directory, bool multiselect)
        {
            if (IsWindows)
            {
                string folder = WindowsOpenFolder(title);
                return string.IsNullOrEmpty(folder) ? Array.Empty<string>() : new[] { folder };
            }

            if (IsMacOs)
                return MacOpenFolders(title, directory, multiselect);

            return LinuxOpenFolders(title, directory, multiselect);
        }

        public static string SaveFile(string title, string directory, string defaultName, ExtensionFilter[] filters)
        {
            if (IsWindows)
            {
                string path = WindowsSaveFile(title, directory, defaultName, filters);
                return string.IsNullOrEmpty(path) ? null : path;
            }

            if (IsMacOs)
                return MacSaveFile(title, directory, defaultName);

            return LinuxSaveFile(title, directory, defaultName, filters);
        }

        // ---- Linux (zenity, with kdialog fallback) -------------------------------------------------

        private static string[] LinuxOpenFiles(string title, string directory, string defaultName, bool multiselect, ExtensionFilter[] filters)
        {
            var zenity = new List<string> { "--file-selection" };
            AppendZenityCommon(zenity, title, BuildInitialPath(directory, defaultName), multiselect);
            AppendZenityFilters(zenity, filters);

            if (TryRun("zenity", zenity, out int code, out string output))
                return code == 0 ? SplitLines(output) : Array.Empty<string>();

            var kdialog = new List<string> { "--getopenfilename", KdialogStartDir(directory) };
            string kdialogFilter = BuildKdialogFilter(filters);
            if (!string.IsNullOrEmpty(kdialogFilter)) kdialog.Add(kdialogFilter);
            if (multiselect) { kdialog.Add("--multiple"); kdialog.Add("--separate-output"); }
            AppendKdialogTitle(kdialog, title);

            if (TryRun("kdialog", kdialog, out code, out output))
                return code == 0 ? SplitLines(output) : Array.Empty<string>();

            LogNoBackend();
            return Array.Empty<string>();
        }

        private static string[] LinuxOpenFolders(string title, string directory, bool multiselect)
        {
            var zenity = new List<string> { "--file-selection", "--directory" };
            AppendZenityCommon(zenity, title, BuildInitialPath(directory, string.Empty), multiselect);

            if (TryRun("zenity", zenity, out int code, out string output))
                return code == 0 ? SplitLines(output) : Array.Empty<string>();

            var kdialog = new List<string> { "--getexistingdirectory", KdialogStartDir(directory) };
            AppendKdialogTitle(kdialog, title);

            if (TryRun("kdialog", kdialog, out code, out output))
                return code == 0 ? SplitLines(output) : Array.Empty<string>();

            LogNoBackend();
            return Array.Empty<string>();
        }

        private static string LinuxSaveFile(string title, string directory, string defaultName, ExtensionFilter[] filters)
        {
            var zenity = new List<string> { "--file-selection", "--save" };
            AppendZenityCommon(zenity, title, BuildInitialPath(directory, defaultName), false);
            AppendZenityFilters(zenity, filters);

            if (TryRun("zenity", zenity, out int code, out string output))
                return code == 0 ? FirstOrNull(SplitLines(output)) : null;

            string initialPath = BuildInitialPath(directory, defaultName);
            var kdialog = new List<string> { "--getsavefilename", initialPath.Length > 0 ? initialPath : KdialogStartDir(directory) };
            string kdialogFilter = BuildKdialogFilter(filters);
            if (!string.IsNullOrEmpty(kdialogFilter)) kdialog.Add(kdialogFilter);
            AppendKdialogTitle(kdialog, title);

            if (TryRun("kdialog", kdialog, out code, out output))
                return code == 0 ? FirstOrNull(SplitLines(output)) : null;

            LogNoBackend();
            return null;
        }

        private static void AppendZenityCommon(List<string> args, string title, string initialPath, bool multiselect)
        {
            if (!string.IsNullOrEmpty(title)) args.Add("--title=" + title);
            if (!string.IsNullOrEmpty(initialPath)) args.Add("--filename=" + initialPath);

            if (multiselect)
            {
                args.Add("--multiple");
                args.Add("--separator=\n");
            }
        }

        private static void AppendZenityFilters(List<string> args, ExtensionFilter[] filters)
        {
            if (filters == null || filters.Length == 0)
                return;

            foreach (ExtensionFilter filter in filters)
            {
                string patterns = BuildGlobPatterns(filter.Extensions, " ");
                if (string.IsNullOrEmpty(patterns)) continue;

                string name = string.IsNullOrEmpty(filter.Name) ? "Files" : filter.Name;
                args.Add("--file-filter=" + name + " | " + patterns);
            }

            args.Add("--file-filter=All files | *");
        }

        private static string BuildKdialogFilter(ExtensionFilter[] filters)
        {
            if (filters == null || filters.Length == 0)
                return string.Empty;

            var groups = new List<string>();

            foreach (ExtensionFilter filter in filters)
            {
                string patterns = BuildGlobPatterns(filter.Extensions, " ");
                if (string.IsNullOrEmpty(patterns)) continue;

                string name = string.IsNullOrEmpty(filter.Name) ? "Files" : filter.Name;
                groups.Add(patterns + "|" + name);
            }

            if (groups.Count == 0)
                return string.Empty;

            groups.Add("*|All Files");
            return string.Join("\n", groups);
        }

        private static void AppendKdialogTitle(List<string> args, string title)
        {
            if (string.IsNullOrEmpty(title))
                return;

            args.Add("--title");
            args.Add(title);
        }

        private static string KdialogStartDir(string directory)
        {
            if (!string.IsNullOrEmpty(directory))
                return directory;

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrEmpty(home) ? "." : home;
        }

        private static void LogNoBackend() =>
            Debug.LogError("[Crosstales.FB] No supported native file dialog found. Install 'zenity' or 'kdialog'.");

        // ---- macOS (osascript / Cocoa panels) -----------------------------------------------------

        private static string[] MacOpenFiles(string title, string directory, bool multiselect, ExtensionFilter[] filters) =>
            MacChoose("file", "theFiles", "aFile", title, MacTypeClause(filters) + MacLocationClause(directory), multiselect);

        private static string[] MacOpenFolders(string title, string directory, bool multiselect) =>
            MacChoose("folder", "theFolders", "aFolder", title, MacLocationClause(directory), multiselect);

        // Shared AppleScript shape for `choose file`/`choose folder`: a single pick returns one POSIX
        // path expression, a multi-select pick collects each POSIX path from the chosen list into a
        // newline-joined "theOutput" string. `collectionVar`/`itemVar` only need to be distinct AppleScript
        // identifiers between the file and folder callers; they don't otherwise affect behavior.
        private static string[] MacChoose(string kind, string collectionVar, string itemVar, string title, string extraClause, bool multiselect)
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
        private static string MacSaveFile(string title, string directory, string defaultName)
        {
            var script = new StringBuilder();
            script.Append("POSIX path of (choose file name with prompt ").Append(AppleQuote(title));
            if (!string.IsNullOrEmpty(defaultName)) script.Append(" default name ").Append(AppleQuote(defaultName));
            script.Append(MacLocationClause(directory)).Append(')');

            return FirstOrNull(RunOsascript(script.ToString()));
        }

        private static string MacTypeClause(ExtensionFilter[] filters)
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

        private static string[] RunOsascript(string script)
        {
            if (TryRun("osascript", new[] { "-e", script }, out int code, out string output))
                return code == 0 ? SplitLines(output) : Array.Empty<string>();

            Debug.LogError("[Crosstales.FB] 'osascript' is unavailable; cannot show a native dialog on this host.");
            return Array.Empty<string>();
        }

        private static string AppleQuote(string value)
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
            public string Filter;
            public string CustomFilter;
            public int MaxCustomFilter;
            public int FilterIndex;
            public IntPtr File;
            public int MaxFile;
            public string FileTitle;
            public int MaxFileTitle;
            public string InitialDir;
            public string Title;
            public int Flags;
            public short FileOffset;
            public short FileExtension;
            public string DefaultExtension;
            public IntPtr CustomData;
            public IntPtr Hook;
            public string TemplateName;
            public IntPtr Reserved1;
            public int Reserved2;
            public int FlagsEx;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct BrowseInfo
        {
            public IntPtr Owner;
            public IntPtr Root;
            public string DisplayName;
            public string Title;
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
                    ? Marshal.PtrToStringUni(buffer)
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
        private static string[] ReadFileBuffer(IntPtr buffer, int maxChars)
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

        private static string BuildWindowsFilter(ExtensionFilter[] filters)
        {
            var builder = new StringBuilder();

            if (filters != null)
            {
                foreach (ExtensionFilter filter in filters)
                {
                    string patterns = ToWindowsPatterns(BuildGlobPatterns(filter.Extensions, ";"));
                    if (string.IsNullOrEmpty(patterns)) continue;

                    string name = string.IsNullOrEmpty(filter.Name) ? "Files" : filter.Name;
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

        private static string FirstBareExtension(ExtensionFilter[] filters)
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

        private static string BuildGlobPatterns(string[] extensions, string separator)
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

        private static string NormalizeGlob(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return "*";

            string value = extension.Trim();

            if (value == "*" || value == "*.*" || value == ".*")
                return "*";

            if (value.StartsWith("*.", StringComparison.Ordinal))
                return value;

            if (value.StartsWith(".", StringComparison.Ordinal))
                return "*" + value;

            return "*." + value;
        }

        private static string BareExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return string.Empty;

            string value = extension.Trim();

            if (value.StartsWith("*.", StringComparison.Ordinal))
                value = value.Substring(2);
            else if (value.StartsWith(".", StringComparison.Ordinal))
                value = value.Substring(1);

            return value == "*" ? string.Empty : value;
        }

        private static string BuildInitialPath(string directory, string defaultName)
        {
            bool hasDirectory = !string.IsNullOrEmpty(directory);
            bool hasName = !string.IsNullOrEmpty(defaultName);

            if (hasDirectory && hasName)
                return Path.Combine(directory, defaultName);

            if (hasDirectory)
                return directory.EndsWith("/", StringComparison.Ordinal) ? directory : directory + "/";

            return hasName ? defaultName : string.Empty;
        }

        private static string FirstOrNull(string[] values) =>
            values.Length > 0 ? values[0] : null;

        private static string[] SplitLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return Array.Empty<string>();

            string[] raw = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
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
        private static bool TryRun(string fileName, IReadOnlyList<string> arguments, out int exitCode, out string standardOutput)
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

            try { process = Process.Start(startInfo); }
            catch (Win32Exception) { return false; }
            catch (Exception e)
            {
                Debug.LogError($"[Crosstales.FB] Failed to launch '{fileName}': {e.Message}");
                return false;
            }

            if (process == null)
                return false;

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
        public static Texture2D CTToTexture(this byte[] data)
        {
            if (data == null || data.Length == 0)
                return null;

            var texture = new Texture2D(2, 2);
            texture.LoadImage(data);
            return texture;
        }

        public static Sprite CTToSprite(this byte[] data, Texture2D texture)
        {
            if (data == null || data.Length == 0 || texture == null)
                return null;

            texture.LoadImage(data);
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
    }
}
