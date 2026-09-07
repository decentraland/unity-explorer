using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace Crosstales.FB.Linux
{
    internal enum DBusMessageType : byte
    {
        Invalid = 0,
        MethodCall = 1,
        MethodReturn = 2,
        Error = 3,
        Signal = 4,
    }

    /// <summary>A protocol-level failure talking to the bus: malformed bytes, a refused handshake, a timeout or a closed socket.</summary>
    internal sealed class DBusException : Exception
    {
        public DBusException(string message) : base(message) { }
    }

    /// <summary>Type-code tables from the marshalling section of the D-Bus specification.</summary>
    internal static class DBusType
    {
        public static int Alignment(char code)
        {
            switch (code)
            {
                case 'y':
                case 'g':
                case 'v':
                    return 1;
                case 'n':
                case 'q':
                    return 2;
                case 'b':
                case 'i':
                case 'u':
                case 'h':
                case 's':
                case 'o':
                case 'a':
                    return 4;
                case 'x':
                case 't':
                case 'd':
                case '(':
                case '{':
                    return 8;
                default:
                    throw new DBusException($"Unknown D-Bus type code '{code}'.");
            }
        }

        /// <summary>Number of signature characters spanned by the single complete type that starts at <paramref name="index" />.</summary>
        public static int CompleteTypeLength(string signature, int index)
        {
            if (index >= signature.Length)
                throw new DBusException($"Truncated D-Bus signature '{signature}'.");

            char code = signature[index];

            if (code == 'a')
                return 1 + CompleteTypeLength(signature, index + 1);

            if (code != '(' && code != '{')
                return 1;

            int depth = 0;

            for (int i = index; i < signature.Length; i++)
            {
                char current = signature[i];

                if (current == '(' || current == '{')
                    depth++;
                else if (current == ')' || current == '}')
                {
                    depth--;

                    if (depth == 0)
                        return i - index + 1;
                }
            }

            throw new DBusException($"Unbalanced container in D-Bus signature '{signature}'.");
        }
    }

    /// <summary>Little-endian D-Bus marshaller. Alignment is relative to the start of the buffer, which must coincide with an 8-byte message boundary.</summary>
    internal sealed class DBusWriter
    {
        public readonly struct ArrayScope
        {
            internal readonly int LengthPosition;
            internal readonly int ElementsStart;

            internal ArrayScope(int lengthPosition, int elementsStart)
            {
                LengthPosition = lengthPosition;
                ElementsStart = elementsStart;
            }
        }

        private const int INITIAL_CAPACITY = 256;
        private const int MAX_SIGNATURE_LENGTH = 255;

        private byte[] buffer = new byte[INITIAL_CAPACITY];

        public int Position { get; private set; }

        public void Pad(int alignment)
        {
            int missing = (alignment - Position % alignment) % alignment;
            Ensure(missing);

            for (int i = 0; i < missing; i++)
                buffer[Position++] = 0;
        }

        public void WriteByte(byte value)
        {
            Ensure(1);
            buffer[Position++] = value;
        }

        public void WriteBool(bool value) =>
            WriteUInt32(value ? 1u : 0u);

        public void WriteUInt32(uint value)
        {
            Pad(4);
            Ensure(4);
            buffer[Position++] = (byte)value;
            buffer[Position++] = (byte)(value >> 8);
            buffer[Position++] = (byte)(value >> 16);
            buffer[Position++] = (byte)(value >> 24);
        }

        public void WriteString(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            WriteUInt32((uint)bytes.Length);
            WriteBytes(bytes);
            WriteByte(0);
        }

        public void WriteObjectPath(string value) =>
            WriteString(value);

        public void WriteSignature(string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);

            if (bytes.Length > MAX_SIGNATURE_LENGTH)
                throw new DBusException($"Signature '{value}' exceeds {MAX_SIGNATURE_LENGTH} bytes.");

            WriteByte((byte)bytes.Length);
            WriteBytes(bytes);
            WriteByte(0);
        }

        public void WriteBytes(byte[] bytes)
        {
            Ensure(bytes.Length);
            Array.Copy(bytes, 0, buffer, Position, bytes.Length);
            Position += bytes.Length;
        }

        /// <summary>Reserves the array length field and pads to the element alignment; the length is patched in by <see cref="EndArray" />.</summary>
        public ArrayScope BeginArray(int elementAlignment)
        {
            WriteUInt32(0);
            int lengthPosition = Position - 4;
            Pad(elementAlignment);
            return new ArrayScope(lengthPosition, Position);
        }

        public void EndArray(ArrayScope scope) =>
            PatchUInt32(scope.LengthPosition, (uint)(Position - scope.ElementsStart));

        public void BeginStruct() =>
            Pad(8);

        public void WriteVariantString(string value)
        {
            WriteSignature("s");
            WriteString(value);
        }

        public void WriteVariantBool(bool value)
        {
            WriteSignature("b");
            WriteBool(value);
        }

        public void WriteVariantByteArray(byte[] value)
        {
            WriteSignature("ay");
            ArrayScope scope = BeginArray(1);
            WriteBytes(value);
            EndArray(scope);
        }

        public void PatchUInt32(int position, uint value)
        {
            buffer[position] = (byte)value;
            buffer[position + 1] = (byte)(value >> 8);
            buffer[position + 2] = (byte)(value >> 16);
            buffer[position + 3] = (byte)(value >> 24);
        }

        public byte[] ToArray()
        {
            var result = new byte[Position];
            Array.Copy(buffer, result, Position);
            return result;
        }

        private void Ensure(int count)
        {
            if (Position + count <= buffer.Length)
                return;

            int capacity = buffer.Length * 2;

            while (capacity < Position + count)
                capacity *= 2;

            Array.Resize(ref buffer, capacity);
        }
    }

    /// <summary>
    ///     Signature-driven D-Bus unmarshaller. Alignment is computed relative to <c>origin</c>, the buffer index of the
    ///     8-byte message boundary the data was marshalled against.
    /// </summary>
    internal sealed class DBusReader
    {
        private readonly byte[] data;
        private readonly int origin;
        private readonly int end;
        private readonly bool littleEndian;

        public int Position { get; set; }

        public int Remaining => end - Position;

        public DBusReader(byte[] data, int origin, int end, bool littleEndian)
        {
            this.data = data;
            this.origin = origin;
            this.end = end;
            this.littleEndian = littleEndian;
            Position = origin;
        }

        /// <summary>Reads a whole little-endian buffer that starts on a message boundary.</summary>
        public DBusReader(byte[] data) : this(data, 0, data.Length, true) { }

        public void Pad(int alignment)
        {
            int missing = (alignment - (Position - origin) % alignment) % alignment;
            Require(missing);
            Position += missing;
        }

        public byte ReadByte()
        {
            Require(1);
            return data[Position++];
        }

        public bool ReadBool()
        {
            uint value = ReadUInt32();

            if (value > 1)
                throw new DBusException($"Invalid D-Bus boolean {value}.");

            return value == 1;
        }

        public ushort ReadUInt16()
        {
            Pad(2);
            Require(2);

            ushort value = littleEndian
                ? (ushort)(data[Position] | (data[Position + 1] << 8))
                : (ushort)((data[Position] << 8) | data[Position + 1]);

            Position += 2;
            return value;
        }

        public uint ReadUInt32()
        {
            Pad(4);
            Require(4);

            uint value = littleEndian
                ? (uint)(data[Position] | (data[Position + 1] << 8) | (data[Position + 2] << 16) | (data[Position + 3] << 24))
                : (uint)((data[Position] << 24) | (data[Position + 1] << 16) | (data[Position + 2] << 8) | data[Position + 3]);

            Position += 4;
            return value;
        }

        public ulong ReadUInt64()
        {
            Pad(8);
            Require(8);
            ulong value = 0;

            for (int i = 0; i < 8; i++)
            {
                int index = littleEndian ? 7 - i : i;
                value = (value << 8) | data[Position + index];
            }

            Position += 8;
            return value;
        }

        public double ReadDouble() =>
            BitConverter.Int64BitsToDouble((long)ReadUInt64());

        public string ReadString()
        {
            int length = checked((int)ReadUInt32());
            Require(length + 1);
            string value = Encoding.UTF8.GetString(data, Position, length);
            Position += length;

            if (data[Position++] != 0)
                throw new DBusException("D-Bus string is not NUL-terminated.");

            return value;
        }

        public string ReadObjectPath() =>
            ReadString();

        public string ReadSignature()
        {
            int length = ReadByte();
            Require(length + 1);
            string value = Encoding.ASCII.GetString(data, Position, length);
            Position += length;

            if (data[Position++] != 0)
                throw new DBusException("D-Bus signature is not NUL-terminated.");

            return value;
        }

        /// <summary>
        ///     Reads one value of the given complete signature. Arrays of strings come back as <c>string[]</c>, byte arrays as
        ///     <c>byte[]</c>, dictionaries as <c>Dictionary&lt;string, object&gt;</c>, other arrays and structs as <c>object[]</c>,
        ///     variants as their contained value.
        /// </summary>
        public object ReadValue(string signature)
        {
            int index = 0;
            object value = ReadValue(signature, ref index);

            if (index != signature.Length)
                throw new DBusException($"Signature '{signature}' holds more than one complete type.");

            return value;
        }

        public object ReadValue(string signature, ref int index)
        {
            char code = signature[index];

            switch (code)
            {
                case 'y':
                    index++;
                    return ReadByte();
                case 'b':
                    index++;
                    return ReadBool();
                case 'n':
                    index++;
                    return (short)ReadUInt16();
                case 'q':
                    index++;
                    return ReadUInt16();
                case 'i':
                    index++;
                    return (int)ReadUInt32();
                case 'u':
                case 'h':
                    index++;
                    return ReadUInt32();
                case 'x':
                    index++;
                    return (long)ReadUInt64();
                case 't':
                    index++;
                    return ReadUInt64();
                case 'd':
                    index++;
                    return ReadDouble();
                case 's':
                case 'o':
                    index++;
                    return ReadString();
                case 'g':
                    index++;
                    return ReadSignature();
                case 'v':
                    index++;
                    return ReadValue(ReadSignature());
                case 'a':
                    return ReadArray(signature, ref index);
                case '(':
                    return ReadStruct(signature, ref index);
                default:
                    throw new DBusException($"Unsupported D-Bus type code '{code}' in '{signature}'.");
            }
        }

        private object ReadArray(string signature, ref int index)
        {
            index++;
            int elementLength = DBusType.CompleteTypeLength(signature, index);
            string elementSignature = signature.Substring(index, elementLength);
            index += elementLength;

            int byteLength = checked((int)ReadUInt32());
            Pad(DBusType.Alignment(elementSignature[0]));
            int elementsEnd = Position + byteLength;

            if (elementsEnd > end)
                throw new DBusException("D-Bus array runs past the end of the message.");

            if (elementSignature == "y")
            {
                var bytes = new byte[byteLength];
                Array.Copy(data, Position, bytes, 0, byteLength);
                Position = elementsEnd;
                return bytes;
            }

            if (elementSignature == "s" || elementSignature == "o")
            {
                var strings = new List<string>();

                while (Position < elementsEnd)
                    strings.Add(ReadString());

                return strings.ToArray();
            }

            if (elementSignature[0] == '{')
            {
                int keyLength = DBusType.CompleteTypeLength(elementSignature, 1);
                string keySignature = elementSignature.Substring(1, keyLength);
                string valueSignature = elementSignature.Substring(1 + keyLength, elementSignature.Length - 2 - keyLength);
                var dictionary = new Dictionary<string, object>();

                while (Position < elementsEnd)
                {
                    Pad(8);
                    object key = ReadValue(keySignature);
                    dictionary[Convert.ToString(key, CultureInfo.InvariantCulture) ?? string.Empty] = ReadValue(valueSignature);
                }

                return dictionary;
            }

            var items = new List<object>();

            while (Position < elementsEnd)
                items.Add(ReadValue(elementSignature));

            return items.ToArray();
        }

        private object[] ReadStruct(string signature, ref int index)
        {
            index++;
            Pad(8);
            var fields = new List<object>();

            while (signature[index] != ')')
                fields.Add(ReadValue(signature, ref index));

            index++;
            return fields.ToArray();
        }

        private void Require(int count)
        {
            if (Position + count > end)
                throw new DBusException("D-Bus data ends before the value being read.");
        }
    }

    /// <summary>One D-Bus message: the header fields the client cares about plus the still-marshalled body.</summary>
    internal sealed class DBusMessage
    {
        public const byte FLAG_NO_REPLY_EXPECTED = 1;

        private const byte LITTLE_ENDIAN = (byte)'l';
        private const byte BIG_ENDIAN = (byte)'B';
        private const byte PROTOCOL_VERSION = 1;
        private const int FIXED_HEADER_LENGTH = 16;
        private const long MAX_MESSAGE_LENGTH = 1L << 27;

        private const byte FIELD_PATH = 1;
        private const byte FIELD_INTERFACE = 2;
        private const byte FIELD_MEMBER = 3;
        private const byte FIELD_ERROR_NAME = 4;
        private const byte FIELD_REPLY_SERIAL = 5;
        private const byte FIELD_DESTINATION = 6;
        private const byte FIELD_SENDER = 7;
        private const byte FIELD_SIGNATURE = 8;

        public DBusMessageType Type;
        public byte Flags;
        public uint Serial;
        public bool LittleEndian = true;
        public string? Path;
        public string? Interface;
        public string? Member;
        public string? ErrorName;
        public uint? ReplySerial;
        public string? Destination;
        public string? Sender;
        public string? Signature;
        public byte[] Body = Array.Empty<byte>();

        public static DBusMessage MethodCall(string destination, string path, string iface, string member) =>
            new ()
            {
                Type = DBusMessageType.MethodCall,
                Destination = destination,
                Path = path,
                Interface = iface,
                Member = member,
            };

        /// <summary>Total byte length of the message that starts at <paramref name="offset" />, or -1 while fewer than 16 bytes are available.</summary>
        public static int RequiredLength(byte[] data, int offset, int count)
        {
            if (count < FIXED_HEADER_LENGTH)
                return -1;

            bool littleEndian = IsLittleEndian(data[offset]);
            uint bodyLength = ReadRawUInt32(data, offset + 4, littleEndian);
            uint fieldsLength = ReadRawUInt32(data, offset + 12, littleEndian);
            long total = Align8(FIXED_HEADER_LENGTH + (long)fieldsLength) + bodyLength;

            if (total > MAX_MESSAGE_LENGTH)
                throw new DBusException($"D-Bus message of {total} bytes exceeds the protocol maximum.");

            return (int)total;
        }

        public static bool TryDecode(byte[] data, int offset, int count, [NotNullWhen(true)] out DBusMessage? message, out int consumed)
        {
            message = null;
            consumed = 0;
            int total = RequiredLength(data, offset, count);

            if (total < 0 || count < total)
                return false;

            bool littleEndian = IsLittleEndian(data[offset]);

            if (data[offset + 3] != PROTOCOL_VERSION)
                throw new DBusException($"Unsupported D-Bus protocol version {data[offset + 3]}.");

            uint bodyLength = ReadRawUInt32(data, offset + 4, littleEndian);
            uint fieldsLength = ReadRawUInt32(data, offset + 12, littleEndian);

            var decoded = new DBusMessage
            {
                LittleEndian = littleEndian,
                Type = (DBusMessageType)data[offset + 1],
                Flags = data[offset + 2],
                Serial = ReadRawUInt32(data, offset + 8, littleEndian),
            };

            var fields = new DBusReader(data, offset, offset + FIXED_HEADER_LENGTH + (int)fieldsLength, littleEndian) { Position = offset + FIXED_HEADER_LENGTH };

            while (fields.Remaining > 0)
            {
                fields.Pad(8);

                if (fields.Remaining == 0)
                    break;

                byte code = fields.ReadByte();
                object value = fields.ReadValue(fields.ReadSignature());
                decoded.AssignField(code, value);
            }

            int bodyStart = offset + (int)Align8(FIXED_HEADER_LENGTH + (long)fieldsLength);
            decoded.Body = new byte[bodyLength];
            Array.Copy(data, bodyStart, decoded.Body, 0, (int)bodyLength);

            message = decoded;
            consumed = total;
            return true;
        }

        public DBusReader OpenBody() =>
            new (Body, 0, Body.Length, LittleEndian);

        public byte[] Encode()
        {
            var writer = new DBusWriter();
            writer.WriteByte(LITTLE_ENDIAN);
            writer.WriteByte((byte)Type);
            writer.WriteByte(Flags);
            writer.WriteByte(PROTOCOL_VERSION);
            writer.WriteUInt32((uint)Body.Length);
            writer.WriteUInt32(Serial);

            DBusWriter.ArrayScope fields = writer.BeginArray(8);
            WriteStringField(writer, FIELD_PATH, "o", Path);
            WriteStringField(writer, FIELD_INTERFACE, "s", Interface);
            WriteStringField(writer, FIELD_MEMBER, "s", Member);
            WriteStringField(writer, FIELD_ERROR_NAME, "s", ErrorName);

            if (ReplySerial.HasValue)
            {
                writer.BeginStruct();
                writer.WriteByte(FIELD_REPLY_SERIAL);
                writer.WriteSignature("u");
                writer.WriteUInt32(ReplySerial.Value);
            }

            WriteStringField(writer, FIELD_DESTINATION, "s", Destination);
            WriteStringField(writer, FIELD_SENDER, "s", Sender);

            if (Signature is { Length: > 0 } signature)
            {
                writer.BeginStruct();
                writer.WriteByte(FIELD_SIGNATURE);
                writer.WriteSignature("g");
                writer.WriteSignature(signature);
            }

            writer.EndArray(fields);
            writer.Pad(8);
            writer.WriteBytes(Body);
            return writer.ToArray();
        }

        private void AssignField(byte code, object value)
        {
            switch (code)
            {
                case FIELD_PATH:
                    Path = value as string;
                    break;
                case FIELD_INTERFACE:
                    Interface = value as string;
                    break;
                case FIELD_MEMBER:
                    Member = value as string;
                    break;
                case FIELD_ERROR_NAME:
                    ErrorName = value as string;
                    break;
                case FIELD_REPLY_SERIAL:
                    ReplySerial = value as uint?;
                    break;
                case FIELD_DESTINATION:
                    Destination = value as string;
                    break;
                case FIELD_SENDER:
                    Sender = value as string;
                    break;
                case FIELD_SIGNATURE:
                    Signature = value as string;
                    break;
            }
        }

        private static void WriteStringField(DBusWriter writer, byte code, string typeSignature, string? value)
        {
            if (value == null)
                return;

            writer.BeginStruct();
            writer.WriteByte(code);
            writer.WriteSignature(typeSignature);
            writer.WriteString(value);
        }

        private static bool IsLittleEndian(byte marker)
        {
            if (marker == LITTLE_ENDIAN)
                return true;

            if (marker == BIG_ENDIAN)
                return false;

            throw new DBusException($"Invalid D-Bus endianness marker 0x{marker:x2}.");
        }

        private static uint ReadRawUInt32(byte[] data, int offset, bool littleEndian) =>
            littleEndian
                ? (uint)(data[offset] | (data[offset + 1] << 8) | (data[offset + 2] << 16) | (data[offset + 3] << 24))
                : (uint)((data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3]);

        private static long Align8(long value) =>
            (value + 7) & ~7L;
    }
}
