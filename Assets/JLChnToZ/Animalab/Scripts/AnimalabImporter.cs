using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using System;

#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif

// To prefent conflict with System.Object, in case `using System;` is added.
using UnityObject = UnityEngine.Object;

namespace JLChnToZ.Animalab {
    [ScriptedImporter(1, "animalab")]
    public class AnimalabImporter : ScriptedImporter {
        [SerializeField] internal bool hasError;
        [SerializeField] internal int errorRow, errorCol;
        [SerializeField] internal string errorMessage;

        public override void OnImportAsset(AssetImportContext ctx) {
            hasError = false;
            errorRow = errorCol = -1;
            errorMessage = "";
            try {
                var parser = new AnimalabParser {
                    assetImportContext = ctx,
                    mainAssetPath = ctx.assetPath,
                };
                parser.Parse(File.ReadAllText(ctx.assetPath));
            } catch (ParseException ex) {
                hasError = true;
                errorRow = ex.Row;
                errorCol = ex.Col;
                var innerException = ex.InnerException;
                if (innerException != null)
                    errorMessage = innerException.Message;
                else
                    errorMessage = ex.Message;
                throw;
            } catch (Exception ex) {
                hasError = true;
                errorMessage = ex.Message;
                throw;
            }
        }

        [MenuItem("Assets/Create/Animalab Controller")]
        static void CreateAsset() {
            string name = "New Animalab Controller";
            string content = "layer \"Default Layer\" {\n}";
            if (Selection.activeObject is AnimatorController controller) {
                var orgName = controller.name;
                if (!string.IsNullOrWhiteSpace(orgName))
                    name = $"{orgName} Converted";
                content = Unparser.Unparse(controller);
            }
            ProjectWindowUtil.CreateAssetWithContent($"{name}.animalab", content);
        }

        [MenuItem("CONTEXT/StateMachineBehaviour/Copy Serialized Code for Animalab")]
        static void CopyStateMachineBehaviourCode(MenuCommand command) {
            var behaviour = command.context as StateMachineBehaviour;
            if (behaviour == null) return;
            var content = Unparser.Unparse(behaviour);
            EditorGUIUtility.systemCopyBuffer = content;
        }
    }

    [CustomEditor(typeof(AnimalabImporter))]
    public class AnimalabImporerEditor : AssetImporterEditor {
        const string message = "Animalab Controllers can only be edited using text editors.\n" +
            "If you need to adjust it with Unity's Animator Editor, you may extract it as a copy, " +
            "but that copy will no longer can be edited with text editors.";

        public override void OnInspectorGUI() {
            var target = this.target as AnimalabImporter;
            EditorGUILayout.HelpBox(message, MessageType.Info);
            using (new EditorGUI.DisabledScope(target.hasError))
            using (new EditorGUILayout.HorizontalScope()) {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Extract as a Copy", GUILayout.ExpandWidth(false)))
                    ExtractAsACopy();
            }
            EditorGUILayout.Space();
            if (target.hasError) {
                if (target.errorRow >= 0 && target.errorCol >= 0) {
                    EditorGUILayout.HelpBox($"Error at line {target.errorRow + 1}, column {target.errorCol + 1}:\n{target.errorMessage}", MessageType.Error);
                    using (new EditorGUILayout.HorizontalScope()) {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Open in Text Editor", GUILayout.ExpandWidth(false)))
                            AssetDatabase.OpenAsset(target, target.errorRow + 1, target.errorCol);
                        if (GUILayout.Button("Reimport", GUILayout.ExpandWidth(false)))
                            AssetDatabase.ImportAsset(target.assetPath);
                    }
                } else {
                    EditorGUILayout.HelpBox($"Error: {target.errorMessage}", MessageType.Error);
                    using (new EditorGUILayout.HorizontalScope()) {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Reimport", GUILayout.ExpandWidth(false)))
                            AssetDatabase.ImportAsset(target.assetPath);
                    }
                }
            }
            ApplyRevertGUI();
        }

        void ExtractAsACopy() {
            var importer = target as AnimalabImporter;
            var path = importer.assetPath;
            path = EditorUtility.SaveFilePanelInProject(
                "Extract as a Copy",
                Path.GetFileNameWithoutExtension(path),
                "controller",
                "Extract as a Copy",
                Path.GetDirectoryName(path)
            );
            if (string.IsNullOrEmpty(path)) return;
            var objMap = new Dictionary<UnityObject, UnityObject>();
            var pendingProcess = new Queue<UnityObject>();
            var org = AssetDatabase.LoadAssetAtPath<AnimatorController>(importer.assetPath);
            var clone = InstantiateWithFix(org);
            AssetDatabase.CreateAsset(clone, path);
            objMap[org] = clone;
            pendingProcess.Enqueue(clone);
            while (pendingProcess.Count > 0)
                using (var so = new SerializedObject(pendingProcess.Dequeue())) {
                    var iterator = so.GetIterator();
                    while (iterator.Next(true)) {
                        if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                        var value = iterator.objectReferenceValue;
                        if (value == null || !AssetDatabase.IsForeignAsset(value)) continue;
                        if (!objMap.TryGetValue(value, out var cloneObj)) {
                            cloneObj = InstantiateWithFix(value);
                            AssetDatabase.AddObjectToAsset(cloneObj, clone);
                            objMap[value] = cloneObj;
                            pendingProcess.Enqueue(cloneObj);
                        }
                        iterator.objectReferenceValue = cloneObj;
                    }
                    so.ApplyModifiedProperties();
                }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        static T InstantiateWithFix<T>(T original) where T : UnityObject {
            var clone = Instantiate(original);
            clone.name = original.name;
            clone.hideFlags = original.hideFlags & ~HideFlags.NotEditable;
            return clone;
        }
    }
}