# Sources and licenses

Access/review date: **2026-09-04**. The original implementation, geometric glyphs, meshes, shader and explanatory prose are written for this repository under its existing GPL-3.0 license. No external model, artwork, font, tutorial prose or solver implementation was copied. Functional algorithm sequences and mathematical conventions are attributed below. External tools remain separately licensed; generated SDK/browser packages and build output are not checked in.

| Source | Use and conventions |
| --- | --- |
| https://www.worldcubeassociation.org/regulations/ | Article 12 cube face, wide and rotation notation; Appendix references the accepted notation convention. Local aliases and slices are documented separately. |
| https://jperm.net/3x3/cfop | Published CFOP stage order: cross, four F2L pairs, OLL, PLL. Our D-first orientation and original explanations are documented in CFOP.md. |
| https://jperm.net/algs/2look/oll | Selected three edge-orientation and seven corner-orientation algorithms; source lowercase r/f mean wide turns. |
| https://jperm.net/algs/2look/pll | Selected two corner-permutation and four edge-permutation algorithms, with setup/final U alignment explicit. |
| https://jperm.net/lib/2lookoll.js and https://jperm.net/lib/2lookpll.js | Author's trainer data used to verify functional move sequences, case names and orientation conventions; trainer code/data bundle is not redistributed. |
| https://kociemba.org/math/CubeDefs.htm | Independent standard corner/edge numbering and URFDLB facelet conventions. |
| https://kociemba.org/math/cubielevel.htm | Position/orientation conventions; no solver code imported. |
| https://kociemba.org/math/faceletlevel.htm | Independent facelet-level conventions used to cross-check move effects. |
| https://kociemba.org/math/oh.htm | Superflip state and its established move generator used as a difficult independent fixture. See CORE_NOTES.md for exact fixture provenance. |
| https://unity.com/releases/editor/whats-new/6000.0.68f1 | Pinned supported Unity 6.0 LTS patch and changeset e1e9baaf294b. There was no existing Editor version or code to migrate. Unity license required separately. |
| https://services.api.unity.com/unity/editor/release/v1/releases?version=6000.0.68f1&architecture=X86_64&platform=WINDOWS&limit=1 | Official release metadata: Windows Editor and Web module URLs, published MD5 integrity values, archive sizes and installed sizes. Exact values and observed verification results are retained in `docs/evidence/unity-toolchain-2026-09-04.json`. |
| https://download.unity3d.com/download_unity/e1e9baaf294b/Windows64EditorInstaller/UnitySetup64-6000.0.68f1.exe | Official Windows Editor package. Downloaded to ignored local tooling; published MD5 and valid Unity Technologies SF Authenticode signature checked before extraction. Actual SHA-256 is recorded in the toolchain evidence. |
| https://download.unity3d.com/download_unity/e1e9baaf294b/TargetSupportInstaller/UnitySetup-WebGL-Support-for-Editor-6000.0.68f1.exe | Official Web module package, verified in the same way. Includes Emscripten, LLVM, Node, Python, Binaryen and Web build dependencies; the core Editor includes IL2CPP. Binaries retain their original licenses and are not redistributed in the repository. |
| https://docs.unity.com/en-us/hub/add-modules | Official Hub workflow for installing a matching Web support module. The local project uses the same official package payload. |
| https://docs.unity.com/en-us/hub/manage-license | Official user-managed Hub sign-in and license activation workflow. Licenses and credentials are not part of this repository. |
| https://docs.unity3d.com/6000.0/Documentation/Manual/plug-ins-managed.html | External managed-assembly compilation and Unity assembly references. `scripts/test-unity-api.ps1` uses the genuine Editor and Web modules for C# API checks; no substitute Unity API definitions are used. |
| https://docs.unity3d.com/6000.0/Documentation/Manual/InstallingUnity.html and https://nsis.sourceforge.io/Docs/Chapter3.html | Installer command-line semantics: `/D` selects an installation directory, not an extraction sandbox. The downloaded installers were not executed for workspace extraction. |
| https://sourceforge.net/projects/nsisbi/files/nsisbi3.10.3/nsis-code-7423-3-NSIS-trunk.zip/download | Official NSISBI source archive, examined under its zlib/libpng licensing. `fileform.h`, `fileform.c`, `state.h`, `util.c` and `mtwdecompress.c` establish the package metadata, string, LZMA chunk and CRC formats. An original Python standard-library parser in ignored local tooling extracted only payload files, with guarded paths and every file CRC checked; no NSIS installer script was executed. |
| https://docs.unity3d.com/6000.0/Documentation/Manual/csharp-compiler.html | C# 9 compatibility; shared code avoids unsupported language/runtime features. |
| https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-native-plugins-with-emscripten.html | Unity's bundled Emscripten toolchain and the version-file location. The actual pinned package reports `3.1.39-git`; no external native solver plugin was added. |
| https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-technical-overview.html | Browser restrictions: no managed threading assumption, no runtime code generation or native solver plugin; cooperative C# work slices. |
| https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-interactingwithbrowserscripting.html | SendMessage/.jslib integration with accessible host HTML. |
| https://docs.unity3d.com/6000.0/Documentation/Manual/webgl-deploying.html | MIME/deployment configuration; uncompressed local build avoids compression header ambiguity. |
| https://builds.dotnet.microsoft.com/dotnet/release-metadata/8.0/releases.json | Microsoft's SDK 8.0.419 Windows x64 archive URL and SHA-512, pinned in scripts/bootstrap-dotnet.ps1. SDK/runtime under Microsoft/.NET licenses (predominantly MIT); no global install. |
| https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-install-script | Official private SDK installation guidance, no global PATH assumption. |
| https://playwright.dev/docs/browsers | Installed Edge/Chrome test channels. Development-only Playwright 1.56.1, Apache-2.0, exact package lock checked in. |

See CORE_NOTES.md for precise coordinate/orientation and independent fixture details; CFOP.md for algorithm coverage, original recognition logic and table integrity; UNITY.md for engine-specific API references. Source references support design and provenance; execution results are recorded separately in STATUS.md and `docs/evidence/`. No tutorial prose or artwork is copied from these sources.
