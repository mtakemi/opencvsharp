using System;
using UnityEngine;

namespace OpenCvSharp.Unity
{
    /// <summary>
    /// Converts between Unity textures and OpenCV <see cref="Mat"/>s.
    /// Unity textures are RGBA with the origin at the bottom-left; the Mats produced here are
    /// BGR (CV_8UC3) with the origin at the top-left, as OpenCV expects.
    /// The pixel buffer is reused between calls, so keep one instance per stream (e.g. per camera).
    /// Not thread-safe.
    /// </summary>
    public sealed class TextureConverter
    {
        private Color32[] buffer = Array.Empty<Color32>();

        /// <summary>
        /// Texture2D (must be readable) to BGR Mat.
        /// </summary>
        public void ToMat(Texture2D src, Mat dst)
        {
            if (src == null)
                throw new ArgumentNullException(nameof(src));
            if (dst == null)
                throw new ArgumentNullException(nameof(dst));

            buffer = src.GetPixels32();
            PixelsToMat(src.width, src.height, dst, flipVertical: true);
        }

        /// <summary>
        /// Current frame of a WebCamTexture to BGR Mat.
        /// Wait until <c>didUpdateThisFrame</c> is true (the size is 16x16 before the first frame).
        /// </summary>
        /// <param name="correctOrientation">
        /// Apply <c>videoRotationAngle</c> and <c>videoVerticallyMirrored</c> so that the Mat is upright.
        /// </param>
        public void ToMat(WebCamTexture src, Mat dst, bool correctOrientation = true)
        {
            if (src == null)
                throw new ArgumentNullException(nameof(src));
            if (dst == null)
                throw new ArgumentNullException(nameof(dst));

            EnsureBuffer(src.width * src.height);
            src.GetPixels32(buffer);
            // Unity rows are bottom-up; a vertically mirrored camera image is already top-down.
            var flip = !(correctOrientation && src.videoVerticallyMirrored);
            PixelsToMat(src.width, src.height, dst, flip);
            if (correctOrientation)
                Rotate(dst, src.videoRotationAngle);
        }

        /// <summary>
        /// Mat (CV_8UC3 BGR, CV_8UC4 BGRA or CV_8UC1 gray) to an RGBA32 Texture2D.
        /// <paramref name="dst"/> is reused (and resized if needed); a new texture is created when it is null.
        /// </summary>
        public Texture2D ToTexture(Mat src, Texture2D dst = null)
        {
            if (src == null)
                throw new ArgumentNullException(nameof(src));

            ColorConversionCodes code;
            if (src.Type() == MatType.CV_8UC3)
                code = ColorConversionCodes.BGR2RGBA;
            else if (src.Type() == MatType.CV_8UC4)
                code = ColorConversionCodes.BGRA2RGBA;
            else if (src.Type() == MatType.CV_8UC1)
                code = ColorConversionCodes.GRAY2RGBA;
            else
                throw new ArgumentException($"Unsupported Mat type {src.Type()}", nameof(src));

            int width = src.Cols, height = src.Rows;
            if (dst == null)
                dst = new Texture2D(width, height, TextureFormat.RGBA32, false);
            else if (dst.width != width || dst.height != height || dst.format != TextureFormat.RGBA32)
                dst.Reinitialize(width, height, TextureFormat.RGBA32, false);

            EnsureBuffer(width * height);
            using (var rgba = Mat.FromPixelData(height, width, MatType.CV_8UC4, buffer))
            {
                // Same size and type, so OpenCV writes directly into the pinned buffer
                Cv2.CvtColor(src, rgba, code);
                Cv2.Flip(rgba, rgba, FlipMode.X);
            }
            dst.SetPixels32(buffer);
            dst.Apply(false);
            return dst;
        }

        private void PixelsToMat(int width, int height, Mat dst, bool flipVertical)
        {
            using (var rgba = Mat.FromPixelData(height, width, MatType.CV_8UC4, buffer))
            {
                Cv2.CvtColor(rgba, dst, ColorConversionCodes.RGBA2BGR);
            }
            if (flipVertical)
                Cv2.Flip(dst, dst, FlipMode.X);
        }

        // Rotates clockwise by the given angle (multiple of 90).
        private static void Rotate(Mat mat, int angle)
        {
            switch ((angle % 360 + 360) % 360)
            {
                case 90:
                    Cv2.Rotate(mat, mat, RotateFlags.Rotate90Clockwise);
                    break;
                case 180:
                    Cv2.Rotate(mat, mat, RotateFlags.Rotate180);
                    break;
                case 270:
                    Cv2.Rotate(mat, mat, RotateFlags.Rotate90Counterclockwise);
                    break;
            }
        }

        private void EnsureBuffer(int length)
        {
            if (buffer.Length != length)
                buffer = new Color32[length];
        }
    }
}
