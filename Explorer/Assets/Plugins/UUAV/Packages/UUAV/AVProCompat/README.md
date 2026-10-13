# Compatability Note

The compatability layer doesn't provide full coverage of AVPRO features and should be extended as it goes.
Feel free to contribute your adapter code 

## Why this layer still mirrors AVPro's surface

This package mirrors AVPro's surface (`MediaPlayer`, `IMediaControl`/`IMediaInfo`/`ITextureProducer`, `Enums`, `TimeRanges`) instead of exposing a UUAV-native API. It was built as migration scaffolding so the Explorer could switch between AVPro and UUAV behind a trivial, symmetric selection layer.

That switch and the AVPro package are gone: the Explorer's media systems (`DCL.SDKComponents.MediaStream`) now consume this facade directly. Several members exist only for AVPro-shape parity and are inert here (`OptionsWindows`/`OptionsApple`, `Windows.VideoApi`/`Windows.AudioOutput`, `MediaPathType`, `MediaPlayerEvent`, `TimeRanges.Count`). The facade is slated to be collapsed into a UUAV-native surface; until then, extend it only with members the Explorer actually calls.
