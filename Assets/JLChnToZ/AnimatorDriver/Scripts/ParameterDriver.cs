using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
using JLChnToZ.CommonUtils;
#endif

using UnityObject = UnityEngine.Object;
using UnityRandom = UnityEngine.Random; 
using ACParameter = UnityEngine.AnimatorControllerParameter;
using ACParameterType = UnityEngine.AnimatorControllerParameterType;

namespace JLChnToZ.AnimatorBehaviours {
    /// <summary>Evaluate operations on parameters.</summary>
    public class ParameterDriver : AnimatorDriverBase {
        readonly HashSet<RuntimeAnimatorController> hasRecordedParameters =
            new HashSet<RuntimeAnimatorController>();
        readonly Dictionary<(RuntimeAnimatorController, int), ACParameterType> parameterTypes =
            new Dictionary<(RuntimeAnimatorController, int), ACParameterType>();

        [SerializeField] Operation[] operations;

        protected override async UniTaskVoid RunCore(
            Animator animator,
            StateMachineTiming timing,
            AnimatorStateInfo stateInfo,
            int layerIndex,
            int pathHash
        ) {
            await Delay(out _);
            if (animator == null) return;
            for (int i = 0; i < operations.Length; i++)
                operations[i].Apply(animator, this);
        }

        ACParameterType GetParameterType(Animator animator, int hash) {
            if (animator == null || hash == 0) return default;
            var controller = animator.runtimeAnimatorController;
            if (hasRecordedParameters.Add(controller))
                for (int i = 0, count = animator.parameterCount; i < count; i++) {
                    var param = animator.GetParameter(i);
                    parameterTypes[(controller, param.nameHash)] = param.type;
                }
            parameterTypes.TryGetValue((controller, hash), out var type);
            return type;
        }

        enum Operator : byte {
            [InspectorName("=")] Assign,
            [InspectorName("+")] Add,
            [InspectorName("-")] Subtract,
            [InspectorName("*")] Multiply,
            [InspectorName("÷")] Divide,
            [InspectorName("%")] Modulo,
            Random,
            Min, Max, Abs, Sign,
            Floor, Ceiling, Round, Truncate,
            Power, Log,
            Sin, Cos, Tan, Asin, Acos, Atan,
            Sinh, Cosh, Tanh, Asinh, Acosh, Atanh,
        }

        [Serializable]
        struct Operation {
            public string parameterName, otherParameterName;
            public Operator op;
            public float value;
            public bool dynamicValue;
            [NonSerialized] bool hasInitialized;
            [NonSerialized] int hash, otherHash;

            public void Apply(Animator animator, ParameterDriver parameterDriver) {
                try {
                    if (!hasInitialized) {
                        if (!string.IsNullOrEmpty(parameterName))
                            hash = Animator.StringToHash(parameterName);
                        if (!string.IsNullOrEmpty(otherParameterName))
                            otherHash = Animator.StringToHash(otherParameterName);
                        hasInitialized = true;
                    }
                    var param1Type = parameterDriver.GetParameterType(animator, hash);
                    double a, b;
                    switch (op) {
                        case Operator.Assign: case Operator.Random: case Operator.Abs: case Operator.Sign:
                        case Operator.Floor: case Operator.Ceiling: case Operator.Round: case Operator.Truncate:
                        case Operator.Log:
                        case Operator.Sin: case Operator.Cos: case Operator.Tan:
                        case Operator.Asin: case Operator.Acos: case Operator.Atan:
                        case Operator.Sinh: case Operator.Cosh: case Operator.Tanh:
                        case Operator.Asinh: case Operator.Acosh: case Operator.Atanh:
                            a = default; break; 
                        default: switch (param1Type) {
                            case ACParameterType.Float: a = animator.GetFloat(hash); break;
                            case ACParameterType.Int: a = animator.GetInteger(hash); break;
                            case ACParameterType.Bool: a = animator.GetBool(hash) ? 1 : 0; break;
                            case ACParameterType.Trigger: a = default; break;
                            default: return;
                        }
                        break;
                    }
                    switch (parameterDriver.GetParameterType(animator, otherHash)) {
                        case ACParameterType.Float: b = animator.GetFloat(otherHash); break;
                        case ACParameterType.Int: b = animator.GetInteger(otherHash); break;
                        case ACParameterType.Bool: b = animator.GetBool(otherHash) ? 1 : 0; break;
                        case ACParameterType.Trigger: b = default; break;
                        default: b = value; break;
                    }
                    switch (op) {
                        case Operator.Assign: a = b; break;
                        case Operator.Add: a += b; break; case Operator.Subtract: a -= b; break; case Operator.Multiply: a *= b; break; case Operator.Divide: a /= b; break; case Operator.Modulo: a %= b; break;
                        case Operator.Random: a = param1Type == ACParameterType.Bool ? UnityRandom.value < b ? 1 : 0 : UnityRandom.value * b; break;
                        case Operator.Min: a = Math.Min(a, b); break; case Operator.Max: a = Math.Max(a, b); break;
                        case Operator.Abs: a = Math.Abs(b); break; case Operator.Sign: a = Math.Sign(b); break;
                        case Operator.Floor: a = Math.Floor(b); break; case Operator.Ceiling: a = Math.Ceiling(b); break;
                        case Operator.Round: a = Math.Round(b); break; case Operator.Truncate: a = Math.Truncate(b); break;
                        case Operator.Power: a = Math.Pow(a, b); break; case Operator.Log: a = Math.Log(b); break;
                        case Operator.Sin: a = Math.Sin(b); break; case Operator.Cos: a = Math.Cos(b); break; case Operator.Tan: a = Math.Tan(b); break;
                        case Operator.Asin: a = Math.Asin(b); break; case Operator.Acos: a = Math.Acos(b); break; case Operator.Atan: a = Math.Atan(b); break;
                        case Operator.Sinh: a = Math.Sinh(b); break; case Operator.Cosh: a = Math.Cosh(b); break; case Operator.Tanh: a = Math.Tanh(b); break;
                        case Operator.Asinh: a = Math.Asinh(b); break; case Operator.Acosh: a = Math.Acosh(b); break; case Operator.Atanh: a = Math.Atanh(b); break;
                        default: return;
                    }
                    switch (param1Type) {
                        case ACParameterType.Float: animator.SetFloat(hash, (float)a); break;
                        case ACParameterType.Int: animator.SetInteger(hash, (int)Math.Clamp(a, int.MinValue, int.MaxValue)); break;
                        case ACParameterType.Bool: animator.SetBool(hash, a != 0 && !double.IsNaN(a)); break;
                        case ACParameterType.Trigger:
                            if (a != 0 && !double.IsNaN(a)) animator.SetTrigger(hash);
                            else animator.ResetTrigger(hash);
                            break;
                    }
                } catch (Exception ex) {
                    Debug.LogException(ex);
                }
            }
        }

#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(Operation))]
        class OperationDrawer : PropertyDrawer {
            static GUIStyle textFieldDropDownStyle, textFieldDropDownTextStyle;
            static GUIContent dynamicValueLabel;
            readonly Dictionary<UnityObject, ACParameter[]> parameters =
                new Dictionary<UnityObject, ACParameter[]>();

            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
                if (dynamicValueLabel == null) dynamicValueLabel = new GUIContent("D", "Dynamic Value");
                var dynamicValue = property.FindPropertyRelative(nameof(Operation.dynamicValue));
                position.height = EditorGUIUtility.singleLineHeight;
                var rect = position;
                rect.width = (position.width - 50) / 2 - 2;
                var nameProperty = property.FindPropertyRelative(nameof(Operation.parameterName));
                DrawParameterDropdown(rect, nameProperty);
                var rect2 = position;
                rect2.x = rect.xMax + 4;
                rect2.width = 48;
                var operatorValue = property.FindPropertyRelative(nameof(Operation.op));
                EditorGUI.PropertyField(rect2, operatorValue, GUIContent.none);
                var dynamicValueSize = EditorStyles.miniButton.CalcSize(dynamicValueLabel);
                var rect3 = position;
                rect3.x = rect2.xMax + 4;
                rect3.width = rect.width - dynamicValueSize.x - 2;
                ACParameterType type = default;
                if (dynamicValue.boolValue)
                    DrawParameterDropdown(rect3, property.FindPropertyRelative(nameof(Operation.otherParameterName)));
                else {
                    var valueProperty = property.FindPropertyRelative(nameof(Operation.value));
                    var parameters = GetParameters(property.serializedObject.targetObject);
                    if (parameters != null) {
                        var nameValue = nameProperty.stringValue;
                        foreach (var parameter in parameters)
                            if (parameter.name == nameValue) {
                                type = parameter.type;
                                break;
                            }
                    }
                    using (new EditorGUI.PropertyScope(rect3, GUIContent.none, valueProperty))
                    using (var changed = new EditorGUI.ChangeCheckScope()) {
                        if (operatorValue.intValue == (int)Operator.Random && type == ACParameterType.Bool) {
                            var newFloat = EditorGUI.Slider(rect3, valueProperty.floatValue, 0, 1);
                            if (changed.changed) valueProperty.floatValue = newFloat;
                        } else switch (type) {
                            case ACParameterType.Trigger:
                            case ACParameterType.Bool:
                                var newBool = EditorGUI.Toggle(rect3, valueProperty.floatValue != 0);
                                if (changed.changed) valueProperty.floatValue = newBool ? 1 : 0;
                                break;
                            case ACParameterType.Int:
                                var newInt = EditorGUI.IntField(rect3, Mathf.RoundToInt(valueProperty.floatValue));
                                if (changed.changed) valueProperty.floatValue = newInt;
                                break;
                            default:
                                var newFloat = EditorGUI.FloatField(rect3, valueProperty.floatValue);
                                if (changed.changed) valueProperty.floatValue = newFloat;
                                break;
                        }
                    }
                }
                var rect4 = position;
                rect4.x = rect3.xMax + 4;
                rect4.width = dynamicValueSize.x;
                using (new EditorGUI.PropertyScope(rect4, GUIContent.none, dynamicValue))
                using (var changed = new EditorGUI.ChangeCheckScope()) {
                    dynamicValue.boolValue = GUI.Toggle(rect4, dynamicValue.boolValue, dynamicValueLabel, EditorStyles.miniButton);
                    if (changed.changed && !dynamicValue.boolValue) {
                        property.FindPropertyRelative(nameof(Operation.otherParameterName)).stringValue = "";
                        var valueProperty = property.FindPropertyRelative(nameof(Operation.value));
                        if (valueProperty.floatValue != 1 && (type == ACParameterType.Trigger || type == ACParameterType.Bool))
                            valueProperty.floatValue = 1;
                    }
                }
            }

            public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
                EditorGUIUtility.singleLineHeight;

            ACParameter[] GetParameters(UnityObject targetObject) {
                if (targetObject == null) return null;
                if (!parameters.TryGetValue(targetObject, out var result)) {
                    var controller = AnimatorDriverBaseEditor.GetAnimatorController(targetObject);
                    if (controller == null) return null;
                    result = controller.parameters;
                    parameters[targetObject] = result;
                }
                return result;
            }

            void DrawParameterDropdown(Rect rect, SerializedProperty property) {
                if (textFieldDropDownTextStyle == null) textFieldDropDownTextStyle = GUI.skin.FindStyle("TextFieldDropDownText");
                if (textFieldDropDownStyle == null) textFieldDropDownStyle = GUI.skin.FindStyle("TextFieldDropDown");
                var dropDownIconSize = textFieldDropDownStyle.CalcSize(GUIContent.none);
                using (new EditorGUI.PropertyScope(rect, GUIContent.none, property)) {
                    var propertyRect = rect;
                    propertyRect.width -= dropDownIconSize.x;
                    property.stringValue = EditorGUI.TextField(propertyRect, property.stringValue, textFieldDropDownTextStyle);
                    var buttonRect = rect;
                    buttonRect.x += propertyRect.width;
                    buttonRect.width = dropDownIconSize.x;
                    if (EditorGUI.DropdownButton(buttonRect, GUIContent.none, FocusType.Passive, textFieldDropDownStyle)) {
                        var menu = new GenericMenu();
                        var parameters = GetParameters(property.serializedObject.targetObject);
                        if (parameters != null) {
                            var currentValue = property.stringValue;
                            foreach (var parameter in parameters)
                                menu.AddItem(
                                    new GUIContent(parameter.name),
                                    currentValue == parameter.name,
                                    ParameterDropdownSelected,
                                    (property, parameter.name)
                                );
                        } else
                            menu.AddDisabledItem(new GUIContent("No Parameters"));
                        menu.DropDown(rect);
                    }
                }
            }

            void ParameterDropdownSelected(object data) {
                var (property, parameterName) = ((SerializedProperty, string))data;
                property.stringValue = parameterName;
                property.serializedObject.ApplyModifiedProperties();
            }
        }

        [CustomEditor(typeof(ParameterDriver))]
        class _Editor : AnimatorDriverBaseEditor {
            SerializedProperty operationsProperty;
            ReorderableList operationsList;

            protected override void OnEnable() {
                base.OnEnable();
                operationsProperty = serializedObject.FindProperty(nameof(operations));
                operationsList = new SerializedReorderableList(operationsProperty);
            }

            public override void OnInspectorGUI() {
                serializedObject.Update();
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Evaluate operations on parameters.", MessageType.None);
                EditorGUILayout.Space();
                DrawBaseFields();
                EditorGUILayout.Space();
                operationsList.DoLayoutList();
                serializedObject.ApplyModifiedProperties();
            }
        }
#endif
    }
}