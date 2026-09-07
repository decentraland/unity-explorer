using Crosstales.FB.Linux;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Crosstales.FB.Tests
{
    public class DBusWireShould
    {
        // Replies captured verbatim from a live session bus (dbus-broker), with its 0xffffffff serial and NO_REPLY_EXPECTED flag.
        private const string RECORDED_HELLO_REPLY =
            "6c0201010d000000ffffffff47000000050175000100000007017300140000006f72672e667265656465736b746f702e44427573000000000601730008"
            + "0000003a312e313430363300000000000000000801670001730000080000003a312e313430363300";

        private const string RECORDED_NAME_ACQUIRED =
            "6c0401010d000000ffffffff9700000007017300140000006f72672e667265656465736b746f702e444275730000000006017300080000003a312e31"
            + "34303633000000000000000001016f00150000002f6f72672f667265656465736b746f702f4442757300000002017300140000006f72672e66726565"
            + "6465736b746f702e4442757300000000030173000c0000004e616d654163717569726564000000000801670001730000080000003a312e313430363300";

        private const string RECORDED_ADD_MATCH_REPLY =
            "6c02010100000000ffffffff46000000050175000200000007017300140000006f72672e667265656465736b746f702e44427573000000000601730008"
            + "0000003a312e313430363300000000000000000801670000000000";

        private const string RECORDED_SERVICE_UNKNOWN =
            "6c03010120000000ffffffff79000000050175000300000007017300140000006f72672e667265656465736b746f702e44427573000000000401730029"
            + "0000006f72672e667265656465736b746f702e444275732e4572726f722e53657276696365556e6b6e6f776e000000000000000801670001730000060173"
            + "00080000003a312e313430363300000000000000001b000000546865206e616d65206973206e6f74206163746976617461626c6500";

        private const string RECORDED_UNIQUE_NAME = ":1.14063";

        // Hello, serial 1, fields in this codec's order (PATH, INTERFACE, MEMBER, DESTINATION); offsets noted per fragment.
        private const string EXPECTED_HELLO_CALL =
            "6c010001" + "00000000" + "01000000" + "6d000000"                                  // 0: fixed header, fields array = 109 bytes
            + "01016f00" + "15000000" + "2f6f72672f667265656465736b746f702f44427573" + "00" + "0000"  // 16: PATH, ends at 46, pad to 48
            + "02017300" + "14000000" + "6f72672e667265656465736b746f702e44427573" + "00" + "000000"  // 48: INTERFACE, ends at 77, pad to 80
            + "03017300" + "05000000" + "48656c6c6f" + "00" + "0000"                                   // 80: MEMBER, ends at 94, pad to 96
            + "06017300" + "14000000" + "6f72672e667265656465736b746f702e44427573" + "00" + "000000"; // 96: DESTINATION, ends at 125, pad to 128

        [Test]
        public void MarshalTheHelloCallByteForByte()
        {
            // Arrange
            DBusMessage hello = DBusMessage.MethodCall(DBusConnection.BUS_NAME, DBusConnection.BUS_PATH, DBusConnection.BUS_INTERFACE, "Hello");
            hello.Serial = 1;

            // Act
            byte[] encoded = hello.Encode();

            // Assert
            Assert.AreEqual(EXPECTED_HELLO_CALL, ToHex(encoded));
            Assert.AreEqual(128, encoded.Length);
        }

        [Test]
        public void ParseTheRecordedHelloReply()
        {
            // Arrange
            byte[] data = FromHex(RECORDED_HELLO_REPLY);

            // Act
            bool decoded = DBusMessage.TryDecode(data, 0, data.Length, out DBusMessage? message, out int consumed);

            // Assert
            Assert.IsTrue(decoded);
            Assert.AreEqual(data.Length, consumed);
            Assert.AreEqual(DBusMessageType.MethodReturn, message!.Type);
            Assert.AreEqual(DBusMessage.FLAG_NO_REPLY_EXPECTED, message.Flags);
            Assert.AreEqual(uint.MaxValue, message.Serial);
            Assert.AreEqual(1u, message.ReplySerial);
            Assert.AreEqual(DBusConnection.BUS_NAME, message.Sender);
            Assert.AreEqual(RECORDED_UNIQUE_NAME, message.Destination);
            Assert.AreEqual("s", message.Signature);
            Assert.AreEqual(RECORDED_UNIQUE_NAME, message.OpenBody().ReadString());
        }

        [Test]
        public void ParseTheRecordedNameAcquiredSignal()
        {
            // Arrange
            byte[] data = FromHex(RECORDED_NAME_ACQUIRED);

            // Act
            DBusMessage.TryDecode(data, 0, data.Length, out DBusMessage? message, out _);

            // Assert
            Assert.AreEqual(DBusMessageType.Signal, message!.Type);
            Assert.AreEqual(DBusConnection.BUS_PATH, message.Path);
            Assert.AreEqual(DBusConnection.BUS_INTERFACE, message.Interface);
            Assert.AreEqual("NameAcquired", message.Member);
            Assert.IsNull(message.ReplySerial);
            Assert.AreEqual(RECORDED_UNIQUE_NAME, message.OpenBody().ReadString());
        }

        [Test]
        public void ParseTheRecordedEmptyAddMatchReply()
        {
            // Arrange
            byte[] data = FromHex(RECORDED_ADD_MATCH_REPLY);

            // Act
            DBusMessage.TryDecode(data, 0, data.Length, out DBusMessage? message, out int consumed);

            // Assert
            Assert.AreEqual(data.Length, consumed);
            Assert.AreEqual(DBusMessageType.MethodReturn, message!.Type);
            Assert.AreEqual(2u, message.ReplySerial);
            Assert.AreEqual(string.Empty, message.Signature);
            Assert.AreEqual(0, message.Body.Length);
        }

        [Test]
        public void ParseTheRecordedServiceUnknownError()
        {
            // Arrange
            byte[] data = FromHex(RECORDED_SERVICE_UNKNOWN);

            // Act
            DBusMessage.TryDecode(data, 0, data.Length, out DBusMessage? message, out _);

            // Assert
            Assert.AreEqual(DBusMessageType.Error, message!.Type);
            Assert.AreEqual("org.freedesktop.DBus.Error.ServiceUnknown", message.ErrorName);
            Assert.AreEqual(3u, message.ReplySerial);
            Assert.AreEqual("s", message.Signature);
            Assert.AreEqual("The name is not activatable", message.OpenBody().ReadString());
        }

        [Test]
        public void SplitTwoMessagesReceivedInOneBuffer()
        {
            // Arrange
            byte[] first = FromHex(RECORDED_HELLO_REPLY);
            byte[] second = FromHex(RECORDED_NAME_ACQUIRED);
            var stream = new byte[first.Length + second.Length];
            Array.Copy(first, stream, first.Length);
            Array.Copy(second, 0, stream, first.Length, second.Length);

            // Act
            bool firstDecoded = DBusMessage.TryDecode(stream, 0, stream.Length, out DBusMessage? reply, out int firstConsumed);
            bool secondDecoded = DBusMessage.TryDecode(stream, firstConsumed, stream.Length - firstConsumed, out DBusMessage? signal, out int secondConsumed);

            // Assert
            Assert.IsTrue(firstDecoded);
            Assert.IsTrue(secondDecoded);
            Assert.AreEqual(first.Length, firstConsumed);
            Assert.AreEqual(second.Length, secondConsumed);
            Assert.AreEqual(DBusMessageType.MethodReturn, reply!.Type);
            Assert.AreEqual("NameAcquired", signal!.Member);
        }

        [Test]
        public void ReportAnIncompleteMessageWithoutConsumingIt()
        {
            // Arrange
            byte[] full = FromHex(RECORDED_HELLO_REPLY);
            const int PARTIAL = 50;

            // Act
            bool decoded = DBusMessage.TryDecode(full, 0, PARTIAL, out DBusMessage? message, out int consumed);
            int required = DBusMessage.RequiredLength(full, 0, PARTIAL);

            // Assert
            Assert.IsFalse(decoded);
            Assert.IsNull(message);
            Assert.AreEqual(0, consumed);
            Assert.AreEqual(full.Length, required);
            Assert.AreEqual(-1, DBusMessage.RequiredLength(full, 0, 15));
        }

        [Test]
        public void AlignArraysStructsAndVariantsPerSpecification()
        {
            // Arrange
            var writer = new DBusWriter();
            writer.WriteByte(7);

            // Act
            DBusWriter.ArrayScope scope = writer.BeginArray(8);
            int elementsStart = writer.Position;
            writer.BeginStruct();
            writer.WriteUInt32(42);
            writer.WriteString("a");
            writer.BeginStruct();
            writer.WriteUInt32(43);
            writer.WriteString("bc");
            writer.EndArray(scope);
            byte[] bytes = writer.ToArray();
            var reader = new DBusReader(bytes);
            byte marker = reader.ReadByte();
            var items = (object[])reader.ReadValue("a(us)");

            // Assert
            Assert.AreEqual(8, elementsStart);
            Assert.AreEqual(7, marker);
            Assert.AreEqual(2, items.Length);
            CollectionAssert.AreEqual(new object[] { 42u, "a" }, (object[])items[0]);
            CollectionAssert.AreEqual(new object[] { 43u, "bc" }, (object[])items[1]);
            Assert.AreEqual(bytes.Length, reader.Position);
        }

        [Test]
        public void RoundTripADictionaryOfVariants()
        {
            // Arrange
            var writer = new DBusWriter();
            DBusWriter.ArrayScope scope = writer.BeginArray(8);
            writer.BeginStruct();
            writer.WriteString("flag");
            writer.WriteVariantBool(true);
            writer.BeginStruct();
            writer.WriteString("name");
            writer.WriteVariantString("value");
            writer.BeginStruct();
            writer.WriteString("bytes");
            writer.WriteVariantByteArray(new byte[] { 1, 2, 0 });
            writer.EndArray(scope);

            // Act
            var dictionary = (Dictionary<string, object>)new DBusReader(writer.ToArray()).ReadValue("a{sv}");

            // Assert
            Assert.AreEqual(true, dictionary["flag"]);
            Assert.AreEqual("value", dictionary["name"]);
            CollectionAssert.AreEqual(new byte[] { 1, 2, 0 }, (byte[])dictionary["bytes"]);
        }

        [Test]
        public void ReadBigEndianIntegersWhenTheMarkerSaysSo()
        {
            // Arrange
            var data = new byte[] { 0x00, 0x00, 0x00, 0x2a, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00 };
            var reader = new DBusReader(data, 0, data.Length, false);

            // Act
            uint first = reader.ReadUInt32();
            ulong second = reader.ReadUInt64();

            // Assert
            Assert.AreEqual(42u, first);
            Assert.AreEqual(1ul << 32, second);
        }

        [Test]
        public void RejectAValueThatRunsPastTheBuffer()
        {
            // Arrange
            var reader = new DBusReader(new byte[] { 0x10, 0x00, 0x00, 0x00, 0x61 });

            // Act & Assert
            Assert.Throws<DBusException>(() => reader.ReadString());
        }

        [Test]
        public void MeasureCompleteTypesInASignature()
        {
            // Act & Assert
            Assert.AreEqual(1, DBusType.CompleteTypeLength("sv", 0));
            Assert.AreEqual(5, DBusType.CompleteTypeLength("a{sv}", 0));
            Assert.AreEqual(9, DBusType.CompleteTypeLength("a(sa(us))", 0));
            Assert.AreEqual(2, DBusType.CompleteTypeLength("uas", 1));
            Assert.Throws<DBusException>(() => DBusType.CompleteTypeLength("(s", 0));
        }

        [Test]
        public void ParseUnixTransportsOutOfBusAddresses()
        {
            // Act
            UnixEndPoint? path = DBusAddress.ParseUnix("unix:path=/run/user/1001/bus");
            UnixEndPoint? abstractName = DBusAddress.ParseUnix("unix:abstract=/tmp/dbus-XYZ,guid=abc");
            UnixEndPoint? escaped = DBusAddress.ParseUnix("tcp:host=localhost,port=1;unix:path=/tmp/a%20b%2Fc");
            UnixEndPoint? none = DBusAddress.ParseUnix("tcp:host=localhost,port=1");

            // Assert
            Assert.AreEqual("/run/user/1001/bus", path!.Path);
            Assert.IsFalse(path.IsAbstract);
            Assert.AreEqual("/tmp/dbus-XYZ", abstractName!.Path);
            Assert.IsTrue(abstractName.IsAbstract);
            Assert.AreEqual("/tmp/a b/c", escaped!.Path);
            Assert.IsNull(none);
        }

        [Test]
        public void SerializeAUnixSocketAddress()
        {
            // Arrange
            var endPoint = new UnixEndPoint("/tmp/s", false);
            var abstractEndPoint = new UnixEndPoint("name", true);

            // Act
            System.Net.SocketAddress address = endPoint.Serialize();
            System.Net.SocketAddress abstractAddress = abstractEndPoint.Serialize();

            // Assert
            Assert.AreEqual(System.Net.Sockets.AddressFamily.Unix, address.Family);
            Assert.AreEqual(2 + 6 + 1, address.Size);
            Assert.AreEqual((byte)'/', address[2]);
            Assert.AreEqual(0, address[address.Size - 1]);
            Assert.AreEqual(0, abstractAddress[2]);
            Assert.AreEqual((byte)'n', abstractAddress[3]);
            Assert.AreEqual("/tmp/s", ((UnixEndPoint)endPoint.Create(address)).Path);
        }

        private static string ToHex(byte[] bytes)
        {
            var builder = new System.Text.StringBuilder(bytes.Length * 2);

            foreach (byte value in bytes)
                builder.Append(value.ToString("x2"));

            return builder.ToString();
        }

        internal static byte[] FromHex(string hex)
        {
            var bytes = new byte[hex.Length / 2];

            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = byte.Parse(hex.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

            return bytes;
        }
    }
}
