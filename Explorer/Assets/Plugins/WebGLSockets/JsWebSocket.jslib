var dcl_ws_library = {
  $LIB_GLOBAL: {
    WS_STATE_NONE: 0,
    WS_STATE_CONNECTING: 1,
    WS_STATE_OPEN: 2,
    WS_STATE_CLOSE_SENT: 3,
    WS_STATE_CLOSE_RECEIVED: 4,
    WS_STATE_CLOSED: 5,
    WS_STATE_ABORTED: 6,

    ws_nextHandleId: 1,
    ws_sockets: {},
    ws_states: {},
    ws_queues: {},
  },

  WS_New: function () {
    const id = LIB_GLOBAL.ws_nextHandleId++;
    LIB_GLOBAL.ws_sockets[id] = null;
    LIB_GLOBAL.ws_queues[id] = [];
    LIB_GLOBAL.ws_states[id] = LIB_GLOBAL.WS_STATE_NONE;
    return id;
  },

  WS_Dispose: function (id) {
    const sockets = LIB_GLOBAL.ws_sockets;
    const states = LIB_GLOBAL.ws_states;
    const queues = LIB_GLOBAL.ws_queues;

    const ws = sockets[id];
    if (ws) {
      try {
        states[id] = LIB_GLOBAL.WS_STATE_CLOSE_SENT;
        ws.close();
      } catch {}
    }

    delete sockets[id];
    delete queues[id];
    delete states[id];
  },

  WS_State: function (id) {
    const states = LIB_GLOBAL.ws_states;
    if (!(id in states)) {
      return -1;
    }
    return states[id];
  },

  WS_Connect: function (id, urlPtr) {
    const states = LIB_GLOBAL.ws_states;
    const sockets = LIB_GLOBAL.ws_sockets;

    if (!(id in states)) return;

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
      const ws = new WebSocket(url);
      sockets[id] = ws;
      states[id] = LIB_GLOBAL.WS_STATE_CONNECTING;

      ws.binaryType = "arraybuffer";

      ws.onopen = () => {
        LIB_GLOBAL.ws_states[id] = LIB_GLOBAL.WS_STATE_OPEN;
      };

      ws.onclose = () => {
        LIB_GLOBAL.ws_states[id] = LIB_GLOBAL.WS_STATE_CLOSED;
      };

      ws.onerror = (e) => {
        console.error(`WebSocket onerror: ${id} and ${e}`);
        LIB_GLOBAL.ws_states[id] = LIB_GLOBAL.WS_STATE_ABORTED;
      };

      ws.onmessage = (e) => {
        const q = LIB_GLOBAL.ws_queues[id];
        if (typeof e.data === "string") {
          q.push({ t: 1, s: e.data });
        } else {
          q.push({ t: 0, b: new Uint8Array(e.data) });
        }
      };
    } catch (e) {
      console.error(`WebSocket connect error: ${id} and ${e}`);
      states[id] = LIB_GLOBAL.WS_STATE_ABORTED;
    }
  },

  WS_Close: function (id) {
    const ws = LIB_GLOBAL.ws_sockets[id];
    const states = LIB_GLOBAL.ws_states;

    if (!ws) {
      states[id] = LIB_GLOBAL.WS_STATE_CLOSED;
      return;
    }

    try {
      states[id] = LIB_GLOBAL.WS_STATE_CLOSE_SENT;
      ws.close();
    } catch (e) {
      console.error(`WebSocket close error: ${id} and ${e}`);
      states[id] = LIB_GLOBAL.WS_STATE_ABORTED;
    }
  },

  WS_Send: function (id, dataPtr, dataLen, messageType) {
    const ws = LIB_GLOBAL.ws_sockets[id];

    if (!ws) return 1;
    if (ws.readyState !== WebSocket.OPEN) return 2;

    try {
      if (messageType === 0) {
        ws.send(HEAPU8.slice(dataPtr, dataPtr + dataLen));
      } else {
        ws.send(UTF8ArrayToString(HEAPU8, dataPtr, dataLen));
      }
      return 0;
    } catch (e) {
      console.error(`WebSocket send error: ${id} and ${e}`);
      LIB_GLOBAL.ws_states[id] = LIB_GLOBAL.WS_STATE_ABORTED;
      return 3;
    }
  },

  WS_NextAvailableToReceive: function (id) {
    const q = LIB_GLOBAL.ws_queues[id];
    if (!q || q.length === 0) return -1;
    return q[0].t;
  },

  WS_TryConsumeNextReceived: function (id, bufferPtr, bufferLen) {
    const q = LIB_GLOBAL.ws_queues[id];
    if (!q || q.length === 0) return 0;

    const it = q[0];
    let bytes;

    if (it.t === 0) {
      bytes = it.b;
    } else {
      const s = it.s ?? "";
      const n = lengthBytesUTF8(s);
      if (n > bufferLen) return -1;
      bytes = new Uint8Array(n);
      stringToUTF8Array(s, bytes, 0, n + 1);
    }

    if (bytes.length > bufferLen) return -1;

    q.shift();
    HEAPU8.set(bytes, bufferPtr);
    return bytes.length;
  },
};

autoAddDeps(dcl_ws_library, "$LIB_GLOBAL");
mergeInto(LibraryManager.library, dcl_ws_library);
