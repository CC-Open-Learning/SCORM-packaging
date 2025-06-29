﻿/***********************************************************************************************************************
 * Unity-SCORM Integration Kit
 * 
 * Unity-SCORM Integration Manager
 * 
 * Copyright (C) 2015, Richard Stals (http://stals.com.au)
 * This Version removes multi-threading which is not supported by WebGL Player
 * ==========================================
 * 
 * 
 * Derived from:
 * Unity-SCORM Integration Toolkit Version 1.0 Beta
 * ==========================================
 *
 * Copyright (C) 2011, by ADL (Advance Distributed Learning). (http://www.adlnet.gov)
 * http://www.adlnet.gov/UnityScormIntegration/
 *
 ***********************************************************************************************************************
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this file except in compliance with
 * the License. You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software distributed under the License is distributed on
 * an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the License for the
 * specific language governing permissions and limitations under the License.
 *
 **********************************************************************************************************************/

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using UnityEngine.Events;

namespace VARLab.SCORM
{
	/// <summary>
	/// The main interface between your Unity3D code and the SCORM API (via the ScormManager and the ScormAPIWrapper).
	/// </summary>
	public class ScormManager : MonoBehaviour
	{
		public enum Event
        {
			Initialized,
			Commit
        }

		[DllImport("__Internal")]
		private static extern void wgldebugPrint(string str);

		/// <summary>The reference to the ScormAPIWrapper object (the bridge between Unity's C# and scorm.js</summary>
		protected static ScormAPIWrapper scormAPIWrapper;

		/// <summary>The student record data returned from the LMS via SCORM</summary>
		protected static StudentRecord studentRecord;

		public static ScormManager Instance { get; private set; }

        public static bool Initialized { get; private set; }

        //public static event Action<Event> ScormMessageReceived;

		public static bool IsDeployedWebGL { get; private set; } = false;

		public bool InitializeOnStart = true;

        [SerializeField]
		protected UnityEvent<Event> m_ScormMessageReceived;

		public static UnityEvent<Event> ScormMessageReceived => Instance.m_ScormMessageReceived;

        private void Awake()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
			IsDeployedWebGL = true;
#endif
            if (!IsDeployedWebGL)
            {
				Debug.Log("ScormManager: Not in a deployed WebGL environment, deleting.");
                Destroy(this);
                return;
            }

			if (Instance != null)
            {
				Debug.LogWarning("ScormManager: Another instance of the ScormManager was attempted to be made! Deleting this duplicate.");
				Destroy(this);
				return;
            }

			Instance = this;
		}

		/// <summary>
		/// Begin the ScormManager. Wait for "Scorm_Initialize_Complete" after Start();
		/// </summary>
		/// <remarks>
		/// Triggered by the Unity Engine, this function begins reading in all the data from the LMS. Launches
		/// Initialize(), that will fire "Scorm_Initialize_Complete" when ready.
		/// </remarks> 
		void Start()
		{
			if (IsDeployedWebGL && InitializeOnStart)
			{
				Initialize();
			}
		}

		/// <summary>
		/// Stop the Scorm Manager. Will commit data.
		/// </summary>
		/// <remarks>
		/// Triggered by the Unity Engine, this will commit data back to the LMS. 
		/// DO NOT RELY on this - the shutdown of Unity may break the message loop and cause timeouts in the 
		/// serilization thread. Commit your data manually and wait for the "Scorm_Commit_Complete" message
		/// before shutting down the engine.
		/// </remarks> 
		void Stop()
		{
			Commit();
		}

		/// <summary>Read the student data in from the LMS/</summary>
		/// <remarks>
		/// Calls the javascript layer to read in all the data. 
		/// Will fire "Scorm_Initialize_Complete" when the studentRecord datamodel is ready to be manipulated
		/// </remarks> 
		public static void Initialize()
		{
			scormAPIWrapper = new ScormAPIWrapper(Instance.gameObject.name, "ScormValueCallback");
			scormAPIWrapper.Initialize();
			try
			{
				if (scormAPIWrapper.IsScorm2004)
				{
					//Load studentRecord data using SCORM 2004
					studentRecord = LoadStudentRecord();
				}
				else
				{
					//Load studentRecord data using SCORM 1.2
					throw new InvalidOperationException("SCORM 1.2 not currently supported");  //TODO: Not currently supported.
				}
			}
			catch (Exception e)
			{
				wgldebugPrint("***Initialize***" + e.Message + "<br/>" + e.StackTrace + "<br/>" + e.Source);
			}

			Initialized = true;
			ScormMessageReceived?.Invoke(Event.Initialized);
		}


		/// <summary>
		/// Used for the Javascript layer to communicate back to the Unity layer.
		/// </summary>
		/// <param name="value" direction="input">
		/// A string in the format "value|number" where the number is the identifier of the Set or Get operation
		/// issued by the ScormAPIWrapper bridge, and the value is the result of that API call
		/// </param>
		/// <remarks>
		/// The javascript layer uses the name of the ScormManager object and the name of this function to return
		/// the results of an operation on the javascript API to the ScormAPIWrapper bridge. The number in the input string
		/// is used by the ScormAPIWrapper bridge to figure out what API call this message answers. This complexity is due to the 
		/// asyncronous nature of the UNITY/Javascript interface.
		/// </remarks> 
		public void ScormValueCallback(string value)
		{
#if DEVELOPMENT_BUILD || UNITY_EDITOR
			Debug.Log($"Received ScormValueCallback with value '{value}'");
#endif

            try
			{
				scormAPIWrapper.SetCallbackValue(value);
			}
			catch (Exception e)
			{
				wgldebugPrint("***ScormValueCallback***" + e.Message + "<br/>" + e.StackTrace + "<br/>" + e.Source);
			}

		}

		/// <summary>
		/// Call the final Commit.  It is the responsibility of the users of this class to write data to the LMS as you go (i.e. call SetValue())
		/// </summary>
		/// <remarks>
		/// Calls Commit on scorm.js. will fire "Scorm_Commit_Complete"
		/// when operation is complete.
		/// </remarks> 
		public static void Commit()
		{
			try
			{
				scormAPIWrapper.Commit();
			}
			catch (Exception e)
			{
				wgldebugPrint("***CallFinalCommit***" + e.Message + "<br/>" + e.StackTrace + "<br/>" + e.Source);
			}

			ScormMessageReceived?.Invoke(Event.Commit);
		}


        /// <summary>
        /// Sets the value.
        /// </summary>
        /// <remarks>
        /// Calls SetValue on scorm.js.
        /// </remarks> 
        /// <param name="identifier">The dot notation identifier of the data model element to set.</param>
        /// <param name="value">The string of the value to set.</param>
        private static void SetValue(string identifier, string value)
		{
            if (!Initialized)
            {
                Debug.LogWarning("SCORM Manager is not initialized - SetValue cannot be called");
                return;
            }

            try
			{
				scormAPIWrapper.SetValue(identifier, value);
			}
			catch (Exception e)
			{
				wgldebugPrint("***ERROR***CallSetValue***" + e.Message + "<br/>" + e.StackTrace + "<br/>" + e.Source);
			}
		}

		/// <summary>Close the course. Be sure to call Commit first if you want to save your data.</summary>
		/// <remarks>Will cause the browser to close the window, and signal to the LMS that the course is over.
		/// Be sure to save your data to the LMS by calling Commit first, then waiting for Scorm_Commit_Complete.</remarks> 
		public static void Terminate()
		{
			scormAPIWrapper.Terminate();
		}

		/// <summary>Gets the completion status.</summary>
		/// <remarks>cmi.completion_status (“completed”, “incomplete”, “not attempted”, “unknown”, RW) Indicates whether the learner has completed the SCO</remarks>
		/// <returns>The completion status (StudentRecord.CompletionStatusType).</returns>
		public static StudentRecord.CompletionStatusType GetCompletionStatus()
		{
			return studentRecord.completionStatus;
		}

		/// <summary>
		/// Sets the completion status.
		/// </summary>
		/// <remarks>
		/// cmi.completion_status (“completed”, “incomplete”, “not attempted”, “unknown”, RW) Indicates whether the learner has completed the SCO
		/// </remarks>
		/// <param name="value">studentRecord.CompletionStatusType object.</param>
		public static void SetCompletionStatus(StudentRecord.CompletionStatusType value)
		{
            if (!Initialized)
            {
                Debug.LogWarning("SCORM Manager is not initialized - SetCompletionStatus cannot be called");
                return;
            }

            if (!IsDeployedWebGL)
            {
				Debug.Log($"SCORM: SetCompletionStatus called to set to {value}. Ignoring as we are not deployed WebGL.");
				return;
            }

			string identifier = "cmi.completion_status";
			string strValue = CustomTypeToString(value);
			studentRecord.completionStatus = value;
			SetValue(identifier, strValue);
		}

		/// <summary>
		/// Gets the version.
		/// </summary>
		/// <remarks>cmi._version (characterstring, RO) Represents the version of the data model</remarks>
		/// <returns>String representation of the SCORM API version.</returns>
		public static string GetVersion()
		{
			return studentRecord.version;
		}

		/// <summary>
		/// Gets the comments from learner.
		/// </summary>
		/// <remarks>The collection of comments made by the learner.</remarks>
		/// <returns>The comments from learner (List<StudentRecord.CommentsFromLearner>).</returns>
		public static List<StudentRecord.CommentsFromLearner> GetCommentsFromLearner()
		{
			return studentRecord.commentsFromLearner;
		}

		public static void AddCommentFromLearnerByFields(string commentText, string location)
		{
			StudentRecord.CommentsFromLearner comment = new StudentRecord.CommentsFromLearner();
			comment.timeStamp = DateTime.Now;
			comment.comment = commentText;
			comment.location = location;
			AddCommentFromLearner(comment);
		}

		/// <summary>
		/// Gets the comments from LMS.
		/// </summary>
		/// <remarks>The collection of comments made by the LMS.</remarks>
		/// <returns>The comments from LMS (List<StudentRecord.CommentsFromLMS>).</returns>
		public static List<StudentRecord.CommentsFromLMS> GetCommentsFromLMS()
		{
			return studentRecord.commentsFromLMS;
		}

		/// <summary>
		/// Adds the comment from learner.
		/// </summary>
		/// <param name="comment">StudentRecord.CommentsFromLearner object.</param>
		/// <c>
		/// Usage:\n
		/// StudentRecord.CommentsFromLearner comment = new StudentRecord.CommentsFromLearner ();\n
		/// comment.comment = "The comment";\n
		/// comment.location = "The location (bookmark) in the SCO";\n
		/// ScormManager.AddCommentFromLearner(comment);
		/// </c>
		public static void AddCommentFromLearner(StudentRecord.CommentsFromLearner comment)
		{
			try
			{
				string identifier;
				string strValue;

				comment.timeStamp = DateTime.Now;                                                       //Set timestamp to Now
																										//All other properties must be set by the caller

				//Set the Comment Values
				int i = studentRecord.commentsFromLearner.Count;

				studentRecord.commentsFromLearner.Add(comment);

				identifier = "cmi.comments_from_learner." + i + ".comment";
				strValue = comment.comment;
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.comments_from_learner." + i + ".location";
				strValue = comment.location;
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.comments_from_learner." + i + ".timestamp";
				strValue = String.Format("{0:s}", comment.timeStamp);
				scormAPIWrapper.SetValue(identifier, strValue);



			}
			catch (Exception e)
			{
				wgldebugPrint("***AddCommentFromLearner***" + e.Message + "<br/>" + e.StackTrace + "<br/>" + e.Source);
			}
		}

		/// <summary>
		/// Updates the comment from learner.
		/// </summary>
		/// <param name="index">Index of the comment to update.</param>
		/// <param name="comment">StudentRecord.CommentsFromLearner object.</param>
		/// <c>
		/// Usage:\n
		/// int index = 1;\n
		/// StudentRecord.CommentsFromLearner comment = new StudentRecord.CommentsFromLearner ();\n
		/// comment.comment = "The comment";\n
		/// comment.location = "The location (bookmark) in the SCO";\n
		/// ScormManager.UpdateCommentFromLearner(index, comment);
		/// </c>
		public static void UpdateCommentFromLearner(int index, StudentRecord.CommentsFromLearner comment)
		{
			try
			{
				string identifier;
				string strValue;

				comment.timeStamp = DateTime.Now;                                                       //Set timestamp to Now
																										//All other properties must be set by the caller

				//Set the Comment Values
				identifier = "cmi.comments_from_learner." + index + ".comment";
				strValue = comment.comment;
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.comments_from_learner." + index + ".location";
				strValue = comment.location;
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.comments_from_learner." + index + ".timestamp";
				strValue = String.Format("{0:s}", comment.timeStamp);
				scormAPIWrapper.SetValue(identifier, strValue);

				studentRecord.commentsFromLearner[index] = comment;
			}
			catch (Exception e)
			{
				wgldebugPrint("***UpdateCommentFromLearner***" + e.Message + "<br/>" + e.StackTrace + "<br/>" + e.Source);
			}
		}

		/// <summary>
		/// Gets the completion thershold.
		/// </summary>
		/// <remarks>cmi.completion_threshold (real(10,7) range (0..1), RO) Used to determine whether the SCO should be considered complete</remarks>
		/// <returns>The completion thershold.</returns>
		public static float GetCompletionThreshold()
		{
			return studentRecord.completionThreshold;
		}

		/// <summary>
		/// Gets the credit.
		/// </summary>
		/// <remarks>cmi.credit (“credit”, “no-credit”, RO) Indicates whether the learner will be credited for performance in the SCO</remarks>
		/// <returns>The StudentRecord.CreditType value.</returns>
		/// <c>
		/// Usage:\n
		/// StudentRecord.CreditType credit = ScormManager.GetCredit ();  or\n
		/// string credit = ScormManager.CustomTypeToString (ScormManager.GetCredit ());
		/// </c>
		public static StudentRecord.CreditType GetCredit()
		{
			return studentRecord.credit;
		}

		/// <summary>
		/// Gets the entry.
		/// </summary>
		/// <remarks>cmi.entry (ab_initio, resume, “”, RO) Asserts whether the learner has previously accessed the SCO</remarks>
		/// <returns>The StudentRecord.EntryType value.</returns>
		/// <c>
		/// Usage:\n
		/// StudentRecord.EntryType entry = ScormManager.GetEntry ();  or\n
		/// string entry = ScormManager.CustomTypeToString (ScormManager.GetEntry ());
		/// </c>
		public static StudentRecord.EntryType GetEntry()
		{
			return studentRecord.entry;
		}

		/// <summary>
		/// Sets the exit.
		/// </summary>
		/// <param name="value">The StudentRecord.ExitType value.</param>
		/// <c>
		/// Usage:\n
		/// ScormManager.SetExit(StudentRecord.ExitType.suspend);
		/// </c>
		public static void SetExit(StudentRecord.ExitType value)
		{
			string identifier = "cmi.exit";
			string strValue = CustomTypeToString(value);
			SetValue(identifier, strValue);
		}

		/// <summary>
		/// Gets the interactions.
		/// </summary>
		/// <returns>The interactions (StudentRecord.LearnerInteractionRecord).</returns>
		/// <c>
		/// Usage:\n
		/// List<StudentRecord.LearnerInteractionRecord> interactions = ScormManager.GetInteractions ();\n
		/// for (int i=0; i < interactions.Count; i++) {\n
		/// 	string id = interactions[i].id;\n
		/// 	string type = CustomTypeToString (interactions[i].type);\n
		/// 	string timeStamp = String.Format("{0:s}", interactions[i].timeStamp);\n
		/// 	string weighting = interactions[i].weighting.ToString();\n
		/// 	string learnerResponse = interactions[i].response;\n
		/// 	string result = CustomTypeToString (interactions[i].result);\n
		/// 	string estimate = interaction.estimate.ToString();\n
		/// 	string latency = secondsToTimeInterval(interactions[i].latency);\n
		/// 	string description = interactions[i].description;\n
		/// 	\n
		/// 	int objectivesCount = interactions[i].objectives.Count;\n
		/// 	if(objectivesCount != 0) {\n
		/// 		for (int x = 0; x < objectivesCount; i++) {\n
		/// 			string objectiveId = interactions[i].objectives[x];\n
		/// 			//Do something with the objective id here\n
		/// 		}\n
		/// 	}\n
		/// 	\n
		/// 	int correctResponsesCount = interactions[i].correctResponses.Count;\n
		/// 	if(correctResponsesCount != 0) {\n
		/// 		for (int x = 0; x < correctResponsesCount; i++) {\n
		/// 			string correctResponsePattern = interactions[i].correctResponses[x];\n
		/// 			//Do something with the correct response pattern here\n
		/// 		}\n
		/// 	}\n
		/// 	\n
		/// }\n
		/// </c>
		public static List<StudentRecord.LearnerInteractionRecord> GetInteractions()
		{
			return studentRecord.interactions;
		}

		/// <summary>
		/// Adds to the interactions.
		/// </summary>
		/// <param name="interaction">Interaction (StudentRecord.LearnerInteractionRecord).</param>
		/// <c>
		/// Usage:\n
		/// StudentRecord.LearnerInteractionRecord newRecord = new StudentRecord.LearnerInteractionRecord();\n
		/// newRecord.type = StudentRecord.InteractionType.other;\n
		/// newRecord.timeStamp = DateTime.Now;\n
		/// newRecord.weighting = 0.5f;\n
		/// newRecord.response = "true";\n
		/// newRecord.latency = 12f;\n
		/// newRecord.description = "Is this easy to use?";\n
		/// newRecord.result = StudentRecord.ResultType.correct;\n
		/// newRecord.estimate = 1f;	//Not used\n
		/// \n
		/// ScormManager.AddInteraction (newRecord);\n
		/// </c>
		public static void AddInteraction(StudentRecord.LearnerInteractionRecord interaction)
		{
			try
			{
				string identifier;
				string strValue;

				interaction.id = "urn:STALS:interaction-id-" + studentRecord.interactions.Count.ToString(); //Override ID to ensure it is unique

				//All other properties must be set by the caller

				//Set the interaction Values
				int i = studentRecord.interactions.Count;

				studentRecord.interactions.Add(interaction);

				identifier = "cmi.interactions." + i + ".id";
				strValue = interaction.id;
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.interactions." + i + ".type";
				strValue = CustomTypeToString(interaction.type);
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.interactions." + i + ".timestamp";
				strValue = String.Format("{0:s}", interaction.timeStamp);
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.interactions." + i + ".weighting";
				strValue = interaction.weighting.ToString();
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.interactions." + i + ".learner_response";
				strValue = interaction.response;
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.interactions." + i + ".result";
				strValue = CustomTypeToString(interaction.result);
				if (interaction.result == StudentRecord.ResultType.estimate)
				{
					strValue = interaction.estimate.ToString();
				}
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.interactions." + i + ".latency";
				strValue = secondsToTimeInterval(interaction.latency);
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.interactions." + i + ".description";
				strValue = interaction.description;
				scormAPIWrapper.SetValue(identifier, strValue);

				if (interaction.objectives != null)
				{
					int objectivesCount = interaction.objectives.Count;
					if (objectivesCount != 0)
					{
						for (int x = 0; x < objectivesCount; i++)
						{
							identifier = "cmi.interactions." + i + ".objectives." + x + ".id";
							strValue = interaction.objectives[x].id;
							scormAPIWrapper.SetValue(identifier, strValue);
						}
					}
				}

				if (interaction.correctResponses != null)
				{
					int correctResponsesCount = interaction.correctResponses.Count;
					if (correctResponsesCount != 0)
					{
						for (int x = 0; x < correctResponsesCount; i++)
						{
							identifier = "cmi.interactions." + i + ".correct_responses." + x + ".pattrern";
							strValue = interaction.correctResponses[x].pattern;
							scormAPIWrapper.SetValue(identifier, strValue);
						}
					}
				}




			}
			catch (System.Exception e)
			{
				wgldebugPrint("***AddInteraction***" + e.Message + "<br/>" + e.StackTrace + "<br/>" + e.Source);
			}
		}

		public static string GetNextInteractionId()
		{
			return "urn:STALS:interaction-id-" + studentRecord.interactions.Count.ToString();
		}

		/// <summary>
		/// Updates the interaction.
		/// </summary>
		/// <param name="index">Index.</param>
		/// <param name="interaction">Interaction (StudentRecord.LearnerInteractionRecord).</param>
		/// <c>
		/// Usage:\n
		/// int index = 0;
		/// StudentRecord.LearnerInteractionRecord newRecord = new StudentRecord.LearnerInteractionRecord();\n
		/// newRecord.type = StudentRecord.InteractionType.other;\n
		/// newRecord.timeStamp = DateTime.Now;\n
		/// newRecord.weighting = 0.5f;\n
		/// newRecord.response = "true";\n
		/// newRecord.latency = 12f;\n
		/// newRecord.description = "Is this easy to use?";\n
		/// newRecord.result = StudentRecord.ResultType.correct;\n
		/// newRecord.estimate = 1f;	//Not used\n
		/// \n
		/// ScormManager.UpdateInteraction (index, newRecord);\n
		/// </c>
		public static void UpdateInteraction(int index, StudentRecord.LearnerInteractionRecord interaction)
		{
			try
			{
				string identifier;
				string strValue;

				interaction.timeStamp = DateTime.Now;                                                       //Set timestamp to Now
																											//All other properties must be set by the caller

				//Set the interaction Values						
				identifier = "cmi.interactions." + index + ".type";
				strValue = CustomTypeToString(interaction.type);
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.interactions." + index + ".timestamp";
				strValue = String.Format("{0:s}", interaction.timeStamp);
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.interactions." + index + ".weighting";
				strValue = interaction.weighting.ToString();
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.interactions." + index + ".learner_response";
				strValue = interaction.response;
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.interactions." + index + ".result";
				strValue = CustomTypeToString(interaction.result);
				if (interaction.result == StudentRecord.ResultType.estimate)
				{
					strValue = interaction.estimate.ToString();
				}
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.interactions." + index + ".latency";
				strValue = secondsToTimeInterval(interaction.latency);
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.interactions." + index + ".description";
				strValue = interaction.description;
				scormAPIWrapper.SetValue(identifier, strValue);

				int objectivesCount = interaction.objectives.Count;
				if (objectivesCount != 0)
				{
					for (int x = 0; x < objectivesCount; x++)
					{
						identifier = "cmi.interactions." + index + ".objectives." + x + ".id";
						strValue = interaction.objectives[x].id;
						scormAPIWrapper.SetValue(identifier, strValue);
					}
				}

				int correctResponsesCount = interaction.correctResponses.Count;
				if (correctResponsesCount != 0)
				{
					for (int x = 0; x < correctResponsesCount; x++)
					{
						identifier = "cmi.interactions." + index + ".correct_responses." + x + ".pattrern";
						strValue = interaction.correctResponses[x].pattern;
						scormAPIWrapper.SetValue(identifier, strValue);
					}
				}

				studentRecord.interactions[index] = interaction;
			}
			catch (System.Exception e)
			{
				wgldebugPrint("***UpdateInteraction***" + e.Message + "<br/>" + e.StackTrace + "<br/>" + e.Source);
			}
		}

		/// <summary>
		/// Gets the launch data.
		/// </summary>
		/// <returns>The launch data string.</returns>
		public static string GetLaunchData()
		{
			return studentRecord.launchData;
		}

		/// <summary>
		/// Gets the learner identifier.
		/// </summary>
		/// <returns>The learner identifier string.</returns>
		public static string GetLearnerId()
		{
			return studentRecord.learnerID;
		}

		/// <summary>
		/// Gets the name of the learner.
		/// </summary>
		/// <returns>The learner name string.</returns>
		public static string GetLearnerName()
		{
			return studentRecord.learnerName;
		}

		/// <summary>
		/// Gets the learner preference.
		/// </summary>
		/// <remarks>
		/// Contains:
		/// 1. cmi.learner_preference.audio_level <see cref="SetLearnerPreferenceAudioLevel"/>
		/// 2. cmi.learner_preference.language <see cref="GetLearnerPreferenceLanguage"/>
		/// 3. cmi.learner_preference.delivery_speed <see cref="GetLearnerPreferenceDeliverySpeed"/>
		/// 4. cmi.learner_preference.audio_captioning <see cref="GetLearnerPreferenceAudioCaptioning"/>
		/// </remarks>
		/// <returns>The learner preference StudentRecord.LearnerPreference.</returns>
		/// <c>
		/// Usage:\n
		/// StudentRecord.LearnerPreference learnerPreference = ScormManager.GetLearnerPreference;\n
		/// int audioCaptioning = learnerPreference.audioCaptioning;\n
		/// </c>
		public static StudentRecord.LearnerPreference GetLearnerPreference()
		{
			return studentRecord.learnerPreference;
		}

		/// <summary>
		/// Sets the learner preference.
		/// </summary>
		/// <param name="learnerPreference">Learner preference.</param>
		/// <c>
		/// Usage:\n
		/// StudentRecord.LearnerPreference learnerPreference = new StudentRecord.LearnerPreference();\n
		/// learnerPreference.audioLevel = 1.1;\n
		/// learnerPreference.deliverySpeed = 1f;\n
		/// learnerPreference.audioCaptioning = 0;\n
		/// learnerPreference.langauge = "";\n
		/// ScormManager.SetLearnerPreference(learnerPreference);
		/// </c>
		public static void SetLearnerPreference(StudentRecord.LearnerPreference learnerPreference)
		{
			string identifier = "cmi.learner_preference.audio_level";
			string strValue = learnerPreference.audioLevel.ToString();
			SetValue(identifier, strValue);

			identifier = "cmi.learner_preference.language";
			strValue = learnerPreference.langauge;
			SetValue(identifier, strValue);

			identifier = "cmi.learner_preference.delivery_speed";
			strValue = learnerPreference.deliverySpeed.ToString();
			SetValue(identifier, strValue);

			identifier = "cmi.learner_preference.audio_captioning";
			strValue = learnerPreference.audioCaptioning.ToString();
			SetValue(identifier, strValue);

			studentRecord.learnerPreference = learnerPreference;
		}

		/// <summary>
		/// Gets the learner preference audio level.
		/// </summary>
		/// <remarks>cmi.learner_preference.audio_level (real(10,7), range (0..*), RW) Specifies an intended change in perceived audio level</remarks>
		/// <returns>The learner preference audio level float.</returns>
		public static float GetLearnerPreferenceAudioLevel()
		{
			return studentRecord.learnerPreference.audioLevel;
		}

		/// <summary>
		/// Sets the learner preference audio level.
		/// </summary>
		/// <param name="value">float Value.</param>
		public static void SetLearnerPreferenceAudioLevel(float value)
		{
			string identifier = "cmi.learner_preference.audio_level";
			string strValue = value.ToString();
			studentRecord.learnerPreference.audioLevel = value;
			SetValue(identifier, strValue);
		}

		/// <summary>
		/// Gets the learner preference language.
		/// </summary>
		/// <remarks>cmi.learner_preference.language (language_type (SPM 250), RW) The learner’s preferred language for SCOs with multilingual capability</remarks>
		/// <returns>The learner preference language string.</returns>
		public static string GetLearnerPreferenceLanguage()
		{
			return studentRecord.learnerPreference.langauge;
		}

		/// <summary>
		/// Sets the learner preference language.
		/// </summary>
		/// <param name="value">string Value.</param>
		public static void SetLearnerPreferenceLanguage(string value)
		{
			string identifier = "cmi.learner_preference.language";
			string strValue = value;
			studentRecord.learnerPreference.langauge = value;
			SetValue(identifier, strValue);
		}

		/// <summary>
		/// Gets the learner preference delivery speed.
		/// </summary>
		/// <remarks>cmi.learner_preference.delivery_speed (real(10,7), range (0..*), RW) The learner’s preferred relative speed of content delivery</remarks>
		/// <returns>The learner preference delivery speed float.</returns>
		public static float GetLearnerPreferenceDeliverySpeed()
		{
			return studentRecord.learnerPreference.deliverySpeed;
		}

		/// <summary>
		/// Sets the learner preference delivery speed.
		/// </summary>
		/// <param name="value">float Value.</param>
		public static void SetLearnerPreferenceDeliverySpeed(float value)
		{
			string identifier = "cmi.learner_preference.delivery_speed";
			string strValue = value.ToString();
			studentRecord.learnerPreference.deliverySpeed = value;
			SetValue(identifier, strValue);
		}

		/// <summary>
		/// Gets the learner preference audio captioning.
		/// </summary>
		/// <remarks>cmi.learner_preference.audio_captioning (“-1″, “0″, “1″, RW) Specifies whether captioning text corresponding to audio is displayed</remarks>
		/// <returns>The learner preference audio captioning integer.</returns>
		public static int GetLearnerPreferenceAudioCaptioning()
		{
			return studentRecord.learnerPreference.audioCaptioning;
		}

		/// <summary>
		/// Sets the learner preference audio captioning.
		/// </summary>
		/// <param name="value">integer Value.</param>
		public static void SetLearnerPreferenceAudioCaptioning(int value)
		{
			string identifier = "cmi.learner_preference.audio_captioning";
			string strValue = value.ToString();
			studentRecord.learnerPreference.audioCaptioning = value;
			SetValue(identifier, strValue);
		}

		/// <summary>
		/// Gets the location.
		/// </summary>
		/// <remarks>cmi.location (characterstring (SPM: 1000), RW) The learner’s current location in the SCO</remarks>
		/// <returns>The location string (bookmark).</returns>
		public static string GetLocation()
		{
			return studentRecord.location;
		}

		/// <summary>
		/// Sets the location.
		/// </summary>
		/// <param name="value">string location (bookmark) value.</param>
		public static void SetLocation(string value)
		{
			string identifier = "cmi.location";
			string strValue = value;
			studentRecord.location = value;
			SetValue(identifier, strValue);
		}

		public static List<StudentRecord.Objectives> GetObjectives()
		{
			return studentRecord.objectives;
		}

		/// <summary>
		/// Adds the objective.
		/// </summary>
		/// <remarks>
		/// Contains:
		/// 1. cmi.objectives.n.id (long_identifier_type (SPM: 4000), RW) Unique label for the objective
		/// 2. cmi.objectives.n.score._children (scaled,raw,min,max, RO) Listing of supported data model elements
		/// 3. cmi.objectives.n.score.scaled (real (10,7) range (-1..1), RW) Number that reflects the performance of the learner for the objective
		/// 4. cmi.objectives.n.score.raw (real (10,7), RW) Number that reflects the performance of the learner, for the objective, relative to the range bounded by the values of min and max
		/// 5. cmi.objectives.n.score.min (real (10,7), RW) Minimum value, for the objective, in the range for the raw score
		/// 6. cmi.objectives.n.score.max (real (10,7), RW) Maximum value, for the objective, in the range for the raw score
		/// 7. cmi.objectives.n.success_status (“passed”, “failed”, “unknown”, RW) Indicates whether the learner has mastered the objective
		/// 8. cmi.objectives.n.completion_status (“completed”, “incomplete”, “not attempted”, “unknown”, RW) Indicates whether the learner has completed the associated objective
		/// 9. cmi.objectives.n.progress_measure (real (10,7) range (0..1), RW) Measure of the progress the learner has made toward completing the objective
		/// 10. cmi.objectives.n.description (localized_string_type (SPM: 250), RW) Provides a brief informative description of the objective
		/// </remarks>
		/// <param name="objective">StudentRecord.Objectives objective.</param>
		/// <c>
		/// Usage:\n
		/// StudentRecord.Objectives newRecord = new StudentRecord.Objectives();\n
		/// StudentRecord.LearnerScore newScore = new StudentRecord.LearnerScore ();\n
		/// newScore.scaled = 0.8f;\n
		/// newScore.raw = 80f;\n
		/// newScore.max = 100f;\n
		/// newScore.min = 0f;\n
		/// newRecord.score = newScore;\n
		/// newRecord.successStatus = StudentRecord.SuccessStatusType.passed;\n
		/// newRecord.completionStatus = StudentRecord.CompletionStatusType.completed;\n
		/// newRecord.progressMeasure = 1f;\n
		/// newRecord.description = "The description of this objective";\n
		/// ScormManager.AddObjective(newRecord);
		/// </c>
		public static void AddObjective(StudentRecord.Objectives objective)
		{
			try
			{
				string identifier;
				string strValue;

				objective.id = "urn:STALS:objective-id-" + studentRecord.objectives.Count.ToString();   //Override ID to ensure it is uniqu												//Set timestamp to Now
																										//All other properties must be set by the caller

				//Set the Objective Values
				int i = studentRecord.objectives.Count;

				studentRecord.objectives.Add(objective);

				identifier = "cmi.objectives." + i + ".id";
				strValue = objective.id;
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.objectives." + i + ".score.scaled";
				strValue = objective.score.scaled.ToString();
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.objectives." + i + ".score.raw";
				strValue = objective.score.raw.ToString();
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.objectives." + i + ".score.max";
				strValue = objective.score.max.ToString();
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.objectives." + i + ".score.min";
				strValue = objective.score.min.ToString();
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.objectives." + i + ".success_status";
				strValue = CustomTypeToString(objective.successStatus);
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.objectives." + i + ".completion_status";
				strValue = CustomTypeToString(objective.completionStatus);
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.objectives." + i + ".progress_measure";
				strValue = objective.progressMeasure.ToString();
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.objectives." + i + ".description";
				strValue = objective.description;
				scormAPIWrapper.SetValue(identifier, strValue);



			}
			catch (System.Exception e)
			{
				wgldebugPrint("***AddObjective***" + e.Message + "<br/>" + e.StackTrace + "<br/>" + e.Source);
			}
		}

		/// <summary>
		/// Updates the objective.
		/// </summary>
		/// <param name="index">Integer Index.</param>
		/// <param name="objective">StudentRecord.Objectives objective.</param>
		/// <c>
		/// Usage:\n
		/// StudentRecord.Objectives newRecord = new StudentRecord.Objectives();\n
		/// StudentRecord.LearnerScore newScore = new StudentRecord.LearnerScore ();\n
		/// newScore.scaled = 0.8f;\n
		/// newScore.raw = 80f;\n
		/// newScore.max = 100f;\n
		/// newScore.min = 0f;\n
		/// newRecord.score = newScore;\n
		/// newRecord.successStatus = StudentRecord.SuccessStatusType.passed;\n
		/// newRecord.completionStatus = StudentRecord.CompletionStatusType.completed;\n
		/// newRecord.progressMeasure = 1f;\n
		/// newRecord.description = "The description of this objective";\n
		/// ScormManager.UpdateObjective(1, newRecord);
		/// </c>
		public static void UpdateObjective(int index, StudentRecord.Objectives objective)
		{
			try
			{
				string identifier;
				string strValue;

				//Set the Objective Values
				identifier = "cmi.objectives." + index + ".id";
				strValue = objective.id;
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.objectives." + index + ".score.scaled";
				strValue = objective.score.scaled.ToString();
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.objectives." + index + ".score.raw";
				strValue = objective.score.raw.ToString();
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.objectives." + index + ".score.max";
				strValue = objective.score.max.ToString();
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.objectives." + index + ".score.min";
				strValue = objective.score.min.ToString();
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.objectives." + index + ".success_status";
				strValue = CustomTypeToString(objective.successStatus);
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.objectives." + index + ".completion_status";
				strValue = CustomTypeToString(objective.completionStatus);
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.objectives." + index + ".progress_measure";
				strValue = objective.progressMeasure.ToString();
				scormAPIWrapper.SetValue(identifier, strValue);

				identifier = "cmi.objectives." + index + ".description";
				strValue = objective.description;
				scormAPIWrapper.SetValue(identifier, strValue);

				studentRecord.objectives[index] = objective;

			}
			catch (System.Exception e)
			{
				wgldebugPrint("***UpdateObjective***" + e.Message + "<br/>" + e.StackTrace + "<br/>" + e.Source);
			}
		}

		/// <summary>
		/// Gets the max time allowed.
		/// </summary>
		/// <remarks>cmi.max_time_allowed (timeinterval (second,10,2), RO) Amount of accumulated time the learner is allowed to use a SCO</remarks>
		/// <returns>The max time allowed in seconds.</returns>
		public static float GetMaxTimeAllowed()
		{
			return studentRecord.maxTimeAllowed;
		}

		/// <summary>
		/// Gets the mode.
		/// </summary>
		/// <remarks>cmi.mode (“browse”, “normal”, “review”, RO) Identifies one of three possible modes in which the SCO may be presented to the learner</remarks>
		/// <returns>The StudentRecord.ModeType mode.</returns>
		public static StudentRecord.ModeType GetMode()
		{
			return studentRecord.mode;
		}

		/// <summary>
		/// Gets the progress measure.
		/// </summary>
		/// <remarks>cmi.progress_measure (real (10,7) range (0..1), RW) Measure of the progress the learner has made toward completing the SCO</remarks>
		/// <returns>The progress measure float.</returns>
		public static float GetProgressMeasure()
		{
			return studentRecord.progressMeasure;
		}

		/// <summary>
		/// Sets the progress measure.
		/// </summary>
		/// <param name="value">float Value.</param>
		public static void SetProgressMeasure(float value)
		{
			string identifier = "cmi.progress_measure";
			string strValue = value.ToString();
			studentRecord.progressMeasure = value;
			SetValue(identifier, strValue);
		}

		/// <summary>
		/// Gets the scaled passing score.
		/// </summary>
		/// <remarks>cmi.scaled_passing_score (real(10,7) range (-1 .. 1), RO) Scaled passing score required to master the SCO</remarks>
		/// <returns>The scaled passing score float.</returns>
		public static float GetScaledPassingScore()
		{
			return studentRecord.scaledPassingScore;
		}

		/// <summary>
		/// Gets the score.
		/// </summary>
		/// <remarks>
		/// Contains:
		/// 1. cmi.score.scaled <see cref="GetScoreScaled"/>
		/// 2. cmi.score.raw <see cref="GetScoreRaw"/>
		/// 3. cmi.score.min <see cref="GetScoreMin"/>
		/// 4. cmi.score.max  <see cref="GetScoreMax"/>
		/// </remarks>
		/// <returns>The StudentRecord.LearnerScore score.</returns>
		public static StudentRecord.LearnerScore GetScore()
		{
			return studentRecord.learnerScore;
		}

		/// <summary>
		/// Sets the score.
		/// </summary>
		/// <param name="learnerScore">Learner score.</param>
		public static void SetScore(StudentRecord.LearnerScore learnerScore)
		{
			string identifier = "cmi.score.scaled";
			string strValue = learnerScore.scaled.ToString();
			SetValue(identifier, strValue);

			identifier = "cmi.score.raw";
			strValue = learnerScore.raw.ToString();
			SetValue(identifier, strValue);

			identifier = "cmi.score.max";
			strValue = learnerScore.max.ToString();
			SetValue(identifier, strValue);

			identifier = "cmi.score.min";
			strValue = learnerScore.min.ToString();
			SetValue(identifier, strValue);

			studentRecord.learnerScore = learnerScore;

		}

		/// <summary>
		/// Gets the score scaled.
		/// </summary>
		/// <remarks>cmi.score.scaled (real (10,7) range (-1..1), RW) Number that reflects the performance of the learner</remarks>
		/// <returns>The score scaled float.</returns>
		public static float GetScoreScaled()
		{
			return studentRecord.learnerScore.scaled;
		}

		/// <summary>
		/// Sets the score scaled.
		/// </summary>
		/// <param name="value">float Value.</param>
		public static void SetScoreScaled(float value)
		{
			string identifier = "cmi.score.scaled";
			string strValue = value.ToString();
			studentRecord.learnerScore.scaled = value;
			SetValue(identifier, strValue);
		}

		/// <summary>
		/// Gets the score raw.
		/// </summary>
		/// <remarks>cmi.score.raw (real (10,7), RW) Number that reflects the performance of the learner relative to the range bounded by the values of min and max</remarks>
		/// <returns>The score raw float.</returns>
		public static float GetScoreRaw()
		{
			return studentRecord.learnerScore.raw;
		}

		/// <summary>
		/// Sets the score raw.
		/// </summary>
		/// <param name="value">float Value.</param>
		public static void SetScoreRaw(float value)
		{
			string identifier = "cmi.score.raw";
			string strValue = value.ToString();
			studentRecord.learnerScore.raw = value;
			SetValue(identifier, strValue);
		}

		/// <summary>
		/// Gets the score max.
		/// </summary>
		/// <remarks>cmi.score.max (real (10,7), RW) Maximum value in the range for the raw score</remarks>
		/// <returns>The score max float.</returns>
		public static float GetScoreMax()
		{
			return studentRecord.learnerScore.max;
		}

		/// <summary>
		/// Sets the score max.
		/// </summary>
		/// <param name="value">floatValue.</param>
		public static void SetScoreMax(float value)
		{
			string identifier = "cmi.score.max";
			string strValue = value.ToString();
			studentRecord.learnerScore.max = value;
			SetValue(identifier, strValue);
		}

		/// <summary>
		/// Gets the score minimum.
		/// </summary>
		/// <remarks>cmi.score.min (real (10,7), RW) Minimum value in the range for the raw score</remarks>
		/// <returns>The score minimum float.</returns>
		public static float GetScoreMin()
		{
			return studentRecord.learnerScore.min;
		}

		/// <summary>
		/// Sets the score minimum.
		/// </summary>
		/// <param name="value">float Value.</param>
		public static void SetScoreMin(float value)
		{
			string identifier = "cmi.score.min";
			string strValue = value.ToString();
			studentRecord.learnerScore.min = value;
			SetValue(identifier, strValue);
		}

		/// <summary>
		/// Sets the session time.
		/// </summary>
		/// <remarks>cmi.session_time (timeinterval (second,10,2), WO) Amount of time that the learner has spent in the current learner session for this SCO</remarks>
		/// <param name="value">float session time in seconds.</param>
		public static void SetSessionTime(float value)
		{
			string identifier = "cmi.session_time";
			string strValue = secondsToTimeInterval(value);
			SetValue(identifier, strValue);
		}

		/// <summary>
		/// Gets the success status.
		/// </summary>
		/// <remarks>cmi.success_status (“passed”, “failed”, “unknown”, RW) Indicates whether the learner has mastered the SCO</remarks>
		/// <returns>The StudentRecord.SuccessStatusType success status.</returns>
		public static StudentRecord.SuccessStatusType GetSuccessStatus()
		{
			return studentRecord.successStatus;
		}

		/// <summary>
		/// Sets the success status.
		/// </summary>
		/// <param name="value">studentRecord.SuccessStatusType Value.</param>
		public static void SetSuccessStatus(StudentRecord.SuccessStatusType value)
		{
			if (!Initialized)
			{
				Debug.LogWarning("SCORM Manager is not initialized - SetSuccessStatus cannot be called");
				return;
			}

			string identifier = "cmi.success_status";
			string strValue = CustomTypeToString(value);
			studentRecord.successStatus = value;
			SetValue(identifier, strValue);
		}

		/// <summary>
		/// Gets the suspend data.
		/// </summary>
		/// <remarks>cmi.suspend_data (characterstring (SPM: 64000), RW) Provides space to store and retrieve data between learner sessions</remarks>
		/// <returns>The suspend data string.</returns>
		public static string GetSuspendData()
		{
			return studentRecord.suspendData;
		}

		/// <summary>
		/// Sets the suspend data.
		/// </summary>
		/// <param name="value">string Value.</param>
		public static void SetSuspendData(string value)
		{
            if (!Initialized)
            {
                Debug.LogWarning("SCORM Manager is not initialized - SetSuspendData cannot be called");
				return;
            }

            string identifier = "cmi.suspend_data";
			string strValue = value;
			studentRecord.suspendData = value;
			SetValue(identifier, strValue);
		}

		/// <summary>
		/// Gets the time limit action.
		/// </summary>
		/// <remarks>cmi.time_limit_action (“exit,message”, “continue,message”, “exit,no message”, “continue,no message”, RO) Indicates what the SCO should do when cmi.max_time_allowed is exceeded</remarks>
		/// <returns>The StudentRecord.TimeLimitActionType time limit action.</returns>
		public static StudentRecord.TimeLimitActionType GetTimeLimitAction()
		{
			return studentRecord.timeLimitAction;
		}

		/// <summary>
		/// Gets the total time.
		/// </summary>
		/// <remarks>cmi.total_time (timeinterval (second,10,2), RO) Sum of all of the learner’s session times accumulated in the current learner attempt</remarks>
		/// <returns>The total time in seconds.</returns>
		public static float GetTotalTime()
		{
			return studentRecord.totalTime;
		}

		/// <summary>
		/// Loads the student record.
		/// </summary>
		/// <remarks>This is called on initialise and loads the SCORM data into the studentRecord object. <see cref="Initialize_imp"/></remarks>
		/// <returns>The studentRecord student record object.</returns>
		private static StudentRecord LoadStudentRecord()
		{
			Debug.Log("Loading the active student record into memory...");
			studentRecord = new StudentRecord
			{
				version = scormAPIWrapper.GetValue("cmi._version")
			};

			//Comments From Learner
			int commentsFromLearnerCount = ParseInt(scormAPIWrapper.GetValue("cmi.comments_from_learner._count"));
			studentRecord.commentsFromLearner = new List<StudentRecord.CommentsFromLearner>();

			if (commentsFromLearnerCount != 0)
			{

				for (int i = 0; i < commentsFromLearnerCount; i++)
				{
					string comment = scormAPIWrapper.GetValue("cmi.comments_from_learner." + i + ".comment");
					string location = scormAPIWrapper.GetValue("cmi.comments_from_learner." + i + ".location");
					DateTime timestamp = DateTime.Parse(scormAPIWrapper.GetValue("cmi.comments_from_learner." + i + ".timestamp"));

					StudentRecord.CommentsFromLearner newRecord = new StudentRecord.CommentsFromLearner();
					newRecord.comment = comment;
					newRecord.location = location;
					newRecord.timeStamp = timestamp;

					studentRecord.commentsFromLearner.Add(newRecord);
				}
			}

			//Comments From LMS
			int commentsFromLMSCount = ParseInt(scormAPIWrapper.GetValue("cmi.comments_from_lms._count"));
			studentRecord.commentsFromLMS = new List<StudentRecord.CommentsFromLMS>();

			if (commentsFromLMSCount != 0)
			{

				for (int i = 0; i < commentsFromLMSCount; i++)
				{
					string comment = scormAPIWrapper.GetValue("cmi.comments_from_lms." + i + ".comment");
					string location = scormAPIWrapper.GetValue("cmi.comments_from_lms." + i + ".location");
					DateTime timeStamp = DateTime.Parse(scormAPIWrapper.GetValue("cmi.comments_from_lms." + i + ".timestamp"));

					StudentRecord.CommentsFromLMS newRecord = new StudentRecord.CommentsFromLMS();
					newRecord.comment = comment;
					newRecord.location = location;
					newRecord.timeStamp = timeStamp;

					studentRecord.commentsFromLMS.Add(newRecord);
				}
			}

			studentRecord.completionStatus = StringToCompletionStatusType(scormAPIWrapper.GetValue("cmi.completion_status"));
			studentRecord.completionThreshold = ParseFloat(scormAPIWrapper.GetValue("cmi.completion_threshold"));
			studentRecord.credit = StringToCreditType(scormAPIWrapper.GetValue("cmi.credit"));
			studentRecord.entry = StringToEntryType(scormAPIWrapper.GetValue("cmi.entry"));

			//Interactions
			int interactionCount = ParseInt(scormAPIWrapper.GetValue("cmi.interactions._count"));
			studentRecord.interactions = new List<StudentRecord.LearnerInteractionRecord>();

			if (interactionCount != 0)
			{

				for (int i = 0; i < interactionCount; i++)
				{
					string id = scormAPIWrapper.GetValue("cmi.interactions." + i + ".id");
					StudentRecord.InteractionType type = StringToInteractionType(scormAPIWrapper.GetValue("cmi.interactions." + i + ".type"));
					DateTime timestamp = DateTime.Parse(scormAPIWrapper.GetValue("cmi.interactions." + i + ".timestamp"));
					float weighting = ParseFloat(scormAPIWrapper.GetValue("cmi.interactions." + i + ".weighting"));
					string response = scormAPIWrapper.GetValue("cmi.interactions." + i + ".learner_response");
					float latency = timeIntervalToSeconds(scormAPIWrapper.GetValue("cmi.interactions." + i + ".latency"));
					string description = scormAPIWrapper.GetValue("cmi.interactions." + i + ".description");
					float estimate = 0;
					StudentRecord.ResultType result = StringToResultType(scormAPIWrapper.GetValue("cmi.interactions." + i + ".result"), out estimate);

					StudentRecord.LearnerInteractionRecord newRecord = new()
					{
						id = id,
						type = type,
						timeStamp = timestamp,
						weighting = weighting,
						response = response,
						latency = latency,
						description = description,
						result = result,
						estimate = estimate
					};

					int interactionObjectivesCount = ParseInt(scormAPIWrapper.GetValue("cmi.interactions." + i + ".objectives._count"));
					newRecord.objectives = new List<StudentRecord.LearnerInteractionObjective>();

					if (interactionObjectivesCount != 0)
					{
						for (int x = 0; x < interactionObjectivesCount; x++)
						{
							StudentRecord.LearnerInteractionObjective newObjective = new StudentRecord.LearnerInteractionObjective();
							newObjective.id = scormAPIWrapper.GetValue("cmi.interactions." + i + ".objectives." + x + ".id");
							newRecord.objectives.Add(newObjective);
						}
					}

					int correctResponsesCount = ParseInt(scormAPIWrapper.GetValue("cmi.interactions." + i + ".correct_responses._count"));
					newRecord.correctResponses = new List<StudentRecord.LearnerInteractionCorrectResponse>();

					if (correctResponsesCount != 0)
					{
						for (int x = 0; x < correctResponsesCount; x++)
						{
							StudentRecord.LearnerInteractionCorrectResponse newCorrectResponse = new StudentRecord.LearnerInteractionCorrectResponse();
							newCorrectResponse.pattern = scormAPIWrapper.GetValue("cmi.interactions." + i + ".correct_responses." + x + ".pattern");
							newRecord.correctResponses.Add(newCorrectResponse);
						}
					}

					studentRecord.interactions.Add(newRecord);
				}
			}

			studentRecord.launchData = scormAPIWrapper.GetValue("cmi.launch_data");
			studentRecord.learnerID = scormAPIWrapper.GetValue("cmi.learner_id");
			studentRecord.learnerName = scormAPIWrapper.GetValue("cmi.learner_name");
			
			//learner_preference
			studentRecord.learnerPreference = new()
            {
                audioLevel = ParseFloat(scormAPIWrapper.GetValue("cmi.learner_preference.audio_level")),
                langauge = scormAPIWrapper.GetValue("cmi.learner_preference.language"),
                deliverySpeed = ParseFloat(scormAPIWrapper.GetValue("cmi.learner_preference.delivery_speed")),
                audioCaptioning = ParseInt(scormAPIWrapper.GetValue("cmi.learner_preference.audio_captioning"))
            };

			studentRecord.location = scormAPIWrapper.GetValue("cmi.location");

			//Objectives
			int objectivesCount = ParseInt(scormAPIWrapper.GetValue("cmi.objectives._count"));
			studentRecord.objectives = new List<StudentRecord.Objectives>();

			if (objectivesCount != 0)
			{
				for (int i = 0; i < objectivesCount; i++)
				{
					string id = scormAPIWrapper.GetValue("cmi.objectives." + i + ".id");

					StudentRecord.LearnerScore objectivesScore = new()
                    {
                        scaled = ParseFloat(scormAPIWrapper.GetValue("cmi.objectives." + i + ".score.scaled")),
                        raw = ParseFloat(scormAPIWrapper.GetValue("cmi.objectives." + i + ".score.raw")),
                        max = ParseFloat(scormAPIWrapper.GetValue("cmi.objectives." + i + ".score.max")),
                        min = ParseFloat(scormAPIWrapper.GetValue("cmi.objectives." + i + ".score.min"))
                    };

					StudentRecord.SuccessStatusType successStatus = StringToSuccessStatusType(scormAPIWrapper.GetValue("cmi.objectives." + i + ".success_status"));
					StudentRecord.CompletionStatusType completionStatus = StringToCompletionStatusType(scormAPIWrapper.GetValue("cmi.objectives." + i + ".completion_status"));
					float progressMeasure = ParseFloat(scormAPIWrapper.GetValue("cmi.objectives." + i + ".progress_measure"));
					string description = scormAPIWrapper.GetValue("cmi.objectives." + i + ".description");

					StudentRecord.Objectives newRecord = new()
					{
						id = id,
						score = objectivesScore,
						successStatus = successStatus,
						completionStatus = completionStatus,
						progressMeasure = progressMeasure,
						description = description
					};

					studentRecord.objectives.Add(newRecord);
				}
			}

			studentRecord.maxTimeAllowed = timeIntervalToSeconds(scormAPIWrapper.GetValue("cmi.max_time_allowed"));
			studentRecord.mode = StringToModeType(scormAPIWrapper.GetValue("cmi.mode"));
			studentRecord.progressMeasure = ParseFloat(scormAPIWrapper.GetValue("cmi.progress_measure"));
			studentRecord.scaledPassingScore = ParseFloat(scormAPIWrapper.GetValue("cmi.scaled_passing_score"));

			//Score
			studentRecord.learnerScore = new StudentRecord.LearnerScore
			{
				scaled = ParseFloat(scormAPIWrapper.GetValue("cmi.score.scaled")),
				raw = ParseFloat(scormAPIWrapper.GetValue("cmi.score.raw")),
				max = ParseFloat(scormAPIWrapper.GetValue("cmi.score.max")),
				min = ParseFloat(scormAPIWrapper.GetValue("cmi.score.min"))
			};

			studentRecord.successStatus = StringToSuccessStatusType(scormAPIWrapper.GetValue("cmi.success_status"));
			studentRecord.suspendData = scormAPIWrapper.GetValue("cmi.suspend_data");
			studentRecord.timeLimitAction = StringToTimeLimitActionType(scormAPIWrapper.GetValue("cmi.time_limit_action"));
			studentRecord.totalTime = timeIntervalToSeconds(scormAPIWrapper.GetValue("cmi.total_time"));

            Debug.Log($"Student record for '{studentRecord.learnerName}' ({studentRecord.learnerID}) loaded");

            return studentRecord;
		}

		/// <summary>
		/// Parses the float.
		/// </summary>
		/// <remarks>float.Parse fails on an empty string, so we use this to return a 0 if an empty string is encountered.</remarks>
		/// <returns>The float.</returns>
		/// <param name="str">String.</param>
		private static float ParseFloat(string str)
		{
			float result = 0f;
			float.TryParse(str, out result);
			return result;
		}

		/// <summary>
		/// Parses the int.
		/// </summary>
		/// <remarks>int.Parse fails on an empty string, so we use this to return a 0 if an empty string is encountered.</remarks>
		/// <returns>The int.</returns>
		/// <param name="str">String.</param>
		private static int ParseInt(string str)
		{
			int result = 0;
			int.TryParse(str, out result);
			return result;
		}

		/// <summary>
		/// Custom type to string.  Basically changes "not_set" to an empty string.
		/// </summary>
		/// <returns>The string representation of the custom enum.</returns>
		/// <param name="value">Value.</param>
		public static string CustomTypeToString(object value)
		{
			string result = value.ToString();
			if (result == "not_set")
			{
				result = "";
			}
			else if (result == "not_attempted")
			{
				result = "not attempted";
			}
			return result;
		}

		/// <summary>
		/// Convert string to the StudentRecord ResultType.  Pass the estimate as an out parameter so that it can be set to a float value if needed.
		/// </summary>
		/// <returns>Result Type.</returns>
		/// <param name="str">String.</param>
		/// <param name="estimate">Estimate.</param>
		public static StudentRecord.ResultType StringToResultType(string str, out float estimate)
		{
			StudentRecord.ResultType result = StudentRecord.ResultType.not_set;

			estimate = 1;

			if (float.TryParse(str, out estimate))
			{
				result = StudentRecord.ResultType.estimate;
			}

			switch (str)
			{
				case "correct":
					result = StudentRecord.ResultType.correct;
					break;
				case "incorrect":
					result = StudentRecord.ResultType.incorrect;
					break;
				case "neutral":
					result = StudentRecord.ResultType.neutral;
					break;
				case "unanticipated":
					result = StudentRecord.ResultType.unanticipated;
					break;
			}

			return result;

		}

		/// <summary>
		/// Convert string to the StudentRecord InteractionType.
		/// </summary>
		/// <returns>The to interaction type.</returns>
		/// <param name="str">String.</param>
		public static StudentRecord.InteractionType StringToInteractionType(string str)
		{
			StudentRecord.InteractionType result = StudentRecord.InteractionType.not_set;
			switch (str)
			{
				case "true-false":
					result = StudentRecord.InteractionType.true_false;
					break;
				case "choice":
					result = StudentRecord.InteractionType.choice;
					break;
				case "fill-in":
					result = StudentRecord.InteractionType.fill_in;
					break;
				case "long-fill-in":
					result = StudentRecord.InteractionType.long_fill_in;
					break;
				case "matching":
					result = StudentRecord.InteractionType.matching;
					break;
				case "performance":
					result = StudentRecord.InteractionType.performance;
					break;
				case "sequencing":
					result = StudentRecord.InteractionType.sequencing;
					break;
				case "likert":
					result = StudentRecord.InteractionType.likert;
					break;
				case "numeric":
					result = StudentRecord.InteractionType.numeric;
					break;
			}
			return result;
		}

		/// <summary>
		/// Convert string to the StudentRecord EntryType.
		/// </summary>
		/// <returns>The to entry type.</returns>
		/// <param name="str">String.</param>
		private static StudentRecord.EntryType StringToEntryType(string str)
		{
			StudentRecord.EntryType result = StudentRecord.EntryType.not_set;
			switch (str)
			{
				case "ab-initio":
					result = StudentRecord.EntryType.start;
					break;
				case "resume":
					result = StudentRecord.EntryType.resume;
					break;
			}
			return result;
		}

		/// <summary>
		/// Convert string to the StudentRecord TimeLimitActionType.
		/// </summary>
		/// <returns>The to time limit action type.</returns>
		/// <param name="str">String.</param>
		private static StudentRecord.TimeLimitActionType StringToTimeLimitActionType(string str)
		{
			StudentRecord.TimeLimitActionType result = StudentRecord.TimeLimitActionType.not_set;
			switch (str)
			{
				case "continue,message":
					result = StudentRecord.TimeLimitActionType.continue_message;
					break;
				case "continue,no message":
					result = StudentRecord.TimeLimitActionType.continue_no_message;
					break;
				case "exit,message":
					result = StudentRecord.TimeLimitActionType.exit_message;
					break;
				case "exit,no message":
					result = StudentRecord.TimeLimitActionType.exit_no_message;
					break;
			}
			return result;
		}

		/// <summary>
		/// Convert string to the StudentRecord CompletionStatusType.
		/// </summary>
		/// <returns>The to completion status.</returns>
		/// <param name="str">String.</param>
		private static StudentRecord.CompletionStatusType StringToCompletionStatusType(string str)
		{
			StudentRecord.CompletionStatusType result = StudentRecord.CompletionStatusType.not_set;
			switch (str)
			{
				case "completed":
					result = StudentRecord.CompletionStatusType.completed;
					break;
				case "incomplete":
					result = StudentRecord.CompletionStatusType.incomplete;
					break;
				case "not attempted":
					result = StudentRecord.CompletionStatusType.not_attempted;
					break;
				case "unknown":
					result = StudentRecord.CompletionStatusType.unknown;
					break;
			}
			return result;
		}


		/// <summary>
		/// Convert string to the StudentRecord SuccessStatusType.
		/// </summary>
		/// <returns>The to success status type.</returns>
		/// <param name="str">String.</param>
		private static StudentRecord.SuccessStatusType StringToSuccessStatusType(string str)
		{
			StudentRecord.SuccessStatusType result = StudentRecord.SuccessStatusType.not_set;
			switch (str)
			{
				case "passed":
					result = StudentRecord.SuccessStatusType.passed;
					break;
				case "failed":
					result = StudentRecord.SuccessStatusType.failed;
					break;
				case "unknown":
					result = StudentRecord.SuccessStatusType.unknown;
					break;
			}
			return result;
		}

		/// <summary>
		/// Convert string to the StudentRecord CreditType.
		/// </summary>
		/// <returns>The to credit type.</returns>
		/// <param name="str">String.</param>
		private static StudentRecord.CreditType StringToCreditType(string str)
		{
			StudentRecord.CreditType result = StudentRecord.CreditType.not_set;
			switch (str)
			{
				case "credit":
					result = StudentRecord.CreditType.credit;
					break;
				case "no-credit":
					result = StudentRecord.CreditType.no_credit;
					break;
			}
			return result;
		}

		/// <summary>
		/// Convert string to the StudentRecord ModeType.
		/// </summary>
		/// <returns>The to mode type.</returns>
		/// <param name="str">String.</param>
		private static StudentRecord.ModeType StringToModeType(string str)
		{
			StudentRecord.ModeType result = StudentRecord.ModeType.not_set;
			switch (str)
			{
				case "browse":
					result = StudentRecord.ModeType.browse;
					break;
				case "normal":
					result = StudentRecord.ModeType.normal;
					break;
				case "review":
					result = StudentRecord.ModeType.review;
					break;
			}
			return result;
		}

		/// <summary>
		/// Convert Seconds to the SCORM timeInterval.
		/// </summary>
		/// <returns>timeInterval string.</returns>
		/// <param name="seconds">Seconds.</param>
		private static string secondsToTimeInterval(float seconds)
		{
			TimeSpan t = TimeSpan.FromSeconds(seconds);
			return string.Format("P{0:D}DT{1:D}H{2:D}M{3:F}S", t.Days, t.Hours, t.Minutes, t.Seconds); //This is good enough to feed into SCORM, no need to include Years and Months
		}

		/// <summary>
		/// Convert SCORM timeInterval to seconds.
		/// </summary>
		/// <returns>Seconds.</returns>
		/// <param name="timeInterval">SCORM TimeInterval.</param>
		private static float timeIntervalToSeconds(string timeInterval)
		{
			float totalSeconds = 0f;

			if (timeInterval != "")
			{

				long hundredthsPerYear = 3155760000;
				long hundredthsPerMonth = 262980000;
				long hundredthsPerDay = 8640000;
				long hundredthsPerHour = 360000;
				long hundredthsPerMinute = 6000;
				long hundredthsPerSecond = 100;

				Regex re = new Regex(@"P(([0-9]+)Y)?(([0-9]+)M)?(([0-9]+)D)?T?(([0-9]+)H)?(([0-9]+)M)?(([0-9]+)(\.[0-9]+)?S)?");
				Match m = re.Match(timeInterval);

				int years = int.Parse(m.Groups[2].Value == "" ? "0" : m.Groups[2].Value);
				int months = int.Parse(m.Groups[4].Value == "" ? "0" : m.Groups[4].Value);
				int days = int.Parse(m.Groups[6].Value == "" ? "0" : m.Groups[6].Value);
				int hours = int.Parse(m.Groups[8].Value == "" ? "0" : m.Groups[8].Value);
				int minutes = int.Parse(m.Groups[10].Value == "" ? "0" : m.Groups[10].Value);
				float seconds = float.Parse(m.Groups[12].Value == "" ? "0" : m.Groups[12].Value);
				float tenths = float.Parse(m.Groups[13].Value == "" ? "0" : "0" + m.Groups[13].Value);
				seconds = seconds + tenths;

				long totalHundredthsOfASecond = 0;      //work at the hundreths of a second level because that is all the precision that is required
				totalHundredthsOfASecond += years * hundredthsPerYear;
				totalHundredthsOfASecond += months * hundredthsPerMonth;
				totalHundredthsOfASecond += days * hundredthsPerDay;
				totalHundredthsOfASecond += hours * hundredthsPerHour;
				totalHundredthsOfASecond += minutes * hundredthsPerMinute;
				totalHundredthsOfASecond += Convert.ToInt64(seconds * hundredthsPerSecond);

				totalSeconds = Convert.ToSingle(totalHundredthsOfASecond) / 100;
			}

			return totalSeconds;
		}
	}
}