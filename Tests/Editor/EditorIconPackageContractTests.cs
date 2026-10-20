using System.IO;
using Essentials.EditorIcon.Editor;
using NUnit.Framework;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Essentials.Tests.Editor.EditorIcon
{
    public sealed class EditorIconPackageContractTests
    {
        const string PackageName = "com.martincalander.projecticon";
        const string NativeBundlePath =
            "Packages/com.martincalander.projecticon/Editor/Plugins/macOS/EssentialsEditorIcon.bundle";
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
                $"Packages/{PackageName}/EssentialsIcon.png"), Is.Not.Null);
        }

        [TestCase("Editor/Essentials.EditorIcon.Editor.asmdef")]
        [TestCase("Tests/Editor/Essentials.EditorIcon.Editor.Tests.asmdef")]
        public void Assemblies_SupportProjectIconAndLegacyDisableSymbols(string relativePath)
        {
            string json = File.ReadAllText(Path.Combine(m_package.resolvedPath, relativePath));
            StringAssert.Contains("\"!ESSENTIALS_DISABLE_PROJECT_ICON\"", json);
            StringAssert.Contains("\"!ESSENTIALS_DISABLE_EDITOR_ICON\"", json);
            StringAssert.Contains("\"!ESSENTIALS_DISABLE_ALL\"", json);
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
        public void NativeBundle_ContainsStableExecutableAndMetadata()
        {
            string bundleRoot = Path.Combine(
                m_package.resolvedPath,
                "Editor/Plugins/macOS/EssentialsEditorIcon.bundle");
            string executablePath = Path.Combine(bundleRoot, "Contents/MacOS/EssentialsEditorIcon");
            string infoPath = Path.Combine(bundleRoot, "Contents/Info.plist");

            Assert.That(File.Exists(executablePath), Is.True, "The checked-in universal executable is missing.");
            Assert.That(File.Exists(infoPath), Is.True, "The native bundle Info.plist is missing.");

            string info = File.ReadAllText(infoPath);
            StringAssert.Contains("com.essentials.editoricon.native", info);
            StringAssert.Contains("<string>EssentialsEditorIcon</string>", info);
        }

        [Test]
        public void NativeContract_RetainsLegacyEntryPointAndAddsModeAwareEntryPoint()
        {
            string source = File.ReadAllText(Path.Combine(
                m_package.resolvedPath,
                "Native~/macOS/EssentialsEditorIcon.mm"));
            string buildScript = File.ReadAllText(Path.Combine(
                m_package.resolvedPath,
                "Native~/macOS/build.sh"));

            StringAssert.Contains("EssentialsEditorIconSetApplicationIcon(const char *utf8Path)", source);
            StringAssert.Contains("EssentialsEditorIconSetApplicationIconWithMode", source);
            StringAssert.Contains("EssentialsEditorIconResetApplicationIcon", source);
            StringAssert.Contains("_EssentialsEditorIconSetApplicationIcon$", buildScript);
            StringAssert.Contains("_EssentialsEditorIconSetApplicationIconWithMode$", buildScript);
            StringAssert.Contains("_EssentialsEditorIconResetApplicationIcon$", buildScript);
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
            string iconPath = Path.Combine(m_package.resolvedPath, "EssentialsIcon.png");
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
