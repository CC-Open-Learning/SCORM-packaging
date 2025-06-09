using System;
using UnityEditor;
using UnityEngine;


namespace VARLab.SCORM.Editor
{
    public class ScormProperties : EditorWindow
    {

        public static readonly string ManifestIdentifier = "Manifest_Identifier";
        public static readonly string CourseTitle = "Course_Title";
        public static readonly string DevelopmentBuilds = "Development_Builds";
        public static readonly string ReleaseBuilds = "Release_Builds";


        [MenuItem("Tools/SCORM/Edit SCORM Properties", false, 3)]
        static void ScormPropertiesWindow()
        {
            GetWindow<ScormProperties>("SCORM Properties").Show();
        }

        protected void OnGUI()
        {
            ScormPropertiesForm();
        }

        public static void ScormPropertiesForm()
        {
            GUILayout.BeginVertical("TextArea");
            GUILayout.Label("Information about your SCORM package including the title and various configuration values.", EditorStyles.miniLabel);

            PlayerPrefs.SetString(ManifestIdentifier, 
                EditorGUILayout.TextField(new GUIContent("Identifier:", "The unique IMS Manifest Identifier (e.g. au.com.stals.myapp)"), 
                PlayerPrefs.GetString(ManifestIdentifier)));

            PlayerPrefs.SetString(CourseTitle, 
                EditorGUILayout.TextField(new GUIContent("Title:", "The title of the SCORM content, as you want it to be displayed in the learning management system (LMS)"), 
                PlayerPrefs.GetString(CourseTitle)));

            PlayerPrefs.SetString("Course_Description", 
                EditorGUILayout.TextField(new GUIContent("Description:", "Description of the SCORM content."), 
                PlayerPrefs.GetString("Course_Description")));
            
            PlayerPrefs.SetString("SCO_Title", 
                EditorGUILayout.TextField(new GUIContent("Module Title:", "The title of the Unity content.  Note, this title may show as the first item in an LMS-provided table of contents."), 
                PlayerPrefs.GetString("SCO_Title")));

            PlayerPrefs.SetString("Data_From_Lms", 
                EditorGUILayout.TextField(new GUIContent("Launch Data:", "User-defined string value that can be used as initial learning experience state data."), 
                PlayerPrefs.GetString("Data_From_Lms")));

            bool progress = GUILayout.Toggle(Convert.ToBoolean(PlayerPrefs.GetInt("completedByMeasure")), new GUIContent("Completed By Measure", "If true, then this activity's completion status will be determined by the progress measure's relation to the minimum progress measure. This derived completion status will override what it explicitly set."));
            PlayerPrefs.SetInt("completedByMeasure", Convert.ToInt16(progress));
            if (progress)
            {
                GUILayout.Label(new GUIContent("Minimum Progress Measure: " + PlayerPrefs.GetFloat("minProgressMeasure").ToString(), "Defines a minimum completion percentage for this activity for use in conjunction with completed by measure."), EditorStyles.miniLabel);
                PlayerPrefs.SetFloat("minProgressMeasure", (float)Math.Round(GUILayout.HorizontalSlider(PlayerPrefs.GetFloat("minProgressMeasure"), 0.0f, 1.0f) * 100.0f) / 100.0f);
                EditorGUILayout.Space(12f);
            }

            GUILayout.Label("If set, this indicates that this activity’s completion status will be determined soley by the relation of the progress measure to Minimum Progress Measure.", EditorStyles.miniLabel);


            GUILayout.Label("Select the Time Limit Action to be passed to the SCO", EditorStyles.largeLabel);
            PlayerPrefs.SetInt("Time_Limit_Action", EditorGUILayout.Popup(PlayerPrefs.GetInt("Time_Limit_Action"), new string[] { "Not Set", "exit,message", "exit,no message", "continue,message", "continue,no message" }, GUILayout.ExpandWidth(false)));
            PlayerPrefs.SetString("Time_Limit_Secs", EditorGUILayout.TextField(new GUIContent("Time Limit (secs):", "The time limit for this SCO in seconds."), PlayerPrefs.GetString("Time_Limit_Secs")));

            GUILayout.Label("Select which build types will automatically build for SCORM:", EditorStyles.largeLabel);
            bool developmentBuilds = GUILayout.Toggle(Convert.ToBoolean(PlayerPrefs.GetInt("Development_Builds")), new GUIContent("Development"));
            bool releaseBuilds = GUILayout.Toggle(Convert.ToBoolean(PlayerPrefs.GetInt("Release_Builds")), new GUIContent("Release"));
            PlayerPrefs.SetInt("Development_Builds", Convert.ToInt16(developmentBuilds));
            PlayerPrefs.SetInt("Release_Builds", Convert.ToInt16(releaseBuilds));

            GUILayout.EndVertical();
        }
    }
}