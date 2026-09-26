using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.DebugUtilities
{
    public static class BytesFormatter
    {
        public enum DataSizeUnit
        {
            /// <summary>
            ///     1 bit
            /// </summary>
            Bit,

            /// <summary>
            ///     8 bits
            /// </summary>
            Byte,

            /// <summary>
            ///     1000 bits
            /// </summary>
            Kilobit,

            /// <summary>
            ///     1024 bytes
            /// </summary>
            Kilobyte,

            /// <summary>
            ///     1000 kilobits, or 1,000,000 bits
            /// </summary>
            Megabit,

            /// <summary>
            ///     1024 kilobytes, or 1,048,576 bytes
            /// </summary>
            Megabyte,

            /// <summary>
            ///     1000 megabits, or 1,000,000,000 bits
            /// </summary>
            Gigabit,

            /// <summary>
            ///     1024 megabytes, or 1,073,741,824 bytes
            /// </summary>
            Gigabyte,

            /// <summary>
            ///     1000 gigabits, or 1,000,000,000,000 bits
            /// </summary>
            Terabit,

            /// <summary>
            ///     1024 gigabytes, or 1,099,511,627,776 bytes
            /// </summary>
            Terabyte,

            /// <summary>
            ///     1000 terabits, or 1,000,000,000,000,000 bits
            /// </summary>
            Petabit,

            /// <summary>
            ///     1024 terabytes, or 1,125,899,906,842,624 bytes
            /// </summary>
            Petabyte,

            /// <summary>
            ///     1000 petabits, or 1,000,000,000,000,000,000 bits
            /// </summary>
            Exabit,

            /// <summary>
            ///     1024 petabytes, or 1,152,921,504,606,846,976 bytes
            /// </summary>
            Exabyte,
        }

        private const int DEFAULT_PRECISION = 2;

        private static readonly IFormatProvider FORMAT_PROVIDER = CreateFormatProvider(DEFAULT_PRECISION);

        private static IFormatProvider CreateFormatProvider(int precision)
        {
            CultureInfo formatProvider = CultureInfo.CurrentCulture;
            var numberFormatInfo = (NumberFormatInfo)(formatProvider.GetFormat(typeof(NumberFormatInfo)) as NumberFormatInfo ?? NumberFormatInfo.CurrentInfo).Clone();
            numberFormatInfo.NumberDecimalDigits = precision;
            return numberFormatInfo;
        }

        public static string Normalize(ulong numberOfUnits, bool isBit)
        {
            DataSizeUnit originalDataSize = isBit ? DataSizeUnit.Bit : DataSizeUnit.Byte;

            ulong inputBytes = isBit ? numberOfUnits / 8 : numberOfUnits;
            var orderOfMagnitude = (int)Mathf.Max(0, Mathf.Floor(Mathf.Log(Mathf.Abs(inputBytes), isBit ? 1000 : 1024)));
            DataSizeUnit outputUnit = ForMagnitude(orderOfMagnitude, isBit);
            return Convert(numberOfUnits, originalDataSize, outputUnit).ToString("N", FORMAT_PROVIDER) + " " + outputUnit.ToAbbreviation();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static double Convert(ulong numberOfUnits, DataSizeUnit dataSize, DataSizeUnit destination) =>
            numberOfUnits * CountBitsInUnit(dataSize) / (double)CountBitsInUnit(destination);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ByteToMB(this ulong numberOfUnits) =>
            numberOfUnits * CountBitsInUnit(DataSizeUnit.Byte) / (float)CountBitsInUnit(DataSizeUnit.Megabyte);

        private static ulong CountBitsInUnit(DataSizeUnit sourceUnit)
        {
            switch (sourceUnit)
            {
                case DataSizeUnit.Byte:
                    return 8;
                case DataSizeUnit.Kilobyte:
                    return 8 << 10;
                case DataSizeUnit.Megabyte:
                    return (ulong)8 << 20;
                case DataSizeUnit.Gigabyte:
                    return (ulong)8 << 30;
                case DataSizeUnit.Terabyte:
                    return (ulong)8 << 40;
                case DataSizeUnit.Petabyte:
                    return (ulong)8 << 50;
                case DataSizeUnit.Exabyte:
                    return (ulong)8 << 60;

                case DataSizeUnit.Bit:
                    return 1;
                case DataSizeUnit.Kilobit:
                    return 1000L;
                case DataSizeUnit.Megabit:
                    return 1000L * 1000;
                case DataSizeUnit.Gigabit:
                    return 1000L * 1000 * 1000;
                case DataSizeUnit.Terabit:
                    return 1000L * 1000 * 1000 * 1000;
                case DataSizeUnit.Petabit:
                    return 1000L * 1000 * 1000 * 1000 * 1000;
                case DataSizeUnit.Exabit:
                    return 1000L * 1000 * 1000 * 1000 * 1000 * 1000;

                default:
                    throw new ArgumentOutOfRangeException(nameof(sourceUnit), sourceUnit, null);
            }
        }

        /// <summary>Get the short version of this DataSizeUnit's name (1-3 characters), such as <c>MB</c>.</summary>
        /// <param name="dataSizeUnit"></param>
        /// <param name="iec">
        ///     <c>true</c> to return the IEC abbreviation (KiB, MiB, etc.), or <c>false</c> (the default) to return
        ///     the JEDEC abbreviation (KB, MB, etc.)
        /// </param>
        /// <returns>The abbreviation for this DataSizeUnit.</returns>
        public static string ToAbbreviation(this DataSizeUnit dataSizeUnit, bool iec = false)
        {
            switch (dataSizeUnit)
            {
                case DataSizeUnit.Byte:
                    return "B";
                case DataSizeUnit.Kilobyte:
                    return iec ? "KiB" : "KB";
                case DataSizeUnit.Megabyte:
                    return iec ? "MiB" : "MB";
                case DataSizeUnit.Gigabyte:
                    return iec ? "GiB" : "GB";
                case DataSizeUnit.Terabyte:
                    return iec ? "TiB" : "TB";
                case DataSizeUnit.Petabyte:
                    return iec ? "PiB" : "PB";
                case DataSizeUnit.Exabyte:
                    return iec ? "EiB" : "EB";

                case DataSizeUnit.Bit:
                    return "b";
                case DataSizeUnit.Kilobit:
                    return "kb";
                case DataSizeUnit.Megabit:
                    return "mb";
                case DataSizeUnit.Gigabit:
                    return "gb";
                case DataSizeUnit.Terabit:
                    return "tb";
                case DataSizeUnit.Petabit:
                    return "pb";
                case DataSizeUnit.Exabit:
                    return "eb";

                default:
                    throw new ArgumentOutOfRangeException(nameof(dataSizeUnit), dataSizeUnit, null);
            }
        }

        private static DataSizeUnit ForMagnitude(int orderOfMagnitude, bool useBitsInsteadOfBytes)
        {
            switch (orderOfMagnitude)
            {
                case 0:
                    return useBitsInsteadOfBytes ? DataSizeUnit.Bit : DataSizeUnit.Byte;
                case 1:
                    return useBitsInsteadOfBytes ? DataSizeUnit.Kilobit : DataSizeUnit.Kilobyte;
                case 2:
                    return useBitsInsteadOfBytes ? DataSizeUnit.Megabit : DataSizeUnit.Megabyte;
                case 3:
                    return useBitsInsteadOfBytes ? DataSizeUnit.Gigabit : DataSizeUnit.Gigabyte;
                case 4:
                    return useBitsInsteadOfBytes ? DataSizeUnit.Terabit : DataSizeUnit.Terabyte;
                case 5:
                    return useBitsInsteadOfBytes ? DataSizeUnit.Petabit : DataSizeUnit.Petabyte;
                default:
                    return useBitsInsteadOfBytes ? DataSizeUnit.Exabit : DataSizeUnit.Exabyte;
            }
        }
    }
}
