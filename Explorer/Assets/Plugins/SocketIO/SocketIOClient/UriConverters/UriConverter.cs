﻿using System;
using System.Collections.Generic;
using System.Text;

namespace SocketIOClient.UriConverters
{
    public static class UriConverter
    {
        public static Uri GetServerUri(bool ws, Uri serverUri, EngineIO eio, string path, IEnumerable<KeyValuePair<string, string>> queryParams)
        {
            var builder = new StringBuilder();

            if (serverUri.Scheme == "https" || serverUri.Scheme == "wss") { builder.Append(ws ? "wss://" : "https://"); }
            else if (serverUri.Scheme == "http" || serverUri.Scheme == "ws") { builder.Append(ws ? "ws://" : "http://"); }
            else { throw new ArgumentException("Only supports 'http, https, ws, wss' protocol"); }

            builder.Append(serverUri.Host);

            if (!serverUri.IsDefaultPort) { builder.Append(":").Append(serverUri.Port); }

            if (string.IsNullOrEmpty(path)) { builder.Append("/socket.io"); }
            else { builder.Append(path); }

            builder
               .Append("/?EIO=")
               .Append((int)eio)
               .Append("&transport=")
               .Append(ws ? "websocket" : "polling");

            if (queryParams != null)
            {
                foreach (KeyValuePair<string, string> item in queryParams) { builder.Append('&').Append(item.Key).Append('=').Append(item.Value); }
            }

            return new Uri(builder.ToString());
        }
    }
}
