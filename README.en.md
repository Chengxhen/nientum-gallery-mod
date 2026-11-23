# NientumGallery — In-Game Gallery Mod

An in-game CG / character-art / music gallery for **Nientum - Op.ZERO**, implemented as a BepInEx 5 + Harmony plugin. The entry is attached to the title screen and all content is read from the game's own resources (Addressables CG, FMOD music, Spine characters).

This repository contains source code only. No game executable, CG, character art, music, or fonts are included.

## Features

- Six tabs: ending CG, original art, story/epilogue, character art, variations, tracks
- Card grid, zoom/drag viewer, hover/selection highlight, chapter filters
- Theater-style player: track cards, preview/full playback, looping, footlight and halo spectrum visualizers, auto-hiding bottom bar
- Ducks the main-menu BGM while the gallery is open and restores it on close
- Media Foundation FLAC fallback for tracks missing from the loaded FMOD banks

## Layout

- `Plugin.cs` — entry point, UI construction, CG/music logic, Harmony patches
- `GalleryData.cs` — resource index (Addressables / FMOD banks)
- `PlayerCardStage.cs`, `PlayerVizOverlay.cs`, `SpectrumGraphic.cs`, `UiSprites.cs` — player and visualizers
- `FlacAudio.cs`, `NientumFlac.cpp` — FLAC fallback decoder (Media Foundation)
- `NientumGallery.csproj`

## Build

Point `GameDir` at the game installation:

```bat
dotnet build NientumGallery.csproj -c Release ^
  -p:GameDir="D:\Games\Liora"
```

Build the native decoder with MSVC (pragma-comment links the required libraries):

```bat
cl /LD /O2 /EHsc NientumFlac.cpp /Fe:NientumFlac.dll
```

Place `NientumFlac.dll` and the asset folders under `BepInEx\plugins\NientumGalleryAssets\`, and the plugin DLL under `BepInEx\plugins\`.

## Release

`NientumGallery_Mod_20261118_no-music.zip`: plugin DLL + assets (CG, thumbnails, UI, fonts, decoder) + bundled BepInEx 5.4 framework. Extract over the game root. Music is not included, so the FLAC fallback for tracks missing from FMOD banks is unavailable.

## Known limitations

- A few tracks are not present in the currently loaded FMOD banks and will report "not in the game audio library"; this cannot be fixed by prefix matching.
- Tied to the game's current assembly version; game updates may require re-checking `Assembly-CSharp`.
- Windows + BepInEx 5 only.

## Copyright and takedown

This is a fan-made, unofficial project and is not affiliated with the developers or publishers of Nientum - Op.ZERO. All game content (CG, character art, music, fonts, script text) is copyright QueseraGames; BepInEx is LGPL-2.1. Release packages are for personal, non-commercial study and sharing only. If a rights holder believes a release infringes their rights, please open an Issue in this repository and the material will be removed promptly.

## License

MIT for the code, see [LICENSE](LICENSE). Game assets and third-party components keep their own licenses.