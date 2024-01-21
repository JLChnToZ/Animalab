using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Animations;

using UnityObject = UnityEngine.Object;

namespace JLChnToZ.Animalab {
    internal class StateMashineParser : StateParserBase {
        StateMachinePath defaultState;

        protected override string Hint  {
            get {
                if (stateMachine != null) return $"State Machine {stateMachine.name}";
                return "State Machine";
            }
        }

        protected override void OnParse(TokenType type, string token, bool hasLineBreak, int indentLevel) {
            switch (nextNode) {
                case Node.Default:
                    switch (type) {
                        case TokenType.Identifier:
                        case TokenType.SingleQuotedString:
                        case TokenType.DoubleQuotedString:
                            CreateStateMachine(token);
                            nextNode = Node.OpenBrace;
                            return;
                    }
                    throw new Exception($"Unexpected {type} `{token}`.");
                case Node.OpenBrace:
                    if (type == TokenType.Symbol && token == "{") {
                        nextNode = Node.Identifier;
                        return;
                    }
                    throw new Exception($"Unexpected {type} `{token}`.");
                case Node.Identifier:
                    if (ParseIdentifier(type, token, hasLineBreak, indentLevel) ||
                        StartParseTypeName(type, token)) return;
                    break;
                case Node.DefaultState:
                    switch (type) {
                        case TokenType.Identifier:
                        case TokenType.SingleQuotedString:
                        case TokenType.DoubleQuotedString:
                            defaultState = path + token;
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
            throw new Exception($"Unexpected {type} `{token}`.");
        }

        protected virtual void CreateStateMachine(string name) {
            var childStateMachine = new AnimatorStateMachine {
                name = name,
                hideFlags = HideFlags.HideInHierarchy,
            };
            if (stateMachine != null) {
                stateMachine.AddStateMachine(childStateMachine, GetNextPlacablePosition());
                path += name;
            } else path = name;
            stateMachine = childStateMachine;
            stateLookup.Add(path, stateMachine);
            SaveAsset(stateMachine);
            transitions = new List<TransitionData>();
        }

        protected virtual bool ParseIdentifier(TokenType type, string token, bool hasLineBreak, int indentLevel) {
            switch (type) {
                case TokenType.Identifier:
                    switch (token) {
                        case "default": nextNode = Node.DefaultState; return true;
                        case "state": Attach<StateParser>(); return true;
                        case "stateMachine": Attach<StateMashineParser>(); return true;
                        case "if": case "noSelf": case "any":
                        case "muted": case "solo": case "wait":
                        case "fade": case "end": case "goto":
                            Attach<ConditionParser>(type, token, hasLineBreak, indentLevel);
                            return true;
                    }
                    break;
                case TokenType.Symbol:
                    switch (token) {
                        case "+": case "-":
                            Attach<ConditionParser>(type, token, hasLineBreak, indentLevel);
                            return true;
                    }
                    break;
                case TokenType.Number:
                    Attach<ConditionParser>(type, token, hasLineBreak, indentLevel);
                    return true;
            }
            return false;
        }

        protected override void OnDetech() {
            if (defaultState.Depth > 0) {
                if (!stateLookup.TryGetValue(this.defaultState, out var defaultState))
                    Debug.LogWarning($"Default state \"{this.defaultState}\" not found.");
                stateMachine.defaultState = defaultState as AnimatorState;
                this.defaultState = default;
            }
            foreach (var transition in transitions) {
                bool isEntryState = transition.fromStatePath.Depth == 0;
                UnityObject source = null, destination = null;
                if (!isEntryState && !stateLookup.TryGetValue(transition.fromStatePath, out source)) {
                    Debug.LogWarning($"Source state \"{transition.fromStatePath}\" not found.");
                    continue;
                }
                if (!transition.isExit && (transition.toStatePath.Depth == 0 ||
                    !stateLookup.TryGetValue(transition.toStatePath, out destination))) {
                    Debug.LogWarning($"Destination state \"{transition.toStatePath}\" not found.");
                    continue;
                }
                var trans = isEntryState ? transition.isAny ?
                    AddAnyStateTransition(destination) :
                    AddEntryTransition(destination) :
                    transition.isExit ?
                    AddExitTransition(source) :
                    AddTransition(source, destination);
                trans.conditions = transition.conditions;
                if (trans is AnimatorStateTransition ant) {
                    ant.hasExitTime = transition.hasExitTime;
                    ant.exitTime = transition.exitTime;
                    ant.hasFixedDuration = transition.hasFixedDuration;
                    ant.duration = transition.duration;
                    ant.offset = transition.offset;
                    ant.interruptionSource = transition.interruptionSource;
                    ant.orderedInterruption = transition.orderedInterruption;
                    ant.canTransitionToSelf = transition.canTransitionToSelf;
                }
                SaveAsset(trans);
            }
            transitions = null;
            var autoLayout = new AutoLayout(stateMachine);
            autoLayout.Iterate(100);
            autoLayout.Apply();
            base.OnDetech();
        }

        AnimatorTransitionBase AddEntryTransition(UnityObject destination) {
            if (destination is AnimatorStateMachine destStateMachine)
                return stateMachine.AddEntryTransition(destStateMachine);
            if (destination is AnimatorState destState)
                return stateMachine.AddEntryTransition(destState);
            throw new Exception($"Unexpected destination type {destination.GetType()}.");
        }

        AnimatorTransitionBase AddAnyStateTransition(UnityObject destination) {
            if (destination is AnimatorStateMachine destStateMachine)
                return stateMachine.AddAnyStateTransition(destStateMachine);
            if (destination is AnimatorState destState)
                return stateMachine.AddAnyStateTransition(destState);
            throw new Exception($"Unexpected destination type {destination.GetType()}.");
        }

        AnimatorTransitionBase AddExitTransition(UnityObject source) {
            if (source is AnimatorState srcState)
                return srcState.AddExitTransition();
            throw new Exception($"Unexpected source type {source.GetType()}.");
        }

        AnimatorTransitionBase AddTransition(UnityObject source, UnityObject destination) {
            if (source is AnimatorState srcState) {
                if (destination is AnimatorStateMachine destStateMachine)
                    return srcState.AddTransition(destStateMachine);
                if (destination is AnimatorState destState)
                    return srcState.AddTransition(destState);
            }
            throw new Exception($"Unexpected source type {source.GetType()} or destination type {destination.GetType()}.");
        }
    }

    internal class LayerParser : StateMashineParser {
        internal AnimatorControllerLayer layer;
        string syncLayerName;

        protected override string Hint  {
            get {
                if (layer != null) return $"Layer {layer.name}";
                return "Layer";
            }
        }
        
        protected override void OnParse(TokenType type, string token, bool hasLineBreak, int indentLevel) {
            switch (nextNode) {
                case Node.Weight:
                    if (type == TokenType.Number && float.TryParse(token, out var weight)) {
                        layer.defaultWeight = weight;
                        nextNode = Node.Unknown;
                        return;
                    }
                    break;
                case Node.Mask:
                    if (type == TokenType.SingleQuotedString || type == TokenType.DoubleQuotedString) {
                        layer.avatarMask = LoadAsset<AvatarMask>(token);
                        nextNode = Node.Unknown;
                        return;
                    }
                    break;
                case Node.SyncLayer:
                    if (type == TokenType.SingleQuotedString || type == TokenType.DoubleQuotedString) {
                        syncLayerName = token;
                        nextNode = Node.Unknown;
                        return;
                    }
                    break;
            }
            base.OnParse(type, token, hasLineBreak, indentLevel);
        }

        protected override void CreateStateMachine(string name) {
            base.CreateStateMachine(name);
            layer = new AnimatorControllerLayer {
                name = name,
                defaultWeight = 1F,
                stateMachine = stateMachine,
            };
        }

        protected override bool ParseIdentifier(TokenType type, string token, bool hasLineBreak, int indentLevel) {
            if (base.ParseIdentifier(type, token, hasLineBreak, indentLevel)) return true;
            if (type == TokenType.Identifier)
                switch (token) {
                    case "weight": nextNode = Node.Weight; return true;
                    case "mask": nextNode = Node.Mask; return true;
                    case "ikPass": layer.iKPass = true; return true;
                    case "sync": nextNode = Node.SyncLayer; return true;
                    case "syncTiming": layer.syncedLayerAffectsTiming = true; return true;
                    default:
                        if (Enum.TryParse(token, true, out AnimatorLayerBlendingMode blendingMode)) {
                            layer.blendingMode = blendingMode;
                            nextNode = Node.Unknown;
                            return true;
                        }
                        break;
                }
            return false;
        }

        protected override void OnAttach(StackParser parent) {
            base.OnAttach(parent);
            layer = null;
        }

        protected override void OnDetech() {
            controller.AddLayer(layer);
            if (!string.IsNullOrEmpty(syncLayerName))
                syncLayers[controller.layers.Length] = syncLayerName;
            layer = null;
            base.OnDetech();
        }
    }
}