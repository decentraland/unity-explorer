mergeInto(LibraryManager.library, {
    IsBrowserSafari: function() {
        return /^((?!chrome|android).)*safari/i.test(navigator.userAgent) ? 1 : 0;
    }
});
