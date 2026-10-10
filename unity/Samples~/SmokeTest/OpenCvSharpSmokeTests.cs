using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
using OpenCvSharp.Aruco;

namespace OpenCvSharp.Unity.Samples
{
    /// <summary>
    /// Smoke tests for OpenCvSharp on Unity (IL2CPP / Android).
    /// Has no UnityEngine dependency so that the same code can also run on plain .NET.
    /// </summary>
    public static class OpenCvSharpSmokeTests
    {
        public sealed class Result
        {
            public string Name { get; }
            public bool Passed { get; }
            public string Message { get; }

            public Result(string name, bool passed, string message)
            {
                Name = name;
                Passed = passed;
                Message = message;
            }

            public override string ToString() => $"[{(Passed ? "PASS" : "FAIL")}] {Name}: {Message}";
        }

        private static readonly (string Name, Func<string> Test)[] Tests =
        {
            ("Version", Version),
            ("CvtColor", CvtColor),
            ("OpenCVException", OpenCvException),
            ("ImEncode/ImDecode", Codec),
            ("ArUco detect", ArucoDetect),
            ("ArUco pose (SolvePnP)", ArucoPose),
            ("QR decode", QrDecode),
            ("QR decode UTF-8", QrDecodeUtf8),
        };

        /// <summary>
        /// Runs all tests. Each test returns a message on success and throws on failure.
        /// </summary>
        public static List<Result> RunAll() => Run(Tests);

        public static List<Result> Run(IEnumerable<(string Name, Func<string> Test)> tests)
        {
            var results = new List<Result>();
            foreach (var (name, test) in tests)
            {
                try
                {
                    results.Add(new Result(name, true, test()));
                }
                catch (Exception e)
                {
                    results.Add(new Result(name, false, $"{e.GetType().Name}: {e.Message}"));
                }
            }
            return results;
        }

        private static string Version()
        {
            var version = Cv2.GetVersionString();
            Check(!string.IsNullOrEmpty(version), "empty version string");
            return $"OpenCV {version}, threads={Cv2.GetNumThreads()}";
        }

        private static string CvtColor()
        {
            using var bgr = new Mat(64, 64, MatType.CV_8UC3, new Scalar(0, 0, 255)); // pure red
            using var gray = new Mat();
            Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
            var value = gray.At<byte>(0, 0);
            Check(value == 76, $"gray value {value}, expected 76");
            return $"gray={value}";
        }

        // Verifies the native error callback (static [MonoPInvokeCallback] method) under IL2CPP.
        private static string OpenCvException()
        {
            using var src = new Mat(10, 10, MatType.CV_8UC1);
            using var dst = new Mat();
            try
            {
                Cv2.CvtColor(src, dst, ColorConversionCodes.BGR2GRAY); // 1 channel is invalid for BGR2GRAY
            }
            catch (OpenCVException e)
            {
                return $"caught: {e.Status} in {e.FuncName}";
            }
            throw new InvalidOperationException("OpenCVException was not thrown");
        }

        private static string Codec()
        {
            using var src = new Mat(48, 32, MatType.CV_8UC3, new Scalar(10, 20, 30));
            var png = src.ImEncode(".png");
            using var decoded = Cv2.ImDecode(png, ImreadModes.Color);
            Check(decoded.Width == 32 && decoded.Height == 48, $"decoded size {decoded.Size()}");
            var px = decoded.At<Vec3b>(0, 0);
            Check(px.Item0 == 10 && px.Item1 == 20 && px.Item2 == 30, $"decoded pixel {px}");
            return $"png {png.Length} bytes";
        }

        private const int MarkerId = 7;
        private const int MarkerPixels = 200;
        private const int CanvasPixels = 400;

        // White canvas with ArUco marker id=7 (DICT_4X4_50) centered.
        private static Mat CreateMarkerImage()
        {
            using var dictionary = CvAruco.GetPredefinedDictionary(PredefinedDictionaryType.Dict4X4_50);
            using var marker = new Mat();
            dictionary.GenerateImageMarker(MarkerId, MarkerPixels, marker);

            var canvas = new Mat(CanvasPixels, CanvasPixels, MatType.CV_8UC1, Scalar.White);
            var offset = (CanvasPixels - MarkerPixels) / 2;
            using (var roi = canvas[new Rect(offset, offset, MarkerPixels, MarkerPixels)])
            {
                marker.CopyTo(roi);
            }
            return canvas;
        }

        private static Point2f[] DetectMarker()
        {
            using var image = CreateMarkerImage();
            using var dictionary = CvAruco.GetPredefinedDictionary(PredefinedDictionaryType.Dict4X4_50);
            using var detector = new ArucoDetector(dictionary);
            detector.DetectMarkers(image, out var corners, out var ids, out _);
            Check(ids.Length == 1 && ids[0] == MarkerId, $"ids=[{string.Join(",", ids)}]");
            return corners[0];
        }

        private static string ArucoDetect()
        {
            var corners = DetectMarker();
            return "corners " + string.Join(" ", corners.Select(p => $"({p.X:F0},{p.Y:F0})"));
        }

        private static string ArucoPose()
        {
            var corners = DetectMarker();

            // Marker side 5 cm. Corner order: top-left, top-right, bottom-right, bottom-left.
            const float half = 0.05f / 2;
            var objectPoints = new[]
            {
                new Point3f(-half, half, 0), new Point3f(half, half, 0),
                new Point3f(half, -half, 0), new Point3f(-half, -half, 0),
            };
            // fx = fy = 400, principal point at the canvas center (= marker center)
            const double f = 400;
            var cameraMatrix = new double[,] { { f, 0, CanvasPixels / 2.0 }, { 0, f, CanvasPixels / 2.0 }, { 0, 0, 1 } };
            var rvec = new double[3];
            var tvec = new double[3];
            Cv2.SolvePnP(objectPoints, corners, cameraMatrix, null, ref rvec, ref tvec, flags: SolvePnPMethod.IPPE_SQUARE);

            // Expected distance: f * size / pixels = 400 * 0.05 / 200 = 0.1 m
            var expectedZ = f * 0.05 / MarkerPixels;
            Check(Math.Abs(tvec[2] - expectedZ) < 0.005, $"tvec.z={tvec[2]:F4}, expected {expectedZ:F4}");
            Check(Math.Abs(tvec[0]) < 0.005 && Math.Abs(tvec[1]) < 0.005, $"tvec=({tvec[0]:F4},{tvec[1]:F4})");
            return $"tvec=({tvec[0]:F4}, {tvec[1]:F4}, {tvec[2]:F4}) m";
        }

        private static string QrDecode()
        {
            using var qr = Cv2.ImDecode(Convert.FromBase64String(SmokeTestImages.QrPngBase64), ImreadModes.Grayscale);
            using var canvas = new Mat(300, 300, MatType.CV_8UC1, Scalar.White);
            using (var roi = canvas[new Rect(100, 100, qr.Width, qr.Height)])
            {
                qr.CopyTo(roi);
            }
            return DecodeQr(canvas, "https://github.com/opencv/opencv");
        }

        // Verifies UTF-8 string marshalling from native code.
        private static string QrDecodeUtf8()
        {
            using var image = Cv2.ImDecode(Convert.FromBase64String(SmokeTestImages.QrMultibytePngBase64), ImreadModes.Color);
            return DecodeQr(image, "Helloこんにちは你好안녕하세요");
        }

        private static string DecodeQr(Mat image, string expected)
        {
            using var detector = new QRCodeDetector();
            var text = detector.DetectAndDecode(image, out var points);
            Check(points.Length == 4, $"points={points.Length}");
            Check(text == expected, $"decoded \"{text}\"");
            return $"\"{text}\"";
        }

        private static void Check(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }
    }
}
