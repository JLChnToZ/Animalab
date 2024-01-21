using System;
using UnityEngine;

namespace JLChnToZ.Animalab {
    internal class VariableParser : AnimalabParserBase {
        AnimatorControllerParameter param;
        protected override string Hint {
            get {
                if (param != null) return $"Parameter {param.name}";
                return "Parameter";
            }
        }


        protected override void OnParse(TokenType type, string token, bool hasLineBreak, int indentLevel) {
            switch (nextNode) {
                case Node.Default:
                    if (type == TokenType.Identifier && Enum.TryParse(token, true, out AnimatorControllerParameterType p)) {
                        param.type = p;
                        nextNode = Node.Parameter;
                        return;
                    }
                    break;
                case Node.Parameter:
                    switch (type) {
                        case TokenType.Identifier:
                        case TokenType.SingleQuotedString:
                        case TokenType.DoubleQuotedString:
                            param.name = token;
                            nextNode = Node.Operator;
                            return;
                    }
                    break;
                case Node.Operator:
                    if (type == TokenType.Symbol && token == "=") {
                        nextNode = Node.Value;
                        return;
                    }
                    break;
                case Node.Value:
                    switch (type) {
                        case TokenType.Identifier:
                            switch (param.type) {
                                case AnimatorControllerParameterType.Bool:
                                    if (bool.TryParse(token, out var boolValue))
                                        param.defaultBool = boolValue;
                                    break;
                                case AnimatorControllerParameterType.Trigger:
                                    if (bool.TryParse(token, out var triggerValue) && triggerValue)
                                        param.defaultBool = true;
                                    break;
                            }
                            nextNode = Node.Unknown;
                            return;
                        case TokenType.Number:
                            switch (param.type) {
                                case AnimatorControllerParameterType.Float:
                                    if (float.TryParse(token, out var floatValue))
                                        param.defaultFloat = floatValue;
                                    break;
                                case AnimatorControllerParameterType.Int:
                                    if (int.TryParse(token, out var intValue))
                                        param.defaultInt = intValue;
                                    break;
                            }
                            nextNode = Node.Unknown;
                            return;
                    }
                    break;
            }
            if (type == TokenType.Symbol && token == ";") {
                Detech();
                return;
            }
            throw new Exception($"Unexpected token. {type} {token}");
        }

        protected override void OnAttach(StackParser parent) {
            base.OnAttach(parent);
            param = new AnimatorControllerParameter();
        }

        protected override void OnDetech() {
            if (controller != null && !string.IsNullOrEmpty(param.name))
                controller.AddParameter(param);
            base.OnDetech();
        }
    }
}