class WebSocket {

    static CONNECTING = 0
    static OPEN = 1
    static CLOSING = 2
    static CLOSED = 3

    #url;
    //#readyState;
    webSocketId;
    onmessage = null;
    onopen = null;
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

        console.log("WS: ", `${url} contructor has finished`);
    }

    #connect() {
        //this.#readyState = WebSocket.CONNECTING;
        return UnityWebSocketApi.ConnectAsync(this.webSocketId, this.#url).then(() => {
           // this.#readyState = WebSocket.OPEN
            if (typeof this.onopen === 'function') {
               this.onopen({ type: "open" });
            }
        }).catch(error => {
            if (typeof this.onerror === 'function') {
               this.onerror(error);
            }
        });
    }  

   send(data) {       
   const thisReadyState = this.readyState;
   console.log("WebSocket.send is called", data.constructor.name, `State is ${thisReadyState}`);

    if (thisReadyState !== WebSocket.OPEN){
        const errorMessage = `WebSocket state is ${thisReadyState}, cannot send data`;
        throw new Error(errorMessage);
    }

    let sendPromise;
    if (typeof data === 'string') {
        sendPromise = UnityWebSocketApi.SendAsync(this.webSocketId, data);
    } else if (data instanceof Uint8Array || data instanceof ArrayBuffer || Array.isArray(data)) {
        console.log("WS: WebSocket.send binary is called", data.constructor.name, `length is ${data.length}`);
        sendPromise = UnityWebSocketApi.SendAsync(this.webSocketId, data);
    }
    else {
        console.error(`Unsupported data type: ${typeof data}`, data);
        throw new new Error("Unsupported data type");
    }

    sendPromise.catch(error => {
        if (typeof this.onerror === 'function') {
            this.onerror(error);
        }
    });
}

   #receive() {
      const self = this;

      return new Promise((resolve, reject) => {
          const receiveData = () => {
            UnityWebSocketApi.ReceiveAsync(self.webSocketId).then(data => {
                console.log("WS: Received binary data", typeof data.data, data.data.length);

                  if (typeof self.onmessage === 'function') {
                    let messageType;
                    if (data.type === 'Binary'){
                        messageType = "binary";
                    } else {
                        messageType = "text";
                     }
                    
                     self.onmessage({type: messageType, data: data.data});
                  }
                  receiveData();
              }).catch(error => {
                  if (typeof self.onerror === 'function') {
                      self.onerror(error);
                  }
                  reject(error);
              });
          };
          receiveData();
      });
    }

    close(code = undefined, reason = undefined) {
        const thisReadyState = this.readyState; 
        
        if (thisReadyState === WebSocket.OPEN || thisReadyState === WebSocket.CONNECTING) {
            //this.#readyState = WebSocket.CLOSING;

            UnityWebSocketApi.CloseAsync(this.webSocketId)
                .then(() => {
                    console.log("WebSocket connection closed");
                    //this.#readyState = WebSocket.CLOSED;
                    if (typeof this.onclose === 'function') {
                        this.onclose({ type: "close"});
                    }
                })
                .catch(error => {
                    console.error("Error closing WebSocket connection:", error);
                });
        } else {
            const errorMessage = `WebSocket state is ${thisReadyState}, cannot close`;
            console.error(errorMessage);
            throw new Error(errorMessage);
        }
    }

    get readyState() {
        // can't store the state in JS as it can be modified from the managed side without any notification
        return UnityWebSocketApi.GetState(this.webSocketId);
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

module.exports.WebSocket = WebSocket