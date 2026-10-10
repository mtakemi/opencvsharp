using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;

namespace OpenCvSharp.Unity.Editor
{
    /// <summary>
    /// Passes a link.xml to the Unity linker (managed code stripping).
    /// A link.xml placed inside a package is not picked up automatically, so it is generated here.
    /// </summary>
    internal sealed class OpenCvSharpLinkerProcessor : IUnityLinkerProcessor
    {
        // Mat.Cast<TMat>() creates Mat subclasses via Activator.CreateInstance(type, Mat),
        // which the linker cannot see; keep their constructors.
        private const string LinkXml =
            "<linker>\n" +
            "  <assembly fullname=\"OpenCvSharp\">\n" +
            "    <type fullname=\"OpenCvSharp.Mat*\" preserve=\"methods\" />\n" +
            "  </assembly>\n" +
            "</linker>\n";

        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            var path = Path.GetFullPath(Path.Combine("Library", "OpenCvSharp", "link.xml"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, LinkXml);
            return path;
        }
    }
}
