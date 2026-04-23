using System.IO;
using UnityEditor;
using UnityEngine;

namespace IyanKim.UVMaskTool.Editor
{
    internal static class UVExporter
    {
        public static bool ExportPng(Texture2D texture, string defaultFileName)
        {
            if (texture == null)
            {
                EditorUtility.DisplayDialog("UV Island Mask Generator", "No texture was generated.", "OK");
                return false;
            }

            var path = EditorUtility.SaveFilePanel(
                "Export UV Mask",
                Application.dataPath,
                string.IsNullOrEmpty(defaultFileName) ? "UV_Island_Mask.png" : defaultFileName,
                "png");

            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            var bytes = texture.EncodeToPNG();
            File.WriteAllBytes(path, bytes);

            if (path.Replace('\\', '/').StartsWith(Application.dataPath.Replace('\\', '/')))
            {
                AssetDatabase.Refresh();
            }

            EditorUtility.RevealInFinder(path);
            return true;
        }
    }
}
