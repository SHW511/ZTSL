# ZTSL

  A container format for Opus audio paired with timestamped lyric/text streams.

  This package provides binary read/write primitives for the ZTSL format: `FileHeader`,
  packet I/O, stream descriptors, and binary reader/writer helpers.

  For playback, pair this with:
  - [`ZTSL.Player.Core`](https://www.nuget.org/packages/ZTSL.Player.Core) — platform-agnostic decoder
  - [`ZTSL.Player.Blazor`](https://www.nuget.org/packages/ZTSL.Player.Blazor) — drop-in Blazor component

  Spec, format documentation, and the open-source player live at
  <https://github.com/SHW511/ZTSL.Player>.

  Licensed under Apache-2.0.