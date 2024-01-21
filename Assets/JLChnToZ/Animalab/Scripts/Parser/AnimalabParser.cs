using System;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor.Animations;
using System.IO;

namespace JLChnToZ.Animalab {
    public class AnimalabParser : AnimalabParserBase {
        protected override void OnParse(TokenType type, string token, bool hasLineBreak, int indentLevel) {
            switch (type) {
                case TokenType.Identifier:
                    switch (token) {
                        case "layer": Attach<LayerParser>(); return;
                    }
                    if (Enum.TryParse(token, true, out AnimatorControllerParameterType _)) {
                        Attach<VariableParser>(type, token, hasLineBreak, indentLevel);
                        return;
                    }
                    break;
            }
            throw new Exception($"Unexpected token.");
        }

        protected override void OnAttach(StackParser parent) {
            base.OnAttach(parent);
            if (parent == null) {
                transitions = new List<TransitionData>();
                syncLayers = new Dictionary<int, string>();
                importedNames = new HashSet<string>();
            }
            controller = new AnimatorController();
            SaveAsset(controller);
            if (assetImportContext != null)
                controller.name = Path.GetFileNameWithoutExtension(assetImportContext.assetPath);
        }

        protected override void OnDetech() {
            var layerMap = new Dictionary<string, int>();
            var layers = controller.layers;
            for (int i = 0; i < layers.Length; i++) {
                var layer = layers[i];
                layerMap[layer.name] = i;
            }
            foreach (var kv in syncLayers)
                if (layerMap.TryGetValue(kv.Value, out var layerIndex))
                    controller.layers[kv.Key].syncedLayerIndex = layerIndex;
                else
                    Debug.LogWarning($"Sync layer \"{kv.Value}\" not found.");
            if (assetImportContext != null)
                assetImportContext.SetMainObject(controller);
            controller = null;
            transitions = null;
            syncLayers = null;
            importedNames = null;
            base.OnDetech();
        }
    }
}