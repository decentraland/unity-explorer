using System;
using System.Collections.Generic;

namespace Crosstales.FB.Linux
{
    /// <summary>
    ///     Linux dialog backends in preference order: the xdg-desktop-portal file chooser (the only backend reachable from
    ///     inside a Flatpak sandbox), then zenity, then kdialog. A backend that answers, whether with a pick or a cancel,
    ///     ends the chain; only an unavailable backend hands over to the next one.
    /// </summary>
    internal static class LinuxFileDialog
    {
        internal delegate bool ProcessRunner(string fileName, IReadOnlyList<string> arguments, out int exitCode, out string standardOutput);

        internal delegate PortalResult PortalRunner(in PortalRequest request);

        public const string ZENITY = "zenity";
        public const string KDIALOG = "kdialog";
        private const string PORTAL_LABEL = "portal";
        private const string UNAVAILABLE = "unavailable";

        internal static ProcessRunner runProcess = NativeFileDialog.TryRun;
        internal static PortalRunner runPortal = PortalFileChooser.Run;

        public static DialogOutcome OpenFiles(string title, string directory, string defaultName, bool multiselect, ExtensionFilter[] filters)
        {
            var reasons = new List<string>(3);
            PortalResult portal = runPortal(new PortalRequest(title, directory, defaultName, multiselect, false, false, filters));

            if (portal.Answered)
                return DialogOutcome.From(portal.Paths);

            reasons.Add($"{PORTAL_LABEL}: {portal.Reason}");

            var zenity = new List<string> { "--file-selection" };
            AppendZenityCommon(zenity, title, NativeFileDialog.BuildInitialPath(directory, defaultName), multiselect);
            AppendZenityFilters(zenity, filters);

            if (runProcess(ZENITY, zenity, out int code, out string output))
                return code == 0 ? DialogOutcome.From(NativeFileDialog.SplitLines(output)) : DialogOutcome.CANCELLED;

            reasons.Add($"{ZENITY}: {UNAVAILABLE}");

            var kdialog = new List<string> { "--getopenfilename", KdialogStartDir(directory) };
            string kdialogFilter = BuildKdialogFilter(filters);

            if (!string.IsNullOrEmpty(kdialogFilter))
                kdialog.Add(kdialogFilter);

            if (multiselect)
            {
                kdialog.Add("--multiple");
                kdialog.Add("--separate-output");
            }

            AppendKdialogTitle(kdialog, title);

            if (runProcess(KDIALOG, kdialog, out code, out output))
                return code == 0 ? DialogOutcome.From(NativeFileDialog.SplitLines(output)) : DialogOutcome.CANCELLED;

            reasons.Add($"{KDIALOG}: {UNAVAILABLE}");
            return DialogOutcome.Failed(NoBackendMessage(reasons));
        }

        public static DialogOutcome OpenFolders(string title, string directory, bool multiselect)
        {
            var reasons = new List<string>(3);
            PortalResult portal = runPortal(new PortalRequest(title, directory, string.Empty, multiselect, true, false, Array.Empty<ExtensionFilter>()));

            if (portal.Answered)
                return DialogOutcome.From(portal.Paths);

            reasons.Add($"{PORTAL_LABEL}: {portal.Reason}");

            var zenity = new List<string> { "--file-selection", "--directory" };
            AppendZenityCommon(zenity, title, NativeFileDialog.BuildInitialPath(directory, string.Empty), multiselect);

            if (runProcess(ZENITY, zenity, out int code, out string output))
                return code == 0 ? DialogOutcome.From(NativeFileDialog.SplitLines(output)) : DialogOutcome.CANCELLED;

            reasons.Add($"{ZENITY}: {UNAVAILABLE}");

            var kdialog = new List<string> { "--getexistingdirectory", KdialogStartDir(directory) };
            AppendKdialogTitle(kdialog, title);

            if (runProcess(KDIALOG, kdialog, out code, out output))
                return code == 0 ? DialogOutcome.From(NativeFileDialog.SplitLines(output)) : DialogOutcome.CANCELLED;

            reasons.Add($"{KDIALOG}: {UNAVAILABLE}");
            return DialogOutcome.Failed(NoBackendMessage(reasons));
        }

        public static DialogOutcome SaveFile(string title, string directory, string defaultName, ExtensionFilter[] filters)
        {
            var reasons = new List<string>(3);
            PortalResult portal = runPortal(new PortalRequest(title, directory, defaultName, false, false, true, filters));

            if (portal.Answered)
                return DialogOutcome.From(portal.Paths);

            reasons.Add($"{PORTAL_LABEL}: {portal.Reason}");

            string initialPath = NativeFileDialog.BuildInitialPath(directory, defaultName);
            var zenity = new List<string> { "--file-selection", "--save" };
            AppendZenityCommon(zenity, title, initialPath, false);
            AppendZenityFilters(zenity, filters);

            if (runProcess(ZENITY, zenity, out int code, out string output))
                return code == 0 ? DialogOutcome.From(NativeFileDialog.SplitLines(output)) : DialogOutcome.CANCELLED;

            reasons.Add($"{ZENITY}: {UNAVAILABLE}");

            var kdialog = new List<string> { "--getsavefilename", initialPath.Length > 0 ? initialPath : KdialogStartDir(directory) };
            string kdialogFilter = BuildKdialogFilter(filters);

            if (!string.IsNullOrEmpty(kdialogFilter))
                kdialog.Add(kdialogFilter);

            AppendKdialogTitle(kdialog, title);

            if (runProcess(KDIALOG, kdialog, out code, out output))
                return code == 0 ? DialogOutcome.From(NativeFileDialog.SplitLines(output)) : DialogOutcome.CANCELLED;

            reasons.Add($"{KDIALOG}: {UNAVAILABLE}");
            return DialogOutcome.Failed(NoBackendMessage(reasons));
        }

        internal static string BuildKdialogFilter(ExtensionFilter[]? filters)
        {
            if (filters == null || filters.Length == 0)
                return string.Empty;

            var groups = new List<string>();

            foreach (ExtensionFilter filter in filters)
            {
                string patterns = NativeFileDialog.BuildGlobPatterns(filter.Extensions, " ");

                if (string.IsNullOrEmpty(patterns))
                    continue;

                string name = string.IsNullOrEmpty(filter.Name) ? NativeFileDialog.DEFAULT_FILTER_NAME : filter.Name;
                groups.Add(patterns + "|" + name);
            }

            if (groups.Count == 0)
                return string.Empty;

            groups.Add("*|All Files");
            return string.Join("\n", groups);
        }

        private static void AppendZenityCommon(List<string> args, string title, string initialPath, bool multiselect)
        {
            if (!string.IsNullOrEmpty(title))
                args.Add("--title=" + title);

            if (!string.IsNullOrEmpty(initialPath))
                args.Add("--filename=" + initialPath);

            if (!multiselect)
                return;

            args.Add("--multiple");
            args.Add("--separator=\n");
        }

        // GTK matches file-filter globs case-sensitively, so each letter is widened to both cases.
        private static void AppendZenityFilters(List<string> args, ExtensionFilter[]? filters)
        {
            if (filters == null || filters.Length == 0)
                return;

            foreach (ExtensionFilter filter in filters)
            {
                if (filter.Extensions == null || filter.Extensions.Length == 0)
                    continue;

                var patterns = new List<string>(filter.Extensions.Length);

                foreach (string extension in filter.Extensions)
                    patterns.Add(NativeFileDialog.CaseInsensitiveGlob(NativeFileDialog.NormalizeGlob(extension)));

                string name = string.IsNullOrEmpty(filter.Name) ? NativeFileDialog.DEFAULT_FILTER_NAME : filter.Name;
                args.Add("--file-filter=" + name + " | " + string.Join(" ", patterns));
            }

            args.Add("--file-filter=" + PortalFileChooser.ALL_FILES_LABEL + " | *");
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

        private static string NoBackendMessage(List<string> reasons) =>
            $"No native file dialog could be shown ({string.Join("; ", reasons)}). Install xdg-desktop-portal with a desktop backend, {ZENITY}, or {KDIALOG}.";
    }
}
