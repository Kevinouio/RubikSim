mergeInto(LibraryManager.library, {
  RubikPublishState: function (jsonPointer) {
    var state = JSON.parse(UTF8ToString(jsonPointer));
    state.wasmHeapBytes = HEAPU8.buffer.byteLength;
    window.dispatchEvent(new CustomEvent('rubik-state', { detail: state }));
  },
  RubikCanvasHasFocus: function () {
    return document.activeElement === Module.canvas ? 1 : 0;
  },
  RubikInitializePointerInput: function () {
    if (Module.rubikPointerInputInitialized) return;
    Module.rubikPointerInputInitialized = true;
    Module.rubikPointerShift = 0;
    var rememberModifiers = function (event) { Module.rubikPointerShift = event.shiftKey ? 1 : 0; };
    // A modifier may be held before the canvas gains keyboard focus. Preserve the
    // actual click's modifier even if keyup occurs before the next Unity frame.
    Module.canvas.addEventListener('pointerdown', rememberModifiers, true);
    Module.canvas.addEventListener('pointerup', rememberModifiers, true);
  },
  RubikPointerShift: function () {
    return Module.rubikPointerShift || 0;
  }
});
