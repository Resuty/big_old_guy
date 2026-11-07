# Audio and font sources (v0.1.6)

Downloaded 2026-10-22. No paid assets or audio services.

## Sound effects: Kenney, CC0 1.0

- [Casino Audio 1.1](https://kenney.nl/assets/casino-audio), by Kenney Vleugels.
  Official archive: https://kenney.nl/media/pages/assets/casino-audio/2472606a04-1721639069/kenney_casino-audio.zip
  `card-slide-1.ogg` → draw; `card-place-1.ogg` → play; `card-shuffle.ogg` → shuffle; `card-shove-1.ogg` → return.
- [Interface Sounds 1.0](https://kenney.nl/assets/interface-sounds), by Kenney.
  Official archive: https://kenney.nl/media/pages/assets/interface-sounds/fa43c1dd4d-1677589452/kenney_interface-sounds.zip
  `click_003.ogg` → click; `glass_001.ogg` → effect; `error_002.ogg` → damage;
  `confirmation_002.ogg` → heal / match end; `minimize_004.ogg` → exile.

The selected OGG files are copied unchanged; Unity imports short effects as mono, decompressed on load.
Original licenses are included alongside this file: `Kenney-Casino.txt`, `Kenney-Interface.txt`.
Both packs explicitly permit personal and commercial use. CC0: https://creativecommons.org/publicdomain/zero/1.0/

## User-supplied BGM

User explicitly selected `audio_df740d01-43a7-44c1-b67e-5c803de92f60.m4a` for this game.
The original stays unchanged outside this repository. This music is user supplied and is **not** represented as CC0 or open-source music.

On 2026-11-07, the project author confirmed permission to distribute this BGM publicly with this game's repository and release. This does not grant a separate license to extract or reuse the music in other projects.

- Original SHA256: `79605675F1E1B951B272B0604A25505CAC23B7F99FBBE5BF19A4E0AFE1E2CC13`.
- Original: AAC, stereo, 44.1 kHz, 184.574762 seconds.
- Game asset: `Resources/Audio/bgm.ogg`, Vorbis quality 4, 0.4-second fade in and final 1.074762-second fade out to soften looping. Unity streams the stereo clip; it loops continuously across restarts and menu pauses.
- FFmpeg conversion: `-af afade=t=in:d=0.4,afade=t=out:st=183.5:d=1.074762 -c:a libvorbis -q:a 4`.

## Bundled multilingual font: Noto Sans CJK SC Regular

- [Official Noto CJK repository](https://github.com/notofonts/noto-cjk).
- Original file: https://raw.githubusercontent.com/notofonts/noto-cjk/main/Sans/OTF/SimplifiedChinese/NotoSansCJKsc-Regular.otf
- License: SIL Open Font License 1.1; full official text in `Noto-Sans-CJK.txt`.
- Copyright 2014-2021 Adobe (see embedded font copyright metadata).
- Font file is unmodified and is bundled for Chinese, Japanese and English. Two old unsupported arrow badges use supported simple arrows instead.

The Windows build and release archive include this entire license directory.
