using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Essentials.EditorIcon.Editor
{
    internal enum EditorIconMode
    {
        Badge = 0,
        IconReplacement = 1
    }

    internal static class EditorIconPreferences
    {
        internal const string AutoApplyPlayerIconKey = "Essentials.EditorIcon.AutoApplyPlayerIcon";
        internal const bool AutoApplyPlayerIconDefaultValue = true;
        internal const string ApplicationIconModeKey = "Essentials.EditorIcon.ApplicationIconMode";
        internal const EditorIconMode ApplicationIconModeDefaultValue = EditorIconMode.Badge;

        internal static bool AutoApplyPlayerIcon
        {
            get => EditorPrefs.GetBool(AutoApplyPlayerIconKey, AutoApplyPlayerIconDefaultValue);
            set => EditorPrefs.SetBool(AutoApplyPlayerIconKey, value);
        }

        internal static EditorIconMode ApplicationIconMode
        {
            get
            {
                int value = EditorPrefs.GetInt(ApplicationIconModeKey, (int)ApplicationIconModeDefaultValue);
                return value == (int)EditorIconMode.IconReplacement
                    ? EditorIconMode.IconReplacement
                    : EditorIconMode.Badge;
            }
            set => EditorPrefs.SetInt(
                ApplicationIconModeKey,
                value == EditorIconMode.IconReplacement
                    ? (int)EditorIconMode.IconReplacement
                    : (int)EditorIconMode.Badge);
        }
    }

    internal static class EditorIconPreferencesProvider
    {
        internal const string SettingsPath = "Preferences/Essentials/Project Icon";

        static readonly GUIContent AutoApplyPlayerIconContent = new GUIContent(
            "Auto Apply Player Icon",
            "On supported platforms, automatically applies the active build target's Player icon to the running Unity Editor.");

        static readonly GUIContent ApplicationIconModeContent = new GUIContent(
            "Appearance",
            "Choose whether the Player icon appears as a badge over Unity's icon or replaces the Unity icon.");

        [SettingsProvider]
        internal static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.User)
            {
                label = "Project Icon",
                guiHandler = DrawGui,
                keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "essentials",
                    "editor",
                    "project",
                    "icon",
                    "player",
                    "badge",
                    "replacement",
                    "appearance",
                    "macos",
                    "windows",
                    "linux",
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

            using (new EditorGUI.DisabledScope(!autoApplyPlayerIcon))
            {
                EditorGUI.BeginChangeCheck();
                var applicationIconMode = (EditorIconMode)EditorGUILayout.EnumPopup(
                    ApplicationIconModeContent,
                    EditorIconApplier.ApplicationIconMode);
                if (EditorGUI.EndChangeCheck())
                    EditorIconApplier.ApplicationIconMode = applicationIconMode;
            }

            EditorGUILayout.HelpBox(
                EditorIconNative.PlatformSupportMessage,
                EditorIconNative.IsSupported ? MessageType.Info : MessageType.Warning);
        }
    }
}
