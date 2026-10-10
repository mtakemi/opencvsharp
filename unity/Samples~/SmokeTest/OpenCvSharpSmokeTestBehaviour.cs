using System.Linq;
using System.Text;
using UnityEngine;

namespace OpenCvSharp.Unity.Samples
{
    /// <summary>
    /// Attach to any GameObject in an empty scene. Runs <see cref="OpenCvSharpSmokeTests"/> and <see cref="TextureConverterSmokeTests"/> on Start,
    /// writes the results to the log (adb logcat -s Unity) and shows them on screen.
    /// </summary>
    public sealed class OpenCvSharpSmokeTestBehaviour : MonoBehaviour
    {
        private string report = "Running...";
        private GUIStyle style;

        private void Start()
        {
            var results = OpenCvSharpSmokeTests.RunAll();
            results.AddRange(TextureConverterSmokeTests.RunAll());

            var sb = new StringBuilder();
            var passed = results.Count(r => r.Passed);
            sb.AppendLine($"OpenCvSharp smoke test: {passed}/{results.Count} passed");
            sb.AppendLine($"{Application.platform}, {SystemInfo.operatingSystem}, {SystemInfo.processorType}");
            foreach (var r in results)
            {
                sb.AppendLine(r.ToString());
                if (r.Passed)
                    Debug.Log($"[OpenCvSharpSmokeTest] {r}");
                else
                    Debug.LogError($"[OpenCvSharpSmokeTest] {r}");
            }
            report = sb.ToString();
            Debug.Log($"[OpenCvSharpSmokeTest] {passed}/{results.Count} passed");
        }

        private void OnGUI()
        {
            style ??= new GUIStyle(GUI.skin.label) { fontSize = Screen.height / 45, wordWrap = true };
            // Fully qualified: inside namespace OpenCvSharp.*, "Rect" resolves to OpenCvSharp.Rect
            GUI.Label(new UnityEngine.Rect(20, 20, Screen.width - 40, Screen.height - 40), report, style);
        }
    }
}
