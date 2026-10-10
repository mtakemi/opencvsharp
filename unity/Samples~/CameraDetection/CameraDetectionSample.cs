using System.Collections;
using System.IO;
using System.Linq;
using OpenCvSharp.Aruco;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace OpenCvSharp.Unity.Samples
{
    /// <summary>
    /// Detects ArUco markers (with distance via SolvePnP) and QR codes in the camera image
    /// and shows the annotated frame in a uGUI RawImage.
    /// Attach to any GameObject in an empty scene; the UI is created at runtime if not assigned.
    /// To get printable markers, use the component's context menu "Save ArUco markers as PNG" in the Editor.
    /// </summary>
    public sealed class CameraDetectionSample : MonoBehaviour
    {
        [SerializeField] private RawImage view;
        // Fully qualified: inside namespace OpenCvSharp.*, "Text" resolves to the OpenCvSharp.Text namespace
        [SerializeField] private UnityEngine.UI.Text info;
        [SerializeField] private PredefinedDictionaryType dictionaryType = PredefinedDictionaryType.Dict4X4_50;
        [Tooltip("Side length of the printed/displayed marker in meters (black border included)")]
        [SerializeField] private float markerLength = 0.05f;
        [SerializeField] private bool detectQr = true;
        [Tooltip("Run QR detection every N frames (it is slower than ArUco detection)")]
        [SerializeField] private int qrInterval = 5;
        [SerializeField] private bool useFrontCamera;
        [SerializeField] private int requestedWidth = 1280;
        [SerializeField] private int requestedHeight = 720;

        private WebCamTexture webCam;
        private readonly TextureConverter converter = new TextureConverter();
        private Mat frame;
        private Mat gray;
        private Texture2D output;
        private Dictionary dictionary;
        private ArucoDetector arucoDetector;
        private QRCodeDetector qrDetector;
        private AspectRatioFitter fitter;

        private int frameCount;
        private string qrText = "";
        private Point2f[] qrPoints = new Point2f[0];
        private float fps;
        private float processMs;
        private string status = "Starting camera...";

        private IEnumerator Start()
        {
            if (view == null)
                CreateUi();
            fitter = view.GetComponent<AspectRatioFitter>();

#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                bool? granted = null;
                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += _ => granted = true;
                callbacks.PermissionDenied += _ => granted = false;
                callbacks.PermissionDeniedAndDontAskAgain += _ => granted = false;
                Permission.RequestUserPermission(Permission.Camera, callbacks);
                yield return new WaitUntil(() => granted.HasValue);
                if (granted != true)
                {
                    SetInfo("Camera permission denied");
                    yield break;
                }
            }
#endif
            var devices = WebCamTexture.devices;
            if (devices.Length == 0)
            {
                SetInfo("No camera found");
                yield break;
            }
            var device = devices.FirstOrDefault(d => d.isFrontFacing == useFrontCamera);
            if (string.IsNullOrEmpty(device.name))
                device = devices[0];

            frame = new Mat();
            gray = new Mat();
            dictionary = CvAruco.GetPredefinedDictionary(dictionaryType);
            arucoDetector = new ArucoDetector(dictionary);
            qrDetector = new QRCodeDetector();

            webCam = new WebCamTexture(device.name, requestedWidth, requestedHeight, 30);
            webCam.Play();
            status = $"Camera: {device.name}";
        }

        private void Update()
        {
            // The texture is 16x16 until the first frame arrives
            if (webCam == null || !webCam.isPlaying || !webCam.didUpdateThisFrame || webCam.width <= 16)
                return;

            var start = Time.realtimeSinceStartup;
            converter.ToMat(webCam, frame);
            Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);

            var markerText = DetectArucoMarkers();
            if (detectQr)
                DetectQrCode();

            output = converter.ToTexture(frame, output);
            view.texture = output;
            fitter.aspectRatio = (float)frame.Width / frame.Height;

            processMs = (Time.realtimeSinceStartup - start) * 1000;
            fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 1e-3f), 0.1f);
            SetInfo($"{status}\n{frame.Width}x{frame.Height} rot={webCam.videoRotationAngle} mirrored={webCam.videoVerticallyMirrored}\n" +
                    $"{fps:F1} fps, processing {processMs:F1} ms\n{markerText}\nQR: {qrText}");
            frameCount++;
        }

        private string DetectArucoMarkers()
        {
            arucoDetector.DetectMarkers(gray, out var corners, out var ids, out _);
            if (ids.Length == 0)
                return "ArUco: -";

            CvAruco.DrawDetectedMarkers(frame, corners, ids);

            // Approximate intrinsics (no calibration): focal length ~ image width, principal point at the center.
            // Distances are therefore only rough estimates; calibrate the camera for accurate values.
            double f = Mathf.Max(frame.Width, frame.Height);
            var cameraMatrix = new double[,] { { f, 0, frame.Width / 2.0 }, { 0, f, frame.Height / 2.0 }, { 0, 0, 1 } };
            var half = markerLength / 2;
            var objectPoints = new[]
            {
                new Point3f(-half, half, 0), new Point3f(half, half, 0),
                new Point3f(half, -half, 0), new Point3f(-half, -half, 0),
            };

            var lines = new System.Text.StringBuilder("ArUco:");
            using var cameraMatrixMat = Mat.FromPixelData(3, 3, MatType.CV_64FC1, cameraMatrix);
            using var distCoeffs = new Mat();
            for (var i = 0; i < ids.Length; i++)
            {
                var rvec = new double[3];
                var tvec = new double[3];
                Cv2.SolvePnP(objectPoints, corners[i], cameraMatrix, null, ref rvec, ref tvec, flags: SolvePnPMethod.IPPE_SQUARE);

                using var rvecMat = Mat.FromPixelData(3, 1, MatType.CV_64FC1, rvec);
                using var tvecMat = Mat.FromPixelData(3, 1, MatType.CV_64FC1, tvec);
                Cv2.DrawFrameAxes(frame, cameraMatrixMat, distCoeffs, rvecMat, tvecMat, markerLength * 0.5f);

                var distance = Mathf.Sqrt((float)(tvec[0] * tvec[0] + tvec[1] * tvec[1] + tvec[2] * tvec[2]));
                var p = corners[i][0];
                Cv2.PutText(frame, $"id={ids[i]} {distance * 100:F0}cm", new Point(p.X, p.Y - 10),
                    HersheyFonts.HersheySimplex, 0.8, new Scalar(0, 255, 255), 2);
                lines.Append($" id={ids[i]} {distance * 100:F1}cm");
            }
            return lines.ToString();
        }

        private void DetectQrCode()
        {
            if (frameCount % Mathf.Max(qrInterval, 1) == 0)
            {
                var text = qrDetector.DetectAndDecode(gray, out var points);
                qrPoints = points ?? new Point2f[0];
                if (!string.IsNullOrEmpty(text))
                    qrText = text;
            }
            if (qrPoints.Length == 4)
            {
                var polygon = qrPoints.Select(p => new Point(p.X, p.Y));
                Cv2.Polylines(frame, new[] { polygon }, true, new Scalar(0, 255, 0), 3);
            }
        }

        private void SetInfo(string text)
        {
            if (info != null)
                info.text = text;
        }

        private void CreateUi()
        {
            var canvasObject = new GameObject("CameraDetectionCanvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var viewObject = new GameObject("CameraView", typeof(RawImage), typeof(AspectRatioFitter));
            viewObject.transform.SetParent(canvasObject.transform, false);
            Stretch(viewObject.GetComponent<RectTransform>());
            viewObject.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            view = viewObject.GetComponent<RawImage>();

            var infoObject = new GameObject("Info", typeof(UnityEngine.UI.Text));
            infoObject.transform.SetParent(canvasObject.transform, false);
            var infoRect = infoObject.GetComponent<RectTransform>();
            Stretch(infoRect);
            infoRect.offsetMin = new Vector2(20, 20);
            infoRect.offsetMax = new Vector2(-20, -20);
            info = infoObject.GetComponent<UnityEngine.UI.Text>();
            info.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            info.fontSize = Mathf.Max(Screen.height / 40, 14);
            info.color = Color.yellow;
            info.alignment = TextAnchor.UpperLeft;
            info.text = status;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        [ContextMenu("Save ArUco markers as PNG")]
        private void SaveMarkers()
        {
            // Saved next to the Assets folder in the Editor (persistentDataPath on devices)
            var dir = Application.isEditor ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", "ArucoMarkers")) : Application.persistentDataPath;
            Directory.CreateDirectory(dir);
            using var dict = CvAruco.GetPredefinedDictionary(dictionaryType);
            for (var id = 0; id < 4; id++)
            {
                using var marker = new Mat();
                dict.GenerateImageMarker(id, 400, marker);
                // White quiet zone around the marker, needed for detection
                using var withMargin = new Mat();
                Cv2.CopyMakeBorder(marker, withMargin, 50, 50, 50, 50, BorderTypes.Constant, Scalar.White);
                File.WriteAllBytes(Path.Combine(dir, $"aruco_{dictionaryType}_id{id}.png"), withMargin.ImEncode(".png"));
            }
            Debug.Log($"Saved ArUco markers (id 0-3) to {dir}");
        }

        private void OnDestroy()
        {
            if (webCam != null)
            {
                webCam.Stop();
                Destroy(webCam);
            }
            if (output != null)
                Destroy(output);
            frame?.Dispose();
            gray?.Dispose();
            arucoDetector?.Dispose();
            dictionary?.Dispose();
            qrDetector?.Dispose();
        }
    }
}
