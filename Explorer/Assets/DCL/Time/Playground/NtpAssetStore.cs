using System;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

/// <summary>
/// Represents an NTP (Network Time Protocol) client.
/// </summary>
public class NtpAssetStore : IDisposable
{
    private readonly string _server;
    private Socket _socket;
    private bool disposedValue;

    /// <summary>
    /// Initializes a new instance of the <see cref="NtpAssetStore"/> class with the default NTP server address.
    /// </summary>
    public NtpAssetStore()
    {
        _server = "pool.ntp.org";
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="NtpAssetStore"/> class with the specified NTP server address.
    /// </summary>
    /// <param name="server">The address of the NTP server.</param>
    public NtpAssetStore(string server)
    {
        _server = server;
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    }

    private uint SwapEndianness(ulong x)
    {
        return (uint)(((x & 0x000000ff) << 24) + ((x & 0x0000ff00) << 8) + ((x & 0x00ff0000) >> 8) + ((x & 0xff000000) >> 24));
    }

    /// <summary>
    /// Retrieves the current network time from the NTP server.
    /// </summary>
    /// <returns>The current network time.</returns>
    /// <exception cref="InvalidOperationException">Thrown when failed to retrieve network time.</exception>
    public DateTime GetNetworkTime()
    {
        try
        {
            // Create a byte array to hold NTP data and set the first byte to 0x1B (indicating a client request)
            byte[] ntpData = new byte[48];
            ntpData[0] = 0x1B;

            // Retrieve the IP addresses associated with the specified NTP server
            IPAddress[] addresses = Dns.GetHostEntry(_server).AddressList;

            // Create an endpoint representing the NTP server
            IPEndPoint ipEndPoint = new IPEndPoint(addresses[0], 123); // Port 123 is the standard NTP port

            // Connect to the NTP server using the socket
            _socket.Connect(ipEndPoint);

            // Set a receive timeout for the socket to 3000 milliseconds (3 seconds)
            _socket.ReceiveTimeout = 3000;

            // Send the NTP request packet to the server
            _socket.Send(ntpData);

            // Receive the NTP response packet from the server
            _socket.Receive(ntpData);

            // Extract the server reply time from the response packet
            const byte serverReplyTime = 40; // Offset in the response packet where the server reply time starts
            ulong intPart = BitConverter.ToUInt32(ntpData, serverReplyTime); // Extract the integer part of the time
            ulong fractPart = BitConverter.ToUInt32(ntpData, serverReplyTime + 4); // Extract the fractional part of the time
            intPart = SwapEndianness(intPart); // Swap the endianness of the integer part
            fractPart = SwapEndianness(fractPart); // Swap the endianness of the fractional part

            // Calculate the total number of milliseconds since 1900-01-01
            ulong milliseconds = (intPart * 1000) + (fractPart * 1000 / 0x100000000L);

            // Create a DateTime object representing the network time
            DateTime networkDateTime = new DateTime(1900, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds((long)milliseconds);

            // Return the network time
            return networkDateTime;
        }
        catch (Exception ex)
        {
            // If an exception occurs during the process, throw an InvalidOperationException with a descriptive error message
            InvalidOperationException IVOPEX = new InvalidOperationException("Failed to get network time.", ex);
            Debug.LogException(IVOPEX);
            throw IVOPEX;
        }
        finally
        {
            _socket?.Close();
        }
    }

    /// <summary>
    /// Releases the unmanaged resources used by the <see cref="NtpAssetStore"/> class and optionally disposes of the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                _socket?.Dispose();
            }
            _socket = null;
            disposedValue = true;
        }
    }

    /// <summary>
    /// Releases all resources used by the current instance of the <see cref="NtpAssetStore"/> class.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
