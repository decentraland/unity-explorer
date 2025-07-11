using Best.HTTP;
using Best.HTTP.Hosts.Connections.HTTP1;
using Best.HTTP.Shared.Extensions;
using Best.HTTP.Shared.PlatformSupport.Memory;
using DCL.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DCL.WebRequests.HTTP2
{
    public static class Http2Utils
    {
        public static Dictionary<string, string>? FlattenHeaders(this Dictionary<string, List<string>>? headers)
        {
            if (headers == null) return null;

            var dict = new Dictionary<string, string>(headers.Count);

            foreach (KeyValuePair<string, List<string>> header in headers)
                dict[header.Key] = string.Join(',', header.Value);

            return dict;
        }

        public static bool TryParseHeaderLong(Dictionary<string, List<string>> headers, string header, ReportData reportData, out long value)
        {
            string? headerValue = headers.GetFirstHeaderValue(header);

            if (headerValue != null && long.TryParse(headerValue, out value)) return true;

            ReportHub.LogWarning(reportData, $"{header}:{headerValue} can't be parsed to \"ulong\"");
            value = 0;
            return false;
        }

        /// <summary>
        ///     It's a copy of <see cref="HTTPResponse.ReadTo" /> as it's not exposed
        /// </summary>
        /// <returns></returns>
        internal static string ReadTo(Stream stream, byte blocker1, byte blocker2)
        {
            byte[] readBuf = BufferPool.Get(1024, true);

            try
            {
                var bufpos = 0;

                int ch = stream.ReadByte();

                while (ch != blocker1 && ch != blocker2 && ch != -1)
                {
                    if (ch > 0x7f) //replaces asciitostring
                        ch = '?';

                    //make buffer larger if too short
                    if (readBuf.Length <= bufpos)
                        BufferPool.Resize(ref readBuf, readBuf.Length * 2, true, true);

                    if (bufpos > 0 || !char.IsWhiteSpace((char)ch)) //trimstart
                        readBuf[bufpos++] = (byte)ch;

                    ch = stream.ReadByte();
                }

                while (bufpos > 0 && char.IsWhiteSpace((char)readBuf[bufpos - 1]))
                    bufpos--;

                return Encoding.UTF8.GetString(readBuf, 0, bufpos);
            }
            finally { BufferPool.Release(readBuf); }
        }

        internal static string ReadTo(Stream stream, byte blocker)
        {
            byte[] readBuf = BufferPool.Get(1024, true);

            try
            {
                var bufpos = 0;

                int ch = stream.ReadByte();

                while (ch != blocker && ch != -1)
                {
                    if (ch > 0x7f) //replaces asciitostring
                        ch = '?';

                    //make buffer larger if too short
                    if (readBuf.Length <= bufpos)
                        BufferPool.Resize(ref readBuf, readBuf.Length * 2, true, false);

                    if (bufpos > 0 || !char.IsWhiteSpace((char)ch)) //trimstart
                        readBuf[bufpos++] = (byte)ch;

                    ch = stream.ReadByte();
                }

                while (bufpos > 0 && char.IsWhiteSpace((char)readBuf[bufpos - 1]))
                    bufpos--;

                return Encoding.UTF8.GetString(readBuf, 0, bufpos);
            }
            finally { BufferPool.Release(readBuf); }
        }

        internal static void LoadHeaders(Stream headersStream, Dictionary<string, List<string>> result)
        {
            string headerName = ReadTo(headersStream, (byte)':', Constants.LF);

            while (headerName != string.Empty)
            {
                string value = ReadTo(headersStream, Constants.LF);

                result.AddHeader(headerName, value);

                headerName = ReadTo(headersStream, (byte)':', Constants.LF);
            }
        }
    }
}
