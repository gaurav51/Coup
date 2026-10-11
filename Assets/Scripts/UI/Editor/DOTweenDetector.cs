using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Coup.EditorTools
{
    /// <summary>
    /// Flips the USE_DOTWEEN scripting define on or off depending on whether DOTween
    /// is actually in the project. DOTween ships as an Asset Store package rather than
    /// a registry package, so it cannot be an asmdef versionDefine; this keeps the
    /// build green before it is imported and switches CoupTween over the moment it is.
    /// </summary>
    [InitializeOnLoad]
    public static class DOTweenDetector
    {
        private const string Define = "USE_DOTWEEN";

        static DOTweenDetector() => EditorApplication.delayCall += Sync;

        [MenuItem("Coup/Re-check DOTween", priority = 40)]
        public static void Sync()
        {
            // Asked of the asset database rather than AppDomain: DOTween arrives as
            // an imported folder, and enumerating loaded assemblies can hand back
            // ones Unity has already unloaded.
            var present = AssetDatabase.FindAssets("DOTween")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Any(path => path.EndsWith("/DOTween.dll")
                          || path.EndsWith("/DOTween.asmdef")
                          || path.EndsWith("/DOTween.cs"));

            var target = NamedBuildTarget.FromBuildTargetGroup(
                BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));

            var defines = PlayerSettings.GetScriptingDefineSymbols(target)
                .Split(';')
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .ToList();

            var has = defines.Contains(Define);
            if (present == has) return;

            if (present) defines.Add(Define);
            else defines.Remove(Define);

            PlayerSettings.SetScriptingDefineSymbols(target, string.Join(";", defines));
            Debug.Log($"[Coup] DOTween {(present ? "detected — enabling" : "not found — disabling")} {Define}.");
        }
    }
}
