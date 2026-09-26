# Compatability Note

The compatability layer doesn't provide full coverage of AVPRO features and should be extended as it goes.
Feel free to contribute your adapter code 

## Why this layer exists (and why it is not collapsed into `AvProSwitch`)

This package deliberately mirrors AVPro's surface (`MediaPlayer`, `IMediaControl`/`IMediaInfo`/`ITextureProducer`, `Enums`, `TimeRanges`) instead of exposing a UUAV-native API, even though that means the UUAV path goes through one more hop than the AVPro path (`AvProSwitch.UuavBackend` → `Compat.MediaPlayer` → `Compat.UUAVBackend`).

The extra layer is temporary migration scaffolding: while both engines ship behind the `AvProSwitch` flag, keeping UUAV shaped exactly like AVPro means the switch layer stays trivial and symmetric. Once UUAV is validated stable in production (expected ~1–2 weeks after rollout), the AVPro integration — the AVPro package reference, `AvProBackend`, and the whole `AvProSwitch` selection layer — can be deleted in one sweep, and callers move onto this facade (or `UUAVPlayer` directly) without touching playback logic. Collapsing the layers now would mean refactoring code that is scheduled for deletion.
