#if UNITY_EDITOR
using System;
#if !UNITY_2022_1_OR_NEWER
using System.Reflection;
#endif
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace JLChnToZ.CommonUtils {
    public class SerializedReorderableList : ReorderableList {
        static GUIContent headerContent;
    #if !UNITY_2022_1_OR_NEWER
        // SerializedProperty.gradientValue is internal before Unity 2022.1
        static readonly PropertyInfo gradientValueField = typeof(SerializedProperty)
            .GetProperty("gradientValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    #endif
        bool[] expandedIndices;

        static void OnAdd(ReorderableList list) {
            var property = list.serializedProperty;
            int index = list.index + 1, count = property.arraySize;
            if (index < 0 || index > count) index = count;
            property.InsertArrayElementAtIndex(index);
            var element = property.GetArrayElementAtIndex(index);
            int depth = element.depth;
            do {
                if (element.isArray) {
                    element.ClearArray();
                    continue;
                }
                switch (element.propertyType) {
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.LayerMask:
                    case SerializedPropertyType.ArraySize:
                    case SerializedPropertyType.Character: element.intValue = default; break;
                    case SerializedPropertyType.Boolean: element.boolValue = default; break;
                    case SerializedPropertyType.Float: element.floatValue = default; break;
                    case SerializedPropertyType.String: element.stringValue = ""; break;
                    case SerializedPropertyType.Color: element.colorValue = Color.black; break;
                    case SerializedPropertyType.ObjectReference: element.objectReferenceValue = default; break;
                    case SerializedPropertyType.Enum: element.enumValueIndex = default; break;
                    case SerializedPropertyType.Vector2: element.vector2Value = default; break;
                    case SerializedPropertyType.Vector3: element.vector3Value = default; break;
                    case SerializedPropertyType.Vector4: element.vector4Value = default; break;
                    case SerializedPropertyType.Rect: element.rectValue = default; break;
                    case SerializedPropertyType.AnimationCurve: element.animationCurveValue = default; break;
                    case SerializedPropertyType.Bounds: element.boundsValue = default; break;
                    case SerializedPropertyType.Gradient:
    #if UNITY_2022_1_OR_NEWER
                        element.gradientValue = default;
    #else
                        gradientValueField.SetValue(element, default);
    #endif
                        break;
                    case SerializedPropertyType.Quaternion: element.quaternionValue = Quaternion.identity; break;
                    case SerializedPropertyType.ExposedReference: element.exposedReferenceValue = default; break;
                    case SerializedPropertyType.Vector2Int: element.vector2IntValue = default; break;
                    case SerializedPropertyType.Vector3Int: element.vector3IntValue = default; break;
                    case SerializedPropertyType.RectInt: element.rectIntValue = default; break;
                    case SerializedPropertyType.BoundsInt: element.boundsIntValue = default; break;
    #if UNITY_2019_3_OR_NEWER
                    case SerializedPropertyType.ManagedReference: element.managedReferenceValue = default; break;
    #endif
    #if UNITY_2021_1_OR_NEWER
                    case SerializedPropertyType.Hash128: element.hash128Value = default; break;
    #endif
                }
            } while (element.Next(element.propertyType == SerializedPropertyType.Generic) && element.depth > depth);
            list.index = index;
            if (list is SerializedReorderableList srl) srl.OnAdd();
        }

        static void OnRemove(ReorderableList list) {
            var property = list.serializedProperty;
            int index = list.index, count = property.arraySize;
            if (index < 0 || index >= count) index = count - 1;
            property.DeleteArrayElementAtIndex(index);
            if (property.arraySize == count) property.DeleteArrayElementAtIndex(index);
            if (list.index == index) list.index--;
            if (list is SerializedReorderableList srl) srl.OnRemove();
        }

        static void OnReorder(ReorderableList list, int oldIndex, int newIndex) {
            if (list is SerializedReorderableList srl) srl.OnReorder(oldIndex, newIndex);
        }

        public SerializedReorderableList(
            SerializedProperty elements
        ) : this(elements.serializedObject, elements, true, true, true, true) { }

        public SerializedReorderableList(
            SerializedObject serializedObject, SerializedProperty elements
        ) : this(serializedObject, elements, true, true, true, true) { }

        public SerializedReorderableList(
            SerializedObject serializedObject, SerializedProperty elements,
            bool draggable, bool displayHeader, bool displayAddButton, bool displayRemoveButton
        ) : base(serializedObject, elements, draggable, displayHeader, displayAddButton, displayRemoveButton) {
            drawHeaderCallback = DrawHeader;
            drawElementCallback = DrawElement;
            elementHeightCallback = GetElementHeight;
            onAddCallback = OnAdd;
            onRemoveCallback = OnRemove;
            onReorderCallbackWithDetails = OnReorder;
        }

        protected bool IsExpanded(int index) => expandedIndices != null && index >= 0 && index < expandedIndices.Length && expandedIndices[index];

        protected void ToggleExpand(int index) {
            if (index < 0) return;
            if (expandedIndices == null || index >= expandedIndices.Length) {
                var newExpandedIndices = new bool[Mathf.Max(4, Mathf.NextPowerOfTwo(serializedProperty.arraySize))];
                if (expandedIndices != null) Array.Copy(expandedIndices, newExpandedIndices, expandedIndices.Length);
                expandedIndices = newExpandedIndices;
            }
            expandedIndices[index] = !expandedIndices[index];
        }

        protected virtual void DrawHeader(Rect rect) {
            if (headerContent == null) headerContent = new GUIContent();
            headerContent.text = serializedProperty.displayName;
            headerContent.tooltip = serializedProperty.tooltip;
            EditorGUI.LabelField(rect, headerContent, EditorStyles.boldLabel);
        }

        protected virtual float GetElementHeight(int index) =>
            EditorGUI.GetPropertyHeight(serializedProperty.GetArrayElementAtIndex(index), GUIContent.none, IsExpanded(index));

        protected virtual void DrawElement(Rect rect, int index, bool isActive, bool isFocused) {
            if (EditorGUI.PropertyField(rect, serializedProperty.GetArrayElementAtIndex(index), GUIContent.none, IsExpanded(index)))
                ToggleExpand(index);
        }

        protected virtual void OnAdd() {}

        protected virtual void OnRemove() {
            if (expandedIndices == null) return;
            int maxIndex = Mathf.Min(expandedIndices.Length, serializedProperty.arraySize + 1);
            if (index < maxIndex)
                Array.Copy(expandedIndices, index + 1, expandedIndices, index, maxIndex - index - 1);
            else
                expandedIndices[index] = false;
        }

        protected virtual void OnReorder(int oldIndex, int newIndex) {
            if (expandedIndices == null) return;
            if (oldIndex < newIndex) {
                if (oldIndex >= expandedIndices.Length) return;
                var expanded = expandedIndices[oldIndex];
                Array.Copy(expandedIndices, oldIndex + 1, expandedIndices, oldIndex, Mathf.Min(newIndex, expandedIndices.Length) - oldIndex - 1);
                if (newIndex < expandedIndices.Length) expandedIndices[newIndex] = expanded;
                return;
            }
            if (oldIndex > newIndex) {
                if (newIndex >= expandedIndices.Length) return;
                var expanded = oldIndex < expandedIndices.Length && expandedIndices[oldIndex];
                Array.Copy(expandedIndices, newIndex, expandedIndices, newIndex + 1, Mathf.Min(oldIndex, expandedIndices.Length) - newIndex - 1);
                expandedIndices[newIndex] = expanded;
                return;
            }
        }
    }
}
#endif