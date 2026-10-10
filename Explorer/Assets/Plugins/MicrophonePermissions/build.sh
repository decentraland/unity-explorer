clang -dynamiclib \
  -framework AVFoundation \
  -framework Foundation \
  -O3 -DNDEBUG \
  -o libMicrophonePermissions.dylib MicrophonePermissions.mm

strip -x libMicrophonePermissions.dylib
