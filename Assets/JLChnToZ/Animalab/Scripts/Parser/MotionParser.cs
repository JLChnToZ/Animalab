using System;
using UnityEditor.Animations;
using UnityEngine;

namespace JLChnToZ.Animalab {
    internal abstract class MotionParser : AnimalabParserBase {
        internal BlendTree parentBlendTree;
        internal Vector2 position;
        internal string directBlendParameter;
        protected Motion motion;
        protected float? speed, offset;
        protected bool isMirror;
        protected Vector2 childPosition;
        
        protected override string Hint {
            get {
                if (motion != null) return $"Motion {motion.name}";
                return "Motion";
            }
        }

        protected override void OnParse(TokenType type, string token, bool hasLineBreak, int indentLevel) {
            switch (nextNode) {
                case Node.Identifier:
                    switch (type) {
                        case TokenType.Symbol:
                            switch (token) {
                                case "*":
                                    nextNode = Node.Speed;
                                    return;
                                case "+": case "-":
                                    nextNode = Node.TimeOffset;
                                    return;
                            }
                            break;
                        case TokenType.Identifier:
                            switch (token) {
                                case "mirror":
                                    isMirror = true;
                                    return;
                            }
                            break;
                        case TokenType.Number:
                            if (float.TryParse(token, out var offset_)) {
                                offset = offset_;
                                return;
                            }
                            break;
                    }
                    break;
                case Node.Speed:
                    switch (type) {
                        case TokenType.Number:
                            if (float.TryParse(token, out var speed_)) {
                                speed = speed_;
                                nextNode = Node.Identifier;
                                return;
                            }
                            break;
                    }
                    break;
                case Node.TimeOffset:
                    switch (type) {
                        case TokenType.Number:
                            if (float.TryParse(token, out var offset_)) {
                                offset = offset_;
                                nextNode = Node.Identifier;
                                return;
                            }
                            break;
                    }
                    break;
            }
            if (type == TokenType.Symbol && token == ";") {
                Detech();
                return;
            }
            throw new Exception($"Unexpected {type} `{token}`.");
        }

        protected override void OnAttach(StackParser parent) {
            base.OnAttach(parent);
            motion = null;
            speed = null;
            offset = null;
            isMirror = false;
            if (parent is MotionParser parentMotionParser) {
                parentBlendTree = parentMotionParser.motion as BlendTree;
                position = parentMotionParser.childPosition;
                directBlendParameter = parentMotionParser.directBlendParameter;
            }
        }

        protected override void OnDetech() {
            if (parentBlendTree != null) {
                var children = parentBlendTree.children;
                Array.Resize(ref children, children.Length + 1);
                children[children.Length - 1] = new ChildMotion {
                    motion = motion,
                    threshold = position.x,
                    position = position,
                    directBlendParameter = directBlendParameter,
                    cycleOffset = offset.GetValueOrDefault(),
                    timeScale = speed.GetValueOrDefault(1),
                    mirror = isMirror,
                };
                parentBlendTree.children = children;
            } else if (state != null) {
                state.motion = motion;
                if (speed.HasValue) state.speed = speed.Value;
                if (offset.HasValue) state.cycleOffset = offset.Value;
            }
            base.OnDetech();
        }
    }

    internal class ClipParser : MotionParser {
        protected override void OnParse(TokenType type, string token, bool hasLineBreak, int indentLevel) {
            switch (nextNode) {
                case Node.Default:
                    switch (type) {
                        case TokenType.SingleQuotedString:
                        case TokenType.DoubleQuotedString:
                        motion = LoadAsset<AnimationClip>(token);
                        nextNode = Node.Identifier;
                        return;
                    }
                    break;
            }
            base.OnParse(type, token, hasLineBreak, indentLevel);
        }
    }

    internal class BlendTreeParser : MotionParser {
        bool hasCloseBrace;
        bool hasOpenBracket;
        int argIndex;

        private BlendTree blendTree {
            get => motion as BlendTree;
            set => motion = value;
        }

        protected override void OnParse(TokenType type, string token, bool hasLineBreak, int indentLevel) {
            if (hasCloseBrace) {
                base.OnParse(type, token, hasLineBreak, indentLevel);
                return;
            }
            switch (nextNode) {
                case Node.Default:
                    switch (type) {
                        case TokenType.SingleQuotedString:
                        case TokenType.DoubleQuotedString:
                            blendTree = new BlendTree {
                                name = token,
                                hideFlags = HideFlags.HideInHierarchy,
                            };
                            SaveAsset(blendTree);
                            nextNode = Node.Identifier;
                            return;
                    }
                    break;
                case Node.Identifier:
                    switch (type) {
                        case TokenType.Identifier:
                            switch (token) {
                                case "threshold":
                                    nextNode = Node.Threhold;
                                    return;
                            }
                            if (Enum.TryParse(token, true, out BlendTreeType blendTreeType)) {
                                blendTree.blendType = blendTreeType;
                                nextNode = Node.Parameter;
                                argIndex = 0;
                                return;
                            }
                            break;
                        case TokenType.Symbol:
                            switch (token) {
                                case "{":
                                    nextNode = Node.OpenBrace;
                                    return;
                            }
                            break;
                    }
                    break;
                case Node.Parameter:
                    switch (type) {
                        case TokenType.Symbol:
                            switch (token) {
                                case "(":
                                    if (hasOpenBracket) break;
                                    hasOpenBracket = true;
                                    argIndex = 0;
                                    return;
                                case ")":
                                    if (!hasOpenBracket) break;
                                    hasOpenBracket = false;
                                    nextNode = Node.Identifier;
                                    return;
                                case ",":
                                    argIndex++;
                                    return;
                            }
                            break;
                        case TokenType.Identifier:
                        case TokenType.SingleQuotedString:
                        case TokenType.DoubleQuotedString:
                            int requiredArgCount = 0;
                            switch (blendTree.blendType) {
                                case BlendTreeType.Simple1D: requiredArgCount = 1; break;
                                case BlendTreeType.SimpleDirectional2D:
                                case BlendTreeType.FreeformDirectional2D:
                                case BlendTreeType.FreeformCartesian2D: requiredArgCount = 2; break;
                            }
                            if (argIndex >= requiredArgCount) break;
                            switch (argIndex) {
                                case 0: blendTree.blendParameter = token; return;
                                case 1: blendTree.blendParameterY = token; return;
                            }
                            break;
                    }
                    if (!hasOpenBracket) goto case Node.Identifier;
                    break;
                case Node.Threhold:
                    switch (type) {
                        case TokenType.Symbol:
                            switch (token) {
                                case "(":
                                    if (hasOpenBracket) break;
                                    hasOpenBracket = true;
                                    argIndex = 0;
                                    return;
                                case ")":
                                    if (!hasOpenBracket) break;
                                    hasOpenBracket = false;
                                    nextNode = Node.Identifier;
                                    return;
                                case ",":
                                    argIndex++;
                                    return;
                            }
                            break;
                        case TokenType.Number:
                            if (float.TryParse(token, out var threhold))
                                switch (argIndex) {
                                    case 0: blendTree.minThreshold = threhold; return;
                                    case 1: blendTree.maxThreshold = threhold; return;
                                }
                            break;
                    }
                    if (!hasOpenBracket) goto case Node.Identifier;
                    break;
                case Node.OpenBrace:
                    switch (type) {
                        case TokenType.Symbol:
                            switch (token) {
                                case "(":
                                    if (hasOpenBracket) break;
                                    hasOpenBracket = true;
                                    argIndex = 0;
                                    return;
                                case ")":
                                    if (!hasOpenBracket) break;
                                    hasOpenBracket = false;
                                    nextNode = Node.AfterParameter;
                                    return;
                                case ",":
                                    argIndex++;
                                    return;
                                case ":":
                                    nextNode = Node.Value;
                                    return;
                                case "}":
                                    hasCloseBrace = true;
                                    nextNode = Node.Identifier;
                                    return;
                            }
                            break;
                        case TokenType.Identifier:
                        case TokenType.SingleQuotedString:
                        case TokenType.DoubleQuotedString:
                            if (hasOpenBracket && argIndex > 0) break;
                            directBlendParameter = token;
                            if (!hasOpenBracket) nextNode = Node.AfterParameter;
                            return;
                        case TokenType.Number:
                            if (float.TryParse(token, out var value))
                                switch (argIndex) {
                                    case 0:
                                        childPosition.x = value;
                                        if (!hasOpenBracket) nextNode = Node.AfterParameter;
                                        return;
                                    case 1:
                                        childPosition.y = value;
                                        if (!hasOpenBracket) nextNode = Node.AfterParameter;
                                        return;
                                }
                            break;
                    }
                    break;
                case Node.AfterParameter:
                    switch (type) {
                        case TokenType.Symbol:
                            switch (token) {
                                case ":":
                                    nextNode = Node.Value;
                                    return;
                            }
                            break;
                    }
                    break;
                case Node.Value:
                    switch (type) {
                        case TokenType.Identifier:
                            switch (token) {
                                case "clip":
                                    Attach<ClipParser>();
                                    nextNode = Node.OpenBrace;
                                    argIndex = 0;
                                    return;
                                case "blendtree":
                                    Attach<BlendTreeParser>();
                                    nextNode = Node.OpenBrace;
                                    argIndex = 0;
                                    return;
                            }
                            break;
                    }
                    break;
            }
            if (type == TokenType.Symbol && token == ";") {
                Detech();
                return;
            }
            throw new Exception($"Unexpected {type} `{token}`. (state = {nextNode})");
        }
    }
}