#if UNITY_EDITOR
using UnityEditor;

namespace VARLab.SCORM.Editor
{
    public static class ScormImportTest
    {

        public const string BuildTemplateAssetPath = "Assets/Package/Editor/WebGLTemplates~";

        [MenuItem("Tools/SCORM/Tests/Import WebGL Templates", false, 201)]
        public static void ImportWebGLTemplatesDefault()
        {
            ScormImport.ImportWebGLTemplates(ScormImport.TopLevelFolder,
                    ScormImport.TemplatesFolder, 
                    BuildTemplateAssetPath,
                    ScormImport.BuildTemplateDesiredPath,
                    ScormImport.DataFolders);
        }
    }
}
#endif