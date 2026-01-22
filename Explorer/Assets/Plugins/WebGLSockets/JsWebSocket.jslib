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
        queues[id].push({ t: 0 });
      };

      ws.onclose = () => {
        // browser does not distinguish sent/received close reliably
        states[id] = WS_STATE_CLOSED;
        queues[id].push({ t: 1 });
      };

      ws.onerror = () => {
        states[id] = WS_STATE_ABORTED;
        queues[id].push({ t: 2 });
      };

      ws.onmessage = (e) => {
        if (typeof e.data === "string") {
          queues[id].push({ t: 3, s: e.data });
        } else {
          queues[id].push({ t: 4, b: new Uint8Array(e.data) });
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
    };
  })(),
);
