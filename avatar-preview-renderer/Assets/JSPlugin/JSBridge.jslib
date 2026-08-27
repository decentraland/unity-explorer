mergeInto(LibraryManager.library, {
  OnScreenshotTaken: function (strPtr) {
    const base64str = UTF8ToString(strPtr)
    const targetWindow = (() => {
      try {
        return window.self !== window.top ? window : window.parent
      } catch (e) {
        return window.parent
      }
    })()
    targetWindow.postMessage(
      {
        type: 'unity-renderer',
        payload: {
          type: 'screenshot',
          payload: base64str,
        },
      },
      '*'
    )
  },
  OnLoadComplete: function () {
    const targetWindow = (() => {
      try {
        return window.self !== window.top ? window : window.parent
      } catch (e) {
        return window.parent
      }
    })()
    targetWindow.postMessage(
      {
        type: 'unity-renderer',
        payload: {
          type: 'loaded',
          payload: true,
        },
      },
      '*'
    )
  },
  OnError: function (strPtr) {
    const messageStr = UTF8ToString(strPtr)
    const targetWindow = (() => {
      try {
        return window.self !== window.top ? window : window.parent
      } catch (e) {
        return window.parent
      }
    })()
    targetWindow.postMessage(
      {
        type: 'unity-renderer',
        payload: {
          type: 'error',
          payload: messageStr,
        },
      },
      '*'
    )
  },
  OnCustomizationDone: function (strPtr) {
      const messageStr = UTF8ToString(strPtr)
      const targetWindow = (() => {
        try {
          return window.self !== window.top ? window : window.parent
        } catch (e) {
          return window.parent
        }
      })()
      targetWindow.postMessage(
        {
          type: 'unity-renderer',
          payload: {
            type: 'customization-done',
            payload: messageStr,
          },
        },
        '*'
      )
    },
  OnElementBounds: function (strPtr) {
    const json = UTF8ToString(strPtr)
    const targetWindow = (() => {
      try {
        return window.self !== window.top ? window : window.parent
      } catch (e) {
        return window.parent
      }
    })()
    targetWindow.postMessage(
      {
        type: 'unity-renderer',
        payload: {
          type: 'element-bounds',
          payload: json,
        },
      },
      '*'
    )
  },
  OnAvatarCustomizationStep: function (step) {
    const targetWindow = (() => {
      try {
        return window.self !== window.top ? window : window.parent
      } catch (e) {
        return window.parent
      }
    })()
    targetWindow.postMessage(
      {
        type: 'unity-renderer',
        payload: {
          type: 'avatar-customization-step',
          payload: step,
        },
      },
      '*'
    )
  },
  OnEmoteLength: function (length) {
    const targetWindow = (() => {
      try {
        return window.self !== window.top ? window : window.parent
      } catch (e) {
        return window.parent
      }
    })()
    targetWindow.postMessage(
      {
        type: 'unity-renderer',
        payload: {
          type: 'emoteLength',
          payload: length,
        },
      },
      '*'
    )
  },
  OnIsEmotePlaying: function (playing) {
    const targetWindow = (() => {
      try {
        return window.self !== window.top ? window : window.parent
      } catch (e) {
        return window.parent
      }
    })()
    targetWindow.postMessage(
      {
        type: 'unity-renderer',
        payload: {
          type: 'isEmotePlaying',
          payload: !!playing,
        },
      },
      '*'
    )
  },
  OnHasSound: function (hasSound) {
    const targetWindow = (() => {
      try {
        return window.self !== window.top ? window : window.parent
      } catch (e) {
        return window.parent
      }
    })()
    targetWindow.postMessage(
      {
        type: 'unity-renderer',
        payload: {
          type: 'hasSound',
          payload: !!hasSound,
        },
      },
      '*'
    )
  },
  PreloadURLs: function(strPtr) {
    const csv = UTF8ToString(strPtr);
    const urls = csv.split(',');

    for (const url of urls) {
      fetch(url, { cache: 'force-cache' }).catch(() => {});
    }
  }
})
