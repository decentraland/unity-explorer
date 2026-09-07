using Crosstales.FB.Linux;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Crosstales.FB.Tests
{
    /// <summary>
    ///     In-process stand-in for a session bus plus the file-chooser portal: one unix socket, the EXTERNAL handshake,
    ///     Hello/AddMatch, and a scripted OpenFile/SaveFile round that records what the client marshalled.
    /// </summary>
    internal sealed class FakeDBusDaemon : IDisposable
    {
        public const string CLIENT_NAME = ":1.42";
        public const string PORTAL_NAME = ":1.7";
        public const string BUS_GUID = "0123456789abcdef0123456789abcdef";

        private const int RECEIVE_TIMEOUT_MS = 10000;
        private const int BUFFER_SIZE = 64 * 1024;

        private readonly Socket listener;
        private readonly string socketPath;
        private readonly Thread thread;
        private readonly List<string> matchRules = new ();

        private uint serial = 1;
        private volatile bool disposed;

        public string Address => "unix:path=" + socketPath;

        public IReadOnlyList<string> MatchRules => matchRules;

        public string? ReturnedHandle { get; set; }

        public bool RespondBeforeReturningHandle { get; set; }

        public uint ResponseCode { get; set; }

        public string[] ResponseUris { get; set; } = Array.Empty<string>();

        public string? PortalErrorName { get; set; }

        public string? ChooserMember { get; private set; }

        public string? ChooserParentWindow { get; private set; }

        public string? ChooserTitle { get; private set; }

        public Dictionary<string, object>? ChooserOptions { get; private set; }

        public Exception? Failure { get; private set; }

        public FakeDBusDaemon()
        {
            socketPath = Path.Combine(Path.GetTempPath(), "fb-dbus-" + Guid.NewGuid().ToString("N").Substring(0, 12));
            listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            listener.Bind(new UnixEndPoint(socketPath, false));
            listener.Listen(1);
            thread = new Thread(Serve) { IsBackground = true, Name = nameof(FakeDBusDaemon) };
            thread.Start();
        }

        public void Dispose()
        {
            disposed = true;
            listener.Close();
            thread.Join(RECEIVE_TIMEOUT_MS);

            try { File.Delete(socketPath); }
            catch (IOException) { }
        }

        private void Serve()
        {
            try
            {
                using Socket client = listener.Accept();
                client.ReceiveTimeout = RECEIVE_TIMEOUT_MS;
                Handshake(client);

                var buffer = new byte[BUFFER_SIZE];
                int count = 0;

                while (true)
                {
                    DBusMessage? message;
                    int consumed;

                    while (!DBusMessage.TryDecode(buffer, 0, count, out message, out consumed))
                    {
                        int read = client.Receive(buffer, count, buffer.Length - count, SocketFlags.None);

                        if (read == 0)
                            return;

                        count += read;
                    }

                    Array.Copy(buffer, consumed, buffer, 0, count - consumed);
                    count -= consumed;
                    Handle(client, message);
                }
            }
            catch (Exception e)
            {
                if (!disposed)
                    Failure = e;
            }
        }

        private static void Handshake(Socket client)
        {
            string auth = ReadLine(client);

            if (!auth.StartsWith("\0AUTH EXTERNAL", StringComparison.Ordinal))
                throw new InvalidOperationException($"Unexpected SASL opener '{auth}'.");

            SendRaw(client, "OK " + BUS_GUID + "\r\n");
            string begin = ReadLine(client);

            if (begin != "BEGIN")
                throw new InvalidOperationException($"Expected BEGIN, got '{begin}'.");
        }

        private void Handle(Socket client, DBusMessage call)
        {
            if (call.Interface == DBusConnection.BUS_INTERFACE && call.Member == "Hello")
            {
                var acquired = new DBusMessage
                {
                    Type = DBusMessageType.Signal,
                    Sender = DBusConnection.BUS_NAME,
                    Destination = CLIENT_NAME,
                    Path = DBusConnection.BUS_PATH,
                    Interface = DBusConnection.BUS_INTERFACE,
                    Member = "NameAcquired",
                    Signature = "s",
                    Body = StringBody(CLIENT_NAME),
                };

                Send(client, acquired);
                Reply(client, call, DBusConnection.BUS_NAME, "s", StringBody(CLIENT_NAME));
                return;
            }

            if (call.Interface == DBusConnection.BUS_INTERFACE && call.Member == "AddMatch")
            {
                matchRules.Add(call.OpenBody().ReadString());
                Reply(client, call, DBusConnection.BUS_NAME, null, Array.Empty<byte>());
                return;
            }

            if (call.Interface == PortalFileChooser.FILE_CHOOSER_INTERFACE && call.Destination == PortalFileChooser.BUS_NAME)
            {
                HandleChooser(client, call);
                return;
            }

            Error(client, call, "org.freedesktop.DBus.Error.UnknownMethod", $"No handler for {call.Interface}.{call.Member}");
        }

        private void HandleChooser(Socket client, DBusMessage call)
        {
            DBusReader body = call.OpenBody();
            ChooserMember = call.Member;
            ChooserParentWindow = body.ReadString();
            ChooserTitle = body.ReadString();
            ChooserOptions = (Dictionary<string, object>)body.ReadValue("a{sv}");

            if (PortalErrorName != null)
            {
                Error(client, call, PortalErrorName, "The name is not activatable");
                return;
            }

            string token = ChooserOptions.TryGetValue("handle_token", out object? rawToken) ? (string)rawToken! : "missing";
            string handle = ReturnedHandle ?? PortalFileChooser.PredictHandle(CLIENT_NAME, token);

            var response = new DBusMessage
            {
                Type = DBusMessageType.Signal,
                Sender = PORTAL_NAME,
                Destination = CLIENT_NAME,
                Path = handle,
                Interface = PortalFileChooser.REQUEST_INTERFACE,
                Member = PortalFileChooser.RESPONSE,
                Signature = PortalFileChooser.RESPONSE_SIGNATURE,
                Body = ResponseBody(ResponseCode, ResponseUris),
            };

            var handleBody = new DBusWriter();
            handleBody.WriteObjectPath(handle);

            if (RespondBeforeReturningHandle)
            {
                Send(client, response);
                Reply(client, call, PORTAL_NAME, "o", handleBody.ToArray());
            }
            else
            {
                Reply(client, call, PORTAL_NAME, "o", handleBody.ToArray());
                Send(client, response);
            }
        }

        private void Reply(Socket client, DBusMessage call, string sender, string? signature, byte[] body) =>
            Send(client, new DBusMessage
            {
                Type = DBusMessageType.MethodReturn,
                Flags = DBusMessage.FLAG_NO_REPLY_EXPECTED,
                ReplySerial = call.Serial,
                Sender = sender,
                Destination = CLIENT_NAME,
                Signature = signature,
                Body = body,
            });

        private void Error(Socket client, DBusMessage call, string errorName, string message) =>
            Send(client, new DBusMessage
            {
                Type = DBusMessageType.Error,
                Flags = DBusMessage.FLAG_NO_REPLY_EXPECTED,
                ReplySerial = call.Serial,
                ErrorName = errorName,
                Sender = DBusConnection.BUS_NAME,
                Destination = CLIENT_NAME,
                Signature = "s",
                Body = StringBody(message),
            });

        private void Send(Socket client, DBusMessage message)
        {
            message.Serial = serial++;
            byte[] bytes = message.Encode();
            int sent = 0;

            while (sent < bytes.Length)
                sent += client.Send(bytes, sent, bytes.Length - sent, SocketFlags.None);
        }

        private static byte[] StringBody(string value)
        {
            var writer = new DBusWriter();
            writer.WriteString(value);
            return writer.ToArray();
        }

        internal static byte[] ResponseBody(uint code, string[] uris)
        {
            var writer = new DBusWriter();
            writer.WriteUInt32(code);
            DBusWriter.ArrayScope results = writer.BeginArray(8);

            if (uris.Length > 0)
            {
                writer.BeginStruct();
                writer.WriteString(PortalFileChooser.URIS_KEY);
                writer.WriteSignature("as");
                DBusWriter.ArrayScope list = writer.BeginArray(4);

                foreach (string uri in uris)
                    writer.WriteString(uri);

                writer.EndArray(list);
            }

            writer.EndArray(results);
            return writer.ToArray();
        }

        private static string ReadLine(Socket client)
        {
            var line = new StringBuilder();
            var single = new byte[1];

            while (true)
            {
                if (client.Receive(single, 0, 1, SocketFlags.None) == 0)
                    throw new InvalidOperationException("Client hung up during the handshake.");

                var current = (char)single[0];

                if (current == '\n')
                {
                    if (line.Length > 0 && line[line.Length - 1] == '\r')
                        line.Length--;

                    return line.ToString();
                }

                line.Append(current);
            }
        }

        private static void SendRaw(Socket client, string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            client.Send(bytes, 0, bytes.Length, SocketFlags.None);
        }
    }
}
