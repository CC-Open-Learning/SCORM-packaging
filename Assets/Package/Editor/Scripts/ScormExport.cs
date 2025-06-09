using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VARLab.SCORM.Editor
{
    /// <summary>This class handles the editor window for the Unity-SCORM Integration Kit</summary>
    public class ScormExport : EditorWindow
    {
        private const string InternalPluginContentsPath = "Assets/Package/Plugins/2004";

        // Relative path for plugin when deployed as a Unity package
        public const string PluginContentsPath = "Packages/com.varlab.scorm/Plugins/2004";

        public const string WarningDialogPrefKey = "SCORMWarningDialogPrefKey";

        bool foldout1, foldout2;

        [MenuItem("Tools/SCORM/Export SCORM Package", false, 4)]
        static void PromptWindow()
        {
            int val = 0;
            if (!EditorPrefs.GetBool(WarningDialogPrefKey)) {
                val = EditorUtility.DisplayDialogComplex("Export this scene as a WebPlayer first",

                    "Because this software is developed for Unity Basic, we cannot automatically build the web player. " +
                    "Please export your simulation to the web player first. Remember to select the SCORM Integration web player template.",

                    "OK",
                    "Cancel",
                    "Don't Show Again");
            }

            switch (val)
            {
                case 1:
                    return;
                case 2:
                    EditorPrefs.SetBool(WarningDialogPrefKey, true);
                    break;
            }

            ShowWindow();
        }


        static void ShowWindow()
        {
            var window = GetWindow<ScormExport>("SCORM Export Tool");
            window.Show();
            window.foldout1 = window.foldout2 = true;
        }
        

        /// <summary>The menu item to display a short about message</summary>
        [MenuItem("Tools/SCORM/About SCORM Integration", false, 100)]
        static void About()
        {
            EditorUtility.DisplayDialog("Unity-SCORM Integration Kit", "This software enables the integration between web deployed Unity3D applications and a Learning Managment System (LMS) using the Sharable Content Object Reference Model (SCORM) developed at the US Department of Defence Advance Distributed Learning (ADL) Inititive. This software is provided 'as-is' and is available free of charge at http://www.adlnet.gov. This software may be used under the provisions of the Apache 2.0 license. This project is derived from the Unity-SCORM Integration Toolkit Version 1.0 Beta project from the ADL (Advance Distributed Learning) [http://www.adlnet.gov]. Source code is available from unity3d.stals.com.au/scorm-integration-kit  ", "OK");
        }

        /// <summary>The menu item to open a browser window to the support page for this package</summary>
        [MenuItem("Tools/SCORM/Help", false, 101)]
        static void Help()
        {
            Application.OpenURL("http://unity3d.stals.com.au/scorm-integration-kit");
        }


        /// <summary>Copies files from one directory to another.</summary>
        /// <param name="source">Source directory to copy from.</param>
        /// <param name="target">Target director to copy to.</param>
        public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            foreach (DirectoryInfo dir in source.GetDirectories())
            {
                CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
            }

            foreach (FileInfo file in source.GetFiles())
            {
                if (!IsFileHiddenOrMetadata(file))
                {
                    file.CopyTo(Path.Combine(target.FullName, file.Name));
                }
            }
        }

        /// <summary>Checks if a file is hidden or metadata.</summary>
        /// <returns><see langword="true"/> if the file is hidden or metadata, <see langword="false"/> otherwise.</returns>
        /// <param name="file">File to check.</param>
        private static bool IsFileHiddenOrMetadata(FileInfo file)
        {
            return file.Name.Substring(0, 1) == "." || file.Name.Substring(file.Name.Length - 4) == "meta";
        }

        /// <summary>Convert Seconds to the SCORM timeInterval.</summary>
        /// <returns>timeInterval string.</returns>
        /// <param name="seconds">Seconds.</param>
        private static string SecondsToTimeInterval(float seconds)
        {
            TimeSpan t = TimeSpan.FromSeconds(seconds);
            return string.Format("P{0:D}DT{1:D}H{2:D}M{3:F}S", t.Days, t.Hours, t.Minutes, t.Seconds); 
            //This is good enough to feed into SCORM, no need to include Years and Months
        }

        /// <summary>Parses a string into a float.</summary>
        /// <remarks>float.Parse fails on an empty string, so we use this to return a 0 if an empty string is encountered.</remarks>
        /// <returns>The float represented by the input <paramref name="str"/>.</returns>
        /// <param name="str">String representation of the float.</param>
        private static float ParseFloat(string str)
        {
            float result = 0f;
            float.TryParse(str, out result);
            return result;
        }

        public static void Publish(string exportDirectory, string exportFilePath)
        {
            SCORMExportData data = new()
            {
                ManifestIdentifier = PlayerPrefs.GetString("Manifest_Identifier"),
                CourseTitle = PlayerPrefs.GetString("Course_Title"),
                CourseDescription = PlayerPrefs.GetString("Course_Description"),
                SCOTitle = PlayerPrefs.GetString("SCO_Title"),
                DataFromLMS = PlayerPrefs.GetString("Data_From_Lms"),
                CompletedByProgressAmount = Convert.ToBoolean(PlayerPrefs.GetInt("completedByMeasure")),
                ProgressAmountForCompletion = PlayerPrefs.GetFloat("minProgressMeasure"),
                TimeLimitAction = (TimeLimitAction)PlayerPrefs.GetInt("Time_Limit_Action"),
                TimeLimit = PlayerPrefs.GetString("Time_Limit_Secs")
            };

            Publish(exportDirectory, exportFilePath, data);
        }

        /// <summary>Publish this SCORM package to a conformant zip file.</summary>
        public static void Publish(string exportDirectory, string exportFilePath, SCORMExportData data)
        {
            var timeLimitAction = "";
            switch (data.TimeLimitAction)
            {
                case TimeLimitAction.None:
                    timeLimitAction = "";
                    break;
                case TimeLimitAction.ExitWithMessage:
                    timeLimitAction = "exit,message";
                    break;
                case TimeLimitAction.ExitNoMessage:
                    timeLimitAction = "exit,no message";
                    break;
                case TimeLimitAction.ContinueWithMessage:
                    timeLimitAction = "continue,message";
                    break;
                case TimeLimitAction.ContinueNoMessage:
                    timeLimitAction = "continue,no message";
                    break;
            }

            var timeLimit = SecondsToTimeInterval(ParseFloat(data.TimeLimit));

            string tempdir = Path.GetTempPath() + Path.GetRandomFileName();
            _ = Directory.CreateDirectory(tempdir);
            CopyFilesRecursively(new DirectoryInfo(exportDirectory), new DirectoryInfo(tempdir));

            if (string.IsNullOrEmpty(exportFilePath)) { return; }

            if (File.Exists(exportFilePath))
                File.Delete(exportFilePath);

            var manifest = GetManifest(timeLimitAction, timeLimit, data);

            var zip = new Ionic.Zip.ZipFile(exportFilePath);
            var pluginContents = AssetDatabase.IsValidFolder(InternalPluginContentsPath) ? InternalPluginContentsPath : PluginContentsPath;
            zip.AddDirectory(tempdir);
            zip.AddItem(pluginContents);  
            zip.AddEntry("imsmanifest.xml", ".", System.Text.Encoding.ASCII.GetBytes(manifest));
            zip.Save();

            EditorUtility.DisplayDialog("SCORM Package Published", "The SCORM Package has been published to " + exportFilePath, "OK");
        }

        /// <summary>Display the Export SCORM dialog.</summary>
        void OnGUI()
        {
            EditorStyles.miniLabel.wordWrap = true;
            EditorStyles.foldout.fontStyle = FontStyle.Bold;

            GUILayout.BeginHorizontal();
            foldout1 = EditorGUILayout.Foldout(foldout1, "Exported Player Location", EditorStyles.foldout);

            bool help1 = GUILayout.Button(new GUIContent("Help", "Help for the Exported Player Location section"), EditorStyles.miniBoldLabel);
            if (help1)
                EditorUtility.DisplayDialog("Help", "You must export this simulation as a webplayer, then tell this packaging tool the location of that exported webplayer folder. Be sure to select the SCORM webplayer template, or the necessary JavaScript components will not be included, and the system will fail to connect to the LMS.", "OK");

            GUILayout.EndHorizontal();

            EditorGUILayout.Space();

            if (foldout1)
            {
                GUILayout.BeginVertical("TextArea");
                GUILayout.Label("Choose the location of the folder where the Webplayer was exported.", EditorStyles.miniLabel);
                PlayerPrefs.SetString("Course_Export", EditorGUILayout.TextField("Folder Location", PlayerPrefs.GetString("Course_Export")));
                PlayerPrefs.SetString("Course_Export_Name", EditorGUILayout.TextField("Application Name", PlayerPrefs.GetString("Course_Export_Name")));

                GUI.skin.button.fontSize = 8;
                GUILayout.BeginHorizontal();
                GUILayout.Space(position.width - 85);
                bool ChooseDir = GUILayout.Button(new GUIContent("Choose Folder", "Select the folder containing the webplayer"), GUILayout.ExpandWidth(false));
                GUILayout.EndHorizontal();
                if (ChooseDir)
                {
                    string export_dir = EditorUtility.OpenFolderPanel("Choose WebPlayer", PlayerPrefs.GetString("Course_Export"), "WebPlayer");
                    if (export_dir != "")
                    {
                        PlayerPrefs.SetString("Course_Export", export_dir);
                        PlayerPrefs.SetString("Course_Export_Name", export_dir.Substring(export_dir.LastIndexOf('/') + 1, (export_dir.Length - (export_dir.LastIndexOf('/') + 1))));
                    }
                }

                GUILayout.EndVertical();
            }


            // Foldout 2 - Set the SCORM properties (manhy of the options available in the imsmanifest.xml file)
            GUILayout.BeginHorizontal();
            foldout2 = EditorGUILayout.Foldout(foldout2, "SCORM Properties", EditorStyles.foldout);

            bool help2 = GUILayout.Button(new GUIContent("Help", "Help for the SCORM Properties section"), EditorStyles.miniBoldLabel);
            if (help2)
                EditorUtility.DisplayDialog("Help", "The properties will control how the LMS controls and displays your SCORM content. These values will be written into the imsmanifest.xml file within the exported zip package. There are many other settings that can be specified in the manifest - for more information read the Content Aggregation Model documents at http://www.adlnet.gov/capabilities/scorm", "OK");

            GUILayout.EndHorizontal();

            EditorGUILayout.Space();


            if (foldout2)
            {
                ScormProperties.ScormPropertiesForm();
            }

            // Publish Button
            GUI.skin.button.fontSize = 12;
            bool publish = GUILayout.Button(new GUIContent("Publish", "Export this course to a SCORM package."));
            if (publish)
                Publish(PlayerPrefs.GetString("Course_Export"), EditorUtility.SaveFilePanel("Choose Output File", PlayerPrefs.GetString("Course_Export"), PlayerPrefs.GetString("Course_Title"), "zip"));
        }

        
        private static string GetManifest(string timeLimitAction, string timeLimit, SCORMExportData data)
        {
            return "<?xml version=\"1.0\" standalone=\"no\" ?>\n" +
                "<manifest identifier=\"" + data.ManifestIdentifier + "\" version=\"1\"\n" +
                "\t\txmlns = \"http://www.imsglobal.org/xsd/imscp_v1p1\"\n" +
                "\t\txmlns:adlcp = \"http://www.adlnet.org/xsd/adlcp_v1p3\"\n" +
                "\t\txmlns:adlseq = \"http://www.adlnet.org/xsd/adlseq_v1p3\"\n" +
                "\t\txmlns:adlnav = \"http://www.adlnet.org/xsd/adlnav_v1p3\"\n" +
                "\t\txmlns:imsss = \"http://www.imsglobal.org/xsd/imsss\"\n" +
                "\t\txmlns:xsi = \"http://www.w3.org/2001/XMLSchema-instance\"\n" +
                "\t\txmlns:lom=\"http://ltsc.ieee.org/xsd/LOM\"\n" +
                "\t\txsi:schemaLocation = \"http://www.imsglobal.org/xsd/imscp_v1p1 imscp_v1p1.xsd\n" +
                "\t\t\thttp://www.adlnet.org/xsd/adlcp_v1p3 adlcp_v1p3.xsd\n" +
                "\t\t\thttp://www.adlnet.org/xsd/adlseq_v1p3 adlseq_v1p3.xsd\n" +
                "\t\t\thttp://www.adlnet.org/xsd/adlnav_v1p3 adlnav_v1p3.xsd\n" +
                "\t\t\thttp://www.imsglobal.org/xsd/imsss imsss_v1p0.xsd\n" +
                "\t\t\thttp://ltsc.ieee.org/xsd/LOM lom.xsd\" >\n" +
                "<metadata>\n" +
                "\t<schema>ADL SCORM</schema>\n" +
                "\t<schemaversion>2004 4th Edition</schemaversion>\n" +
                "<lom:lom>\n" +
                "\t<lom:general>\n" +
                "\t\t<lom:description>\n" +
                "\t\t\t<lom:string language=\"en-US\">" + data.CourseDescription + "</lom:string>\n" +
                "\t\t</lom:description>\n" +
                "\t</lom:general>\n" +
                "\t</lom:lom>\n" +
                "</metadata>\n" +
                "<organizations default=\"B0\">\n" +
                "\t<organization identifier=\"B0\" adlseq:objectivesGlobalToSystem=\"false\">\n" +
                "\t\t<title>" + data.CourseTitle + "</title>\n" +
                "\t\t<item identifier=\"i1\" identifierref=\"r1\" isvisible=\"true\">\n" +
                "\t\t\t<title>" + data.SCOTitle + "</title>\n" +
                "\t\t\t<adlcp:timeLimitAction>" + timeLimitAction + "</adlcp:timeLimitAction>\n" +
                "\t\t\t<adlcp:dataFromLMS>" + data.DataFromLMS + "</adlcp:dataFromLMS> \n" +
                "\t\t\t<adlcp:completionThreshold completedByMeasure = \"" + data.CompletedByProgressAmount.ToString().ToLower() + "\" minProgressMeasure= \"" + data.ProgressAmountForCompletion + "\" />\n" +
                "\t\t\t<imsss:sequencing>\n" +
                "\t\t\t<imsss:limitConditions attemptAbsoluteDurationLimit=\"" + timeLimit + "\"/>\n" +
                "\t\t\t</imsss:sequencing>\n" +
                "\t\t</item>\n" +
                "\t</organization>\n" +
                "</organizations>\n" +
                "<resources>\n" +
                "\t<resource identifier=\"r1\" type=\"webcontent\" adlcp:scormType=\"sco\" href=\"index.html\">\n" +
                "\t\t<file href=\"index.html\" />\n" +
                "\t\t<file href=\"TemplateData/scorm.js\" />\n" +
                "\t</resource>\n" +
                "</resources>\n" +
                "</manifest>";
        }
    }
}