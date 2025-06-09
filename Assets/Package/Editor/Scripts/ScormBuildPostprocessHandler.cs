using UnityEngine;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor;
using System.IO;
using System;

namespace VARLab.SCORM.Editor
{
    public class ScormBuildPostprocessHandler : IPostprocessBuildWithReport
    {
        /// <value>
        ///     1000 : Low-priority callback order
        /// </value>
        public int callbackOrder { get { return 1000; } }

        public void OnPostprocessBuild(BuildReport report)
        {
            switch (report.summary.platform)
            {
                case BuildTarget.WebGL:
                    BuildForWebGL(report);
                    break;
                default:
                    Debug.Log($"Build platform is not {BuildTarget.WebGL}. " +
                        $"Ignoring {nameof(ScormBuildPostprocessHandler)}.{nameof(OnPostprocessBuild)}()");
                    return;
            }
        }

        /// <summary>
        /// Determines whether this build is a valid SCORM target, depending on user settings.
        /// </summary>
        /// <param name="options">The build options from the build report.</param>
        /// <returns>True if build if a valid SCORM target, false if not.</returns>
        private bool IsValidSCORMTarget(BuildOptions options)
        {
            bool developmentBuilds = Convert.ToBoolean(PlayerPrefs.GetInt(ScormProperties.DevelopmentBuilds));
            bool releaseBuilds = Convert.ToBoolean(PlayerPrefs.GetInt(ScormProperties.ReleaseBuilds));

            if (options.HasFlag(BuildOptions.Development) && !developmentBuilds) { return false; }
                
            if (!options.HasFlag(BuildOptions.Development) && !releaseBuilds) { return false; }
                
            // Avoiding building for SCORM if the build is for PlayMode tests.
            if (options.HasFlag(BuildOptions.IncludeTestAssemblies)) { return false; }

            return true;
        }

        /// <summary>
        /// Modifies a built WebGL player for SCORM if it is a valid SCORM target.
        /// </summary>
        /// <param name="report">The build report.</param>
        private void BuildForWebGL(BuildReport report)
        {
            if (!IsValidSCORMTarget(report.summary.options)) { return; }

            Debug.Log("Postprocess build step for generating a SCORM object");

            string exportPath = $"{report.summary.outputPath}/{PlayerPrefs.GetString(ScormProperties.ManifestIdentifier)}.zip";

            ScormExport.Publish(report.summary.outputPath, exportPath);
        }
    }
}