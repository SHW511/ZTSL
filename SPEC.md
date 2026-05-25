# ZTSL Format Specification — v1.0

**ZTSL** (Zerg's Tech Song and Lyrics) is a custom audio container format that carries lyric data as first-class, timestamp-bearing packets interleaved with audio frames. The format is designed for streaming-style playback in which lyrics scroll in sync with the audio without needing a separate sidecar file.

---

## 1. Goals and Non-Goals

**Goals**
- Tight lyric/audio sync via packet-level interleaving.
- Streamable: a player can produce output while reading the file linearly. No mandatory upfront index.
- Portable record of music-generation context (e.g., prompt, model, seed for Suno-generated music).
- Forward-compatible for additive changes (new stream types, new optional fields).

**Non-Goals (v1)**
- Lossless audio fidelity.
- Per-word or per-syllable lyric timing.
- Vocal-track separation (lead / harmony / ad-lib distinction in the lyric stream).
- Seek index in the container itself.
- DRM, encryption, signed payloads.
- Embedded cover art (may be added in a future minor version).

---

## 2. File Anatomy

A ZTSL file consists of three contiguous regions:

```
+-------------------------+
|       FILE HEADER       |   Fixed-prefix fields, stream descriptors, metadata block.
+-------------------------+
|       PACKET STREAM     |   Time-ordered multiplex of audio + lyric packets.
+-------------------------+
|       FOOTER (optional) |   Empty in v1. Reserved for an optional seek index.
+-------------------------+
```

**Byte ordering**: All multi-byte integers are stored **little-endian**.
**Strings**: All textual data is **UTF-8**, except where stated otherwise (e.g. fixed ASCII fields like `magic` and `language`).
**File extension**: `.ztsl`.

---

## 3. File Header

| Offset | Length | Field | Type | Description |
|---|---|---|---|---|
| 0 | 4 | `magic` | bytes | ASCII `"ZTSL"` = `0x5A 0x54 0x53 0x4C`. |
| 4 | 1 | `version_major` | uint8 | Format major version. `0x01` for v1.x. |
| 5 | 1 | `version_minor` | uint8 | Format minor version. `0x00` for v1.0. |
| 6 | 4 | `header_length` | uint32 | Total bytes of the header region, from offset 0 through the end of the metadata block (exclusive of the first packet). |
| 10 | 1 | `stream_count` | uint8 | Number of stream descriptors that immediately follow. Must be at least 1. |
| 11 | varies | `stream_descriptors` | array | `stream_count` stream descriptors, packed back-to-back. See **§4**. |
| ... | 4 | `metadata_length` | uint32 | Byte length of the metadata block that follows. May be `0`. |
| ... | varies | `metadata` | UTF-8 JSON | Metadata block. See **§7**. |

The first packet starts at byte offset `header_length`.

---

## 4. Stream Descriptors

Each stream descriptor declares one stream (audio, lyric, etc.). Descriptors are length-prefixed so a decoder can skip stream types it does not understand.

| Offset | Length | Field | Type | Description |
|---|---|---|---|---|
| 0 | 1 | `descriptor_length` | uint8 | Total bytes of this descriptor **after** this byte. Does not include itself. Maximum total descriptor size is therefore 1 + 255 = 256 bytes. |
| 1 | 1 | `stream_id` | uint8 | Stream identifier. Packets reference this value. Must be unique within a file. |
| 2 | 1 | `stream_type` | uint8 | See **§4.1**. |
| 3 | varies | `type_specific` | bytes | Fixed-size fields determined by `stream_type`. |
| ... | varies | `extras` | UTF-8 JSON | Optional. Fills the remaining bytes up to the descriptor boundary set by `descriptor_length`. May be empty. See **§4.4**. |

### 4.1 Stream Types

| Value | Name | Type-specific size | Notes |
|---|---|---|---|
| `0x01` | Audio (Opus) | 8 bytes | §4.2 |
| `0x02` | Lyric | 3 bytes | §4.3 |
| `0x03`–`0xFF` | Reserved | — | Decoders MUST skip unknown stream types using `descriptor_length`. |

### 4.2 Audio (Opus) Type-Specific Fields

| Length | Field | Type | Description |
|---|---|---|---|
| 1 | `codec_id` | uint8 | `0x01` = Opus. Other values reserved. |
| 4 | `sample_rate` | uint32 | Audio sample rate in Hz. For ZTSL v1, MUST be `48000` (Opus's native rate; encoders resample at ingest if needed). |
| 1 | `channels` | uint8 | `1` = mono, `2` = stereo. Other values reserved. |
| 2 | `preskip` | uint16 | Number of samples to discard at the start of decoded audio. Comes from the Opus encoder; pass through unchanged. |

A minimum Opus descriptor (no extras) is 11 bytes total: `descriptor_length=0x0A`, then 10 bytes (`stream_id` + `stream_type` + 8 type-specific).

### 4.3 Lyric Type-Specific Fields

| Length | Field | Type | Description |
|---|---|---|---|
| 3 | `language` | ASCII | ISO 639-3 language code (e.g. `"eng"`, `"spa"`, `"jpn"`). |

A minimum lyric descriptor (no extras) is 6 bytes total.

### 4.4 Extras Slot (Additional Properties)

Any bytes between the end of the type-specific section and the descriptor boundary (set by `descriptor_length`) are interpreted as a single UTF-8 JSON value — typically a JSON object. Encoders MAY use it to attach arbitrary additional properties to a stream (loudness data, encoder profile, mixing flags, etc.). Decoders MUST NOT fail on unknown keys inside this slot.

Examples:

- Audio descriptor with extras `{"loudness_lufs":-14.2,"encoder_profile":"music-high"}` → `descriptor_length` = 10 (fixed Opus header bytes) + N (JSON byte count).
- Lyric descriptor with no extras → `descriptor_length` = 5.

---

## 5. Packet Stream

Packets follow the file header back-to-back, multiplexed across all declared streams in **strict increasing-timestamp order**.

### 5.1 Packet Layout

| Length | Field | Type | Description |
|---|---|---|---|
| 1 | `stream_id` | uint8 | Must match a `stream_id` declared in a stream descriptor. |
| 4 | `timestamp` | uint32 | Playback time of this packet, in **milliseconds from the start of the file**. |
| 2 | `payload_length` | uint16 | Length of `payload` in bytes (max 65,535). |
| varies | `payload` | bytes | Stream-type-specific. |

Per-packet overhead is **7 bytes**.

### 5.2 Audio Packet Payload

For an Opus audio stream, `payload` contains **exactly one Opus frame** as produced by the Opus encoder. The packet's `timestamp` is the playback time of the **first sample** in the frame.

Audio packets in a single stream are typically spaced 20 ms apart, matching the standard Opus frame size. Other frame sizes (2.5 / 5 / 10 / 40 / 60 ms) are permitted, but consistency simplifies player scheduling.

### 5.3 Lyric Packet Payload

| Length | Field | Type | Description |
|---|---|---|---|
| 2 | `text_length` | uint16 | Length of `text` in bytes. May be `0`. |
| varies | `text` | UTF-8 | The lyric line (or empty). |

**Player display rule**:

> The currently-displayed lyric line is the most recent lyric packet (per stream) whose `timestamp ≤ now`.

A lyric packet with `text_length = 0` therefore clears the display — useful during instrumental sections.

### 5.4 Section Markers

Section markers such as `[Verse]`, `[Chorus]`, `[Bridge]`, `[Outro]` ride **inline** in the lyric stream as ordinary lyric packets whose `text` begins with `[`. Players MAY render bracketed lines differently (smaller, dimmer, as a heading). Authoring tools SHOULD preserve Suno's native inline label shape.

---

## 6. Multiplex and Ordering Rules

- Packets MUST appear in non-decreasing `timestamp` order across all streams.
- When multiple packets share a `timestamp`, lyric packets SHOULD precede audio packets at that instant so the display updates by the time playback reaches the audio.
- The packet stream has no terminator. End-of-file marks the end of the packet stream.
- There is no explicit packet count. The duration of audio playback is bounded by the audio packets present.

---

## 7. Metadata Block

The metadata block is a single UTF-8 JSON value (typically an object), length-prefixed by the `metadata_length` uint32 from the file header. The schema below is *suggested*; decoders MUST tolerate unknown keys and missing optional keys.

```json
{
  "title": "Song Title",
  "artist": "Display Name",
  "created_at": "2026-05-14T12:34:56Z",
  "duration_ms": 180000,
  "suno": {
    "prompt": "epic synthwave ballad about a lost robot",
    "style_tags": ["synthwave", "ballad"],
    "model": "v4",
    "seed": 12345
  }
}
```

| Key | Type | Required | Notes |
|---|---|---|---|
| `title` | string | recommended | Display title. |
| `artist` | string | recommended | Display artist. |
| `created_at` | string | recommended | ISO 8601 UTC creation timestamp. |
| `duration_ms` | uint32 | recommended | Total playback duration. Lets a player render a progress bar without scanning. |
| `suno` | object | optional | Suno generation context. |
| `suno.prompt` | string | optional | Original generation prompt. |
| `suno.style_tags` | array of string | optional | Style tags supplied to the model. |
| `suno.model` | string | optional | Model identifier (e.g. `"v4"`). |
| `suno.seed` | uint64 | optional | Generation seed. |

Custom keys are allowed at any level. Encoders SHOULD namespace custom keys (e.g. `"ztsl_custom_..."` or a tool-specific prefix) to avoid future collisions with reserved keys.

---

## 8. Versioning Policy

The format follows semver:

- **Major bump** (`version_major + 1`): breaking changes that prevent older decoders from reading the file correctly. Examples: renaming fields, changing the packet header layout, removing required fields, changing magic bytes.
- **Minor bump** (`version_minor + 1`): additive changes that older decoders can ignore safely. Examples: new optional metadata keys, new `stream_type` values, new fields appended via the descriptor `extras` slot.

### 8.1 Decoder Conformance

A conforming v1.x decoder MUST:

1. Verify `magic == "ZTSL"`. Reject otherwise.
2. Reject files whose `version_major` is greater than its supported major.
3. Tolerate any `version_minor` value at or above its supported minor, ignoring new fields it does not understand.
4. Skip stream descriptors whose `stream_type` it does not recognize, using `descriptor_length` to advance past them.
5. Tolerate unknown keys in JSON metadata and JSON extras blobs without failing.
6. Tolerate empty lyric packets (`text_length = 0`) as a display-clear signal.

A conforming encoder MUST:

1. Write `magic = "ZTSL"`, valid `version_major` / `version_minor` for the spec version it targets.
2. Emit at least one audio stream and at most one of any reserved-singleton stream type.
3. Emit packets in non-decreasing `timestamp` order.
4. Set `sample_rate = 48000` for audio streams in v1.x.

---

## 9. Data Type Quick Reference

| Type | Width | Notes |
|---|---|---|
| `uint8` | 1 byte | — |
| `uint16` | 2 bytes | little-endian |
| `uint32` | 4 bytes | little-endian |
| `uint64` | 8 bytes | little-endian (used only in JSON, where it's encoded as a number) |
| ASCII string | per char | fixed-width fields only |
| UTF-8 string | per byte | for all general text |

---

## 10. Design Rationale (Non-Normative)

A few choices that may not be obvious from the spec alone:

- **No sync word per packet.** Reduces overhead. The format isn't aimed at lossy streaming channels where mid-stream packet recovery is needed; it's a file-on-disk format.
- **No seek index in v1.** Players scan from start; an optional seek index can be added later in a footer without breaking the header layout.
- **Single ms clock for all streams.** Every packet schedules itself off a unified `uint32 ms from start of file`. Sub-millisecond drift is real but inaudible for music with text lyrics. Internal Opus sample timing remains sample-accurate.
- **Strict time-ordered multiplex.** A player can run as a linear scan-and-schedule loop with no buffering or reordering.
- **JSON for metadata and stream extras.** Easy to extend and debug; size cost is negligible relative to the audio payload.
- **Section markers inline in the lyric stream.** Matches Suno's native output shape. A player can detect `[bracket]`-prefixed lines and style them without a dedicated stream type.
- **Length-prefixed descriptors + extras slot.** Lets the format grow new fields and new stream types without breaking older decoders, while keeping the on-disk shape compact.

---

*End of specification, v1.0.*
