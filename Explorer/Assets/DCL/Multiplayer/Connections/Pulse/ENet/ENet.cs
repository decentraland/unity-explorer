/*
 *  Managed C# wrapper for an extended version of ENet
 *  This is a fork from upstream and is available at http://github.com/SoftwareGuy/ENet-CSharp
 *
 *  Copyright (c) 2019-2023 Matt Coburn (SoftwareGuy/Coburn64), Chris Burns (c6burns)
 *  Copyright (c) 2013 James Bellinger, 2016 Nate Shoffner, 2018 Stanislav Denisov
 *
 *  Permission is hereby granted, free of charge, to any person obtaining a copy
 *  of this software and associated documentation files (the "Software"), to deal
 *  in the Software without restriction, including without limitation the rights
 *  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 *  copies of the Software, and to permit persons to whom the Software is
 *  furnished to do so, subject to the following conditions:
 *
 *  The above copyright notice and this permission notice shall be included in all
 *  copies or substantial portions of the Software.
 *
 *  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 *  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 *  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 *  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 *  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 *  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 *  SOFTWARE.
 */

using System;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;

namespace DCL.Multiplayer.Connections.Pulse.ENet
{
    [Flags]
    public enum PacketFlags
    {
        None = 0,
        Reliable = 1 << 0,
        Unsequenced = 1 << 1,
        NoAllocate = 1 << 2,
        UnreliableFragmented = 1 << 3,
        Instant = 1 << 4,
        Unthrottled = 1 << 5,
        Sent = 1 << 8,
    }

    public enum EventType
    {
        None = 0,
        Connect = 1,
        Disconnect = 2,
        Receive = 3,
        Timeout = 4,
    }

    public enum PeerState
    {
        Uninitialized = -1,
        Disconnected = 0,
        Connecting = 1,
        AcknowledgingConnect = 2,
        ConnectionPending = 3,
        ConnectionSucceeded = 4,
        Connected = 5,
        DisconnectLater = 6,
        Disconnecting = 7,
        AcknowledgingDisconnect = 8,
        Zombie = 9,
    }

    [StructLayout(LayoutKind.Explicit, Size = 18)]
    internal struct ENetAddress
    {
        [FieldOffset(16)]
        public ushort port;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ENetEvent
    {
        public EventType type;
        public IntPtr peer;
        public byte channelID;
        public uint data;
        public IntPtr packet;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct ENetCallbacks
    {
        public AllocCallback malloc;
        public FreeCallback free;
        public NoMemoryCallback noMemory;
    }

    public delegate IntPtr AllocCallback(IntPtr size);
    public delegate void FreeCallback(IntPtr memory);
    public delegate void NoMemoryCallback();
    public delegate void PacketFreeCallback(Packet packet);
    public delegate int InterceptCallback(ref Event @event, ref Address address, IntPtr receivedData, int receivedDataLength);
    public delegate ulong ChecksumCallback(IntPtr buffers, int bufferCount);

    internal static class ArrayPool
    {
        [ThreadStatic]
        private static byte[] byteBuffer;
        [ThreadStatic]
        private static IntPtr[] pointerBuffer;

        public static byte[] GetByteBuffer()
        {
            if (byteBuffer == null)
                byteBuffer = new byte[64];

            return byteBuffer;
        }

        public static IntPtr[] GetPointerBuffer()
        {
            if (pointerBuffer == null)
                pointerBuffer = new IntPtr[Library.maxPeers];

            return pointerBuffer;
        }
    }

    public struct Address
    {
        private ENetAddress nativeAddress;

        internal ENetAddress NativeData
        {
            get => nativeAddress;

            set => nativeAddress = value;
        }

        internal Address(ENetAddress address)
        {
            nativeAddress = address;
        }

        public ushort Port
        {
            get => nativeAddress.port;

            set => nativeAddress.port = value;
        }

        public string GetIP()
        {
            var ip = new StringBuilder(1025);

            if (Native.enet_address_get_ip(ref nativeAddress, ip, (IntPtr) ip.Capacity) != 0)
                return string.Empty;

            return ip.ToString();
        }

        public bool SetIP(string ip)
        {
            if (ip == null)
                throw new ArgumentNullException("ip");

            return Native.enet_address_set_ip(ref nativeAddress, ip) == 0;
        }

        public string GetHost()
        {
            var hostName = new StringBuilder(1025);

            if (Native.enet_address_get_hostname(ref nativeAddress, hostName, (IntPtr) hostName.Capacity) != 0)
                return string.Empty;

            return hostName.ToString();
        }

        public bool SetHost(string hostName)
        {
            if (hostName == null)
                throw new ArgumentNullException("hostName");

            return Native.enet_address_set_hostname(ref nativeAddress, hostName) == 0;
        }
    }

    public struct Event
    {
        private ENetEvent nativeEvent;

        internal ENetEvent NativeData
        {
            get => nativeEvent;

            set => nativeEvent = value;
        }

        internal Event(ENetEvent @event)
        {
            nativeEvent = @event;
        }

        public EventType Type => nativeEvent.type;

        public Peer Peer => new (nativeEvent.peer);

        public byte ChannelID => nativeEvent.channelID;

        public uint Data => nativeEvent.data;

        public Packet Packet => new (nativeEvent.packet);
    }

    public class Callbacks
    {
        private ENetCallbacks nativeCallbacks;

        internal ENetCallbacks NativeData
        {
            get => nativeCallbacks;

            set => nativeCallbacks = value;
        }

        public Callbacks(AllocCallback allocCallback, FreeCallback freeCallback, NoMemoryCallback noMemoryCallback)
        {
            nativeCallbacks.malloc = allocCallback;
            nativeCallbacks.free = freeCallback;
            nativeCallbacks.noMemory = noMemoryCallback;
        }
    }

    public struct Packet : IDisposable
    {
        internal IntPtr NativeData { get; set; }

        internal Packet(IntPtr packet)
        {
            NativeData = packet;
        }

        public void Dispose()
        {
            if (NativeData != IntPtr.Zero)
            {
                Native.enet_packet_dispose(NativeData);
                NativeData = IntPtr.Zero;
            }
        }

        public bool IsSet => NativeData != IntPtr.Zero;

        public IntPtr Data
        {
            get
            {
                ThrowIfNotCreated();

                return Native.enet_packet_get_data(NativeData);
            }
        }

        public IntPtr UserData
        {
            get
            {
                ThrowIfNotCreated();

                return Native.enet_packet_get_user_data(NativeData);
            }

            set
            {
                ThrowIfNotCreated();

                Native.enet_packet_set_user_data(NativeData, value);
            }
        }

        public int Length
        {
            get
            {
                ThrowIfNotCreated();

                return Native.enet_packet_get_length(NativeData);
            }
        }

        public bool HasReferences
        {
            get
            {
                ThrowIfNotCreated();

                return Native.enet_packet_check_references(NativeData) != 0;
            }
        }

        internal void ThrowIfNotCreated()
        {
            if (NativeData == IntPtr.Zero)
                throw new InvalidOperationException("Packet not created");
        }

        public void SetFreeCallback(IntPtr callback)
        {
            ThrowIfNotCreated();

            Native.enet_packet_set_free_callback(NativeData, callback);
        }

        public void SetFreeCallback(PacketFreeCallback callback)
        {
            ThrowIfNotCreated();

            Native.enet_packet_set_free_callback(NativeData, Marshal.GetFunctionPointerForDelegate(callback));
        }

        public void Create(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            Create(data, data.Length);
        }

        public void Create(byte[] data, int length)
        {
            Create(data, length, PacketFlags.None);
        }

        public void Create(byte[] data, PacketFlags flags)
        {
            Create(data, data.Length, flags);
        }

        public void Create(byte[] data, int length, PacketFlags flags)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            if (length < 0 || length > data.Length)
                throw new ArgumentOutOfRangeException("length");

            NativeData = Native.enet_packet_create(data, (IntPtr) length, flags);
        }

        public unsafe void Create(Span<byte> data, PacketFlags flags)
        {
            fixed (byte* ptr = data) { NativeData = Native.enet_packet_create((IntPtr)ptr, (IntPtr) data.Length, flags); }
        }

        public void Create(IntPtr data, int length, PacketFlags flags)
        {
            if (data == IntPtr.Zero)
                throw new ArgumentNullException("data");

            if (length < 0)
                throw new ArgumentOutOfRangeException("length");

            NativeData = Native.enet_packet_create(data, (IntPtr) length, flags);
        }

        public void Create(byte[] data, int offset, int length, PacketFlags flags)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));

            if (length < 0 || length > data.Length)
                throw new ArgumentOutOfRangeException(nameof(length));

            NativeData = Native.enet_packet_create_offset(data, (IntPtr) length, (IntPtr) offset, flags);
        }

        public void Create(IntPtr data, int offset, int length, PacketFlags flags)
        {
            if (data == IntPtr.Zero)
                throw new ArgumentNullException("data");

            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset");

            if (length < 0)
                throw new ArgumentOutOfRangeException("length");

            NativeData = Native.enet_packet_create_offset(data, (IntPtr) length, (IntPtr) offset, flags);
        }

        public void CopyTo(byte[] destination, int startPos = 0)
        {
            if (destination == null)
                throw new ArgumentNullException("destination");

            // Fix by katori, prevents trying to copy a NULL
            // from native world (ie. disconnect a client)
            if (Data == null) { return; }

            Marshal.Copy(Data, destination, startPos, Length);
        }
    }

    public class Host : IDisposable
    {
        internal IntPtr NativeData { get; set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (NativeData != IntPtr.Zero)
            {
                Native.enet_host_destroy(NativeData);
                NativeData = IntPtr.Zero;
            }
        }

        ~Host()
        {
            Dispose(false);
        }

        public bool IsSet => NativeData != IntPtr.Zero;

        public uint PeersCount
        {
            get
            {
                ThrowIfNotCreated();

                return Native.enet_host_get_peers_count(NativeData);
            }
        }

        public uint PacketsSent
        {
            get
            {
                ThrowIfNotCreated();

                return Native.enet_host_get_packets_sent(NativeData);
            }
        }

        public uint PacketsReceived
        {
            get
            {
                ThrowIfNotCreated();

                return Native.enet_host_get_packets_received(NativeData);
            }
        }

        public uint BytesSent
        {
            get
            {
                ThrowIfNotCreated();

                return Native.enet_host_get_bytes_sent(NativeData);
            }
        }

        public uint BytesReceived
        {
            get
            {
                ThrowIfNotCreated();

                return Native.enet_host_get_bytes_received(NativeData);
            }
        }

        internal void ThrowIfNotCreated()
        {
            if (NativeData == IntPtr.Zero)
                throw new InvalidOperationException("Host not created");
        }

        private static void ThrowIfChannelsExceeded(int channelLimit)
        {
            if (channelLimit < 0 || channelLimit > Library.maxChannelCount)
                throw new ArgumentOutOfRangeException("channelLimit");
        }

        public void Create()
        {
            Create(null, 1, 0);
        }

        public void Create(int bufferSize)
        {
            Create(null, 1, 0, 0, 0, bufferSize);
        }

        public void Create(Address? address, int peerLimit)
        {
            Create(address, peerLimit, 0);
        }

        public void Create(Address? address, int peerLimit, int channelLimit)
        {
            Create(address, peerLimit, channelLimit, 0, 0, 0);
        }

        public void Create(int peerLimit, int channelLimit)
        {
            Create(null, peerLimit, channelLimit, 0, 0, 0);
        }

        public void Create(int peerLimit, int channelLimit, uint incomingBandwidth, uint outgoingBandwidth)
        {
            Create(null, peerLimit, channelLimit, incomingBandwidth, outgoingBandwidth, 0);
        }

        public void Create(Address? address, int peerLimit, int channelLimit, uint incomingBandwidth, uint outgoingBandwidth)
        {
            Create(address, peerLimit, channelLimit, incomingBandwidth, outgoingBandwidth, 0);
        }

        public void Create(Address? address, int peerLimit, int channelLimit, uint incomingBandwidth, uint outgoingBandwidth,
            int bufferSize)
        {
            if (NativeData != IntPtr.Zero)
                throw new InvalidOperationException("Host already created");

            if (peerLimit < 0 || peerLimit > Library.maxPeers)
                throw new ArgumentOutOfRangeException("peerLimit");

            ThrowIfChannelsExceeded(channelLimit);

            if (address != null)
            {
                ENetAddress nativeAddress = address.Value.NativeData;

                NativeData = Native.enet_host_create(ref nativeAddress, (IntPtr) peerLimit, (IntPtr) channelLimit, incomingBandwidth, outgoingBandwidth, bufferSize);
            }
            else { NativeData = Native.enet_host_create(IntPtr.Zero, (IntPtr) peerLimit, (IntPtr) channelLimit, incomingBandwidth, outgoingBandwidth, bufferSize); }

            if (NativeData == IntPtr.Zero)
                throw new InvalidOperationException("Host creation call failed");
        }

        public void PreventConnections(bool state)
        {
            ThrowIfNotCreated();

            Native.enet_host_prevent_connections(NativeData, (byte)(state ? 1 : 0));
        }

        public void Broadcast(byte channelID, ref Packet packet)
        {
            ThrowIfNotCreated();

            packet.ThrowIfNotCreated();
            Native.enet_host_broadcast(NativeData, channelID, packet.NativeData);
            packet.NativeData = IntPtr.Zero;
        }

        public void Broadcast(byte channelID, ref Packet packet, Peer excludedPeer)
        {
            ThrowIfNotCreated();

            packet.ThrowIfNotCreated();
            Native.enet_host_broadcast_exclude(NativeData, channelID, packet.NativeData, excludedPeer.NativeData);
            packet.NativeData = IntPtr.Zero;
        }

        public void Broadcast(byte channelID, ref Packet packet, Peer[] peers)
        {
            if (peers == null)
                throw new ArgumentNullException("peers");

            ThrowIfNotCreated();

            packet.ThrowIfNotCreated();

            if (peers.Length > 0)
            {
                IntPtr[] nativePeers = ArrayPool.GetPointerBuffer();
                var nativeCount = 0;

                for (var i = 0; i < peers.Length; i++)
                {
                    if (peers[i].NativeData != IntPtr.Zero)
                    {
                        nativePeers[nativeCount] = peers[i].NativeData;
                        nativeCount++;
                    }
                }

                Native.enet_host_broadcast_selective(NativeData, channelID, packet.NativeData, nativePeers, (IntPtr) nativeCount);
            }

            packet.NativeData = IntPtr.Zero;
        }

        public int CheckEvents(out Event @event)
        {
            ThrowIfNotCreated();

            ENetEvent nativeEvent;

            int result = Native.enet_host_check_events(NativeData, out nativeEvent);

            if (result <= 0)
            {
                @event = default(Event);

                return result;
            }

            @event = new Event(nativeEvent);

            return result;
        }

        public Peer Connect(Address address) =>
            Connect(address, 0, 0);

        public Peer Connect(Address address, int channelLimit) =>
            Connect(address, channelLimit, 0);

        public Peer Connect(Address address, int channelLimit, uint data)
        {
            ThrowIfNotCreated();
            ThrowIfChannelsExceeded(channelLimit);

            ENetAddress nativeAddress = address.NativeData;
            var peer = new Peer(Native.enet_host_connect(NativeData, ref nativeAddress, (IntPtr) channelLimit, data));

            if (peer.NativeData == IntPtr.Zero)
                throw new InvalidOperationException("Host connect call failed");

            return peer;
        }

        public int Service(int timeout, out Event @event)
        {
            if (timeout < 0)
                throw new ArgumentOutOfRangeException("timeout");

            ThrowIfNotCreated();

            ENetEvent nativeEvent;

            int result = Native.enet_host_service(NativeData, out nativeEvent, (uint)timeout);

            if (result <= 0)
            {
                @event = default(Event);

                return result;
            }

            @event = new Event(nativeEvent);

            return result;
        }

        public void SetBandwidthLimit(uint incomingBandwidth, uint outgoingBandwidth)
        {
            ThrowIfNotCreated();

            Native.enet_host_bandwidth_limit(NativeData, incomingBandwidth, outgoingBandwidth);
        }

        public void SetChannelLimit(int channelLimit)
        {
            ThrowIfNotCreated();
            ThrowIfChannelsExceeded(channelLimit);

            Native.enet_host_channel_limit(NativeData, (IntPtr) channelLimit);
        }

        public void SetMaxDuplicatePeers(ushort number)
        {
            ThrowIfNotCreated();

            Native.enet_host_set_max_duplicate_peers(NativeData, number);
        }

        public void SetInterceptCallback(IntPtr callback)
        {
            ThrowIfNotCreated();

            Native.enet_host_set_intercept_callback(NativeData, callback);
        }

        public void SetInterceptCallback(InterceptCallback callback)
        {
            ThrowIfNotCreated();

            Native.enet_host_set_intercept_callback(NativeData, Marshal.GetFunctionPointerForDelegate(callback));
        }

        public void SetChecksumCallback(IntPtr callback)
        {
            ThrowIfNotCreated();

            Native.enet_host_set_checksum_callback(NativeData, callback);
        }

        public void SetChecksumCallback(ChecksumCallback callback)
        {
            ThrowIfNotCreated();

            Native.enet_host_set_checksum_callback(NativeData, Marshal.GetFunctionPointerForDelegate(callback));
        }

        public void Flush()
        {
            ThrowIfNotCreated();

            Native.enet_host_flush(NativeData);
        }
    }

    public struct Peer
    {
        internal IntPtr NativeData { get; set; }

        internal Peer(IntPtr peer)
        {
            NativeData = peer;
            ID = NativeData != IntPtr.Zero ? Native.enet_peer_get_id(NativeData) : 0;
        }

        public bool IsSet => NativeData != IntPtr.Zero;

        public uint ID { get; }

        public string IP
        {
            get
            {
                ThrowIfNotCreated();

                byte[] ip = ArrayPool.GetByteBuffer();

                if (Native.enet_peer_get_ip(NativeData, ip, (IntPtr) ip.Length) == 0)
                    return Encoding.ASCII.GetString(ip, 0, ip.StringLength());

                return string.Empty;
            }
        }

        public ushort Port
        {
            get
            {
                ThrowIfNotCreated();

                return Native.enet_peer_get_port(NativeData);
            }
        }

        public uint MTU
        {
            get
            {
                ThrowIfNotCreated();

                return Native.enet_peer_get_mtu(NativeData);
            }
        }

        public PeerState State => NativeData == IntPtr.Zero ? PeerState.Uninitialized : Native.enet_peer_get_state(NativeData);

        public uint RoundTripTime
        {
            get
            {
                ThrowIfNotCreated();

                return Native.enet_peer_get_rtt(NativeData);
            }
        }

        public uint LastRoundTripTime
        {
            get
            {
                ThrowIfNotCreated();

                return Native.enet_peer_get_last_rtt(NativeData);
            }
        }

        public uint LastSendTime
        {
            get
            {
                ThrowIfNotCreated();

                return Native.enet_peer_get_lastsendtime(NativeData);
            }
        }

        public uint LastReceiveTime
        {
            get
            {
                ThrowIfNotCreated();

                return Native.enet_peer_get_lastreceivetime(NativeData);
            }
        }

        public ulong PacketsSent
        {
            get
            {
                ThrowIfNotCreated();

                return Native.enet_peer_get_packets_sent(NativeData);
            }
        }

        public ulong PacketsLost
        {
            get
            {
                ThrowIfNotCreated();

                return Native.enet_peer_get_packets_lost(NativeData);
            }
        }

        public float PacketsThrottle
        {
            get
            {
                ThrowIfNotCreated();

                return Native.enet_peer_get_packets_throttle(NativeData);
            }
        }

        public ulong BytesSent
        {
            get
            {
                ThrowIfNotCreated();

                return Native.enet_peer_get_bytes_sent(NativeData);
            }
        }

        public ulong BytesReceived
        {
            get
            {
                ThrowIfNotCreated();

                return Native.enet_peer_get_bytes_received(NativeData);
            }
        }

        public IntPtr Data
        {
            get
            {
                ThrowIfNotCreated();

                return Native.enet_peer_get_data(NativeData);
            }

            set
            {
                ThrowIfNotCreated();

                Native.enet_peer_set_data(NativeData, value);
            }
        }

        internal void ThrowIfNotCreated()
        {
            if (NativeData == IntPtr.Zero)
                throw new InvalidOperationException("Peer not created");
        }

        public void ConfigureThrottle(uint interval, uint acceleration, uint deceleration, uint threshold)
        {
            ThrowIfNotCreated();

            Native.enet_peer_throttle_configure(NativeData, interval, acceleration, deceleration, threshold);
        }

        public int Send(byte channelID, ref Packet packet)
        {
            ThrowIfNotCreated();

            packet.ThrowIfNotCreated();

            return Native.enet_peer_send(NativeData, channelID, packet.NativeData);
        }

        public bool Receive(out byte channelID, out Packet packet)
        {
            ThrowIfNotCreated();

            IntPtr nativePacket = Native.enet_peer_receive(NativeData, out channelID);

            if (nativePacket != IntPtr.Zero)
            {
                packet = new Packet(nativePacket);

                return true;
            }

            packet = default(Packet);

            return false;
        }

        public void Ping()
        {
            ThrowIfNotCreated();

            Native.enet_peer_ping(NativeData);
        }

        public void PingInterval(uint interval)
        {
            ThrowIfNotCreated();

            Native.enet_peer_ping_interval(NativeData, interval);
        }

        public void Timeout(uint timeoutLimit, uint timeoutMinimum, uint timeoutMaximum)
        {
            ThrowIfNotCreated();

            Native.enet_peer_timeout(NativeData, timeoutLimit, timeoutMinimum, timeoutMaximum);
        }

        public void Disconnect(uint data)
        {
            ThrowIfNotCreated();

            Native.enet_peer_disconnect(NativeData, data);
        }

        public void DisconnectNow(uint data)
        {
            ThrowIfNotCreated();

            Native.enet_peer_disconnect_now(NativeData, data);
        }

        public void DisconnectLater(uint data)
        {
            ThrowIfNotCreated();

            Native.enet_peer_disconnect_later(NativeData, data);
        }

        public void Reset()
        {
            ThrowIfNotCreated();

            Native.enet_peer_reset(NativeData);
        }
    }

    public static class Extensions
    {
        public static int StringLength(this byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            int i;

            for (i = 0; i < data.Length && data[i] != 0; i++) ;

            return i;
        }
    }

    public static class Library
    {
        public const uint maxChannelCount = 0xFF;
        public const uint maxPeers = 0xFFF;
        public const uint maxPacketSize = 32 * 1024 * 1024;
        public const uint throttleThreshold = 40;
        public const uint throttleScale = 32;
        public const uint throttleAcceleration = 2;
        public const uint throttleDeceleration = 2;
        public const uint throttleInterval = 5000;
        public const uint timeoutLimit = 32;
        public const uint timeoutMinimum = 5000;
        public const uint timeoutMaximum = 30000;
        public const uint version = (2 << 16) | (4 << 8) | 9;

        public static uint Time => Native.enet_time_get();

        public static bool Initialize()
        {
            if (Native.enet_linked_version() != version)
                throw new InvalidOperationException("ENet native library is out of date, please download the latest release from https://github.com/SoftwareGuy/ENet-CSharp/releases");

            return Native.enet_initialize() == 0;
        }

        public static bool Initialize(Callbacks callbacks)
        {
            if (callbacks == null)
                throw new ArgumentNullException("callbacks");

            if (Native.enet_linked_version() != version)
                throw new InvalidOperationException("ENet native library is out of date, please download the latest release from https://github.com/SoftwareGuy/ENet-CSharp/releases");

            ENetCallbacks nativeCallbacks = callbacks.NativeData;

            return Native.enet_initialize_with_callbacks(version, ref nativeCallbacks) == 0;
        }

        public static void Deinitialize()
        {
            Native.enet_deinitialize();
        }

        public static ulong CRC64(IntPtr buffers, int bufferCount) =>
            Native.enet_crc64(buffers, bufferCount);
    }

    [SuppressUnmanagedCodeSecurity]
    internal static class Native
    {
        // This should address Unity usage and bug #66: Platform specific Enet / libenet
        // https://github.com/SoftwareGuy/Ignorance/issues/66
#if UNITY_EDITOR

        // We are inside the Unity Editor.
#if UNITY_EDITOR_OSX

        // Unity Editor on macOS needs to use libenet.
        private const string nativeLibrary = "libenet";
#else
        private const string nativeLibrary = "enet";
#endif
#endif

#if !UNITY_EDITOR
    // We're not inside the Unity Editor.
#if __APPLE__ && !(__IOS__ || UNITY_IOS)
		// Use libenet on macOS.
		private const string nativeLibrary = "libenet";
#elif __IOS__ || UNITY_IOS
        // We're building for a certain mobile fruity OS.
		private const string nativeLibrary = "__Internal";
#else

    // Assume everything else, Windows et al.
    private const string nativeLibrary = "enet";
#endif
#endif

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int enet_initialize();

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int enet_initialize_with_callbacks(uint version, ref ENetCallbacks inits);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_deinitialize();

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint enet_linked_version();

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint enet_time_get();

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong enet_crc64(IntPtr buffers, int bufferCount);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int enet_address_set_ip(ref ENetAddress address, string ip);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int enet_address_set_hostname(ref ENetAddress address, string hostName);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int enet_address_get_ip(ref ENetAddress address, StringBuilder ip, IntPtr ipLength);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int enet_address_get_hostname(ref ENetAddress address, StringBuilder hostName, IntPtr nameLength);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr enet_packet_create(byte[] data, IntPtr dataLength, PacketFlags flags);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr enet_packet_create(IntPtr data, IntPtr dataLength, PacketFlags flags);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr enet_packet_create_offset(byte[] data, IntPtr dataLength, IntPtr dataOffset, PacketFlags flags);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr enet_packet_create_offset(IntPtr data, IntPtr dataLength, IntPtr dataOffset, PacketFlags flags);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int enet_packet_check_references(IntPtr packet);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr enet_packet_get_data(IntPtr packet);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr enet_packet_get_user_data(IntPtr packet);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr enet_packet_set_user_data(IntPtr packet, IntPtr userData);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int enet_packet_get_length(IntPtr packet);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_packet_set_free_callback(IntPtr packet, IntPtr callback);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_packet_dispose(IntPtr packet);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr enet_host_create(ref ENetAddress address, IntPtr peerLimit, IntPtr channelLimit, uint incomingBandwidth, uint outgoingBandwidth,
            int bufferSize);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr enet_host_create(IntPtr address, IntPtr peerLimit, IntPtr channelLimit, uint incomingBandwidth, uint outgoingBandwidth,
            int bufferSize);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr enet_host_connect(IntPtr host, ref ENetAddress address, IntPtr channelCount, uint data);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_host_broadcast(IntPtr host, byte channelID, IntPtr packet);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_host_broadcast_exclude(IntPtr host, byte channelID, IntPtr packet, IntPtr excludedPeer);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_host_broadcast_selective(IntPtr host, byte channelID, IntPtr packet, IntPtr[] peers, IntPtr peersLength);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int enet_host_service(IntPtr host, out ENetEvent @event, uint timeout);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int enet_host_check_events(IntPtr host, out ENetEvent @event);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_host_channel_limit(IntPtr host, IntPtr channelLimit);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_host_bandwidth_limit(IntPtr host, uint incomingBandwidth, uint outgoingBandwidth);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint enet_host_get_peers_count(IntPtr host);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint enet_host_get_packets_sent(IntPtr host);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint enet_host_get_packets_received(IntPtr host);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint enet_host_get_bytes_sent(IntPtr host);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint enet_host_get_bytes_received(IntPtr host);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_host_set_max_duplicate_peers(IntPtr host, ushort number);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_host_set_intercept_callback(IntPtr host, IntPtr callback);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_host_set_checksum_callback(IntPtr host, IntPtr callback);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_host_flush(IntPtr host);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_host_destroy(IntPtr host);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_host_prevent_connections(IntPtr host, byte state);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_peer_throttle_configure(IntPtr peer, uint interval, uint acceleration, uint deceleration, uint threshold);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint enet_peer_get_id(IntPtr peer);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int enet_peer_get_ip(IntPtr peer, byte[] ip, IntPtr ipLength);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ushort enet_peer_get_port(IntPtr peer);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint enet_peer_get_mtu(IntPtr peer);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern PeerState enet_peer_get_state(IntPtr peer);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint enet_peer_get_rtt(IntPtr peer);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint enet_peer_get_last_rtt(IntPtr peer);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint enet_peer_get_lastsendtime(IntPtr peer);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint enet_peer_get_lastreceivetime(IntPtr peer);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong enet_peer_get_packets_sent(IntPtr peer);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong enet_peer_get_packets_lost(IntPtr peer);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern float enet_peer_get_packets_throttle(IntPtr peer);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong enet_peer_get_bytes_sent(IntPtr peer);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern ulong enet_peer_get_bytes_received(IntPtr peer);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr enet_peer_get_data(IntPtr peer);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_peer_set_data(IntPtr peer, IntPtr data);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern int enet_peer_send(IntPtr peer, byte channelID, IntPtr packet);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern IntPtr enet_peer_receive(IntPtr peer, out byte channelID);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_peer_ping(IntPtr peer);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_peer_ping_interval(IntPtr peer, uint pingInterval);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_peer_timeout(IntPtr peer, uint timeoutLimit, uint timeoutMinimum, uint timeoutMaximum);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_peer_disconnect(IntPtr peer, uint data);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_peer_disconnect_now(IntPtr peer, uint data);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_peer_disconnect_later(IntPtr peer, uint data);

        [DllImport(nativeLibrary, CallingConvention = CallingConvention.Cdecl)]
        internal static extern void enet_peer_reset(IntPtr peer);
    }
}
