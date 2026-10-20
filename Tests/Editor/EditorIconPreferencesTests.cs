using ProjectIcon.Editor;
using NUnit.Framework;
using UnityEditor;

namespace ProjectIcon.Editor.Tests
{
    public sealed class EditorIconPreferencesTests
    {
        bool hadAutoApplyPreference;
        bool originalAutoApplyPreference;
        bool hadApplicationIconModePreference;
        int originalApplicationIconModePreference;

        [SetUp]
        public void SetUp()
        {
            hadAutoApplyPreference = EditorPrefs.HasKey(EditorIconPreferences.AutoApplyPlayerIconKey);
            originalAutoApplyPreference = EditorPrefs.GetBool(
                EditorIconPreferences.AutoApplyPlayerIconKey,
                EditorIconPreferences.AutoApplyPlayerIconDefaultValue);
            hadApplicationIconModePreference = EditorPrefs.HasKey(EditorIconPreferences.ApplicationIconModeKey);
            originalApplicationIconModePreference = EditorPrefs.GetInt(
                EditorIconPreferences.ApplicationIconModeKey,
                (int)EditorIconPreferences.ApplicationIconModeDefaultValue);
        }

        [TearDown]
        public void TearDown()
        {
            if (hadAutoApplyPreference)
                EditorPrefs.SetBool(EditorIconPreferences.AutoApplyPlayerIconKey, originalAutoApplyPreference);
            else
                EditorPrefs.DeleteKey(EditorIconPreferences.AutoApplyPlayerIconKey);

            if (hadApplicationIconModePreference)
                EditorPrefs.SetInt(
                    EditorIconPreferences.ApplicationIconModeKey,
                    originalApplicationIconModePreference);
            else
                EditorPrefs.DeleteKey(EditorIconPreferences.ApplicationIconModeKey);
        }

        [Test]
        public void SettingsProvider_IsUserScopedUnderProjectIcon()
        {
            SettingsProvider provider = EditorIconPreferencesProvider.CreateProvider();

            Assert.That(provider, Is.Not.Null);
            Assert.That(provider.settingsPath, Is.EqualTo("Preferences/Project Icon"));
            Assert.That(provider.label, Is.EqualTo("Project Icon"));
            Assert.That(provider.scope, Is.EqualTo(SettingsScope.User));
        }

        [Test]
        public void Preferences_UseAutoApplyAndBadgeDefaults_WhenKeysAreMissing()
        {
            EditorPrefs.DeleteKey(EditorIconPreferences.AutoApplyPlayerIconKey);
            EditorPrefs.DeleteKey(EditorIconPreferences.ApplicationIconModeKey);

            Assert.That(EditorIconApplier.AutoApplyEnabled, Is.True);
            Assert.That(EditorIconApplier.ApplicationIconMode, Is.EqualTo(EditorIconMode.Badge));
        }

        [Test]
        public void AutoApplyPlayerIcon_PersistsInEditorPrefs()
        {
            EditorIconApplier.AutoApplyEnabled = false;
            Assert.That(
                EditorPrefs.GetBool(EditorIconPreferences.AutoApplyPlayerIconKey, true),
                Is.False);

            EditorIconApplier.AutoApplyEnabled = true;
            Assert.That(
                EditorPrefs.GetBool(EditorIconPreferences.AutoApplyPlayerIconKey, false),
                Is.True);
        }

        [Test]
        public void ApplicationIconMode_PersistsEverySupportedModeInEditorPrefs()
        {
            EditorIconApplier.AutoApplyEnabled = false;

            EditorIconApplier.ApplicationIconMode = EditorIconMode.IconReplacement;
            Assert.That(
                EditorPrefs.GetInt(EditorIconPreferences.ApplicationIconModeKey, -1),
                Is.EqualTo((int)EditorIconMode.IconReplacement));

            EditorIconApplier.ApplicationIconMode = EditorIconMode.Badge;
            Assert.That(
                EditorPrefs.GetInt(EditorIconPreferences.ApplicationIconModeKey, -1),
                Is.EqualTo((int)EditorIconMode.Badge));
        }

        [Test]
        public void ApplicationIconMode_FallsBackToBadge_WhenStoredValueIsInvalid()
        {
            EditorPrefs.SetInt(EditorIconPreferences.ApplicationIconModeKey, 99);

            Assert.That(EditorIconApplier.ApplicationIconMode, Is.EqualTo(EditorIconMode.Badge));
        }
    }
}
