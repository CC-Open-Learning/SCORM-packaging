using System.IO;
using UnityEditor;
using UnityEngine;

namespace VARLab.SCORM.Editor
{
    public static class ScormImport
    {

        // Directory names for importing WebGLTemplate build settings for SCORM
        public const string TopLevelFolder = "Assets";
        public const string TemplatesFolder = "WebGLTemplates";
        public static readonly string[] DataFolders = { "SCORM Fixed", "SCORM Resize" };

        // Expected file path to the SCORM WebGL Template data when deployed as a UPM package
        public const string BuildTemplateAssetPath = "Packages/com.varlab.scorm/Editor/WebGLTemplates~";
        public const string BuildTemplateDesiredPath = TopLevelFolder + "/" + TemplatesFolder;

        [MenuItem("Tools/SCORM/Import WebGL Templates", false, 1)]
        public static void ImportWebGLTemplatesDefault()
        {
            ImportWebGLTemplates(TopLevelFolder, TemplatesFolder, BuildTemplateAssetPath, BuildTemplateDesiredPath, DataFolders);
        }

        /// <summary>The menu item to allow the initialisation of a scene to allow integration with SCORM</summary>
        [MenuItem("Tools/SCORM/Create SCORM Manager", false, 2)]
        public static void CreateManager()
        {
            var manager = Object.FindObjectOfType<ScormManager>();

            if (manager)
            {
                EditorUtility.DisplayDialog("SCORM Manager is already present", 
                    "You only need one SCORM Manager game object in your simulation. " +
                    "Remember to place objects that need messages from the ScormManager " +
                    "under it in the scene heirarchy.", 
                    "OK");
                return;
            }

            new GameObject("ScormManager").AddComponent<ScormManager>();
            EditorUtility.DisplayDialog("The SCORM Manager has been added to the scene", 
                "Subscribe to the 'ScormMessageReceived' in order to respond to the " +
                "ScormManager establishing communication with the LMS.", 
                "OK");
        }

        public static void ImportWebGLTemplates(string TopLevelFolder, string TemplatesFolder, string BuildTemplateAssetPath, string BuildTemplateDesiredPath, string[] DataFolders)
        {
            string guid = AssetDatabase.IsValidFolder($"{TopLevelFolder}/{TemplatesFolder}")
                ? AssetDatabase.AssetPathToGUID($"{TopLevelFolder}/{TemplatesFolder}")
                : AssetDatabase.CreateFolder(TopLevelFolder, TemplatesFolder);

            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError($"Unable to create '{TemplatesFolder}' folder");
                return;
            }

            foreach (var folder in DataFolders)
            {
                try
                {
                    if (AssetDatabase.IsValidFolder($"{BuildTemplateDesiredPath}/{folder}")
                        && !EditorUtility.DisplayDialog("Folder already exists",
                                $"Template '{folder}' already exists. Overwrite?",
                                "Yes", "No"))
                    {
                        // User has selected not to overwrite existing template
                        return;
                    }

                    Debug.Log($"Copying template <b>'{folder}'</b> into WebGL Templates folder");

                    AssetDatabase.DeleteAsset($"{BuildTemplateDesiredPath}/{folder}");
                    FileUtil.CopyFileOrDirectory($"{BuildTemplateAssetPath}/{folder}",
                        $"{BuildTemplateDesiredPath}/{folder}");
                    Debug.Log($"Successfully imported WebGL Template for SCORM to <b>'{BuildTemplateDesiredPath}/{folder}'</b>");

                }
                catch (IOException ex)
                {
                    Debug.LogError(ex.Message);
                }
            }

            AssetDatabase.Refresh();
        }
    }
}