using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenCvSharp.Unity.Samples
{
    /// <summary>
    /// Checks orientation and channel order of <see cref="TextureConverter"/> (requires UnityEngine).
    /// </summary>
    public static class TextureConverterSmokeTests
    {
        private static readonly Color32 Red = new Color32(255, 0, 0, 255);
        private static readonly Color32 Blue = new Color32(0, 0, 255, 255);
        private static readonly Color32 Black = new Color32(0, 0, 0, 255);

        private static readonly (string Name, Func<string> Test)[] Tests =
        {
            ("Texture2D -> Mat", TextureToMat),
            ("Mat -> Texture2D", MatToTexture),
            ("Gray Mat -> Texture2D", GrayMatToTexture),
            ("Texture2D round trip", RoundTrip),
        };

        public static List<OpenCvSharpSmokeTests.Result> RunAll() => OpenCvSharpSmokeTests.Run(Tests);

        // 3x2 texture: red at the top-left, blue at the bottom-right, black elsewhere.
        // Unity's y axis points up, so the top row is y = 1.
        private static Texture2D CreateTexture()
        {
            var tex = new Texture2D(3, 2, TextureFormat.RGBA32, false);
            var pixels = new Color32[6];
            for (var i = 0; i < pixels.Length; i++)
                pixels[i] = Black;
            pixels[1 * 3 + 0] = Red;  // x=0, y=1 (top-left)
            pixels[0 * 3 + 2] = Blue; // x=2, y=0 (bottom-right)
            tex.SetPixels32(pixels);
            tex.Apply(false);
            return tex;
        }

        private static string TextureToMat()
        {
            var tex = CreateTexture();
            try
            {
                using var mat = new Mat();
                new TextureConverter().ToMat(tex, mat);
                Check(mat.Type() == MatType.CV_8UC3 && mat.Rows == 2 && mat.Cols == 3, $"type {mat.Type()} size {mat.Size()}");
                // OpenCV: row 0 is the top row, channels are BGR
                CheckBgr(mat.At<Vec3b>(0, 0), 0, 0, 255, "top-left");
                CheckBgr(mat.At<Vec3b>(1, 2), 255, 0, 0, "bottom-right");
                CheckBgr(mat.At<Vec3b>(0, 2), 0, 0, 0, "top-right");
                return "top-left red, bottom-right blue";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        private static string MatToTexture()
        {
            using var mat = new Mat(2, 3, MatType.CV_8UC3, Scalar.All(0));
            mat.Set(0, 0, new Vec3b(0, 0, 255));   // top-left red (BGR)
            mat.Set(1, 2, new Vec3b(255, 0, 0));   // bottom-right blue
            var tex = new TextureConverter().ToTexture(mat);
            try
            {
                Check(tex.width == 3 && tex.height == 2 && tex.format == TextureFormat.RGBA32, $"{tex.width}x{tex.height} {tex.format}");
                CheckColor(tex.GetPixel(0, 1), Red, "top-left");
                CheckColor(tex.GetPixel(2, 0), Blue, "bottom-right");
                CheckColor(tex.GetPixel(2, 1), Black, "top-right");
                return "top-left red, bottom-right blue";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        private static string GrayMatToTexture()
        {
            using var mat = new Mat(2, 3, MatType.CV_8UC1, Scalar.All(128));
            var tex = new TextureConverter().ToTexture(mat);
            try
            {
                CheckColor(tex.GetPixel(1, 1), new Color32(128, 128, 128, 255), "pixel");
                return "gray 128";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        private static string RoundTrip()
        {
            var src = CreateTexture();
            Texture2D dst = null;
            try
            {
                var converter = new TextureConverter();
                using var mat = new Mat();
                converter.ToMat(src, mat);
                dst = converter.ToTexture(mat);
                var a = src.GetPixels32();
                var b = dst.GetPixels32();
                Check(a.Length == b.Length, "length mismatch");
                for (var i = 0; i < a.Length; i++)
                    CheckColor(b[i], a[i], $"pixel {i}");
                return $"{a.Length} pixels identical";
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(src);
                if (dst != null)
                    UnityEngine.Object.DestroyImmediate(dst);
            }
        }

        private static void CheckBgr(Vec3b px, byte b, byte g, byte r, string where)
        {
            Check(px.Item0 == b && px.Item1 == g && px.Item2 == r, $"{where}: BGR=({px.Item0},{px.Item1},{px.Item2}), expected ({b},{g},{r})");
        }

        private static void CheckColor(Color32 actual, Color32 expected, string where)
        {
            Check(actual.r == expected.r && actual.g == expected.g && actual.b == expected.b && actual.a == expected.a,
                $"{where}: {actual}, expected {expected}");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
