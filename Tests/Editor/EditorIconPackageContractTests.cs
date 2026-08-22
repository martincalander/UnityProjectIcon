using System.IO;
using System.Reflection;
using Essentials.EditorIcon.Editor;
using NUnit.Framework;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Essentials.Tests.Editor.EditorIcon
{
    public sealed class EditorIconPackageContractTests
    {
        const string PackageName = "com.essentials.projecticon";
        const string NativeBundlePath =
            "Packages/com.essentials.projecticon/Editor/Plugins/macOS/EssentialsEditorIcon.bundle";
        const string NativeBundleGuid = "061098808649b4ba88a30729526dbde4";
        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void Package_UsesProjectIconIdentity()
        {
            PackageInfo package = PackageInfo.FindForAssetPath("Packages/com.essentials.projecticon/package.json");

            Assert.That(package, Is.Not.Null);
            Assert.That(package.name, Is.EqualTo(PackageName));
            Assert.That(package.version, Is.EqualTo("0.2.0"));
            Assert.That(package.displayName, Is.EqualTo("Project Icon"));
        }

        [TestCase("Editor/Essentials.EditorIcon.Editor.asmdef")]
        [TestCase("Tests/Editor/Essentials.EditorIcon.Editor.Tests.asmdef")]
        public void Assemblies_SupportProjectIconAndLegacyDisableSymbols(string relativePath)
        {
            PackageInfo package = PackageInfo.FindForAssetPath("Packages/com.essentials.projecticon/package.json");
            Assert.That(package, Is.Not.Null);

            string json = File.ReadAllText(Path.Combine(package.resolvedPath, relativePath));
            StringAssert.Contains("\"!ESSENTIALS_DISABLE_PROJECT_ICON\"", json);
            StringAssert.Contains("\"!ESSENTIALS_DISABLE_EDITOR_ICON\"", json);
            StringAssert.Contains("\"!ESSENTIALS_DISABLE_ALL\"", json);
        }

        [TestCase("Tools/Essentials/Editor Icon/Apply Player Icon")]
        [TestCase("Tools/Essentials/Editor Icon/Reset")]
        public void LegacyMenuItemPath_IsNotRegistered(string removedPath)
        {
            foreach (MethodInfo method in typeof(EditorIconApplier).GetMethods(AnyStatic))
            {
                foreach (CustomAttributeData attribute in method.GetCustomAttributesData())
                {
                    if (attribute.AttributeType != typeof(MenuItem) || attribute.ConstructorArguments.Count == 0)
                        continue;

                    Assert.That(attribute.ConstructorArguments[0].Value, Is.Not.EqualTo(removedPath),
                        $"{method.Name} should not register the removed menu path '{removedPath}'.");
                }
            }
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
            PackageInfo package = PackageInfo.FindForAssetPath(NativeBundlePath);
            Assert.That(package, Is.Not.Null);
            Assert.That(package.name, Is.EqualTo(PackageName));
            Assert.That(package.version, Is.EqualTo("0.2.0"));

            string bundleRoot = Path.Combine(
                package.resolvedPath,
                "Editor/Plugins/macOS/EssentialsEditorIcon.bundle");
            string executablePath = Path.Combine(bundleRoot, "Contents/MacOS/EssentialsEditorIcon");
            string infoPath = Path.Combine(bundleRoot, "Contents/Info.plist");

            Assert.That(File.Exists(executablePath), Is.True, "The checked-in universal executable is missing.");
            Assert.That(File.Exists(infoPath), Is.True, "The native bundle Info.plist is missing.");

            string info = File.ReadAllText(infoPath);
            StringAssert.Contains("com.essentials.editoricon.native", info);
            StringAssert.Contains("<string>EssentialsEditorIcon</string>", info);
            StringAssert.Contains("<string>0.2.0</string>", info);
        }
    }
}
