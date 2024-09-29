#if UNITY_EDITOR
#if !UNITY_2022_1_OR_NEWER
using System.Reflection;
#endif
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

public class SerializedReorderableList : ReorderableList {
    static GUIContent headerContent;
#if !UNITY_2022_1_OR_NEWER
    // SerializedProperty.gradientValue is internal before Unity 2022.1
    static readonly PropertyInfo gradientValueField = typeof(SerializedProperty)
        .GetProperty("gradientValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
#endif

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
    }

    static void OnRemove(ReorderableList list) {
        var property = list.serializedProperty;
        int index = list.index, count = property.arraySize;
        if (index < 0 || index >= count) index = count - 1;
        property.DeleteArrayElementAtIndex(index);
#if !UNITY_2021_2_OR_NEWER
        // In older versions, DeleteArrayElementAtIndex does not shift the array elements and just clears the contents at the index if it is not empty
        // So we need check if the array size is the same as before and if so, delete the element again
        if (property.arraySize == count) property.DeleteArrayElementAtIndex(index);
#endif
        if (list.index == index) list.index--;
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
    }

    protected void ToggleExpand(int index) {
        if (index < 0 || index >= serializedProperty.arraySize) return;

    }

    protected virtual void DrawHeader(Rect rect) {
        headerContent ??= new GUIContent();
        headerContent.text = serializedProperty.displayName;
        headerContent.tooltip = serializedProperty.tooltip;
        EditorGUI.LabelField(rect, headerContent, EditorStyles.boldLabel);
    }

    protected virtual float GetElementHeight(int index) =>
        EditorGUI.GetPropertyHeight(serializedProperty.GetArrayElementAtIndex(index), GUIContent.none);

    protected virtual void DrawElement(Rect rect, int index, bool isActive, bool isFocused) =>
        EditorGUI.PropertyField(rect, serializedProperty.GetArrayElementAtIndex(index), GUIContent.none);
}
#endif