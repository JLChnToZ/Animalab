using System;
using System.Text;
using System.Collections.Generic;
using UnityEngine;

using UnityObject = UnityEngine.Object;

namespace JLChnToZ.Animalab {
    internal abstract class StateParserBase : AnimalabParserBase {
        internal protected StateMachinePath path;
        internal protected Dictionary<StateMachinePath, UnityObject> stateLookup;
        StringBuilder typeName;
        int typeNameMode;

        protected bool StartParseTypeName(TokenType type, string token) {
            switch (type) {
                case TokenType.Identifier:
                    typeName = new StringBuilder(token);
                    typeNameMode = 1;
                    nextNode = Node.TypeName;
                    return true;
                case TokenType.SingleQuotedString:
                case TokenType.DoubleQuotedString:
                    typeName = new StringBuilder(token);
                    typeNameMode = 2;
                    nextNode = Node.TypeName;
                    return true;
            }
            return false;
        }

        protected bool OnParseTypeName(TokenType type, string token) {
            switch (nextNode) {
                case Node.TypeName:
                    switch (type) {
                        case TokenType.Identifier:
                            if (typeNameMode != 1) break;
                            typeName.Append(token);
                            return true;
                        case TokenType.SingleQuotedString:
                        case TokenType.DoubleQuotedString:
                            if (typeNameMode != 2) break;
                            typeName.Append(token);
                            return true;
                        case TokenType.Symbol:
                            switch (token) {
                                case ".": case ",": case ":":
                                    if (typeNameMode != 1) break;
                                    typeName.Append(token);
                                    return true;
                                case "{":
                                    typeNameMode = 0;
                                    var stringified = typeName.ToString();
                                    var behaviourType = Type.GetType(stringified, false);
                                    if (behaviourType == null || behaviourType.IsAbstract || !behaviourType.IsSubclassOf(typeof(StateMachineBehaviour))) {
                                        behaviourType = null;
                                        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                                            behaviourType = assembly.GetType(stringified, false);
                                            if (behaviourType != null &&
                                                !behaviourType.IsAbstract &&
                                                behaviourType.IsSubclassOf(typeof(StateMachineBehaviour)))
                                                break;
                                            behaviourType = null;
                                        }
                                    }
                                    typeName = null;
                                    if (behaviourType == null)
                                        throw new Exception($"Invalid type name `{stringified}`.");
                                    behaviour = ScriptableObject.CreateInstance(behaviourType) as StateMachineBehaviour;
                                    behaviour.hideFlags |= HideFlags.HideInHierarchy | HideFlags.HideInInspector;
                                    SaveAsset(behaviour);
                                    Attach<BehaviourParser>();
                                    return true;
                            }
                            break;
                    }
                    throw new Exception($"Unexpected {type} `{token}`.");
            }
            return false;
        }

        protected override void OnAttach(StackParser parent) {
            base.OnAttach(parent);
            if (parent is StateParserBase stateParser) {
                stateLookup = stateParser.stateLookup;
                path = stateParser.path;
            } else {
                stateMachine = null;
                stateLookup = new Dictionary<StateMachinePath, UnityObject>();
                path = default;
            }
            state = null;
        }

        protected override void OnDetech() {
            state = null;
            stateLookup = null;
            base.OnDetech();
        }

        protected Vector2 GetNextPlacablePosition() {
            float dist = Mathf.Log(stateMachine.stateMachines.Length + stateMachine.states.Length + 1, 12F * 30F);
            float rad = dist * Mathf.PI / 12F;
            return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * dist * 30F;
        }
    }
    internal class StateParser : StateParserBase {

        protected override string Hint {
            get {
                if (state != null) return $"State {state.name}";
                return "State";
            }
        }

        protected override void OnParse(TokenType type, string token, bool hasLineBreak, int indentLevel) {
            switch (nextNode) {
                case Node.Default:
                    switch (type) {
                        case TokenType.Identifier:
                        case TokenType.SingleQuotedString:
                        case TokenType.DoubleQuotedString:
                            if (state == null) {
                                state = stateMachine.AddState(token, GetNextPlacablePosition());
                                state.hideFlags = HideFlags.HideInHierarchy;
                                stateLookup.Add(path + token, state);
                                SaveAsset(state);
                                nextNode = Node.OpenBrace;
                                return;
                            }
                            break;
                    }
                    throw new Exception($"Unexpected token. {type} {token}");
                case Node.OpenBrace:
                    if (type == TokenType.Symbol && token == "{") {
                        nextNode = Node.Identifier;
                        return;
                    }
                    throw new Exception($"Unexpected {type} `{token}`.");
                case Node.Identifier:
                    switch (type) {
                        case TokenType.Identifier:
                            switch (token) {
                                case "speed": nextNode = Node.Speed; return;
                                case "cycleOffset": nextNode = Node.CycleOffset; return;
                                case "time": nextNode = Node.TimeOffset; return;
                                case "mirror": nextNode = Node.Mirror; return;
                                case "ikOnFeet": state.iKOnFeet = true; return;
                                case "writeDefaults": state.writeDefaultValues = true; return;
                                case "clip": Attach<ClipParser>(); return;
                                case "blendtree": Attach<BlendTreeParser>(); return;
                                case "empty": nextNode = Node.Unknown; return;
                                case "if": case "noSelf": case "any":
                                case "muted": case "solo": case "wait":
                                case "fade": case "end": case "goto":
                                    Attach<ConditionParser>(type, token, hasLineBreak, indentLevel);
                                    return;
                            }
                            break;
                        case TokenType.Symbol:
                            switch (token) {
                                case "+": case "-":
                                    Attach<ConditionParser>(type, token, hasLineBreak, indentLevel);
                                    return;
                            }
                            break;
                    }
                    if (StartParseTypeName(type, token)) return;
                    break;
                case Node.Speed:
                    switch (type) {
                        case TokenType.Identifier:
                        case TokenType.SingleQuotedString:
                        case TokenType.DoubleQuotedString:
                            state.speedParameterActive = true;
                            state.speedParameter = token;
                            nextNode = Node.Unknown;
                            return;
                        case TokenType.Number:
                            if (!float.TryParse(token, out var speed)) break;
                            state.speedParameterActive = false;
                            state.speed = speed;
                            nextNode = Node.Unknown;
                            return;
                    }
                    break;
                case Node.CycleOffset:
                    switch (type) {
                        case TokenType.Identifier:
                        case TokenType.SingleQuotedString:
                        case TokenType.DoubleQuotedString:
                            state.cycleOffsetParameterActive = true;
                            state.cycleOffsetParameter = token;
                            nextNode = Node.Unknown;
                            return;
                        case TokenType.Number:
                            if (!float.TryParse(token, out var cycleOffset)) break;
                            state.cycleOffsetParameterActive = false;
                            state.cycleOffset = cycleOffset;
                            nextNode = Node.Unknown;
                            return;
                    }
                    break;
                case Node.TimeOffset:
                    switch (type) {
                        case TokenType.Identifier:
                        case TokenType.SingleQuotedString:
                        case TokenType.DoubleQuotedString:
                            state.timeParameterActive = true;
                            state.timeParameter = token;
                            nextNode = Node.Unknown;
                            return;
                    }
                    break;
                case Node.Mirror:
                    switch (type) {
                        case TokenType.Identifier:
                        case TokenType.SingleQuotedString:
                        case TokenType.DoubleQuotedString:
                            state.mirrorParameterActive = true;
                            state.mirrorParameter = token;
                            nextNode = Node.Unknown;
                            return;
                    }
                    state.mirrorParameterActive = false;
                    state.mirror = true;
                    break;
                case Node.Tag:
                    if (type == TokenType.SingleQuotedString || type == TokenType.DoubleQuotedString) {
                        state.tag = token;
                        nextNode = Node.Unknown;
                        return;
                    }
                    break;
            }
            if (OnParseTypeName(type, token)) return;
            if (type == TokenType.Symbol) {
                switch (token) {
                    case ";": nextNode = Node.Identifier; return;
                    case "}": Detech(); return;
                }
            }
            throw new Exception($"Unexpected {type} `{token}`. ({nextNode})");
        }
    }
}