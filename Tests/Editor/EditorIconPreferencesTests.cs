using Essentials.EditorIcon.Editor;
using NUnit.Framework;
using UnityEditor;

namespace Essentials.Tests.Editor.EditorIcon
{
    public sealed class EditorIconPreferencesTests
    {
        bool hadAutoApplyPreference;
        bool originalAutoApplyPreference;

        [SetUp]
        public void SetUp()
        {
            hadAutoApplyPreference = EditorPrefs.HasKey(EditorIconPreferences.AutoApplyPlayerIconKey);
            originalAutoApplyPreference = EditorPrefs.GetBool(
                EditorIconPreferences.AutoApplyPlayerIconKey,
                EditorIconPreferences.AutoApplyPlayerIconDefaultValue);
        }

        [TearDown]
        public void TearDown()
        {
            if (hadAutoApplyPreference)
            {
                EditorIconApplier.AutoApplyEnabled = originalAutoApplyPreference;
            }
            else
            {
                EditorPrefs.DeleteKey(EditorIconPreferences.AutoApplyPlayerIconKey);
            }
        }

        [Test]
        public void SettingsProvider_IsUserScopedUnderEssentials()
        {
            SettingsProvider provider = EditorIconPreferencesProvider.CreateProvider();

            Assert.That(provider, Is.Not.Null);
            Assert.That(provider.settingsPath, Is.EqualTo("Preferences/Essentials/Editor Icon"));
            Assert.That(provider.scope, Is.EqualTo(SettingsScope.User));
        }

        [Test]
        public void AutoApplyPlayerIcon_UsesExistingDefault_WhenKeyIsMissing()
        {
            EditorPrefs.DeleteKey(EditorIconPreferences.AutoApplyPlayerIconKey);

            Assert.That(EditorIconApplier.AutoApplyEnabled, Is.True);
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
    }
}
