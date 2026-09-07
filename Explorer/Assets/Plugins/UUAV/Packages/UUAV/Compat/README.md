# Compat layer

A thin poll-based facade over `UUAVPlayer` (`MediaPlayer`,
`IMediaControl`/`IMediaInfo`/`ITextureProducer`, `Enums`, `TimeRanges`).
The Explorer's `DCL.VideoPlayback` layer drives this surface; scene media
systems poll it rather than subscribing to events. It intentionally covers
only the members the Explorer calls — extend it as consumers need more.
