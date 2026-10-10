#!/usr/bin/env bash
# Copy the binaries into the Unity package (unity/).
#
#   - Runtime/Plugins/OpenCvSharp.dll (+ .xml)       netstandard2.1 build of src/OpenCvSharp
#   - Runtime/Plugins/System.Runtime.CompilerServices.Unsafe.dll
#   - Runtime/Plugins/Android/arm64-v8a/libOpenCvSharpExtern.so   from build_android.sh
#   - Runtime/Plugins/Windows/x86_64/OpenCvSharpExtern.dll        from the official NuGet runtime (Editor)
#   - ThirdPartyNotices~/opencv-3rdparty/                         license files of OpenCV's bundled libraries
#
# Usage:
#   ./build_unity_package.sh
#
#   DOTNET            dotnet executable (default: dotnet in PATH, then ~/.dotnet/dotnet)
#   ANDROID_SO        libOpenCvSharpExtern.so to use (default: build_android/arm64-v8a/libOpenCvSharpExtern.so)
#   WIN_RUNTIME_VER   version of OpenCvSharp4.runtime.win (default below)
set -euo pipefail

REPO_ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
PKG=$REPO_ROOT/unity
PLUGINS=$PKG/Runtime/Plugins
CACHE=$REPO_ROOT/build_unity
WIN_RUNTIME_VER=${WIN_RUNTIME_VER:-4.13.0.20260627}
ANDROID_SO=${ANDROID_SO:-$REPO_ROOT/build_android/arm64-v8a/libOpenCvSharpExtern.so}
OPENCV_LICENSES=$REPO_ROOT/build_android/arm64-v8a/opencv_install/sdk/etc/licenses

if [ -z "${DOTNET:-}" ]; then
  if command -v dotnet >/dev/null; then DOTNET=dotnet; else DOTNET=$HOME/.dotnet/dotnet; fi
fi

mkdir -p "$CACHE" "$PLUGINS/Android/arm64-v8a" "$PLUGINS/Windows/x86_64"

echo "=== OpenCvSharp.dll (netstandard2.1) ==="
"$DOTNET" build "$REPO_ROOT/src/OpenCvSharp/OpenCvSharp.csproj" -c Release -f netstandard2.1 -nologo -v quiet
OUT=$REPO_ROOT/src/OpenCvSharp/bin/Release/netstandard2.1
cp "$OUT/OpenCvSharp.dll" "$OUT/OpenCvSharp.xml" "$PLUGINS/"

echo "=== System.Runtime.CompilerServices.Unsafe.dll ==="
UNSAFE_VER=$(python3 -I -c 'import json,sys
libs = json.load(open(sys.argv[1]))["libraries"]
print(next(k.split("/")[1] for k in libs if k.startswith("System.Runtime.CompilerServices.Unsafe/")))' \
  "$REPO_ROOT/src/OpenCvSharp/obj/project.assets.json")
cp "$HOME/.nuget/packages/system.runtime.compilerservices.unsafe/$UNSAFE_VER/lib/netstandard2.0/System.Runtime.CompilerServices.Unsafe.dll" "$PLUGINS/"
echo "version $UNSAFE_VER"

echo "=== Android arm64-v8a: $ANDROID_SO ==="
cp "$ANDROID_SO" "$PLUGINS/Android/arm64-v8a/libOpenCvSharpExtern.so"

echo "=== Windows x64 (OpenCvSharp4.runtime.win $WIN_RUNTIME_VER) ==="
NUPKG=$CACHE/opencvsharp4.runtime.win.$WIN_RUNTIME_VER.nupkg
if [ ! -f "$NUPKG" ]; then
  curl -fsSL -o "$NUPKG" "https://api.nuget.org/v3-flatcontainer/opencvsharp4.runtime.win/$WIN_RUNTIME_VER/opencvsharp4.runtime.win.$WIN_RUNTIME_VER.nupkg"
fi
# FFmpeg plugin DLL (LGPL) is intentionally not copied
python3 -I -c 'import zipfile,sys
with zipfile.ZipFile(sys.argv[1]) as z, open(sys.argv[2], "wb") as f:
    f.write(z.read("runtimes/win-x64/native/OpenCvSharpExtern.dll"))' \
  "$NUPKG" "$PLUGINS/Windows/x86_64/OpenCvSharpExtern.dll"

echo "=== Third-party license files ==="
rm -rf "$PKG/ThirdPartyNotices~/opencv-3rdparty"
mkdir -p "$PKG/ThirdPartyNotices~"
cp -r "$OPENCV_LICENSES" "$PKG/ThirdPartyNotices~/opencv-3rdparty"
cp "$REPO_ROOT/opencv/LICENSE" "$PKG/ThirdPartyNotices~/opencv-LICENSE"
cp "$REPO_ROOT/opencv_contrib/LICENSE" "$PKG/ThirdPartyNotices~/opencv_contrib-LICENSE"

echo "=== Done ==="
find "$PLUGINS" -type f ! -name "*.meta" -exec ls -lh {} \; | awk '{print $5, $9}'
