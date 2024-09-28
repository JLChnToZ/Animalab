using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityObject = UnityEngine.Object;

namespace JLChnToZ.AnimatorBehaviours {
    /// <summary>Sets the weight of a layer.</summary>
    public class LayerDriver : AnimatorDriverBase {
        [SerializeField] int layer;
        [SerializeField, Range(0, 1)] float weight = 1;
        [SerializeField] float lerpTime;

        protected override async UniTaskVoid RunCore(
            Animator animator,
            StateMachineTiming timing,
            AnimatorStateInfo stateInfo,
            int layerIndex,
            int pathHash
        ) {
            await Delay(out var cancellationToken);
            if (lerpTime > 0)
                for (float t = 0, startWeight = animator.GetLayerWeight(layer); t < lerpTime; t += Time.deltaTime) {
                    animator.SetLayerWeight(layer, Mathf.Lerp(startWeight, weight, t / lerpTime));
                    await UniTask.Yield(cancellationToken);
                }
            animator.SetLayerWeight(layer, weight);
        }

#if UNITY_EDITOR
        [CustomEditor(typeof(LayerDriver))]
        class _Editor : AnimatorDriverBaseEditor {
            readonly Dictionary<UnityObject, string[]> layers = new Dictionary<UnityObject, string[]>();
            SerializedProperty layerProperty, weightProperty, lerpTimeProperty;

            protected override void OnEnable() {
                base.OnEnable();
                layerProperty = serializedObject.FindProperty(nameof(layer));
                weightProperty = serializedObject.FindProperty(nameof(weight));
                lerpTimeProperty = serializedObject.FindProperty(nameof(lerpTime));
            }

            public override void OnInspectorGUI() {
                serializedObject.Update();
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Sets the weight of a layer.", MessageType.None);
                EditorGUILayout.Space();
                DrawBaseFields();
                EditorGUILayout.Space();
                if (!layers.TryGetValue(target, out var layerNames)) {
                    var layerArray = GetAnimatorController(target).layers;
                    layerNames = new string[layerArray.Length];
                    for (var i = 0; i < layerArray.Length; i++)
                        layerNames[i] = layerArray[i].name;
                    layers.Add(target, layerNames);
                }
                var layerRect = EditorGUILayout.GetControlRect();
                using (new EditorGUI.PropertyScope(layerRect, null, layerProperty))
                    layerProperty.intValue = EditorGUI.Popup(layerRect, layerProperty.displayName, layerProperty.intValue, layerNames);
                EditorGUILayout.PropertyField(weightProperty);
                EditorGUILayout.PropertyField(lerpTimeProperty);
                serializedObject.ApplyModifiedProperties();
            }
        }
#endif
    }
}
