using System;
using System.Collections.Generic;
using UnityEditor.Animations;

namespace JLChnToZ.Animalab {
    internal class ConditionParser : AnimalabParserBase {
        List<List<AnimatorCondition>> conditions;
        TransitionData current;
        bool hasOpenBracket;
        string gotoStateName;
        AnimatorCondition currentCondition;
        StateMachinePath srcPath, destPath;
        char lastSymbol = '\0';
        bool absolutePath;

        protected override string Hint {
            get {
                if (current.fromStatePath.Depth > 0) {
                    if (current.toStatePath.Depth > 0)
                        return $"Transition {current.fromStatePath} - {current.toStatePath}";
                    return $"Transition {current.fromStatePath}";
                }
                if (current.toStatePath.Depth > 0)
                    return $"Transition any to {current.toStatePath}";
                return "Transition";
            }
        }

        protected override void OnParse(TokenType type, string token, bool hasLineBreak, int indentLevel) {
            switch (nextNode) {
                case Node.Default:
                case Node.Identifier:
                    switch (type) {
                        case TokenType.Identifier:
                            switch (token) {
                                case "if": conditions.Add(new List<AnimatorCondition>()); nextNode = Node.If; return;
                                case "muted": current.isMuted = true; nextNode = Node.Identifier; return;
                                case "solo": current.isSolo = true; nextNode = Node.Identifier; return;
                                case "wait": current.hasExitTime = true; nextNode = Node.ExitTime; return;
                                case "fade": nextNode = Node.Duration; return;
                                case "end": current.isExit = true; nextNode = Node.Identifier; return;
                                case "goto": nextNode = Node.Goto; return;
                                case "noSelf": current.canTransitionToSelf = false; nextNode = Node.Identifier; return;
                                case "any": current.isAny = true; nextNode = Node.Identifier; return;
                            }
                            if (Enum.TryParse(token, true, out TransitionInterruptionSource source)) {
                                current.interruptionSource = source;
                                nextNode = Node.OrderedInterrput;
                                return;
                            }
                            break;
                        case TokenType.Symbol:
                            switch (token) {
                                case "+": case "-":
                                    nextNode = Node.TimeOffset;
                                    return;
                            }
                            break;
                        case TokenType.Number:
                            nextNode = Node.TimeOffset;
                            return;
                    }
                    break;
                case Node.If:
                    switch (type) {
                        case TokenType.Symbol:
                            switch (token) {
                                case "(": 
                                    if (hasOpenBracket) break;
                                    hasOpenBracket = true;
                                    return;
                                case "!":
                                    currentCondition = new AnimatorCondition {
                                        mode = AnimatorConditionMode.IfNot,
                                    };
                                    nextNode = Node.Parameter;
                                    return;
                            }
                            break;
                        case TokenType.Identifier:
                        case TokenType.SingleQuotedString:
                        case TokenType.DoubleQuotedString:
                            currentCondition = new AnimatorCondition {
                                parameter = token,
                                mode = AnimatorConditionMode.If,
                            };
                            nextNode = Node.Operator;
                            return;
                    }
                    break;
                case Node.Parameter:
                    switch (type) {
                        case TokenType.Identifier:
                        case TokenType.SingleQuotedString:
                        case TokenType.DoubleQuotedString:
                            currentCondition.parameter = token;
                            conditions[conditions.Count - 1].Add(currentCondition);
                            nextNode = Node.Next;
                            return;
                    }
                    break;
                case Node.Operator:
                    if (type == TokenType.Symbol && currentCondition.mode != AnimatorConditionMode.IfNot)
                        switch (token) {
                            case ">":
                                if (lastSymbol != '\0') break;
                                currentCondition.mode = AnimatorConditionMode.Greater;
                                nextNode = Node.Value;
                                return;
                            case "<":
                                if (lastSymbol != '\0') break;
                                currentCondition.mode = AnimatorConditionMode.Less;
                                nextNode = Node.Value;
                                return;
                            case "=":
                                switch (lastSymbol) {
                                    case '=':
                                        currentCondition.mode = AnimatorConditionMode.Equals;
                                        nextNode = Node.Value;
                                        lastSymbol = '\0';
                                        return;
                                    case '!':
                                        currentCondition.mode = AnimatorConditionMode.NotEqual;
                                        nextNode = Node.Value;
                                        lastSymbol = '\0';
                                        return;
                                    case '\0':
                                        lastSymbol = '=';
                                        return;
                                    default:
                                        throw new Exception($"Unexpected symbol `{token}`.");
                                }
                            case "!":
                                switch (lastSymbol) {
                                    case '\0':
                                        lastSymbol = '!';
                                        return;
                                    default:
                                        throw new Exception($"Unexpected symbol `{token}`.");
                                }
                        }
                    goto case Node.Next;
                case Node.Value:
                    if (type == TokenType.Number && float.TryParse(token, out var value)) {
                        currentCondition.threshold = value;
                        conditions[conditions.Count - 1].Add(currentCondition);
                        nextNode = Node.Next;
                        return;
                    }
                    break;
                case Node.Next:
                    switch (type) {
                        case TokenType.Symbol:
                            switch (token) {
                                case "|":
                                    switch (lastSymbol) {
                                        case '|':
                                            if (currentCondition.mode == AnimatorConditionMode.If)
                                                conditions[conditions.Count - 1].Add(currentCondition);
                                            conditions.Add(new List<AnimatorCondition>());
                                            nextNode = Node.If;
                                            lastSymbol = '\0';
                                            return;
                                        case '\0':
                                            lastSymbol = '|';
                                            return;
                                        default:
                                            throw new Exception($"Unexpected symbol `{token}`.");
                                    }
                                case "&":
                                    switch (lastSymbol) {
                                        case '&':
                                            nextNode = Node.If;
                                            lastSymbol = '\0';
                                            return;
                                        case '\0':
                                            lastSymbol = '&';
                                            return;
                                        default:
                                            throw new Exception($"Unexpected symbol `{token}`.");
                                    }
                                case ")":
                                    if (lastSymbol != '\0') break;
                                    if (currentCondition.mode == AnimatorConditionMode.If)
                                        conditions[conditions.Count - 1].Add(currentCondition);
                                    if (!hasOpenBracket) break;
                                    hasOpenBracket = false;
                                    nextNode = Node.Identifier;
                                    return;
                            }
                            break;
                    }
                    if (hasOpenBracket) break;
                    goto case Node.Identifier;
                case Node.Duration:
                    switch (type) {
                        case TokenType.Symbol:
                            switch (token) {
                                case "(":
                                    if (hasOpenBracket) break;
                                    hasOpenBracket = true;
                                    return;
                                case ")":
                                    if (!hasOpenBracket) break;
                                    hasOpenBracket = false;
                                    nextNode = Node.Identifier;
                                    return;
                            }
                            break;
                        case TokenType.Number:
                            if (float.TryParse(token, out current.duration)) {
                                current.hasFixedDuration = false;
                                if (!hasOpenBracket) nextNode = Node.Identifier;
                                return;
                            }
                            break;
                        case TokenType.Identifier:
                            if (token == "s") {
                                current.hasFixedDuration = true;
                                if (!hasOpenBracket) nextNode = Node.Identifier;
                                return;
                            }
                            break;
                    }
                    break;
                case Node.ExitTime:
                    switch (type) {
                        case TokenType.Symbol:
                            switch (token) {
                                case "(":
                                    if (hasOpenBracket) break;
                                    hasOpenBracket = true;
                                    return;
                                case ")":
                                    if (!hasOpenBracket) break;
                                    hasOpenBracket = false;
                                    nextNode = Node.Identifier;
                                    return;
                            }
                            break;
                        case TokenType.Number:
                            if (float.TryParse(token, out current.exitTime)) {
                                current.hasFixedDuration = false;
                                if (!hasOpenBracket) nextNode = Node.Identifier;
                                return;
                            }
                            break;
                    }
                    break;
                case Node.TimeOffset:
                    switch (type) {
                        case TokenType.Symbol:
                            switch (token) {
                                case "+": current.offset = 1; return;
                                case "-": current.offset = -1; return;
                            }
                            break;
                        case TokenType.Number:
                            if (float.TryParse(token, out var offset2)) {
                                if (current.offset != 0) current.offset *= offset2;
                                else current.offset = offset2;
                                nextNode = Node.Identifier;
                                return;
                            }
                            break;
                    }
                    break;
                case Node.OrderedInterrput:
                    if (type == TokenType.Identifier && token == "ordered") {
                        current.orderedInterruption = true;
                        nextNode = Node.Identifier;
                        return;
                    }
                    goto case Node.Identifier;
                case Node.Goto:
                    switch (type) {
                        case TokenType.Identifier:
                        case TokenType.SingleQuotedString:
                        case TokenType.DoubleQuotedString:
                            if (absolutePath)
                                destPath += token;
                            else
                                gotoStateName = token;
                            nextNode = Node.GotoOrNext;
                            return;
                        case TokenType.Symbol:
                            if (token == "/" && !absolutePath) {
                                absolutePath = true;
                                destPath = default;
                                nextNode = Node.Goto;
                                return;
                            }
                            break;
                    }
                    break;
                case Node.GotoOrNext:
                    if (type == TokenType.Symbol && token == "/") {
                        if (!absolutePath) {
                            absolutePath = true;
                            destPath = gotoStateName;
                            gotoStateName = null;
                        }
                        nextNode = Node.Goto;
                        return;
                    }
                    goto case Node.Identifier;
            }
            if (type == TokenType.Symbol && token == ";") {
                Detech();
                return;
            }
            throw new Exception($"Unexpected {type} `{token}`.");
        }

        protected override void OnAttach(StackParser parent) {
            base.OnAttach(parent);
            current = new TransitionData() {
                fromStateMachine = stateMachine,
                canTransitionToSelf = true,
            };
            conditions = new List<List<AnimatorCondition>>();
            if (parent is StateMashineParser stateMachineParser)
                srcPath = stateMachineParser.path;
            else if (parent is StateParser stateParser)
                srcPath = stateParser.path;
            else
                srcPath = default;
            destPath = srcPath;
        }

        protected override void OnDetech() {
            if (absolutePath)
                current.toStatePath = destPath;
            else if (!string.IsNullOrEmpty(gotoStateName))
                current.toStatePath = destPath + gotoStateName;
            if (state != null)
                current.fromStatePath = srcPath + state.name;
            if (conditions.Count == 0) {
                current.conditions = new AnimatorCondition[0];
                transitions.Add(current);
            } else foreach (var cond in conditions) {
                current.conditions = cond.ToArray();
                transitions.Add(current);
            }
            srcPath = default;
            base.OnDetech();
        }
    }

    public struct TransitionData {
        public AnimatorStateMachine fromStateMachine;
        public StateMachinePath fromStatePath;
        public StateMachinePath toStatePath;
        public bool isAny;
        public bool isMuted;
        public bool isSolo;
        public bool hasFixedDuration;
        public bool hasExitTime;
        public bool isExit;
        public bool canTransitionToSelf;
        public bool orderedInterruption;
        public float duration;
        public float exitTime;
        public float offset;
        public TransitionInterruptionSource interruptionSource;
        public AnimatorCondition[] conditions;
    }
}