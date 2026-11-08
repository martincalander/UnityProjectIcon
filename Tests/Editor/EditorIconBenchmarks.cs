using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ProjectIcon.Editor.Tests
{
    // Opt-in measurements, separate from correctness tests and normal Editor startup.
    public static class EditorIconBenchmarks
    {
        const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;
        const string ImageAsset = "Packages/com.martincalander.projecticon/ProjectIcon.png";
        static readonly Func<long> AllocatedBytes = CreateAllocationCounter();
        static object s_sink;

        [Serializable]
        public sealed class Measurement
        {
            public string name;
            public int iterations;
            public double medianMicroseconds;
            public double minMicroseconds;
            public double maxMicroseconds;
            public double medianBytes;
            public double[] sampleMicroseconds;
        }

        [Serializable]
        public sealed class Report
        {
            public string unity;
            public string operatingSystem;
            public string processor;
            public string graphics;
            public string runtime;
            public bool batchMode;
            public bool allocationCounterAvailable;
            public List<Measurement> measurements = new List<Measurement>();
        }

#if UNITY_EDITOR_OSX
        [DllImport("ProjectIcon", EntryPoint = "ProjectIconSetApplicationIconWithMode")]
        static extern int ApplyUtf8([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int mode);
#endif

        public static string Run(string outputPath)
        {
            var report = new Report
            {
                unity = Application.unityVersion,
                operatingSystem = SystemInfo.operatingSystem,
                processor = SystemInfo.processorType,
                graphics = SystemInfo.graphicsDeviceType + ": " + SystemInfo.graphicsDeviceName,
                runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                batchMode = Application.isBatchMode,
                allocationCounterAvailable = AllocatedBytes != null
            };
            bool hadPreference = EditorPrefs.HasKey(EditorIconPreferences.AutoApplyPlayerIconKey);
            bool previousPreference = EditorIconApplier.AutoApplyEnabled;
            string cache = Path.GetFullPath("Library/com.martincalander.projecticon/ProjectIcon.png");
            byte[] previousCache = File.Exists(cache) ? File.ReadAllBytes(cache) : null;
            var textures = new List<Texture2D>();
            EditorIconApplier.Cleanup();
            try
            {
                Action update = (Action)Delegate.CreateDelegate(typeof(Action),
                    typeof(EditorIconApplier).GetMethod("ReapplyAfterDelay", PrivateStatic));
                EditorIconApplier.AutoApplyEnabled = false;
                SetField("s_autoApplyWasEnabled", false);
                Measure(report, "scheduler.disabled", 100000, update);
                EditorIconApplier.AutoApplyEnabled = true;
                SetField("s_autoApplyWasEnabled", true);
                SetField("s_applyRequested", false);
                SetField("s_lastApplyTime", double.MaxValue);
                Measure(report, "scheduler.idle", 100000, update);
                Measure(report, "preference.read", 100000,
                    () => { if (!EditorIconApplier.AutoApplyEnabled) throw new Exception("Preference changed."); });

                var lookup = (Func<Texture2D>)Delegate.CreateDelegate(typeof(Func<Texture2D>),
                    typeof(EditorIconApplier).GetMethod("GetCurrentPlayerIcon", PrivateStatic,
                        null, Type.EmptyTypes, null));
                Measure(report, "lookup.actual-project", 100, () => s_sink = lookup());
                var selected = new Texture2D(2, 2);
                textures.Add(selected);
                var assigned = new[] { selected };
                var empty = Array.Empty<Texture2D>();
                Func<BuildTargetGroup, IconKind, Texture2D[]> active = (group, kind) => assigned;
                Func<BuildTargetGroup, IconKind, Texture2D[]> general = (group, kind) =>
                    group == BuildTargetGroup.Unknown ? assigned : empty;
                Func<BuildTargetGroup, IconKind, Texture2D[]> missing = (group, kind) => empty;
                Measure(report, "lookup.active.synthetic", 100000, () =>
                    s_sink = EditorIconApplier.GetCurrentPlayerIcon(BuildTargetGroup.Standalone, active));
                Measure(report, "lookup.general.synthetic", 100000, () =>
                    s_sink = EditorIconApplier.GetCurrentPlayerIcon(BuildTargetGroup.Standalone, general));
                Measure(report, "lookup.empty.synthetic", 10000, () =>
                    s_sink = EditorIconApplier.GetCurrentPlayerIcon(BuildTargetGroup.Standalone, missing));

                string source = Path.GetFullPath(ImageAsset);
                Measure(report, "file.exists", 10000, () => { if (!File.Exists(source)) throw new Exception(source); });
                Measure(report, "file.info", 10000, () =>
                {
                    var info = new FileInfo(source);
                    if (!info.Exists || info.Length == 0 || info.LastWriteTimeUtc == default(DateTime))
                        throw new Exception(source);
                });
                Measure(report, "path.utf8-manual", 10000, () =>
                {
                    byte[] bytes = Encoding.UTF8.GetBytes(source + "\0");
                    IntPtr pointer = Marshal.AllocHGlobal(bytes.Length);
                    try { Marshal.Copy(bytes, 0, pointer, bytes.Length); }
                    finally { Marshal.FreeHGlobal(pointer); }
                });

#if UNITY_EDITOR_OSX
                foreach (EditorIconMode mode in new[] { EditorIconMode.Badge, EditorIconMode.IconReplacement })
                {
                    Measure(report, "native.warm." + mode, 5, () =>
                        Require(EditorIconNative.TrySetApplicationIcon(source, mode, out string error), error));
                    Measure(report, "candidate.utf8-marshaler." + mode, 5, () =>
                    {
                        Require(ApplyUtf8(source, (int)mode) == 1, "Native UTF-8 apply failed.");
                        SessionState.SetBool("ProjectIcon.AppliedThisSession", true);
                    });
                }
                Texture2D imported = AssetDatabase.LoadAssetAtPath<Texture2D>(ImageAsset);
                Require(imported != null, "Package artwork must be imported.");
                Measure(report, "apply.imported-source.warm", 5, () =>
                    Require(EditorIconApplier.TryApplyIcon(imported, EditorIconMode.IconReplacement,
                        out string error), error));
#endif

                var encode = (Func<Texture2D, byte[]>)Delegate.CreateDelegate(typeof(Func<Texture2D, byte[]>),
                    typeof(EditorIconApplier).GetMethod("TryEncodeTexture", PrivateStatic));
                foreach (int size in new[] { 32, 256, 1024, 2048 })
                {
                    Texture2D readable = CreateTexture(size);
                    textures.Add(readable);
                    int iterations = size >= 1024 ? 2 : 10;
                    Measure(report, "png.encode." + size, iterations, () => s_sink = encode(readable));
                    Measure(report, "png.cold-write." + size, iterations, () =>
                    {
                        SetField("s_readableIconSource", null);
                        Require(EditorIconApplier.TryWriteReadableIconCopy(readable, out _, out string error), error);
                    });
                    Measure(report, "png.cache-hit." + size, 1000, () =>
                        Require(EditorIconApplier.TryWriteReadableIconCopy(readable, out _, out string error), error));
#if UNITY_EDITOR_OSX
                    Measure(report, "apply.generated.warm." + size, 5, () =>
                        Require(EditorIconApplier.TryApplyIcon(readable, EditorIconMode.IconReplacement,
                            out string error), error));
#endif
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    {
                        readable.Apply(false, true);
                        Measure(report, "png.gpu-readback-encode." + size, iterations, () => s_sink = encode(readable));
                    }
                }
                File.WriteAllText(outputPath, JsonUtility.ToJson(report, true));
                return outputPath;
            }
            finally
            {
                s_sink = null;
                EditorIconApplier.Cleanup();
                foreach (Texture2D texture in textures)
                    if (texture != null) UnityEngine.Object.DestroyImmediate(texture);
                if (previousCache != null) File.WriteAllBytes(cache, previousCache);
                else if (File.Exists(cache)) File.Delete(cache);
                if (hadPreference) EditorIconApplier.AutoApplyEnabled = previousPreference;
                else EditorPrefs.DeleteKey(EditorIconPreferences.AutoApplyPlayerIconKey);
                SetField("s_lastApplyTime", -30d);
                EditorIconApplier.Initialize();
            }
        }

        static Texture2D CreateTexture(int size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color32((byte)x, (byte)y, (byte)(x ^ y), 255);
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        static void Measure(Report report, string name, int iterations, Action action)
        {
            for (int i = 0; i < Math.Min(iterations, 5); i++) action();
            var times = new double[9];
            var allocations = new double[9];
            for (int sample = 0; sample < times.Length; sample++)
            {
                long bytes = AllocatedBytes != null ? AllocatedBytes() : 0;
                long start = Stopwatch.GetTimestamp();
                for (int i = 0; i < iterations; i++) action();
                times[sample] = (Stopwatch.GetTimestamp() - start) * 1000000d / Stopwatch.Frequency / iterations;
                allocations[sample] = AllocatedBytes != null ? (AllocatedBytes() - bytes) / (double)iterations : -1;
            }
            var sorted = (double[])times.Clone();
            Array.Sort(sorted);
            Array.Sort(allocations);
            report.measurements.Add(new Measurement
            {
                name = name, iterations = iterations, medianMicroseconds = sorted[4],
                minMicroseconds = sorted[0], maxMicroseconds = sorted[8],
                medianBytes = allocations[4], sampleMicroseconds = times
            });
        }

        static Func<long> CreateAllocationCounter()
        {
            MethodInfo method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static);
            return method == null ? null : (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method);
        }

        static void SetField(string name, object value) => typeof(EditorIconApplier).GetField(name, PrivateStatic).SetValue(null, value);
        static void Require(bool success, string error)
        {
            if (!success) throw new InvalidOperationException(error);
        }
    }
}
