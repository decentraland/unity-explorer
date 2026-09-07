using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Crosstales.FB.Linux
{
    /// <summary>What a caller wants from the portal's file chooser, independent of the D-Bus encoding.</summary>
    internal readonly struct PortalRequest
    {
        public readonly string Title;
        public readonly string Directory;
        public readonly string DefaultName;
        public readonly bool Multiple;
        public readonly bool SelectDirectory;
        public readonly bool Save;
        public readonly ExtensionFilter[] Filters;

        public PortalRequest(string title, string directory, string defaultName, bool multiple, bool selectDirectory, bool save, ExtensionFilter[] filters)
        {
            Title = title;
            Directory = directory;
            DefaultName = defaultName;
            Multiple = multiple;
            SelectDirectory = selectDirectory;
            Save = save;
            Filters = filters;
        }
    }

    /// <summary>
    ///     Outcome of a portal request. <see cref="Answered" /> means a dialog was shown and the user picked or backed out;
    ///     otherwise <see cref="Reason" /> says why no portal could take the request.
    /// </summary>
    internal readonly struct PortalResult
    {
        public static readonly PortalResult CANCELLED = new (true, Array.Empty<string>(), null);

        public readonly bool Answered;
        public readonly string[] Paths;
        public readonly string? Reason;

        private PortalResult(bool answered, string[] paths, string? reason)
        {
            Answered = answered;
            Paths = paths;
            Reason = reason;
        }

        public static PortalResult Picked(string[] paths) =>
            new (true, paths, null);

        public static PortalResult Unavailable(string reason) =>
            new (false, Array.Empty<string>(), reason);
    }

    /// <summary>
    ///     Client for <c>org.freedesktop.portal.FileChooser</c> (interface version 4) over the session bus. The portal is the
    ///     dialog backend that exists inside a Flatpak sandbox, where zenity/kdialog are not on the path and the returned
    ///     document-portal paths are readable from within the sandbox.
    /// </summary>
    internal static class PortalFileChooser
    {
        public const string BUS_NAME = "org.freedesktop.portal.Desktop";
        public const string OBJECT_PATH = "/org/freedesktop/portal/desktop";
        public const string FILE_CHOOSER_INTERFACE = "org.freedesktop.portal.FileChooser";
        public const string REQUEST_INTERFACE = "org.freedesktop.portal.Request";
        public const string REQUEST_PATH_PREFIX = "/org/freedesktop/portal/desktop/request/";
        public const string OPEN_FILE = "OpenFile";
        public const string SAVE_FILE = "SaveFile";
        public const string RESPONSE = "Response";
        public const string NAME_OWNER_CHANGED = "NameOwnerChanged";
        public const string CALL_SIGNATURE = "ssa{sv}";
        public const string RESPONSE_SIGNATURE = "ua{sv}";
        public const string URIS_KEY = "uris";
        public const string ALL_FILES_LABEL = "All files";
        public const uint RESPONSE_SUCCESS = 0;
        public const uint FILTER_GLOB = 0;

        private const string TOKEN_PREFIX = "dcl_fb_";
        private const string FILE_SCHEME = "file:";
        private static readonly TimeSpan CALL_TIMEOUT = TimeSpan.FromSeconds(30);

        private static int tokenCounter;

        public static PortalResult Run(in PortalRequest request) =>
            Run(request, DBusAddress.SessionBus());

        public static PortalResult Run(in PortalRequest request, string? busAddress)
        {
            if (busAddress is not { Length: > 0 } address)
                return PortalResult.Unavailable("no session bus address");

            try
            {
                using DBusConnection connection = DBusConnection.Connect(address, CALL_TIMEOUT);
                return Exchange(connection, request);
            }
            catch (SocketException e) { return PortalResult.Unavailable($"bus connection failed ({e.SocketErrorCode})"); }
            catch (DBusException e) { return PortalResult.Unavailable(e.Message); }
            catch (IOException e) { return PortalResult.Unavailable(e.Message); }
        }

        /// <summary>The request object path the portal will use for a caller with this unique name and handle token.</summary>
        public static string PredictHandle(string uniqueName, string token) =>
            REQUEST_PATH_PREFIX + uniqueName.TrimStart(':').Replace('.', '_') + "/" + token;

        public static string ResponseMatchRule(string handle) =>
            $"type='signal',interface='{REQUEST_INTERFACE}',member='{RESPONSE}',path='{handle}'";

        public static string PortalOwnerMatchRule() =>
            $"type='signal',sender='{DBusConnection.BUS_NAME}',interface='{DBusConnection.BUS_INTERFACE}',member='{NAME_OWNER_CHANGED}',arg0='{BUS_NAME}'";

        public static DBusMessage BuildCall(in PortalRequest request, string token)
        {
            var body = new DBusWriter();
            body.WriteString(string.Empty);
            body.WriteString(request.Title ?? string.Empty);
            WriteOptions(body, request, token);

            DBusMessage call = DBusMessage.MethodCall(BUS_NAME, OBJECT_PATH, FILE_CHOOSER_INTERFACE, request.Save ? SAVE_FILE : OPEN_FILE);
            call.Signature = CALL_SIGNATURE;
            call.Body = body.ToArray();
            return call;
        }

        public static PortalResult ParseResponse(DBusMessage response)
        {
            DBusReader reader = response.OpenBody();
            uint code = reader.ReadUInt32();

            if (code != RESPONSE_SUCCESS)
                return PortalResult.CANCELLED;

            if (reader.ReadValue("a{sv}") is Dictionary<string, object> results && results.TryGetValue(URIS_KEY, out object? uris) && uris is string[] list)
                return PortalResult.Picked(UrisToPaths(list));

            return PortalResult.CANCELLED;
        }

        public static string[] UrisToPaths(IReadOnlyList<string> uris)
        {
            var paths = new List<string>(uris.Count);

            foreach (string uri in uris)
            {
                if (UriToPath(uri) is { } path)
                    paths.Add(path);
            }

            return paths.ToArray();
        }

        /// <summary>Decodes a <c>file:</c> URI (optionally carrying an authority) into a filesystem path; other schemes yield null.</summary>
        public static string? UriToPath(string uri)
        {
            if (!uri.StartsWith(FILE_SCHEME, StringComparison.OrdinalIgnoreCase))
                return null;

            string rest = uri.Substring(FILE_SCHEME.Length);

            if (rest.StartsWith("//", StringComparison.Ordinal))
            {
                rest = rest.Substring(2);
                int slash = rest.IndexOf('/');

                if (slash < 0)
                    return null;

                rest = rest.Substring(slash);
            }

            return Uri.UnescapeDataString(rest);
        }

        private static PortalResult Exchange(DBusConnection connection, in PortalRequest request)
        {
            string token = NextToken();
            string predictedHandle = PredictHandle(connection.UniqueName, token);
            connection.AddMatch(ResponseMatchRule(predictedHandle), CALL_TIMEOUT);
            connection.AddMatch(PortalOwnerMatchRule(), CALL_TIMEOUT);

            DBusMessage reply = connection.Call(BuildCall(request, token), CALL_TIMEOUT);

            if (reply.Type == DBusMessageType.Error)
                return PortalResult.Unavailable(DescribeError(reply));

            string handle = reply.Signature == "o" ? reply.OpenBody().ReadObjectPath() : predictedHandle;

            if (handle != predictedHandle)
                connection.AddMatch(ResponseMatchRule(handle), CALL_TIMEOUT);

            DBusMessage message = connection.WaitFor(candidate => IsResponse(candidate, handle, predictedHandle) || PortalLeftTheBus(candidate), null);

            return message.Member == RESPONSE
                ? ParseResponse(message)
                : PortalResult.Unavailable("portal left the bus before answering");
        }

        private static bool IsResponse(DBusMessage message, string handle, string predictedHandle) =>
            message.Type == DBusMessageType.Signal
            && message.Interface == REQUEST_INTERFACE
            && message.Member == RESPONSE
            && (message.Path == handle || message.Path == predictedHandle);

        private static bool PortalLeftTheBus(DBusMessage message)
        {
            if (message.Type != DBusMessageType.Signal || message.Interface != DBusConnection.BUS_INTERFACE || message.Member != NAME_OWNER_CHANGED || message.Signature != "sss")
                return false;

            DBusReader reader = message.OpenBody();
            string name = reader.ReadString();
            reader.ReadString();
            string newOwner = reader.ReadString();
            return name == BUS_NAME && newOwner.Length == 0;
        }

        private static void WriteOptions(DBusWriter body, in PortalRequest request, string token)
        {
            DBusWriter.ArrayScope options = body.BeginArray(8);
            WriteStringOption(body, "handle_token", token);
            WriteBoolOption(body, "modal", true);

            if (request.Save)
            {
                if (!string.IsNullOrEmpty(request.DefaultName))
                    WriteStringOption(body, "current_name", request.DefaultName);
            }
            else
            {
                WriteBoolOption(body, "multiple", request.Multiple);

                if (request.SelectDirectory)
                    WriteBoolOption(body, "directory", true);
            }

            if (!string.IsNullOrEmpty(request.Directory))
            {
                body.BeginStruct();
                body.WriteString("current_folder");
                body.WriteVariantByteArray(NulTerminated(request.Directory));
            }

            if (!request.SelectDirectory)
                WriteFilters(body, request.Filters);

            body.EndArray(options);
        }

        private static void WriteFilters(DBusWriter body, ExtensionFilter[]? filters)
        {
            if (filters == null || filters.Length == 0)
                return;

            body.BeginStruct();
            body.WriteString("filters");
            body.WriteSignature("a(sa(us))");
            DBusWriter.ArrayScope list = body.BeginArray(8);

            foreach (ExtensionFilter filter in filters)
            {
                if (filter.Extensions == null || filter.Extensions.Length == 0)
                    continue;

                body.BeginStruct();
                body.WriteString(string.IsNullOrEmpty(filter.Name) ? NativeFileDialog.DEFAULT_FILTER_NAME : filter.Name);
                DBusWriter.ArrayScope patterns = body.BeginArray(8);

                foreach (string extension in filter.Extensions)
                    WriteGlob(body, NativeFileDialog.CaseInsensitiveGlob(NativeFileDialog.NormalizeGlob(extension)));

                body.EndArray(patterns);
            }

            body.BeginStruct();
            body.WriteString(ALL_FILES_LABEL);
            DBusWriter.ArrayScope any = body.BeginArray(8);
            WriteGlob(body, "*");
            body.EndArray(any);

            body.EndArray(list);
        }

        private static void WriteGlob(DBusWriter body, string pattern)
        {
            body.BeginStruct();
            body.WriteUInt32(FILTER_GLOB);
            body.WriteString(pattern);
        }

        private static void WriteStringOption(DBusWriter body, string key, string value)
        {
            body.BeginStruct();
            body.WriteString(key);
            body.WriteVariantString(value);
        }

        private static void WriteBoolOption(DBusWriter body, string key, bool value)
        {
            body.BeginStruct();
            body.WriteString(key);
            body.WriteVariantBool(value);
        }

        private static byte[] NulTerminated(string value)
        {
            byte[] utf8 = Encoding.UTF8.GetBytes(value);
            var bytes = new byte[utf8.Length + 1];
            Array.Copy(utf8, bytes, utf8.Length);
            return bytes;
        }

        private static string DescribeError(DBusMessage error)
        {
            string detail = error.Signature != null && error.Signature.StartsWith("s", StringComparison.Ordinal)
                ? error.OpenBody().ReadString()
                : string.Empty;

            return detail.Length > 0 ? $"{error.ErrorName}: {detail}" : error.ErrorName ?? "unknown error";
        }

        private static string NextToken() =>
            TOKEN_PREFIX + Interlocked.Increment(ref tokenCounter) + "_" + (uint)Environment.TickCount;
    }
}
