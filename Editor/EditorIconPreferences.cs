using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Essentials.EditorIcon.Editor
{
    internal static class EditorIconPreferences
    {
        internal const string AutoApplyPlayerIconKey = "Essentials.EditorIcon.AutoApplyPlayerIcon";
        internal const bool AutoApplyPlayerIconDefaultValue = true;

        internal static bool AutoApplyPlayerIcon
        {
            get => EditorPrefs.GetBool(AutoApplyPlayerIconKey, AutoApplyPlayerIconDefaultValue);
            set => EditorPrefs.SetBool(AutoApplyPlayerIconKey, value);
        }
    }

    internal static class EditorIconPreferencesProvider
    {
        internal const string SettingsPath = "Preferences/Essentials/Editor Icon";

        static readonly GUIContent AutoApplyPlayerIconContent = new GUIContent(
            "Auto Apply Player Icon Badge",
            "On macOS, automatically badges the Unity Editor application icon with the active build target's Player icon.");

        [SettingsProvider]
        internal static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.User)
            {
                label = "Editor Icon",
                guiHandler = DrawGui,
                keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "essentials",
                    "editor",
                    "project",
                    "icon",
                    "player",
                    "badge",
                    "macos",
                    "auto apply"
                }
            };
        }

        static void DrawGui(string _)
        {
            EditorGUI.BeginChangeCheck();
            bool autoApplyPlayerIcon = EditorGUILayout.ToggleLeft(
                AutoApplyPlayerIconContent,
                EditorIconApplier.AutoApplyEnabled);
            if (EditorGUI.EndChangeCheck())
                EditorIconApplier.AutoApplyEnabled = autoApplyPlayerIcon;

            EditorGUILayout.HelpBox(
                "Editor icon badging is applied to the current Unity Editor process on macOS. It does not modify the Unity installation, project icon assets, or Player builds.",
                MessageType.Info);
        }
    }
}
