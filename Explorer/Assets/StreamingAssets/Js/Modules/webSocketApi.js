class WebSocket {
   constructor(url) {
       WebSocketApiWrapper.Create(url);
       this.connect;
       this.receive;
   }

   onopen = null;
   onmessage = null;
   onerror = null;
   onclose = null;

   connect() {
       return WebSocketApiWrapper.ConnectAsync().then(() => {
           if (typeof this.onopen === 'function') {
               this.onopen();
           }
       }).catch(error => {
           if (typeof this.onerror === 'function') {
               this.onerror(error);
           }
       });
   }

   send(data) {
       return WebSocketApiWrapper.SendAsync(data).catch(error => {
           if (typeof this.onerror === 'function') {
               this.onerror(error);
           }
       });
   }

   receive() {
      return new Promise((resolve, reject) => {
          const receiveData = () => {
              WebSocketApiWrapper.ReceiveAsync().then(data => {
                  if (typeof this.onmessage === 'function') {
                      this.onmessage(data);
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
       return WebSocketApiWrapper.CloseAsync().then(() => {
           if (typeof this.onclose === 'function') {
               this.onclose();
           }
       }).catch(error => {
           if (typeof this.onerror === 'function') {
               this.onerror(error);
           }
       });
   }
}

 
 module.exports = WebSocket