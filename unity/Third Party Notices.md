# Third Party Notices

This package redistributes the following components in binary form.
Full license texts are in the `ThirdPartyNotices~` folder (copied by `build_unity_package.sh`).

## Managed

| Component | License | Notes |
|---|---|---|
| OpenCvSharp | Apache-2.0 | `LICENSE.md` |
| System.Runtime.CompilerServices.Unsafe | MIT | Copyright (c) .NET Foundation and Contributors. https://github.com/dotnet/runtime |

## Native (all platforms)

| Component | License | Notes |
|---|---|---|
| OpenCV 4.13.0 | Apache-2.0 | `ThirdPartyNotices~/opencv-LICENSE` |
| opencv_contrib 4.13.0 | Apache-2.0 | `ThirdPartyNotices~/opencv_contrib-LICENSE` |
| zlib, libpng, libjpeg-turbo, libtiff, OpenJPEG, libwebp, OpenEXR | Permissive (zlib, libpng, IJG/BSD, BSD) | Bundled with OpenCV, `ThirdPartyNotices~/opencv-3rdparty/` |
| protobuf, flatbuffers, dlpack | BSD-3-Clause, Apache-2.0 | Used by the `dnn` module |
| ittnotify | BSD-3-Clause (dual licensed with GPL-2.0; used under BSD-3-Clause) | |
| SoftFloat, mscr chi table | BSD-style | |

## Android only

| Component | License | Notes |
|---|---|---|
| cpufeatures | BSD-2-Clause | Android NDK |
| KleidiCV | Apache-2.0 | OpenCV HAL for Arm |
| LLVM libc++ (statically linked, `c++_static`) | Apache-2.0 WITH LLVM-exception | Android NDK |

## Windows x64 only (official `OpenCvSharp4.runtime.win` build)

| Component | License | Notes |
|---|---|---|
| Tesseract | Apache-2.0 | |
| Leptonica | BSD-2-Clause style | |
| libarchive (with bzip2, liblzma, zstd) | BSD-2-Clause, bzip2 license, 0BSD/public domain, BSD-3-Clause | Tesseract dependency |
| HDF5 | BSD-3-Clause style | |
| Eigen | MPL-2.0 | Unmodified; source available at https://gitlab.com/libeigen/eigen |
| Intel IPP ICV | Intel Simplified Software License | Redistributable binary |

The Windows build is configured with `OPENCV_ENABLE_NONFREE=ON` and contains patented algorithms
(e.g. SURF in `xfeatures2d`). Avoid them in commercial products unless you have checked the patent status.
The Android build does not enable non-free algorithms.

FFmpeg (LGPL) is **not** included: the `opencv_videoio_ffmpeg*.dll` plugin of the official Windows package is omitted,
and the Android build has FFmpeg disabled.
