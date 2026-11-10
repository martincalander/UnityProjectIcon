using System.Collections;
using System.Linq;
using System.Reflection;
using ProjectIcon.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace ProjectIcon.Editor.Tests
{
    public sealed class EditorIconLifecycleTests
    {
        const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        bool m_optionsEnabled;
        EnterPlayModeOptions m_options;
        bool m_hadAutoApply;
        bool m_autoApply;

        [SetUp]
        public void SetUp()
        {
            m_optionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            m_options = EditorSettings.enterPlayModeOptions;
            m_hadAutoApply = EditorPrefs.HasKey(EditorIconPreferences.AutoApplyPlayerIconKey);
            m_autoApply = EditorIconApplier.AutoApplyEnabled;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (EditorApplication.isPlaying)
                yield return new ExitPlayMode();

            EditorSettings.enterPlayModeOptions = m_options;
            EditorSettings.enterPlayModeOptionsEnabled = m_optionsEnabled;
            if (m_hadAutoApply)
                EditorIconApplier.AutoApplyEnabled = m_autoApply;
            else
                EditorPrefs.DeleteKey(EditorIconPreferences.AutoApplyPlayerIconKey);
            EditorIconApplier.Initialize();
        }

        [Test]
        public void InitializeAndCleanup_OwnExactlyOneUpdateCallback()
        {
            EditorIconApplier.Initialize();
            EditorIconApplier.Initialize();
            AssertUpdateCallbackCount();

            EditorIconApplier.Cleanup();
            EditorIconApplier.Cleanup();
            Assert.That(UpdateCallbackCount(), Is.Zero);

            EditorIconApplier.Initialize();
            AssertUpdateCallbackCount();
        }

        [Test]
        public void ProjectChange_DoesNotExtendTheStartupRetryWindow()
        {
            FieldInfo window = typeof(EditorIconApplier).GetField("s_forceReapplyUntilTime", StaticPrivate);
            MethodInfo onProjectChanged = typeof(EditorIconApplier).GetMethod("OnProjectChanged", StaticPrivate);
            Assert.That(window, Is.Not.Null);
            Assert.That(onProjectChanged, Is.Not.Null);

            object before = window.GetValue(null);
            onProjectChanged.Invoke(null, null);
            Assert.That(window.GetValue(null), Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator RepeatedPlayModeWithoutDomainReload_KeepsIconUpdatesRegistered()
        {
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload;
            EditorIconApplier.Initialize();
            AssertUpdateCallbackCount();

            for (int i = 0; i < 3; i++)
            {
                yield return new EnterPlayMode(expectDomainReload: false);
                Assert.That(EditorApplication.isPlaying, Is.True);
                AssertUpdateCallbackCount();
#if UNITY_EDITOR_OSX
                // Outside retry windows, Play Mode skips the 30-second maintenance apply.
                double overdue = EditorApplication.timeSinceStartup - 60d;
                EditorIconApplier.AutoApplyEnabled = true;
                SetField("s_autoApplyWasEnabled", true);
                SetField("s_applyRequested", false);
                SetField("s_forceReapplyUntilTime", -1d);
                SetField("s_fastReapplyUntilTime", -1d);
                SetField("s_lastApplyTime", overdue);
                Update();
                Assert.That(GetField<double>("s_lastApplyTime"), Is.EqualTo(overdue));
#endif

                yield return new ExitPlayMode();
                Assert.That(EditorApplication.isPlaying, Is.False);
                AssertUpdateCallbackCount();
#if UNITY_EDITOR_OSX
                // The first update after leaving Play Mode applies the overdue maintenance check.
                Update();
                Assert.That(GetField<double>("s_lastApplyTime"), Is.GreaterThan(overdue));
#endif
            }
        }

        static void Update() => typeof(EditorIconApplier).GetMethod("ReapplyAfterDelay", StaticPrivate).Invoke(null, null);
        static void SetField(string name, object value) => typeof(EditorIconApplier).GetField(name, StaticPrivate).SetValue(null, value);
        static T GetField<T>(string name) => (T)typeof(EditorIconApplier).GetField(name, StaticPrivate).GetValue(null);

        static int UpdateCallbackCount()
        {
            return EditorApplication.update?.GetInvocationList().Count(callback =>
                callback.Method.DeclaringType == typeof(EditorIconApplier)) ?? 0;
        }

        static void AssertUpdateCallbackCount()
        {
#if UNITY_EDITOR_OSX
            Assert.That(UpdateCallbackCount(), Is.EqualTo(1));
#else
            Assert.That(UpdateCallbackCount(), Is.Zero);
#endif
        }
    }
}
