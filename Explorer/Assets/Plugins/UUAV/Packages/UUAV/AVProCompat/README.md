# Compatibility Note

The compatibility layer covers only the subset of AVPro's surface the Explorer calls; it is not a general AVPro adapter.

## Why this layer still mirrors AVPro's surface

This package mirrors AVPro's surface (`MediaPlayer`, `IMediaControl`/`IMediaInfo`/`ITextureProducer`, `Enums`) instead of exposing a UUAV-native API. The Explorer's media systems (`DCL.SDKComponents.MediaStream`) consume it directly.

With a single backend the AVPro shape buys nothing: the facade is scaffolding to be collapsed into a UUAV-native surface ([#10441](https://github.com/decentraland/unity-explorer/issues/10441)). Until then, extend it only with members the Explorer actually calls.
