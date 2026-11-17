# Third-party notices

Hometown Store Access itself is MIT licensed (see `LICENSE`). The release packages also contain
the following software, which keeps its own license.

## Prism

`prism.dll`: the speech library that talks to screen readers and Windows speech.

- Project: <https://github.com/ethindp/prism>
- License: Mozilla Public License 2.0 (`licenses/LICENSE-prism-MPL-2.0.txt`)
- `prism.dll` is distributed unmodified. Its source code is available from the project page.

Prism includes code from these projects, whose license texts are in `licenses/prism-third-party`:

| Component | License |
| --- | --- |
| concurrentqueue | Simplified BSD, or Boost Software License |
| djinni | Apache License 2.0 |
| dr_wav | Public domain (Unlicense), or MIT No Attribution |
| {fmt} | MIT |
| Highway | Apache License 2.0, or BSD 3-Clause |
| moderncom | MIT |
| NVDA Controller Client | GNU LGPL 2.1 |
| NVGT | zlib |
| simdutf | Apache License 2.0, or MIT |

## BepInEx

`BepInEx/core/BepInEx*.dll` and the BepInEx Harmony interop files: the mod loader.

- Project: <https://github.com/BepInEx/BepInEx> (version 5.4.23.6)
- License: GNU Lesser General Public License 2.1 (`licenses/BepInEx-and-Doorstop-LGPL-2.1.txt`)
- Distributed unmodified, as published on the project's release page. The source code is
  available from the project page.

## Unity Doorstop

`winhttp.dll`, `doorstop_config.ini`, `.doorstop_version`: loads BepInEx when the game starts.

- Project: <https://github.com/NeighTools/UnityDoorstop>
- License: GNU Lesser General Public License 2.1 (`licenses/BepInEx-and-Doorstop-LGPL-2.1.txt`)
- Distributed unmodified, as part of the BepInEx release.

## HarmonyX

`BepInEx/core/0Harmony.dll`: runtime patching, used by BepInEx and the mod.

- Project: <https://github.com/BepInEx/HarmonyX>
- License: MIT. Copyright (c) 2017 Andreas Pardeike, and the HarmonyX contributors.

## MonoMod

`BepInEx/core/MonoMod.*.dll`: used by HarmonyX.

- Project: <https://github.com/MonoMod/MonoMod>
- License: MIT. Copyright (c) 2015 - 2020 0x0ade.

## Mono.Cecil

`BepInEx/core/Mono.Cecil*.dll`: used by BepInEx and MonoMod.

- Project: <https://github.com/jbevain/cecil>
- License: MIT. Copyright (c) 2008 - 2015 Jb Evain, Copyright (c) 2008 - 2011 Novell, Inc.

## MIT license text

The components above that are licensed under MIT use this text, with their own copyright line:

```
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
associated documentation files (the "Software"), to deal in the Software without restriction,
including without limitation the rights to use, copy, modify, merge, publish, distribute,
sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or
substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT
NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM,
DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT
OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
```

## Hometown Store

Hometown Store is made by Circle Cat. This mod does not contain any of the game's files. It
changes the game only while running, through BepInEx.
