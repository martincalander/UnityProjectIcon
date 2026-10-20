using System;
using System.IO;
using ProjectIcon.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace ProjectIcon.Editor.Tests
{
    public sealed class EditorIconPackageContractTests
    {
        const string PackageName = "com.martincalander.projecticon";
        const string NativeBundlePath =
            "Packages/com.martincalander.projecticon/Editor/Plugins/macOS/ProjectIcon.bundle";
        const string NativeBundleGuid = "061098808649b4ba88a30729526dbde4";
        PackageInfo m_package;

        [OneTimeSetUp]
        public void ResolvePackage()
        {
            m_package = PackageInfo.FindForAssetPath($"Packages/{PackageName}/package.json");
            Assert.That(m_package, Is.Not.Null);
        }

        [Test]
        public void Package_UsesProjectIconIdentity()
        {
            Assert.That(m_package.name, Is.EqualTo(PackageName));
            Assert.That(m_package.displayName, Is.EqualTo("Project Icon"));
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(
                $"Packages/{PackageName}/ProjectIcon.png"), Is.Not.Null);
        }

        [TestCase("Editor/ProjectIcon.Editor.asmdef")]
        [TestCase("Tests/Editor/ProjectIcon.Editor.Tests.asmdef")]
        public void Assemblies_SupportProjectIconDisableSymbol(string relativePath)
        {
            string json = File.ReadAllText(Path.Combine(m_package.resolvedPath, relativePath));
            StringAssert.Contains("\"!PROJECT_ICON_DISABLE_ALL\"", json);
        }

        [Test]
        public void NativeBundle_UsesStableGuidAndMacOsEditorOnlyImporter()
        {
            Assert.That(AssetDatabase.AssetPathToGUID(NativeBundlePath), Is.EqualTo(NativeBundleGuid));

            var importer = AssetImporter.GetAtPath(NativeBundlePath) as PluginImporter;
            Assert.That(importer, Is.Not.Null, "The native bundle must be imported as a PluginImporter.");
            Assert.That(importer.GetCompatibleWithAnyPlatform(), Is.False);
            Assert.That(importer.GetCompatibleWithEditor(), Is.True);
            Assert.That(importer.GetCompatibleWithPlatform(BuildTarget.StandaloneOSX), Is.False,
                "The native bundle is an Editor plugin, not a Player plugin.");
            Assert.That(importer.GetEditorData("OS"), Is.EqualTo("OSX"));
            Assert.That(importer.GetEditorData("CPU"), Is.EqualTo("AnyCPU"));
            Assert.That(importer.isPreloaded, Is.False);
        }

        [Test]
        public void NativeBundle_ContainsProjectIconExecutableAndMetadata()
        {
            string bundleRoot = Path.Combine(
                m_package.resolvedPath,
                "Editor/Plugins/macOS/ProjectIcon.bundle");
            string executablePath = Path.Combine(bundleRoot, "Contents/MacOS/ProjectIcon");
            string infoPath = Path.Combine(bundleRoot, "Contents/Info.plist");

            Assert.That(File.Exists(executablePath), Is.True, "The checked-in universal executable is missing.");
            Assert.That(File.Exists(infoPath), Is.True, "The native bundle Info.plist is missing.");

            string info = File.ReadAllText(infoPath);
            StringAssert.Contains("com.martincalander.projecticon.native", info);
            StringAssert.Contains("<string>ProjectIcon</string>", info);
        }

        [Test]
        public void NativeContract_ExportsBadgeModeAwareAndResetEntryPoints()
        {
            string source = File.ReadAllText(Path.Combine(
                m_package.resolvedPath,
                "Native~/macOS/ProjectIcon.mm"));
            string buildScript = File.ReadAllText(Path.Combine(
                m_package.resolvedPath,
                "Native~/macOS/build.sh"));

            StringAssert.Contains("ProjectIconSetApplicationIcon(const char *utf8Path)", source);
            StringAssert.Contains("ProjectIconSetApplicationIconWithMode", source);
            StringAssert.Contains("ProjectIconResetApplicationIcon", source);
            StringAssert.Contains("_ProjectIconSetApplicationIcon$", buildScript);
            StringAssert.Contains("_ProjectIconSetApplicationIconWithMode$", buildScript);
            StringAssert.Contains("_ProjectIconResetApplicationIcon$", buildScript);
        }

        [Test]
        public void ReadableCopy_ReusesUnchangedTextureAndRegeneratesAfterUpdatesOrDeletion()
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            string path = null;
            try
            {
                texture.SetPixel(0, 0, Color.red);
                texture.Apply();
                Assert.That(EditorIconApplier.TryWriteReadableIconCopy(texture, out path, out string error), Is.True, error);
                byte[] original = File.ReadAllBytes(path);
                DateTime marker = File.GetLastWriteTimeUtc(path);
                Assert.That(EditorIconApplier.TryWriteReadableIconCopy(texture, out string cachedPath, out error), Is.True, error);
                Assert.That(cachedPath, Is.EqualTo(path));
                Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(marker), "An unchanged icon must not be rewritten.");

                texture.SetPixel(0, 0, Color.blue);
                texture.Apply();
                Assert.That(EditorIconApplier.TryWriteReadableIconCopy(texture, out _, out error), Is.True, error);
                Assert.That(File.ReadAllBytes(path), Is.Not.EqualTo(original), "Texture updates invalidate the cached PNG.");

                marker = File.GetLastWriteTimeUtc(path);
                byte[] expected = File.ReadAllBytes(path);
                byte[] corrupted = (byte[])expected.Clone();
                corrupted[0] ^= 1;
                File.WriteAllBytes(path, corrupted);
                File.SetLastWriteTimeUtc(path, marker);
                // Keep the file fingerprint unchanged so only the texture hash triggers regeneration.
                texture.imageContentsHash = Hash128.Compute("reimported icon");
                Assert.That(EditorIconApplier.TryWriteReadableIconCopy(texture, out _, out error), Is.True, error);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(expected), "Imported content changes invalidate the cache.");

                File.WriteAllText(path, "overwritten cache");
                Assert.That(EditorIconApplier.TryWriteReadableIconCopy(texture, out _, out error), Is.True, error);
                Assert.That(File.ReadAllBytes(path).Length, Is.GreaterThan("overwritten cache".Length),
                    "An overwritten cache file must be regenerated.");

                File.Delete(path);
                Assert.That(EditorIconApplier.TryWriteReadableIconCopy(texture, out _, out error), Is.True, error);
                Assert.That(File.Exists(path), Is.True, "Deleting the Library cache must recover automatically.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
                if (path != null)
                    File.Delete(path);
            }
        }

        [TestCase(TextureFormat.R8)]
        [TestCase(TextureFormat.R16)]
        [TestCase(TextureFormat.RHalf)]
        [TestCase(TextureFormat.RFloat)]
        public void ReadableCopy_SingleChannelTextureRemainsGrayscale(TextureFormat format)
        {
            var texture = new Texture2D(4, 4, format, false);
            var decoded = new Texture2D(2, 2);
            string path = null;
            try
            {
                texture.SetPixels(new[] {
                    Color.white, Color.white, Color.white, Color.white,
                    Color.white, Color.white, Color.white, Color.white,
                    Color.white, Color.white, Color.white, Color.white,
                    Color.white, Color.white, Color.white, Color.white
                });
                texture.Apply();
                Assert.That(EditorIconApplier.TryWriteReadableIconCopy(texture, out path, out string error), Is.True, error);
                Assert.That(decoded.LoadImage(File.ReadAllBytes(path)), Is.True);
                Color pixel = decoded.GetPixel(0, 0);
                Assert.That(pixel.r, Is.GreaterThan(0.95f));
                Assert.That(pixel.g, Is.GreaterThan(0.95f), "The red channel must be encoded as grayscale.");
                Assert.That(pixel.b, Is.GreaterThan(0.95f), "The red channel must be encoded as grayscale.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(decoded);
                if (path != null)
                    File.Delete(path);
            }
        }

        [Test]
        public void ReadableCopy_DifferentTextureInvalidatesCache()
        {
            var first = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var second = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            string path = null;
            try
            {
                first.SetPixel(0, 0, Color.red);
                first.Apply();
                second.SetPixel(0, 0, Color.blue);
                second.Apply();
                Assert.That(EditorIconApplier.TryWriteReadableIconCopy(first, out path, out string error), Is.True, error);
                byte[] original = File.ReadAllBytes(path);
                Assert.That(EditorIconApplier.TryWriteReadableIconCopy(second, out _, out error), Is.True, error);
                Assert.That(File.ReadAllBytes(path), Is.Not.EqualTo(original));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(first);
                UnityEngine.Object.DestroyImmediate(second);
                if (path != null)
                    File.Delete(path);
            }
        }

        [Test]
        public void ReadableCopy_UnreadableTextureUsesGpuWithoutChangingActiveTarget()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("GPU readback requires an Editor with a graphics device.");

            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var decoded = new Texture2D(2, 2);
            RenderTexture previous = RenderTexture.active;
            RenderTexture sentinel = RenderTexture.GetTemporary(8, 8);
            string path = null;
            try
            {
                texture.SetPixels(new[] {
                    Color.blue, Color.blue, Color.blue, Color.blue,
                    Color.blue, Color.blue, Color.blue, Color.blue,
                    Color.blue, Color.blue, Color.blue, Color.blue,
                    Color.blue, Color.blue, Color.blue, Color.blue
                });
                texture.Apply(false, true);
                RenderTexture.active = sentinel;
                Assert.That(EditorIconApplier.TryWriteReadableIconCopy(texture, out path, out string error), Is.True, error);
                Assert.That(RenderTexture.active, Is.SameAs(sentinel));
                Assert.That(decoded.LoadImage(File.ReadAllBytes(path)), Is.True);
                Assert.That(decoded.GetPixel(0, 0).b, Is.GreaterThan(0.95f));
                Assert.That(decoded.GetPixel(0, 0).r, Is.LessThan(0.05f));
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(sentinel);
                UnityEngine.Object.DestroyImmediate(texture);
                UnityEngine.Object.DestroyImmediate(decoded);
                if (path != null)
                    File.Delete(path);
            }
        }

        [Test]
        public void ApplyIcon_MissingTextureReturnsFailure()
        {
            Assert.That(EditorIconApplier.TryApplyIcon(null, EditorIconMode.Badge, out string error), Is.False);
            Assert.That(error, Is.EqualTo("No Player icon is assigned for this project."));
        }

#if UNITY_EDITOR_OSX
        [TestCase((int)EditorIconMode.Badge)]
        [TestCase((int)EditorIconMode.IconReplacement)]
        public void TextureAsset_AppKitRejectsSource_UsesReadablePngFallback(int rawMode)
        {
            string assetPath = AssetDatabase.GenerateUniqueAssetPath("Assets/ProjectIconFallbackTest.asset");
            var texture = new UnityEngine.Texture2D(4, 4, UnityEngine.TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, UnityEngine.Color.red);
            texture.Apply();
            var mode = (EditorIconMode)rawMode;
            try
            {
                AssetDatabase.CreateAsset(texture, assetPath);
                Assert.That(EditorIconNative.TrySetApplicationIcon(
                    Path.GetFullPath(assetPath), mode, out _, out bool imageRejected), Is.False);
                Assert.That(imageRejected, Is.True, "A source-format rejection permits texture conversion.");
                Assert.That(EditorIconApplier.TryApplyIcon(texture, mode, out string error), Is.True, error);
                Assert.That(EditorIconApplier.TryWriteReadableIconCopy(texture, out string cachedPath, out error), Is.True, error);
                DateTime marker = File.GetLastWriteTimeUtc(cachedPath);
                Assert.That(EditorIconApplier.TryApplyIcon(texture, mode, out error), Is.True, error);
                Assert.That(File.GetLastWriteTimeUtc(cachedPath), Is.EqualTo(marker), "Repeated native fallback must reuse the PNG.");
            }
            finally
            {
                EditorIconNative.TryResetApplicationIcon(out _);
                if (!AssetDatabase.DeleteAsset(assetPath))
                    UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [TestCase((int)EditorIconMode.Badge)]
        [TestCase((int)EditorIconMode.IconReplacement)]
        public void NativeBridge_AppliesEverySupportedMode(int rawMode)
        {
            string iconPath = Path.Combine(m_package.resolvedPath, "ProjectIcon.png");
            Assert.That(File.Exists(iconPath), Is.True);
            var mode = (EditorIconMode)rawMode;

            try
            {
                Assert.That(
                    EditorIconNative.TrySetApplicationIcon(iconPath, mode, out string error),
                    Is.True,
                    error);
            }
            finally
            {
                Assert.That(
                    EditorIconNative.TryResetApplicationIcon(out string resetError),
                    Is.True,
                    resetError);
            }
        }
#endif
    }
}
