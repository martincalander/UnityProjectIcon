using System;
using System.Collections.Generic;
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
        readonly List<Texture2D> m_textures = new List<Texture2D>();
        PackageInfo m_package;
        string m_generatedIconPath;

        [OneTimeSetUp]
        public void ResolvePackage()
        {
            m_package = PackageInfo.FindForAssetPath($"Packages/{PackageName}/package.json");
            Assert.That(m_package, Is.Not.Null);
        }

        [TearDown]
        public void CleanupTextures()
        {
            foreach (Texture2D texture in m_textures)
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            m_textures.Clear();
            if (m_generatedIconPath != null)
                File.Delete(m_generatedIconPath);
            m_generatedIconPath = null;
        }

        [Test]
        public void Package_UsesProjectIconIdentity()
        {
            Assert.That(m_package.name, Is.EqualTo(PackageName));
            Assert.That(m_package.displayName, Is.EqualTo("Project Icon"));
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(
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
        public void ReadableCopy_ReusesUnchangedTextureAndRegeneratesAfterUpdatesOrDeletion()
        {
            Texture2D texture = CreateTexture(Color.red);
            string path = WriteReadableCopy(texture);
            byte[] original = File.ReadAllBytes(path);
            DateTime marker = File.GetLastWriteTimeUtc(path);
            Assert.That(WriteReadableCopy(texture), Is.EqualTo(path));
            Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(marker), "An unchanged icon must not be rewritten.");

            texture.SetPixel(0, 0, Color.blue);
            texture.Apply();
            WriteReadableCopy(texture);
            Assert.That(File.ReadAllBytes(path), Is.Not.EqualTo(original), "Texture updates invalidate the cached PNG.");

            marker = File.GetLastWriteTimeUtc(path);
            byte[] expected = File.ReadAllBytes(path);
            byte[] corrupted = (byte[])expected.Clone();
            corrupted[0] ^= 1;
            File.WriteAllBytes(path, corrupted);
            File.SetLastWriteTimeUtc(path, marker);
            // Keep the file fingerprint unchanged so only the texture hash triggers regeneration.
            texture.imageContentsHash = Hash128.Compute("reimported icon");
            WriteReadableCopy(texture);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(expected), "Imported content changes invalidate the cache.");

            File.WriteAllText(path, "overwritten cache");
            WriteReadableCopy(texture);
            Assert.That(File.ReadAllBytes(path).Length, Is.GreaterThan("overwritten cache".Length),
                "An overwritten cache file must be regenerated.");

            File.Delete(path);
            WriteReadableCopy(texture);
            Assert.That(File.Exists(path), Is.True, "Deleting the Library cache must recover automatically.");
        }

        [TestCase(TextureFormat.R8)]
        [TestCase(TextureFormat.R16)]
        [TestCase(TextureFormat.RHalf)]
        [TestCase(TextureFormat.RFloat)]
        public void ReadableCopy_SingleChannelTextureRemainsGrayscale(TextureFormat format)
        {
            Texture2D texture = CreateTexture(Color.white, format);
            Color pixel = ReadPixel(WriteReadableCopy(texture));
            Assert.That(pixel.r, Is.GreaterThan(0.95f));
            Assert.That(pixel.g, Is.GreaterThan(0.95f), "The red channel must be encoded as grayscale.");
            Assert.That(pixel.b, Is.GreaterThan(0.95f), "The red channel must be encoded as grayscale.");
        }

        [Test]
        public void ReadableCopy_DifferentTextureInvalidatesCache()
        {
            Texture2D first = CreateTexture(Color.red);
            Texture2D second = CreateTexture(Color.blue);
            byte[] original = File.ReadAllBytes(WriteReadableCopy(first));
            Assert.That(File.ReadAllBytes(WriteReadableCopy(second)), Is.Not.EqualTo(original));
        }

        [Test]
        public void ReadableCopy_UnreadableTextureUsesGpuWithoutChangingActiveTarget()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("GPU readback requires an Editor with a graphics device.");

            Texture2D texture = CreateTexture(Color.blue, readable: false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture sentinel = RenderTexture.GetTemporary(8, 8);
            try
            {
                RenderTexture.active = sentinel;
                string path = WriteReadableCopy(texture);
                Assert.That(RenderTexture.active, Is.SameAs(sentinel));
                Color pixel = ReadPixel(path);
                Assert.That(pixel.b, Is.GreaterThan(0.95f));
                Assert.That(pixel.r, Is.LessThan(0.05f));
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(sentinel);
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
        public void NativeBridge_AppliesSourceImagesAndTextureFallbackForEveryMode(int rawMode)
        {
            string assetPath = AssetDatabase.GenerateUniqueAssetPath("Assets/ProjectIconFallbackTest.asset");
            Texture2D texture = CreateTexture(Color.red);
            var mode = (EditorIconMode)rawMode;
            try
            {
                Assert.That(EditorIconNative.TrySetApplicationIcon(
                    Path.Combine(m_package.resolvedPath, "ProjectIcon.png"), mode, out string error), Is.True, error);
                AssetDatabase.CreateAsset(texture, assetPath);
                Assert.That(EditorIconNative.TrySetApplicationIcon(
                    Path.GetFullPath(assetPath), mode, out _, out bool imageRejected), Is.False);
                Assert.That(imageRejected, Is.True, "A source-format rejection permits texture conversion.");
                Assert.That(EditorIconApplier.TryApplyIcon(texture, mode, out error), Is.True, error);
                string cachedPath = WriteReadableCopy(texture);
                DateTime marker = File.GetLastWriteTimeUtc(cachedPath);
                Assert.That(EditorIconApplier.TryApplyIcon(texture, mode, out error), Is.True, error);
                Assert.That(File.GetLastWriteTimeUtc(cachedPath), Is.EqualTo(marker), "Repeated native fallback must reuse the PNG.");
            }
            finally
            {
                bool reset = EditorIconNative.TryResetApplicationIcon(out string resetError);
                if (!AssetDatabase.DeleteAsset(assetPath))
                    UnityEngine.Object.DestroyImmediate(texture);
                Assert.That(reset, Is.True, resetError);
            }
        }
#endif

        Texture2D CreateTexture(Color color, TextureFormat format = TextureFormat.RGBA32, bool readable = true)
        {
            var texture = new Texture2D(4, 4, format, false);
            m_textures.Add(texture);
            var pixels = new Color[texture.width * texture.height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            texture.SetPixels(pixels);
            texture.Apply(false, !readable);
            return texture;
        }

        string WriteReadableCopy(Texture2D texture)
        {
            Assert.That(EditorIconApplier.TryWriteReadableIconCopy(texture, out string path, out string error), Is.True, error);
            m_generatedIconPath = path;
            return path;
        }

        Color ReadPixel(string path)
        {
            var decoded = new Texture2D(2, 2);
            m_textures.Add(decoded);
            Assert.That(decoded.LoadImage(File.ReadAllBytes(path)), Is.True);
            return decoded.GetPixel(0, 0);
        }
    }
}
