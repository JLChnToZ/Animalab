using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;

namespace JLChnToZ.Animalab {
    internal class BehaviourParser : AnimalabParserBase {
        static readonly PropertyInfo objectReferenceTypeString = typeof(SerializedProperty)
            .GetProperty("objectReferenceTypeString", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        SerializedObject serializedObject;
        readonly Stack<SerializedProperty> propertyStack = new Stack<SerializedProperty>();
        bool shouldCreateElement;

        static Type GetObjectReferenceType(SerializedProperty prop) {
            if (objectReferenceTypeString == null) return null;
            var typeString = objectReferenceTypeString.GetValue(prop) as string;
            if (string.IsNullOrEmpty(typeString)) return null;
            return Type.GetType(typeString, false);
        }

        protected override void OnParse(TokenType type, string token, bool hasLineBreak, int indentLevel) {
            SerializedProperty prop;
            switch (nextNode) {
                case Node.Default:
                case Node.Identifier:
                    switch (type) {
                        case TokenType.Identifier:
                        case TokenType.SingleQuotedString:
                        case TokenType.DoubleQuotedString:
                            if (propertyStack.Count == 0)
                                prop = serializedObject.FindProperty(token);
                            else
                                prop = propertyStack.Peek().FindPropertyRelative(token);
                            if (prop != null) {
                                propertyStack.Push(prop);
                                if (prop.propertyType == SerializedPropertyType.LayerMask)
                                    prop.intValue = 0;
                                nextNode = Node.Operator;
                                shouldCreateElement = false;
                                return;
                            }
                            break;
                        case TokenType.Symbol:
                            switch (token[0]) {
                                case '}':
                                    if (propertyStack.Count == 0) {
                                        nextNode = Node.Unknown;
                                        return;
                                    }
                                    if (propertyStack.Peek().isArray) break;
                                    if (token.Length == 1) {
                                        nextNode = Node.Unknown;
                                        return;
                                    }
                                    token = token.Substring(1);
                                    break;
                            }
                            break;
                    }
                    break;
                case Node.Operator:
                    switch (type) {
                        case TokenType.Symbol:
                            switch (token) {
                                case "=":
                                    nextNode = Node.Value;
                                    return;
                                case ",":
                                    prop = propertyStack.Peek();
                                    if (prop.propertyType == SerializedPropertyType.LayerMask)
                                        return;
                                    break;
                            }
                            break;
                    }
                    break;
                case Node.Value:
                    prop = propertyStack.Peek();
                    if (shouldCreateElement) {
                        int i = prop.arraySize;
                        prop.arraySize++;
                        prop = prop.GetArrayElementAtIndex(i);
                        propertyStack.Push(prop);
                        shouldCreateElement = false;
                    }
                    switch (type) {
                        case TokenType.Identifier:
                            switch (prop.propertyType) {
                                case SerializedPropertyType.Boolean:
                                    if (bool.TryParse(token, out var boolValue))
                                        prop.boolValue = boolValue;
                                    nextNode = Node.Unknown;
                                    return;
                            }
                            goto case TokenType.SingleQuotedString;
                        case TokenType.SingleQuotedString:
                        case TokenType.DoubleQuotedString:
                            switch (prop.propertyType) {
                                case SerializedPropertyType.String:
                                    prop.stringValue = token;
                                    nextNode = Node.Unknown;
                                    return;
                                case SerializedPropertyType.Enum:
                                    prop.enumValueIndex = Array.IndexOf(prop.enumNames, token);
                                    nextNode = Node.Unknown;
                                    return;
                                case SerializedPropertyType.ObjectReference:
                                    if (token == "null") {
                                        prop.objectReferenceValue = null;
                                        nextNode = Node.Unknown;
                                        return;
                                    }
                                    var obj = LoadAsset(token, GetObjectReferenceType(prop));
                                    if (obj != null) prop.objectReferenceValue = obj;
                                    nextNode = Node.Unknown;
                                    return;
                            }
                            break;
                        case TokenType.Number:
                            switch (prop.propertyType) {
                                case SerializedPropertyType.Boolean:
                                    if (float.TryParse(token, out var boolValue))
                                        prop.boolValue = boolValue != 0;
                                    nextNode = Node.Unknown;
                                    return;
                                case SerializedPropertyType.Integer:
                                case SerializedPropertyType.Enum:
                                    if (long.TryParse(token, out var longValue))
                                        prop.longValue = longValue;
                                    nextNode = Node.Unknown;
                                    return;
                                case SerializedPropertyType.LayerMask:
                                    if (int.TryParse(token, out var layerMask))
                                        prop.intValue |= layerMask;
                                    nextNode = Node.Operator;
                                    return;
                                case SerializedPropertyType.Character:
                                    if (int.TryParse(token, out var intValue))
                                        prop.intValue = intValue;
                                    nextNode = Node.Unknown;
                                    return;
                                case SerializedPropertyType.Float:
                                    if (double.TryParse(token, out var floatValue))
                                        prop.doubleValue = floatValue;
                                    nextNode = Node.Unknown;
                                    return;
                            }
                            break;
                        case TokenType.Symbol:
                            switch (token[0]) {
                                case '[':
                                    if (!prop.isArray) break;
                                    shouldCreateElement = true;
                                    nextNode = Node.Value;
                                    return;
                                case '{':
                                    if (prop.isArray) break;
                                    nextNode = Node.Identifier;
                                    if (token.Length == 1) return;
                                    token = token.Substring(1);
                                    break;
                                case ']':
                                    propertyStack.Pop();
                                    if (propertyStack.Count == 0) break;
                                    var top = propertyStack.Peek();
                                    if (!top.isArray) break;
                                    top.arraySize--; // remove last element
                                    if (token.Length == 1) {
                                        nextNode = Node.Unknown;
                                        return;
                                    }
                                    token = token.Substring(1);
                                    break;
                            }
                            break;
                    }
                    break;
            }
            if (type == TokenType.Symbol && token == ";") {
                if (propertyStack.Count == 0) {
                    Detech();
                    return;
                }
                propertyStack.Pop();
                if (propertyStack.Count > 0 && propertyStack.Peek().isArray) {
                    shouldCreateElement = true;
                    nextNode = Node.Value;
                } else {
                    shouldCreateElement = false;
                    nextNode = Node.Identifier;
                }
                return;
            }
            throw new Exception($"Unexpected {type} `{token}`.");
        }

        protected override void OnAttach(StackParser parent) {
            base.OnAttach(parent);
            serializedObject = new SerializedObject(behaviour);
            serializedObject.Update();
        }

        protected override void OnDetech() {
            serializedObject.ApplyModifiedProperties();
            serializedObject.Dispose();
            var b = state != null ? state.behaviours :
                stateMachine != null ? stateMachine.behaviours :
                null;
            if (b == null)
                b = new [] { behaviour };
            else {
                int last = b.Length;
                Array.Resize(ref b, last + 1);
                b[last] = behaviour;
            }
            if (state != null)
                state.behaviours = b;
            else if (stateMachine != null)
                stateMachine.behaviours = b;
            serializedObject = null;
            base.OnDetech();
        }
    }
}