using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Crosstales.FB.Linux
{
    /// <summary>
    ///     AF_UNIX socket address. Unity's Mono profile ships no <c>UnixDomainSocketEndPoint</c>, so the sockaddr_un layout
    ///     (2-byte family, then the path; a leading NUL for Linux abstract names) is serialised here.
    /// </summary>
    internal sealed class UnixEndPoint : EndPoint
    {
        private const int FAMILY_LENGTH = 2;

        private readonly string path;
        private readonly bool isAbstract;

        public string Path => path;

        public bool IsAbstract => isAbstract;

        public override AddressFamily AddressFamily => AddressFamily.Unix;

        public UnixEndPoint(string path, bool isAbstract)
        {
            this.path = path;
            this.isAbstract = isAbstract;
        }

        public override SocketAddress Serialize()
        {
            byte[] bytes = Encoding.UTF8.GetBytes(path);
            int size = FAMILY_LENGTH + bytes.Length + 1;
            var address = new SocketAddress(AddressFamily.Unix, size);
            int offset = FAMILY_LENGTH;

            if (isAbstract)
                address[offset++] = 0;

            for (int i = 0; i < bytes.Length; i++)
                address[offset + i] = bytes[i];

            return address;
        }

        public override EndPoint Create(SocketAddress socketAddress)
        {
            int length = socketAddress.Size - FAMILY_LENGTH;

            if (length <= 0)
                return new UnixEndPoint(string.Empty, false);

            bool abstractName = socketAddress[FAMILY_LENGTH] == 0;
            int start = abstractName ? FAMILY_LENGTH + 1 : FAMILY_LENGTH;
            var bytes = new List<byte>(length);

            for (int i = start; i < socketAddress.Size; i++)
            {
                byte value = socketAddress[i];

                if (value == 0 && !abstractName)
                    break;

                bytes.Add(value);
            }

            return new UnixEndPoint(Encoding.UTF8.GetString(bytes.ToArray()), abstractName);
        }

        public override string ToString() =>
            isAbstract ? "@" + path : path;
    }

    /// <summary>Resolves and parses the session bus address per the D-Bus specification's server-address syntax.</summary>
    internal static class DBusAddress
    {
        public const string SESSION_BUS_VARIABLE = "DBUS_SESSION_BUS_ADDRESS";
        public const string RUNTIME_DIR_VARIABLE = "XDG_RUNTIME_DIR";

        private const string UNIX_TRANSPORT = "unix";
        private const string PATH_KEY = "path";
        private const string ABSTRACT_KEY = "abstract";

        /// <summary>The bus address from the environment, or the systemd per-user bus socket when the variable is unset.</summary>
        public static string? SessionBus()
        {
            if (Environment.GetEnvironmentVariable(SESSION_BUS_VARIABLE) is { Length: > 0 } address)
                return address;

            if (Environment.GetEnvironmentVariable(RUNTIME_DIR_VARIABLE) is not { Length: > 0 } runtimeDirectory)
                return null;

            string bus = System.IO.Path.Combine(runtimeDirectory, "bus");
            return File.Exists(bus) ? "unix:path=" + bus : null;
        }

        /// <summary>The first unix-transport entry of a (possibly semicolon-separated) address, or null when there is none.</summary>
        public static UnixEndPoint? ParseUnix(string address)
        {
            foreach (string entry in address.Split(';'))
            {
                int colon = entry.IndexOf(':');

                if (colon < 0 || entry.Substring(0, colon) != UNIX_TRANSPORT)
                    continue;

                string path = string.Empty;
                string abstractName = string.Empty;
                bool hasPath = false;
                bool hasAbstractName = false;

                foreach (string pair in entry.Substring(colon + 1).Split(','))
                {
                    int equals = pair.IndexOf('=');

                    if (equals < 0)
                        continue;

                    string key = pair.Substring(0, equals);
                    string value = Unescape(pair.Substring(equals + 1));

                    if (key == PATH_KEY)
                    {
                        path = value;
                        hasPath = true;
                    }
                    else if (key == ABSTRACT_KEY)
                    {
                        abstractName = value;
                        hasAbstractName = true;
                    }
                }

                if (hasPath)
                    return new UnixEndPoint(path, false);

                if (hasAbstractName)
                    return new UnixEndPoint(abstractName, true);
            }

            return null;
        }

        /// <summary>Decodes the %XX escapes an address value may carry.</summary>
        public static string Unescape(string value)
        {
            if (value.IndexOf('%') < 0)
                return value;

            var bytes = new List<byte>(value.Length);

            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];

                if (current == '%' && i + 2 < value.Length && IsHex(value[i + 1]) && IsHex(value[i + 2]))
                {
                    bytes.Add((byte)((HexValue(value[i + 1]) << 4) | HexValue(value[i + 2])));
                    i += 2;
                    continue;
                }

                byte[] encoded = Encoding.UTF8.GetBytes(current.ToString());
                bytes.AddRange(encoded);
            }

            return Encoding.UTF8.GetString(bytes.ToArray());
        }

        private static bool IsHex(char value) =>
            (value >= '0' && value <= '9') || (value >= 'a' && value <= 'f') || (value >= 'A' && value <= 'F');

        private static int HexValue(char value) =>
            value <= '9' ? value - '0' : (value | 0x20) - 'a' + 10;
    }

    /// <summary>
    ///     A single authenticated connection to a message bus: EXTERNAL SASL over a unix socket, the mandatory Hello, then
    ///     serial-matched method calls and predicate-matched signal waits. Messages that arrive while waiting for something
    ///     else are queued so an early signal is never lost.
    /// </summary>
    internal sealed class DBusConnection : IDisposable
    {
        public const string BUS_NAME = "org.freedesktop.DBus";
        public const string BUS_PATH = "/org/freedesktop/DBus";
        public const string BUS_INTERFACE = "org.freedesktop.DBus";

        private const int RECEIVE_CHUNK = 64 * 1024;
        private const int MAX_PENDING = 256;
        private const int MAX_AUTH_ROUNDS = 4;
        private const string PROC_STATUS = "/proc/self/status";
        private const string UID_LINE_PREFIX = "Uid:";

        private readonly Socket socket;
        private readonly List<DBusMessage> pending = new ();

        private byte[] receiveBuffer = new byte[RECEIVE_CHUNK];
        private int receiveCount;
        private uint nextSerial = 1;

        public string UniqueName { get; private set; } = string.Empty;

        private DBusConnection(Socket socket)
        {
            this.socket = socket;
        }

        /// <summary>Connects, authenticates and registers with the bus at <paramref name="address" />.</summary>
        public static DBusConnection Connect(string address, TimeSpan timeout)
        {
            UnixEndPoint endPoint = DBusAddress.ParseUnix(address) ?? throw new DBusException($"No unix transport in bus address '{address}'.");
            var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            var connection = new DBusConnection(socket);

            try
            {
                int milliseconds = (int)timeout.TotalMilliseconds;
                socket.ReceiveTimeout = milliseconds;
                socket.SendTimeout = milliseconds;
                socket.Connect(endPoint);
                connection.Authenticate();
                connection.SayHello(timeout);
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        public void Dispose() =>
            socket.Close();

        public void AddMatch(string rule, TimeSpan timeout)
        {
            var body = new DBusWriter();
            body.WriteString(rule);
            DBusMessage call = DBusMessage.MethodCall(BUS_NAME, BUS_PATH, BUS_INTERFACE, "AddMatch");
            call.Signature = "s";
            call.Body = body.ToArray();
            DBusMessage reply = Call(call, timeout);

            if (reply.Type == DBusMessageType.Error)
                throw new DBusException($"AddMatch refused: {reply.ErrorName}");
        }

        /// <summary>Assigns the next serial, writes the message and returns that serial.</summary>
        public uint Send(DBusMessage message)
        {
            message.Serial = nextSerial++;
            byte[] bytes = message.Encode();
            int sent = 0;

            while (sent < bytes.Length)
                sent += socket.Send(bytes, sent, bytes.Length - sent, SocketFlags.None);

            return message.Serial;
        }

        /// <summary>Sends a method call and returns its reply, which may be an error message.</summary>
        public DBusMessage Call(DBusMessage call, TimeSpan timeout)
        {
            uint serial = Send(call);
            return WaitFor(message => (message.Type == DBusMessageType.MethodReturn || message.Type == DBusMessageType.Error) && message.ReplySerial == serial, timeout);
        }

        /// <summary>Returns the first queued or incoming message the predicate accepts; a null timeout waits indefinitely.</summary>
        public DBusMessage WaitFor(Func<DBusMessage, bool> predicate, TimeSpan? timeout)
        {
            for (int i = 0; i < pending.Count; i++)
            {
                if (!predicate(pending[i]))
                    continue;

                DBusMessage queued = pending[i];
                pending.RemoveAt(i);
                return queued;
            }

            DateTime deadline = timeout.HasValue ? DateTime.UtcNow + timeout.Value : DateTime.MaxValue;

            while (true)
            {
                DBusMessage message = Receive(deadline);

                if (predicate(message))
                    return message;

                if (pending.Count == MAX_PENDING)
                    pending.RemoveAt(0);

                pending.Add(message);
            }
        }

        private void Authenticate()
        {
            bool withUid = TryReadOwnUid(out string uid);
            SendRaw(withUid ? "\0AUTH EXTERNAL " + ToHex(uid) + "\r\n" : "\0AUTH EXTERNAL\r\n");

            for (int round = 0; round < MAX_AUTH_ROUNDS; round++)
            {
                string line = ReadLine();

                if (line == "OK" || line.StartsWith("OK ", StringComparison.Ordinal))
                {
                    SendRaw("BEGIN\r\n");
                    return;
                }

                if (line.StartsWith("DATA", StringComparison.Ordinal))
                {
                    SendRaw("DATA\r\n");
                    continue;
                }

                if (line.StartsWith("REJECTED", StringComparison.Ordinal) && withUid)
                {
                    withUid = false;
                    SendRaw("AUTH EXTERNAL\r\n");
                    continue;
                }

                throw new DBusException($"Bus refused EXTERNAL authentication: {line}");
            }

            throw new DBusException("Bus did not complete EXTERNAL authentication.");
        }

        private void SayHello(TimeSpan timeout)
        {
            DBusMessage reply = Call(DBusMessage.MethodCall(BUS_NAME, BUS_PATH, BUS_INTERFACE, "Hello"), timeout);

            if (reply.Type == DBusMessageType.Error)
                throw new DBusException($"Hello refused: {reply.ErrorName}");

            UniqueName = reply.OpenBody().ReadString();
        }

        private DBusMessage Receive(DateTime deadline)
        {
            while (true)
            {
                if (DBusMessage.TryDecode(receiveBuffer, 0, receiveCount, out DBusMessage? message, out int consumed))
                {
                    Array.Copy(receiveBuffer, consumed, receiveBuffer, 0, receiveCount - consumed);
                    receiveCount -= consumed;
                    return message;
                }

                int required = DBusMessage.RequiredLength(receiveBuffer, 0, receiveCount);
                int capacity = Math.Max(required, receiveCount + 1);

                if (capacity > receiveBuffer.Length)
                    Array.Resize(ref receiveBuffer, Math.Max(capacity, receiveBuffer.Length * 2));

                int waitMilliseconds = 0;

                if (deadline != DateTime.MaxValue)
                {
                    double remaining = (deadline - DateTime.UtcNow).TotalMilliseconds;

                    if (remaining <= 0)
                        throw new DBusException("Timed out waiting for the bus.");

                    waitMilliseconds = Math.Max(1, (int)remaining);
                }

                socket.ReceiveTimeout = waitMilliseconds;
                int read;

                try { read = socket.Receive(receiveBuffer, receiveCount, receiveBuffer.Length - receiveCount, SocketFlags.None); }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.TimedOut || e.SocketErrorCode == SocketError.WouldBlock)
                {
                    throw new DBusException("Timed out waiting for the bus.");
                }

                if (read == 0)
                    throw new DBusException("The bus closed the connection.");

                receiveCount += read;
            }
        }

        private void SendRaw(string text)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(text);
            int sent = 0;

            while (sent < bytes.Length)
                sent += socket.Send(bytes, sent, bytes.Length - sent, SocketFlags.None);
        }

        // SASL lines are read one byte at a time so nothing past the terminating CRLF is pulled off the socket.
        private string ReadLine()
        {
            var line = new StringBuilder();
            var single = new byte[1];

            while (true)
            {
                int read;

                try { read = socket.Receive(single, 0, 1, SocketFlags.None); }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.TimedOut || e.SocketErrorCode == SocketError.WouldBlock)
                {
                    throw new DBusException("Timed out during bus authentication.");
                }

                if (read == 0)
                    throw new DBusException("The bus closed the connection during authentication.");

                char current = (char)single[0];

                if (current == '\n')
                {
                    if (line.Length > 0 && line[line.Length - 1] == '\r')
                        line.Length--;

                    return line.ToString();
                }

                line.Append(current);
            }
        }

        // The real uid of this process, read from procfs; an empty result means the bus must identify the peer itself.
        private static bool TryReadOwnUid(out string uid)
        {
            uid = string.Empty;

            if (!File.Exists(PROC_STATUS))
                return false;

            foreach (string line in File.ReadAllLines(PROC_STATUS))
            {
                if (!line.StartsWith(UID_LINE_PREFIX, StringComparison.Ordinal))
                    continue;

                string[] parts = line.Substring(UID_LINE_PREFIX.Length).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length == 0)
                    return false;

                uid = parts[0];
                return true;
            }

            return false;
        }

        private static string ToHex(string ascii)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(ascii);
            var hex = new StringBuilder(bytes.Length * 2);

            foreach (byte value in bytes)
                hex.Append(value.ToString("x2"));

            return hex.ToString();
        }
    }
}
