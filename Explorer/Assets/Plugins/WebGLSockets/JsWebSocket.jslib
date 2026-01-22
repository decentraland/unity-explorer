mergeInto(
  LibraryManager.library,
  (() => {
    const WS_STATE_NONE = 0;
    const WS_STATE_CONNECTING = 1;
    const WS_STATE_OPEN = 2;
    const WS_STATE_CLOSE_SENT = 3;
    const WS_STATE_CLOSE_RECEIVED = 4;
    const WS_STATE_CLOSED = 5;
    const WS_STATE_ABORTED = 6;

    let nextHandleId = 1;
    const sockets = {};
    const states = {};
    const queues = {};

    function attachHandlers(id, ws) {
      ws.binaryType = "arraybuffer";

      ws.onopen = () => {
        states[id] = WS_STATE_OPEN;
      };

      ws.onclose = () => {
        // browser does not distinguish sent/received close reliably
        states[id] = WS_STATE_CLOSED;
      };

      ws.onerror = () => {
        states[id] = WS_STATE_ABORTED;
      };

      ws.onmessage = (e) => {
        if (typeof e.data === "string") {
          queues[id].push({ t: 1, s: e.data });
        } else {
          queues[id].push({ t: 0, b: new Uint8Array(e.data) });
        }
      };
    }

    return {
      WS_New: function () {
        const id = nextHandleId++;
        sockets[id] = null;
        queues[id] = [];
        states[id] = WS_STATE_NONE;
        return id;
      },

      WS_Dispose: function (id) {
        const ws = sockets[id];
        if (ws) {
          try {
            states[id] = WS_STATE_CLOSE_SENT;
            ws.close();
          } catch (_) {}
        }
        delete sockets[id];
        delete queues[id];
        delete states[id];
      },

      WS_State: function (id) {
        if (!(id in states)) {
          console.error(`WS_State: no state entry for id=${id}`);
          return WS_STATE_CLOSED;
        }
        return states[id];
      },

      WS_Connect: function (id, urlPtr) {
        if (!(id in states)) {
          console.error(`WS_Connect: no state entry for id=${id}`);
          return;
        }

        const url = UTF8ToString(urlPtr);

        const existing = sockets[id];
        if (
          existing &&
          (existing.readyState === WebSocket.CONNECTING ||
            existing.readyState === WebSocket.OPEN)
        ) {
          return;
        }

        try {
          const ws = new WebSocket(url); // actual connect trigger
          sockets[id] = ws;
          states[id] = WS_STATE_CONNECTING;
          attachHandlers(id, ws);
        } catch (e) {
          console.error(`WS_Connect exception for id=${id}`, e);
          states[id] = WS_STATE_ABORTED;
        }
      },

      WS_Close: function (id) {
        if (!(id in states)) {
          console.error(`WS_Close: no state entry for id=${id}`);
          return;
        }

        const ws = sockets[id];
        if (!ws) {
          states[id] = WS_STATE_CLOSED;
          return;
        }

        try {
          const rs = ws.readyState;

          if (rs === WebSocket.CONNECTING || rs === WebSocket.OPEN) {
            states[id] = WS_STATE_CLOSE_SENT;
            ws.close(); // async → onclose will finalize state
          } else if (rs === WebSocket.CLOSING) {
            states[id] = WS_STATE_CLOSE_SENT;
          } else {
            // CLOSED
            states[id] = WS_STATE_CLOSED;
          }
        } catch (e) {
          console.error(`WS_Close exception for id=${id}`, e);
          states[id] = WS_STATE_ABORTED;
        }
      },

      WS_Send: function (id, dataPtr, dataLen, messageType) {
        // int error code
        // 0 = OK
        // 1 = invalid handle
        // 2 = invalid state
        // 3 = send failed

        if (!(id in states)) {
          console.error(`WS_Send: no state entry for id=${id}`);
          return 1;
        }

        const ws = sockets[id];
        if (!ws) {
          states[id] = WS_STATE_CLOSED;
          return 1;
        }

        if (ws.readyState !== WebSocket.OPEN) {
          return 2;
        }

        try {
          if (messageType === 0) {
            // Binary
            const view = HEAPU8.subarray(dataPtr, dataPtr + dataLen);
            // copy to detach from WASM memory
            ws.send(view.slice());
          } else {
            // Text
            const str = UTF8ArrayToString(HEAPU8, dataPtr, dataLen);
            ws.send(str);
          }

          return 0;
        } catch (e) {
          console.error(`WS_Send failed for id=${id}`, e);
          states[id] = WS_STATE_ABORTED;
          return 3;
        }
      },

      // Returns:
      //  -1 = nothing available
      //   0 = binary
      //   1 = text
      WS_NextAvailableToReceive: function (id) {
        const q = queues[id];
        if (!q || q.length === 0) return -1;

        const it = q[0];
        // t: 0 = binary, 1 = text
        return it.t === 0 || it.t === 1 ? it.t : -1;
      },

      // Copies payload into bufferPtr.
      // Returns:
      //  >0  = number of bytes copied
      //  -1  = buffer too small
      //   0  = nothing consumed (no data available)
      WS_TryConsumeNextReceived: function (id, bufferPtr, bufferLen) {
        const q = queues[id];
        if (!q || q.length === 0) return 0;

        const it = q[0];
        if (it.t !== 0 && it.t !== 1) return 0;

        let bytes;

        if (it.t === 0) {
          // binary
          bytes = it.b;
        } else {
          // text -> UTF8 bytes
          const s = it.s ?? "";
          const n = lengthBytesUTF8(s);
          if (n > bufferLen) return -1;

          bytes = new Uint8Array(n);
          stringToUTF8Array(s, bytes, 0, n + 1); // terminator ignored
        }

        if (bytes.length > bufferLen) return -1;

        // consume
        q.shift();
        HEAPU8.set(bytes, bufferPtr);

        return bytes.length;
      },
    };
  })(),
);
