class WebSocket {
    
    static CLOSED = 1
    static CLOSING = 2
    static CONNECTING = 3
    static OPEN = 4

    constructor(url) {
        WebSocketApiWrapper.Create(url);
        this.readyState = WebSocket.CONNECTING;
        this.url = url;
        this.onopen = null;
        this.onmessage = null;
        this.onerror = null;
        this.onclose = null;
        this.connect().then(() => {
            this.receive().catch(error => {
                console.error('Error receiving data:', error);
                });
        }).catch(error => {
            console.error('Error connecting:', error);
        });
    }


   connect() {
       return WebSocketApiWrapper.ConnectAsync().then(() => {
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

    get url(){
        return this.url;
    }
    

   send(data) {
    let sendPromise;
    if (typeof data === 'string') {
        sendPromise = WebSocketApiWrapper.SendAsync({ type: 'Text', data });
    } else if (data instanceof Uint8Array || data instanceof ArrayBuffer || Array.isArray(data)) {
        sendPromise = WebSocketApiWrapper.SendAsync({ type: 'Binary', data });
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

   receive() {
      return new Promise((resolve, reject) => {
          const receiveData = () => {
              WebSocketApiWrapper.ReceiveAsync().then(data => {
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
        this.readyState = WebSocket.CLOSING;
        return WebSocketApiWrapper.CloseAsync().then(() => {
            if (typeof this.onclose === 'function') {
                this.readyState = WebSocket.CLOSED;
                this.onclose({type: "close"});
           }
        }).catch(error => {
            if (typeof this.onerror === 'function') {
                this.onerror(error);
            }
        });
    }
}

 
 module.exports.webSocket = WebSocket