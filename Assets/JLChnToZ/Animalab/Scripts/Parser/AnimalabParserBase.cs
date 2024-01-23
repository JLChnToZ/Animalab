using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;

#if UNITY_2020_2_OR_NEWER
using UnityEditor.AssetImporters;
#else
using UnityEditor.Experimental.AssetImporters;
#endif

using UnityObject = UnityEngine.Object;

namespace JLChnToZ.Animalab {
    public abstract class AnimalabParserBase : StackParser {
        internal static readonly char[] symbols = new [] { '<', '=', '>', '+', '-', '*', ',', '.', '&', '|', '!', '(', ')', '[', ']', ':', ';', '{', '}', '/' };
        public AssetImportContext assetImportContext;
        protected HashSet<string> importedNames;
        protected internal string mainAssetPath;
        protected Node nextNode;
        internal AnimatorController controller;
        internal AnimatorStateMachine stateMachine;
        internal AnimatorState state;
        internal StateMachineBehaviour behaviour;
        internal List<TransitionData> transitions;
        internal Dictionary<int, string> syncLayers;

        protected virtual string Hint => "";

        public AnimalabParserBase() {
            symbolOverride = symbols;
        }

        internal void PreParse(TokenType type, string token, bool hasLineBreak, int indentLevel) =>
            OnParse(type, token, hasLineBreak, indentLevel);

        protected override void OnAttach(StackParser parent) {
            base.OnAttach(parent);
            if (parent is AnimalabParserBase parentParser) {
                assetImportContext = parentParser.assetImportContext;
                mainAssetPath = parentParser.mainAssetPath;
                importedNames = parentParser.importedNames;
                controller = parentParser.controller;
                stateMachine = parentParser.stateMachine;
                state = parentParser.state;
                transitions = parentParser.transitions;
                behaviour = parentParser.behaviour;
            }
            nextNode = Node.Default;
        }

        protected override void OnDetech() {
            assetImportContext = null;
            mainAssetPath = null;
            importedNames = null;
            controller = null;
            stateMachine = null;
            state = null;
            transitions = null;
            behaviour = null;
            base.OnDetech();
        }

        protected T LoadAsset<T>(string path) where T : UnityObject =>
            LoadAsset(path, typeof(T)) as T;

        protected UnityObject LoadAsset(string path, Type type) {
            UnityObject asset = null;
            if (path.StartsWith("{") && path.EndsWith("}")) {
                var guid = path.Substring(1, path.Length - 2);
                path = AssetDatabase.GUIDToAssetPath(guid);
            }
            if (!string.IsNullOrEmpty(mainAssetPath)) {
                var baseUrl = new Uri($"file:///{mainAssetPath}/..");
                var combinedUrl = new Uri(baseUrl, path);
                var combinedPath = Uri.UnescapeDataString(combinedUrl.AbsolutePath);
                if (combinedPath.StartsWith("./")) combinedPath = combinedPath.Substring(2);
                else if (combinedPath.StartsWith("/")) combinedPath = combinedPath.Substring(1);
                var fragment = combinedUrl.Fragment;
                if (string.IsNullOrEmpty(fragment)) {
                    asset = AssetDatabase.LoadAssetAtPath(combinedPath, type);
                } else {
                    fragment = fragment.Substring(1);
                    var assets = AssetDatabase.LoadAllAssetsAtPath(combinedPath);
                    if (assets != null)
                        foreach (var a in assets)
                            if (a.GetType() == type && a.name == fragment) {
                                asset = a;
                                break;
                            }
                }
                if (asset != null) path = combinedPath;
            }
            if (asset == null) asset = AssetDatabase.LoadAssetAtPath(path, type);
            if (asset != null && assetImportContext != null)
                assetImportContext.DependsOnSourceAsset(path);
            return asset;
        }

        protected void SaveAsset(UnityObject obj) {
            if (obj == null) {
                Debug.LogWarning("Trying to save null object.");
                return;
            }
            if (assetImportContext != null) {
                var name = string.IsNullOrEmpty(obj.name) ?
                    Guid.NewGuid().ToString() :
                    $"{string.Join("/", Array.ConvertAll(CopyStack(), ParserToString))}/{obj.name}";
                var otherName = name;
                int i = 0;
                while (!importedNames.Add(otherName))
                    otherName = $"{name}{++i}";
                assetImportContext.AddObjectToAsset(otherName, obj);
            } else if (!string.IsNullOrEmpty(mainAssetPath))
                AssetDatabase.AddObjectToAsset(obj, mainAssetPath);
        }

        static string ParserToString(StackParser parser) {
            if (parser is AnimalabParserBase animalabParserBase)
                return animalabParserBase.Hint;
            return "";
        }

        protected void Attach<T>() where T : AnimalabParserBase, new() {
            var parser = new T();
            parser.Attach(this);
            nextNode = Node.Identifier;
        }

        protected void Attach<T>(TokenType type, string token, bool hasLineBreak, int indentLevel) where T : AnimalabParserBase, new() {
            var parser = new T();
            parser.Attach(this);
            parser.PreParse(type, token, hasLineBreak, indentLevel);
            nextNode = Node.Identifier;
        }

        public enum Node {
            // Shared
            Unknown,
            Default,
            OpenBrace,
            Identifier,

            // Layer
            DefaultState,
            Weight,
            IKPass,
            Mask,
            SyncLayer,
            State,

            // State
            Speed,
            CycleOffset,
            Mirror,
            IKOnFeet,
            WriteDefaults,
            Tag,
            Clip,
            BlendTree,
            TypeName,

            // Condition & Parameter
            Parameter,
            Operator,
            Value,
            Next,
            OrderedInterrput,

            // Transition
            If,
            ExitTime,
            Duration,
            TimeOffset,
            Goto,
            GotoOrNext,

            Threhold,
            AfterParameter,
        }
    }
}