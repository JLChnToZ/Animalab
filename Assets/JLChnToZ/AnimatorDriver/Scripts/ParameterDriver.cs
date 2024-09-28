using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using JLChnToZ.MathUtilities;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditorInternal;
#endif
using UnityObject = UnityEngine.Object;
using ACParameter = UnityEngine.AnimatorControllerParameter;
using ACParameterType = UnityEngine.AnimatorControllerParameterType;

namespace JLChnToZ.AnimatorBehaviours {
    /// <summary>Evaluate operations on parameters.</summary>
    public class ParameterDriver : AnimatorDriverBase {
        static readonly Dictionary<ushort, int> id2Hash = new();
        static UnityMathEvalulator mathExpressions;
        static Animator activeAnimator;
        static ParameterDriver activeParameterDriver;
        readonly HashSet<RuntimeAnimatorController> hasRecordedParameters = new();
        readonly Dictionary<(RuntimeAnimatorController, int), ACParameterType> parameterTypes = new();

        [SerializeField] Operation[] operations;

        static float GetVariableFuncById(ushort id) {
            if (id2Hash.TryGetValue(id, out var hash))
                switch (activeParameterDriver.GetParameterType(activeAnimator, hash)) {
                    case ACParameterType.Float: return activeAnimator.GetFloat(hash);
                    case ACParameterType.Int: return activeAnimator.GetInteger(hash);
                    case ACParameterType.Bool: return activeAnimator.GetBool(hash) ? 1 : 0;
                }
            return 0;
        }

        static void ApplyOperation(ParameterDriver parameterDriver, Animator animator, ref UnityMathExpression expression, int hash) {
            float value = 0;
            activeAnimator = animator;
            activeParameterDriver = parameterDriver;
            if (mathExpressions == null) {
                mathExpressions = new() {
                    GetVariableFuncById = GetVariableFuncById,
                };
                mathExpressions.RegisterDefaultFunctions();
            }
            var tokens = expression.GetOptimizedTokens(mathExpressions, true);
            if (tokens != null && tokens.Length > 0) {
                mathExpressions.Tokens = tokens;
                value = mathExpressions.Evalulate();
            }
            switch (parameterDriver.GetParameterType(animator, hash)) {
                case ACParameterType.Float:
                    animator.SetFloat(hash, value);
                    break;
                case ACParameterType.Int:
                    animator.SetInteger(hash, (int)value);
                    break;
                case ACParameterType.Bool:
                    animator.SetBool(hash, value != 0 && !float.IsNaN(value));
                    break;
                case ACParameterType.Trigger:
                    if (value != 0 && !float.IsNaN(value)) animator.SetTrigger(hash);
                    break;
            }
        }

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
                    if (mathExpressions.TryGetId(param.name, out var id, true))
                        id2Hash[id] = param.nameHash;
                }
            parameterTypes.TryGetValue((controller, hash), out var type);
            return type;
        }

        [Serializable]
        struct Operation {
            public string parameterName;
            public UnityMathExpression expression;
            [NonSerialized] bool hasInitialized;
            [NonSerialized] int targetHash;

            public void Apply(Animator animator, ParameterDriver parameterDriver) {
                try {
                    if (!hasInitialized) {
                        targetHash = Animator.StringToHash(parameterName);
                        hasInitialized = true;
                    }
                    ApplyOperation(parameterDriver, animator, ref expression, targetHash);
                } catch (Exception ex) {
                    Debug.LogException(ex);
                }
            }
        }


#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(Operation))]
        class OperationDrawer : PropertyDrawer {
            static GUIStyle textFieldDropDownStyle, textFieldDropDownTextStyle;
            readonly Dictionary<UnityObject, ACParameter[]> parameters = new();

            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
                position.height = EditorGUIUtility.singleLineHeight;
                var rect = position;
                rect.width = EditorGUIUtility.labelWidth;
                var nameProperty = property.FindPropertyRelative(nameof(Operation.parameterName));
                DrawParameterDropdown(rect, nameProperty);
                var rect2 = position;
                rect2.xMin = rect.xMax + 2;
                var expression = property.FindPropertyRelative(nameof(Operation.expression));
                EditorGUI.PropertyField(rect2, expression, GUIContent.none);
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
                textFieldDropDownTextStyle ??= GUI.skin.FindStyle("TextFieldDropDownText");
                textFieldDropDownStyle ??= GUI.skin.FindStyle("TextFieldDropDown");
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
            ReorderableList operationsList;

            protected override void OnEnable() {
                base.OnEnable();
                operationsList = new SerializedReorderableList(serializedObject.FindProperty(nameof(operations)));
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