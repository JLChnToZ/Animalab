#if UNITY_EDITOR && !ZSTRING_INCLUDED
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;

namespace JLChnToZ.MathUtilities {
    public static class ZStringDetector {
        
        [InitializeOnLoadMethod]
        static void DetectZStringInstalled() {
            if (Type.GetType("Cysharp.Text.ZString, ZString") == null) return;
            PlayerSettings.SetScriptingDefineSymbolsForGroup(
                (BuildTargetGroup)typeof(EditorUserBuildSettings).GetProperty("activeBuildTargetGroup", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).GetValue(null),
                $"{PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone)};ZSTRING_INCLUDED"
            );
            CompilationPipeline.RequestScriptCompilation();
        }
    }
}
#endif