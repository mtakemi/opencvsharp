# OpenCvSharp for Unity (Android)

[OpenCvSharp](https://github.com/shimat/opencvsharp) (OpenCV 4.13.0) packaged for Unity.
This is an unofficial fork; upstream OpenCvSharp does not support Unity.

| Platform | Native library | Notes |
|---|---|---|
| Android arm64-v8a (IL2CPP) | `libOpenCvSharpExtern.so` | Built with `build_android.sh` (NDK, API 23+, 16 KB page aligned) |
| Windows x64 Editor / Standalone | `OpenCvSharpExtern.dll` | From the official `OpenCvSharp4.runtime.win` NuGet package (without the FFmpeg DLL) |

Requires Unity 2022.3 or later (.NET Standard 2.1 API compatibility level).

## Install

Package Manager → **+** → **Add package from git URL...**

```
https://github.com/mtakemi/opencvsharp.git?path=/unity#4.x-unity-android
```

For local development, use **Add package from disk...** and select `unity/package.json`.

## Texture conversion

`OpenCvSharp.Unity.TextureConverter` converts Unity textures (RGBA, bottom-up) to BGR `Mat`s (top-down) and back.
Keep one instance per stream; the pixel buffer is reused.

```csharp
var converter = new TextureConverter();
using var frame = new Mat();
converter.ToMat(webCamTexture, frame);       // applies videoRotationAngle / videoVerticallyMirrored
// ... OpenCV processing ...
output = converter.ToTexture(frame, output); // reuses (or creates) an RGBA32 Texture2D
rawImage.texture = output;
```

Inside your own `namespace OpenCvSharp.*` code, names such as `Rect` and `Text` resolve to OpenCvSharp
(`OpenCvSharp.Rect`, the `OpenCvSharp.Text` namespace); write `UnityEngine.Rect` / `UnityEngine.UI.Text` explicitly.

## Samples

Package Manager → OpenCvSharp (Android) → **Samples** → Import, then add the component to a GameObject in an empty scene.

- **Smoke Test** (`OpenCvSharpSmokeTestBehaviour`): basic checks including Texture2D ⇔ Mat conversion.
  Results are shown on screen and in the log (`adb logcat -s Unity`).
- **Camera Detection** (`CameraDetectionSample`): ArUco markers (with distance via SolvePnP) and QR codes
  from the camera, shown in a uGUI `RawImage`. Use the component's context menu
  **Save ArUco markers as PNG** in the Editor to get markers (saved to `<project>/ArucoMarkers`).
  Distances use approximate camera intrinsics and are rough estimates.

## Limitations

- `highgui` (`Cv2.ImShow`, `Window`, trackbars, mouse callbacks) is not available on Android.
- `videoio` has no FFmpeg backend. Use Unity's `WebCamTexture` for camera input.
- `Mat.ForEachAs*` passes arbitrary delegates to native code and does not work with IL2CPP.
  Use indexers, `GetGenericIndexer`, or `Mat.GetUnsafeGenericIndexer` instead.
- Only arm64-v8a is built for Android.

## Building the binaries

The binaries in `Runtime/Plugins/` are committed so that the package can be installed from the git URL.
To rebuild them, run from the repository root:

```bash
ANDROID_NDK_HOME=/path/to/android-ndk-r27c ./build_android.sh   # libOpenCvSharpExtern.so
./build_unity_package.sh                                          # copies everything into unity/
```

## License

OpenCvSharp is licensed under Apache-2.0 (`LICENSE.md`).
See `Third Party Notices.md` for bundled third-party components.
