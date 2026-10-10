#!/usr/bin/env bash
# Build OpenCV (+contrib, static) and libOpenCvSharpExtern.so for Android with the NDK.
#
# Usage:
#   ANDROID_NDK_HOME=/path/to/android-ndk ./build_android.sh [abi ...]
#
#   abi                default: arm64-v8a (also: armeabi-v7a, x86_64)
#   ANDROID_API_LEVEL  minimum API level (default: 23)
#   BUILD_ROOT         working directory (default: <repo>/build_android)
#   JOBS               parallel jobs (default: nproc)
#
# Output: $BUILD_ROOT/<abi>/libOpenCvSharpExtern.so
set -euo pipefail

: "${ANDROID_NDK_HOME:?Set ANDROID_NDK_HOME to the Android NDK directory}"
REPO_ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
API_LEVEL=${ANDROID_API_LEVEL:-23}
BUILD_ROOT=${BUILD_ROOT:-$REPO_ROOT/build_android}
JOBS=${JOBS:-$(nproc)}
TOOLCHAIN=$ANDROID_NDK_HOME/build/cmake/android.toolchain.cmake

if [ $# -eq 0 ]; then
  set -- arm64-v8a
fi

for ABI in "$@"; do
  WORK=$BUILD_ROOT/$ABI
  OPENCV_INSTALL=$WORK/opencv_install

  # c++_static: avoid clashing with the libc++_shared.so of the host app (e.g. Unity).
  # max-page-size=16384: 16 KB page alignment required by Google Play for Android 15+.
  COMMON_ARGS=(
    -G Ninja
    -D CMAKE_TOOLCHAIN_FILE="$TOOLCHAIN"
    -D ANDROID_ABI="$ABI"
    -D ANDROID_PLATFORM="android-$API_LEVEL"
    -D ANDROID_STL=c++_static
    -D CMAKE_BUILD_TYPE=Release
    -D CMAKE_SHARED_LINKER_FLAGS="-Wl,-z,max-page-size=16384"
  )

  if [ -f "$OPENCV_INSTALL/sdk/native/jni/OpenCVConfig.cmake" ]; then
    echo "=== [$ABI] OpenCV already installed in $OPENCV_INSTALL, skipping (delete it to rebuild) ==="
  else
    echo "=== [$ABI] Configure OpenCV ==="
    cmake "${COMMON_ARGS[@]}" \
      -C "$REPO_ROOT/cmake/opencv_build_options_android.cmake" \
      -S "$REPO_ROOT/opencv" \
      -B "$WORK/opencv_build" \
      -D OPENCV_EXTRA_MODULES_PATH="$REPO_ROOT/opencv_contrib/modules" \
      -D CMAKE_INSTALL_PREFIX="$OPENCV_INSTALL"

    echo "=== [$ABI] Build OpenCV ==="
    cmake --build "$WORK/opencv_build" -j "$JOBS"
    cmake --install "$WORK/opencv_build"
  fi

  echo "=== [$ABI] Build OpenCvSharpExtern ==="
  cmake "${COMMON_ARGS[@]}" \
    -S "$REPO_ROOT/src" \
    -B "$WORK/extern_build" \
    -D OpenCV_DIR="$OPENCV_INSTALL/sdk/native/jni" \
    -D NO_HIGHGUI=ON
  cmake --build "$WORK/extern_build" -j "$JOBS"

  SO=$WORK/extern_build/OpenCvSharpExtern/libOpenCvSharpExtern.so
  "$ANDROID_NDK_HOME"/toolchains/llvm/prebuilt/*/bin/llvm-strip --strip-unneeded "$SO" -o "$WORK/libOpenCvSharpExtern.so"
  echo "=== [$ABI] Done: $WORK/libOpenCvSharpExtern.so ==="
  ls -lh "$WORK/libOpenCvSharpExtern.so"
done
