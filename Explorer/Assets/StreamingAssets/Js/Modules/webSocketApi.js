class WebSocket {
    
    static CLOSED = 1
    static CLOSING = 2
    static CONNECTING = 3
    static OPEN = 4

    constructor(url, protocols) {
        //TODO: add checks if Scene can actually use WebSocket

        if (url.toString().toLowerCase().substr(0, 4) !== 'wss:') {
                throw new Error("Can't connect to unsafe WebSocket server")
            }
          
        this.webSocketId = WebSocketApiWrapper.Create(url);
        this.url = url;
        this.onopen = null;
        this.onmessage = null;
        this.onerror = null;
        this.onclose = null;
        this.#connect().then(() => {
            this.#receive().catch(error => {
                console.error('Error receiving data:', error);
                });
        }).catch(error => {
            console.error('Error connecting:', error);
        });
    }


    #connect() {
        this.readyState = WebSocket.CONNECTING;
        return WebSocketApiWrapper.ConnectAsync(webSocketId).then(() => {
            if (typeof this.onopen === 'function') {
               this.readyState = WebSocket.OPEN
               this.onopen({ type: "open" });
            }
        }).catch(error => {
            if (typeof this.onerror === 'function') {
               this.onerror(error);
            }
        });
    }

    get readyState() {
        return this.readyState
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
        return this.url;
    }
    

   async send(data) {
    if (this.readyState !== WebSocket.OPEN){
        const errorMessage = `WebSocket state is ${this.readyState}, cannot send data`;
        return Promise.reject(new Error(errorMessage));
    }

    let sendPromise;
    if (typeof data === 'string') {
        sendPromise = WebSocketApiWrapper.SendAsync(webSocketId, { type: 'Text', data });
    } else if (data instanceof Uint8Array || data instanceof ArrayBuffer || Array.isArray(data)) {
        sendPromise = WebSocketApiWrapper.SendAsync(webSocketId, { type: 'Binary', data });
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
            WebSocketApiWrapper.ReceiveAsync(webSocketId).then(data => {
                  if (typeof this.onmessage === 'function') {
                    if (data.type === 'Binary'){
                        data = new Uint8Array(data.data);
                        type = "binary";
                    } else {
                        data = data.data;
                        type = "text";
                     }
                      this.onmessage(type, data);
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

    close() {
        if (this.readyState === WebSocket.OPEN || this.readyState === WebSocket.CONNECTING) {

            this.readyState = WebSocket.CLOSING;

            return WebSocketApiWrapper.CloseAsync(webSocketId).then(() => {
                if (typeof this.onclose === 'function') {
                    this.readyState = WebSocket.CLOSED;
                    this.onclose({ type: "close" });
                }
            }).catch(error => {
                if (typeof this.onerror === 'function') {
                    this.onerror(error);
                }
            });
        } else {
            const errorMessage = `WebSocket state is ${this.readyState}, cannot close`;
            console.error(errorMessage);
        }
    }
}

 
 module.exports.webSocket = WebSocket