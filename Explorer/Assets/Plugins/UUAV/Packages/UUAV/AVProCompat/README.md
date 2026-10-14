# Compatibility Note

The compatibility layer covers only the subset of AVPro's surface the Explorer calls; it is not a general AVPro adapter.

## Why this layer still mirrors AVPro's surface

This package mirrors AVPro's surface (`MediaPlayer`, `IMediaControl`/`IMediaInfo`/`ITextureProducer`, `Enums`, `TimeRanges`) instead of exposing a UUAV-native API. It was built as migration scaffolding so the Explorer could switch between AVPro and UUAV behind a trivial, symmetric selection layer.

That switch and the AVPro package are gone: the Explorer's media systems (`DCL.SDKComponents.MediaStream`) now consume this facade directly. Several members exist only for AVPro-shape parity and are inert here (`OptionsWindows`/`OptionsApple`, `Windows.VideoApi`/`Windows.AudioOutput`, `MediaPathType`, `MediaPlayerEvent`); `TimeRanges` is reduced to `Count`, which only reports whether a buffered range exists. The facade is slated to be collapsed into a UUAV-native surface; until then, extend it only with members the Explorer actually calls.
