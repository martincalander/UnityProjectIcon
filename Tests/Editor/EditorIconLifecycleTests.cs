using System.Collections;
using System.Linq;
using ProjectIcon.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;

namespace ProjectIcon.Editor.Tests
{
    public sealed class EditorIconLifecycleTests
    {
        bool m_optionsEnabled;
        EnterPlayModeOptions m_options;

        [SetUp]
        public void SetUp()
        {
            m_optionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            m_options = EditorSettings.enterPlayModeOptions;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (EditorApplication.isPlaying)
                yield return new ExitPlayMode();

            EditorSettings.enterPlayModeOptions = m_options;
            EditorSettings.enterPlayModeOptionsEnabled = m_optionsEnabled;
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

                yield return new ExitPlayMode();
                Assert.That(EditorApplication.isPlaying, Is.False);
                AssertUpdateCallbackCount();
            }
        }

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
