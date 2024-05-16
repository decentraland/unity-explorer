class WebSocket {
    
    static CLOSED = 1
    static CLOSING = 2
    static CONNECTING = 3
    static OPEN = 4

    #url;
    #readyState;
    webSocketId;
    onopen = null;
    onmessage = null;
    onerror = null;
    onclose = null;

    constructor(url, protocols) {
        //TODO: add checks if Scene can actually use WebSocket

        if (url.toString().toLowerCase().substr(0, 4) !== 'wss:') {
                throw new Error("Can't connect to unsafe WebSocket server")
            }
          
        this.webSocketId = UnityWebSocketApi.Create(url);
        this.#url = url;
        this.#connect().then(() => {
            this.#receive().catch(error => {
                console.error('Error receiving data:', error);
                });
        }).catch(error => {
            console.error('Error connecting:', error);
        });
    }


    #connect() {
        this.#readyState = WebSocket.CONNECTING;
        return UnityWebSocketApi.ConnectAsync(this.webSocketId, this.#url).then(() => {
            if (typeof this.onopen === 'function') {
               this.#readyState = WebSocket.OPEN
               this.onopen({ type: "open" });
            }
        }).catch(error => {
            if (typeof this.onerror === 'function') {
               this.onerror(error);
            }
        });
    }  

   async send(data) {
    if (this.#readyState !== WebSocket.OPEN){
        const errorMessage = `WebSocket state is ${this.#readyState}, cannot send data`;
        return Promise.reject(new Error(errorMessage));
    }

    let sendPromise;
    if (typeof data === 'string') {
        sendPromise = UnityWebSocketApi.SendAsync(this.webSocketId, { type: 'Text', data });
    } else if (data instanceof Uint8Array || data instanceof ArrayBuffer || Array.isArray(data)) {
        sendPromise = UnityWebSocketApi.SendAsync(this.webSocketId, { type: 'Binary', data });
    }
    else {
        console.error(`Unsupported data type: ${typeof data}`, data);
        return Promise.reject(new Error("Unsupported data type"));
    }

    sendPromise.catch(error => {
        if (typeof this.onerror === 'function') {
            this.onerror(error);
        }
    });
    return sendPromise;
}
    
   #receive() {
      return new Promise((resolve, reject) => {
          const receiveData = () => {
            UnityWebSocketApi.ReceiveAsync(this.webSocketId).then(data => {
                  if (typeof this.onmessage === 'function') {
                    let messageType, parsedData;
                    if (data.type === 'Binary'){
                        parsedData = new Uint8Array(data.data);
                        messageType = "binary";
                    } else {
                        parsedData = data.data;
                        messageType = "text";
                     }
                      this.onmessage({type: messageType, data: parsedData});
                  }
                  receiveData();
              }).catch(error => {
                  if (typeof this.onerror === 'function') {
                      this.onerror(error);
                  }
                  reject(error);
              });
          };
          receiveData();
      });
    }

    async close(code = undefined, reason = undefined) {
        if (this.#readyState === WebSocket.OPEN || this.#readyState === WebSocket.CONNECTING) {
            this.#readyState = WebSocket.CLOSING;
    
            UnityWebSocketApi.CloseAsync(this.webSocketId)
                .then(() => {
                    console.log("WebSocket connection closed");
                    this.#readyState = WebSocket.CLOSED;
                    if (typeof this.onclose === 'function') {
                        this.onclose({ type: "close"});
                    }
                })
                .catch(error => {
                    console.error("Error closing WebSocket connection:", error);
                });
    
            return Promise.resolve();
        } else {
            const errorMessage = `WebSocket state is ${this.#readyState}, cannot close`;
            console.error(errorMessage);
            return Promise.reject(new Error(errorMessage));
        }
    }
        

    get readyState() {
        return this.#readyState
    }

    get binaryType() {
        return "arraybuffer"
    }

    set binaryType(value) {
        if (value !== "arraybuffer") {
            throw new Error("Only 'arraybuffer' is supported as binaryType")
        }
    }

    get protocol() {
        return ""
    }

    get extensions() {
        return ""
    }

    // There is no send buffer here, it is handled on C# side
    get bufferedAmount() {
            return 0
        }

    get url() {
        return this.#url;
    }
}

module.exports.WebSocket = (url, protocols) => new WebSocket(url, protocols)