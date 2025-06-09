using UnityEditor;
using UnityEngine;
using System;

namespace VARLab.SCORM
{
    [Serializable]
    public class SCORMExportData
    {
        public string ManifestIdentifier;
        public string CourseTitle;
        public string CourseDescription;
        public string SCOTitle;
        public string DataFromLMS;
        public bool CompletedByProgressAmount;
        public float ProgressAmountForCompletion;
        public TimeLimitAction TimeLimitAction;
        public string TimeLimit;
    }

    public enum TimeLimitAction
    {
        None = 0,
        ExitWithMessage = 1,
        ExitNoMessage = 2,
        ContinueWithMessage = 3,
        ContinueNoMessage = 4
    }
}
