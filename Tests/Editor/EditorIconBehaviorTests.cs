using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace ProjectIcon.Editor.Tests
{
    public sealed class EditorIconBehaviorTests
    {
        const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        readonly List<Texture2D> m_textures = new List<Texture2D>();
        bool m_hadAutoApply;
        bool m_autoApply;

        [SetUp]
        public void SetUp()
        {
            m_hadAutoApply = EditorPrefs.HasKey(EditorIconPreferences.AutoApplyPlayerIconKey);
            m_autoApply = EditorIconApplier.AutoApplyEnabled;
            EditorIconApplier.Cleanup();
            EditorIconApplier.AutoApplyEnabled = false;
        }

        [TearDown]
        public void TearDown()
        {
            EditorIconApplier.Cleanup();
            foreach (Texture2D texture in m_textures)
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            m_textures.Clear();
            SetField("s_lastApplyTime", -30d);
            SetField("s_forceReapplyUntilTime", 0d);
            SetField("s_fastReapplyUntilTime", 0d);
            if (m_hadAutoApply)
                EditorIconApplier.AutoApplyEnabled = m_autoApply;
            else
                EditorPrefs.DeleteKey(EditorIconPreferences.AutoApplyPlayerIconKey);
            EditorIconApplier.Initialize();
        }

        [TestCase(IconKind.Application)]
        [TestCase(IconKind.Any)]
        public void Selection_PrefersActiveTargetBeforeDefault(IconKind kind)
        {
            Texture2D active = Texture();
            Texture2D fallback = Texture();
            Assert.That(EditorIconApplier.GetCurrentPlayerIcon(BuildTargetGroup.Standalone,
                (group, requestedKind) => group == BuildTargetGroup.Standalone
                    ? requestedKind == kind ? new[] { null, active } : null
                    : new[] { fallback }), Is.SameAs(active));
        }

        [Test]
        public void Selection_UsesDefaultBeforeOtherPlatforms()
        {
            Texture2D fallback = Texture();
            Texture2D other = Texture();
            Assert.That(EditorIconApplier.GetCurrentPlayerIcon(BuildTargetGroup.Standalone,
                (group, kind) => group == BuildTargetGroup.Standalone ? null :
                    new[] { group == BuildTargetGroup.Unknown ? fallback : other }), Is.SameAs(fallback));
        }

        [Test]
        public void Selection_RecoversFromUnsupportedApplicationKind()
        {
            Texture2D icon = Texture();
            Assert.That(EditorIconApplier.GetCurrentPlayerIcon(BuildTargetGroup.Standalone,
                (group, kind) => kind == IconKind.Application
                    ? throw new InvalidOperationException("Unsupported application icons")
                    : new[] { icon }), Is.SameAs(icon));
        }

        [Test]
        public void Selection_SkipsDestroyedTexturesAndUnsupportedTargets()
        {
            Texture2D destroyed = Texture();
            Texture2D icon = Texture();
            UnityEngine.Object.DestroyImmediate(destroyed);
            Assert.That(EditorIconApplier.GetCurrentPlayerIcon(BuildTargetGroup.Standalone,
                (group, kind) => group == BuildTargetGroup.Android ? new[] { destroyed, null, icon } :
                    throw new InvalidOperationException("Unsupported target")), Is.SameAs(icon));
        }

        [TestCase(BuildTargetGroup.Unknown)]
        [TestCase(BuildTargetGroup.Standalone)]
        public void Selection_QueriesEachTargetAndKindAtMostOnceWhenEmpty(BuildTargetGroup active)
        {
            var queried = new HashSet<string>();
            int queryCount = 0;
            Assert.That(EditorIconApplier.GetCurrentPlayerIcon(active, (group, kind) =>
            {
                ++queryCount;
                queried.Add($"{(int)group}:{(int)kind}");
                return Array.Empty<Texture2D>();
            }), Is.Null);
            Assert.That(queryCount, Is.EqualTo(queried.Count),
                "Empty projects must not repeat unsupported or aliased target queries.");
            Assert.That(queried.Count, Is.GreaterThan(2));
        }

#if UNITY_6000_0_OR_NEWER
        [Test]
        public void Selection_QueriesEveryRepresentableTargetAndSkipsRejectedEnumValues()
        {
            var queried = new HashSet<BuildTargetGroup>();
            EditorIconApplier.GetCurrentPlayerIcon(BuildTargetGroup.Standalone, (group, kind) =>
            {
                queried.Add(group);
                return Array.Empty<Texture2D>();
            });

            var representable = new HashSet<BuildTargetGroup>();
            foreach (BuildTargetGroup group in Enum.GetValues(typeof(BuildTargetGroup)))
            {
                try
                {
                    UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(group);
                    representable.Add(group);
                }
                catch (ArgumentException)
                {
                    Assert.That(queried.Contains(group), Is.False,
                        "Rejected enum values cannot have icons in this Editor.");
                }
            }
            Assert.That(queried, Is.EquivalentTo(representable),
                "Keep valid aliases and targets even when their build modules are not installed.");
        }
#endif

#if UNITY_EDITOR_OSX
        [Test]
        public void AutoApply_EnablingAfterDisableAppliesOnTheNextUpdate()
        {
            SetField("s_autoApplyWasEnabled", false);
            SetField("s_lastApplyTime", double.MaxValue);
            SetField("s_applyRequested", false);
            EditorIconApplier.AutoApplyEnabled = true;
            Update();
            Assert.That(GetField<double>("s_lastApplyTime"), Is.LessThan(double.MaxValue));
            Assert.That(GetField<bool>("s_applyRequested"), Is.False);
        }

        [Test]
        public void Initialize_RequestsAnImmediateApplyEvenAfterRecentWork()
        {
            SetField("s_lastApplyTime", double.MaxValue);
            EditorIconApplier.AutoApplyEnabled = true;
            EditorIconApplier.Initialize();
            Update();
            Assert.That(GetField<double>("s_lastApplyTime"), Is.LessThan(double.MaxValue));
        }

        [Test]
        public void ProjectChangeBurst_CoalescesIntoOneApplyWithoutExtendingRetryWindows()
        {
            EditorIconApplier.AutoApplyEnabled = true;
            SetField("s_autoApplyWasEnabled", true);
            SetField("s_lastApplyTime", EditorApplication.timeSinceStartup);
            SetField("s_forceReapplyUntilTime", -1d);
            SetField("s_fastReapplyUntilTime", -1d);
            MethodInfo projectChanged = typeof(EditorIconApplier).GetMethod("OnProjectChanged", StaticPrivate);
            for (int i = 0; i < 100; i++)
                projectChanged.Invoke(null, null);
            Assert.That(GetField<bool>("s_applyRequested"), Is.True);
            Update();
            double applied = GetField<double>("s_lastApplyTime");
            Assert.That(GetField<bool>("s_applyRequested"), Is.False);
            Update();
            Assert.That(GetField<double>("s_lastApplyTime"), Is.EqualTo(applied));
            Assert.That(GetField<double>("s_forceReapplyUntilTime"), Is.EqualTo(-1d));
            Assert.That(GetField<double>("s_fastReapplyUntilTime"), Is.EqualTo(-1d));
        }

        // Window lengths in seconds: startup retries after code loads, focus retries after the Editor gains focus.
        const double StartupWindow = 120d;
        const double FocusWindow = 6d;

        // Within a window, the next retry waits as long as the window had run at the last apply,
        // at least the window minimum (2 s startup, 0.25 s focus) and at most 30 s.
        [TestCase(false, 10d, 2d, false, TestName = "Scheduler_StartupRetryWaitsForTheWindowAgeAtLastApply")]
        [TestCase(false, 10d, 6d, true, TestName = "Scheduler_StartupRetryIsDueAfterTheWindowAgeAtLastApply")]
        [TestCase(false, 1d, 1.5d, false, TestName = "Scheduler_StartupRetryWaitsAtLeastTheMinimum")]
        [TestCase(false, 100d, 29d, false, TestName = "Scheduler_StartupRetryIntervalIsCappedAtThirtySeconds")]
        [TestCase(false, 100d, 31d, true, TestName = "Scheduler_StartupRetryIsDueAfterThirtySeconds")]
        [TestCase(true, 3d, 1d, false, TestName = "Scheduler_FocusRetryWaitsForTheWindowAgeAtLastApply")]
        [TestCase(true, 3d, 2d, true, TestName = "Scheduler_FocusRetryIsDueAfterTheWindowAgeAtLastApply")]
        [TestCase(true, 0.1d, 0.1d, false, TestName = "Scheduler_FocusRetryWaitsAtLeastTheMinimum")]
        [TestCase(true, 0.4d, 0.4d, true, TestName = "Scheduler_FocusRetryIsDueAfterTheMinimum")]
        public void Scheduler_RetryWindowsBackOff(bool focusWindow, double windowAge, double lastApplyAge, bool due)
        {
            double now = EditorApplication.timeSinceStartup;
            PrepareScheduler(now - lastApplyAge);
            if (focusWindow)
                SetField("s_fastReapplyUntilTime", now - windowAge + FocusWindow);
            else
                SetField("s_forceReapplyUntilTime", now - windowAge + StartupWindow);

            Update();
            Assert.That(GetField<double>("s_lastApplyTime") != now - lastApplyAge, Is.EqualTo(due));
        }

        [TestCase(29d, false)]
        [TestCase(31d, true)]
        public void Scheduler_MaintenanceAppliesEveryThirtySecondsOutsideWindows(double lastApplyAge, bool due)
        {
            double lastApply = EditorApplication.timeSinceStartup - lastApplyAge;
            PrepareScheduler(lastApply);
            Update();
            Assert.That(GetField<double>("s_lastApplyTime") != lastApply, Is.EqualTo(due));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FocusChange_AppliesOnceAndOnlyGainingFocusStartsTheRetryWindow(bool hasFocus)
        {
            PrepareScheduler(-1000d);
            typeof(EditorIconApplier).GetMethod("OnEditorFocusChanged", StaticPrivate)
                .Invoke(null, new object[] { hasFocus });
            Assert.That(GetField<double>("s_lastApplyTime"), Is.GreaterThan(-1000d));
            Assert.That(GetField<double>("s_fastReapplyUntilTime") > EditorApplication.timeSinceStartup,
                Is.EqualTo(hasFocus));
        }

        static void PrepareScheduler(double lastApplyTime)
        {
            EditorIconApplier.AutoApplyEnabled = true;
            SetField("s_autoApplyWasEnabled", true);
            SetField("s_applyRequested", false);
            SetField("s_forceReapplyUntilTime", -1d);
            SetField("s_fastReapplyUntilTime", -1d);
            SetField("s_lastApplyTime", lastApplyTime);
        }

        [Test]
        public void AutoApply_DisablingClearsPendingWorkAndOwnedIcon()
        {
            ApplyNativeIcon();
            SetField("s_autoApplyWasEnabled", true);
            SetField("s_applyRequested", true);
            Update();
            Assert.That(EditorIconNative.AppliedThisSession, Is.False);
            Assert.That(GetField<bool>("s_applyRequested"), Is.False);
        }

        [Test]
        public void Cleanup_ReleasesOwnedNativeIconAndPendingWork()
        {
            ApplyNativeIcon();
            SetField("s_applyRequested", true);
            EditorIconApplier.Cleanup();
            EditorIconApplier.Cleanup();
            Assert.That(EditorIconNative.AppliedThisSession, Is.False);
            Assert.That(GetField<bool>("s_applyRequested"), Is.False);
        }

        [Test]
        public void RemovingLastPlayerIcon_ReleasesOwnedNativeIcon()
        {
            ApplyNativeIcon();
            Assert.That(EditorIconApplier.TryApplyPlayerIcon(null, out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(EditorIconNative.AppliedThisSession, Is.False);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("/tmp/icon.png\0ignored")]
        public void NativeBridge_InvalidPathIsRejectedBeforeImageLoading(string path)
        {
            Assert.That(EditorIconNative.TrySetApplicationIcon(path, EditorIconMode.Badge,
                out string error, out bool rejected), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(rejected, Is.False, "Invalid input must not request texture conversion.");
            Assert.That(EditorIconNative.AppliedThisSession, Is.False);
        }

        [TestCase(-1)]
        [TestCase(2)]
        [TestCase(int.MaxValue)]
        public void NativeBridge_InvalidModeDoesNotPoisonLaterValidCalls(int mode)
        {
            Assert.That(EditorIconNative.TrySetApplicationIcon(NativeImagePath, (EditorIconMode)mode,
                out string error, out bool rejected), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(rejected, Is.False);
            ApplyNativeIcon();
        }

        static string NativeImagePath => System.IO.Path.GetFullPath(
            "Packages/com.martincalander.projecticon/ProjectIcon.png");

        static void ApplyNativeIcon()
        {
            Assert.That(EditorIconNative.TrySetApplicationIcon(NativeImagePath,
                EditorIconMode.Badge, out string error), Is.True, error);
            Assert.That(EditorIconNative.AppliedThisSession, Is.True);
        }
#endif

        Texture2D Texture()
        {
            var texture = new Texture2D(2, 2);
            m_textures.Add(texture);
            return texture;
        }

        static void Update() => typeof(EditorIconApplier).GetMethod("ReapplyAfterDelay", StaticPrivate).Invoke(null, null);
        static void SetField(string name, object value) => typeof(EditorIconApplier).GetField(name, StaticPrivate).SetValue(null, value);
        static T GetField<T>(string name) => (T)typeof(EditorIconApplier).GetField(name, StaticPrivate).GetValue(null);
    }
}
