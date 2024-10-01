using System;
using System.Collections.Generic;
#if ZSTRING_INCLUDED
using Cysharp.Text;
#else
using System.Text;
#endif

namespace JLChnToZ.MathUtilities {
    public abstract partial class AbstractMathEvalulator<TNumber> where TNumber : struct {
        static readonly Dictionary<string, TokenType> tokenMap = new(StringComparer.OrdinalIgnoreCase);
        static readonly byte[] precedences = new byte[(int)TokenType.MaxToken]; // Lower value means higher precedence
        static readonly sbyte[] argumentInfos = new sbyte[(int)TokenType.MaxToken]; // Negative value means the operator is right-to-left
#if ZSTRING_INCLUDED
        Utf16ValueStringBuilder sb;
#else
        StringBuilder sb;
#endif
        FastStack<Token> ops, result;
        FastStack<int> backtraceStack;

        static AbstractMathEvalulator() {
            DefineToken(TokenType.Primitive,           null, 0,    0);
            DefineToken(TokenType.Identifier,          null, 0,    0);
            DefineToken(TokenType.External,            null, 1, -127);
            DefineToken(TokenType.UnaryPlus,           "+",  1,   -1);
            DefineToken(TokenType.UnaryMinus,          "-",  1,   -1);
            DefineToken(TokenType.LogicalNot,          "!",  1,   -1);
            DefineToken(TokenType.BitwiseNot,          "~",  1,   -1);
            DefineToken(TokenType.Multiply,            "*",  2,    2);
            DefineToken(TokenType.Divide,              "/",  2,    2);
            DefineToken(TokenType.Modulo,              "%",  2,    2);
            DefineToken(TokenType.Power,               "**", 2,    2);
            DefineToken(TokenType.Add,                 "+",  3,    2);
            DefineToken(TokenType.Subtract,            "-",  3,    2);
            DefineToken(TokenType.LeftShift,           "<<", 4,    2);
            DefineToken(TokenType.RightShift,          ">>", 4,    2);
            DefineToken(TokenType.GreaterThan,         ">",  5,    2);
            DefineToken(TokenType.GreaterThanOrEquals, ">=", 5,    2);
            DefineToken(TokenType.LessThan,            "<",  5,    2);
            DefineToken(TokenType.LessThanOrEquals,    "<=", 5,    2);
            DefineToken(TokenType.Equals,              "==", 6,    2);
            DefineToken(TokenType.NotEquals,           "!=", 6,    2);
            DefineToken(TokenType.BitwiseAnd,          "&",  7,    2);
            DefineToken(TokenType.BitwiseXor,          "^",  8,    2);
            DefineToken(TokenType.BitwiseOr,           "!",  9,    2);
            DefineToken(TokenType.LogicalAnd,          "&&", 10,   2);
            DefineToken(TokenType.LogicalOr,           "||", 11,   2);
            DefineToken(TokenType.If,                  "?",  12,   2);
            DefineToken(TokenType.Else,                ":",  13,   4);
            DefineToken(TokenType.LeftParenthesis,     "(",  0,    0);
            DefineToken(TokenType.RightParenthesis,    ")",  0,    0);
            DefineToken(TokenType.Comma,               ",",  14,   0);
        }

        static void DefineToken(TokenType tokenType, string input, byte precedence, sbyte argumentInfo) {
            if (input != null) tokenMap[input] = tokenType;
            precedences[(int)tokenType] = precedence;
            argumentInfos[(int)tokenType] = argumentInfo;
        }

        public Token[] Parse(string expression, bool preEvaluated = false) =>
            string.IsNullOrWhiteSpace(expression) ? Array.Empty<Token>() :
            ShuntingYard(Tokenize(expression), preEvaluated);

        IEnumerable<Token> Tokenize(IEnumerable<char> expression) {
#if ZSTRING_INCLUDED
            using (sb = ZString.CreateStringBuilder()) {
#else
            sb ??= new();
#endif
            var type = ParseType.Unknown;
            var previousType = ParseType.Unknown;
            var previousTokenType = TokenType.Unknown;
            bool preivousWhiteSpace = false;
            foreach (var c in expression)
                switch (preivousWhiteSpace ? ParseType.Unknown : type) {
                    case ParseType.Number:
                        if (c == '.') {
                            previousType = type;
                            type = ParseType.NumberWithDot;
                            sb.Append(c);
                            break;
                        }
                        goto case ParseType.NumberWithDot;
                    case ParseType.NumberWithDot:
                        if (char.IsDigit(c)) {
                            sb.Append(c);
                            break;
                        }
                        goto default;
                    case ParseType.Identifier:
                        if (char.IsLetterOrDigit(c) || c == '_') {
                            sb.Append(c);
                            break;
                        }
                        goto default;
                    case ParseType.Operator:
                        if ((char.IsSymbol(c) || char.IsPunctuation(c)) && c != '.') {
                            sb.Append(c);
                            break;
                        }
                        goto default;
                    default:
                        foreach (var token in YieldTokens(previousTokenType, previousType, type)) {
                            yield return token;
                            previousTokenType = token.type;
                        }
                        if (char.IsWhiteSpace(c)) {
                            preivousWhiteSpace = true;
                            break;
                        }
                        preivousWhiteSpace = false;
                        previousType = type;
                        sb.Append(c);
                        switch (c) {
                            case '.': type = ParseType.NumberWithDot; break;
                            case '_': type = ParseType.Identifier; break;
                            default:
                                if (char.IsDigit(c)) {
                                    type = ParseType.Number;
                                    break;
                                }
                                if (char.IsLetter(c)) {
                                    type = ParseType.Identifier;
                                    break;
                                }
                                if (char.IsSymbol(c) || char.IsPunctuation(c)) {
                                    type = ParseType.Operator;
                                    break;
                                }
                                throw new Exception($"Unexpected token '{c}'");
                        }
                        break;
                }
            foreach (var token in YieldTokens(previousTokenType, previousType, type))
                yield return token;
#if ZSTRING_INCLUDED
            }
#endif
        }

        IEnumerable<Token> YieldTokens(TokenType prevTokenType, ParseType prevType, ParseType type) {
            if (sb.Length <= 0) yield break;
            var tokenData = sb.ToString();
            sb.Clear();
            TokenType tokenType;
            switch (type) {
                case ParseType.Number:
                case ParseType.NumberWithDot:
                    yield return new(ParseNumber(tokenData));
                    break;
                case ParseType.Operator:
                    for (int offset = 0, length = tokenData.Length; offset < tokenData.Length;) {
                        var token = offset == 0 && length == tokenData.Length ?
                            tokenData : tokenData.Substring(offset, length);
                        if (tokenMap.TryGetValue(token, out tokenType)) {
                            switch (tokenType) {
                                case TokenType.Add:
                                    if (offset > 0 || (
                                        prevType != ParseType.Number &&
                                        prevType != ParseType.NumberWithDot &&
                                        prevType != ParseType.Identifier &&
                                        prevTokenType != TokenType.RightParenthesis)) {
                                        yield return new(TokenType.UnaryPlus);
                                        prevTokenType = TokenType.UnaryPlus;
                                        break;
                                    }
                                    goto default;
                                case TokenType.Subtract:
                                    if (offset > 0 || (
                                        prevType != ParseType.Number &&
                                        prevType != ParseType.NumberWithDot &&
                                        prevType != ParseType.Identifier &&
                                        prevTokenType != TokenType.RightParenthesis)) {
                                        yield return new(TokenType.UnaryMinus);
                                        prevTokenType = TokenType.UnaryMinus;
                                        break;
                                    }
                                    goto default;
                                default:
                                    yield return new(tokenType);
                                    prevTokenType = tokenType;
                                    break;
                            }
                            offset += length;
                            length = tokenData.Length - offset;
                        } else if (--length <= 0)
                            throw new Exception($"Unknown operator '{tokenData[offset..]}'");
                    }
                    break;
                case ParseType.Identifier:
                    if (tokenMap.TryGetValue(tokenData, out tokenType)) {
                        yield return new(tokenType);
                        break;
                    }
                    TryGetId(tokenData, out var identifierHash);
                    yield return new(TokenType.Identifier, tokenData, identifierHash);
                    break;
            }
        }

        Token[] ShuntingYard(IEnumerable<Token> tokens, bool preEvaluated) {
            try {
                Token lastToken = default;
                foreach (var token in tokens) {
                    switch (token.type) {
                        case TokenType.Primitive:
                            PushPreviousIdentifier(ref lastToken, true, preEvaluated);
                            PushToken(token, preEvaluated);
                            break;
                        case TokenType.Identifier:
                            PushPreviousIdentifier(ref lastToken, true, preEvaluated);
                            lastToken = token;
                            break;
                        case TokenType.LeftParenthesis:
                            PushPreviousIdentifier(ref lastToken, true, preEvaluated);
                            ops.Push(token);
                            break;
                        case TokenType.Comma:
                        case TokenType.RightParenthesis:
                            PushPreviousIdentifier(ref lastToken, false, preEvaluated);
                            while (ops.TryPeek(out var top)) {
                                if (top.type == TokenType.If)
                                    throw new Exception("Mismatched parenthesis");
                                if (top.type == TokenType.LeftParenthesis) break;
                                PushToken(top, preEvaluated);
                                ops.Pop();
                            }
                            if (token.type != TokenType.Comma) {
                                if (ops.Count == 0)
                                    throw new Exception("Mismatched parenthesis");
                                ops.Pop();
                            }
                            break;
                        case TokenType.Else:
                            PushPreviousIdentifier(ref lastToken, false, preEvaluated);
                            while (ops.TryPop(out var top)) {
                                if (top.type == TokenType.LeftParenthesis)
                                    throw new Exception("Mismatched parenthesis");
                                PushToken(top, preEvaluated);
                                if (top.type == TokenType.If) break;
                            }
                            ops.Push(token);
                            break;
                        default:
                            PushPreviousIdentifier(ref lastToken, false, preEvaluated);
                            if (argumentInfos[(int)token.type] < 0)
                                while (ops.TryPeek(out var top)) {
                                    if (precedences[(int)top.type] > precedences[(int)token.type] ||
                                        top.type == TokenType.LeftParenthesis) break;
                                    PushToken(top, preEvaluated);
                                    ops.Pop();
                                }
                            ops.Push(token);
                            break;
                    }
                }
                PushPreviousIdentifier(ref lastToken, false, preEvaluated);
                while (ops.TryPop(out var token))
                    switch (token.type) {
                        case TokenType.LeftParenthesis:
                            throw new Exception("Mismatched parenthesis");
                        default:
                            PushToken(token, preEvaluated);
                            break;
                    }
                return valueStack.Count == 1 ? new[] { new Token(valueStack.Pop()) } : result.Pop(result.Count).ToArray();
            } finally {
                ops.Clear();
                result.Clear();
                valueStack.Clear();
                conditionStack.Clear();
                argumentStack.Clear();
            }
        }

        void PushPreviousIdentifier(ref Token lastToken, bool treatAsExternal, bool preEvaluated) {
            if (lastToken.type != TokenType.Identifier) return;
            if (treatAsExternal) {
                ops.Push(new Token(TokenType.External, lastToken.data, lastToken.identifierIndex));
                PushToken(new(TokenType.LeftParenthesis), preEvaluated);
            } else
                PushToken(lastToken, preEvaluated);
            lastToken = default;
        }

        void PushToken(Token token, bool preEvaluated) {
            if (preEvaluated && EvaluateOperatorOnResultStack(token.type, token.identifierIndex, out var argc, out var value)) {
                if (argc < 0) return; // Already handled
                result.Pop(argc);
                token = new(value);
            }
            result.Push(token);
        }

        bool EvaluateOperatorOnResultStack(TokenType tokenType, ushort identifierIndex, out int argc, out TNumber value) {
            switch (tokenType) {
                case TokenType.External: return EvaluateExternalOnResultStack(identifierIndex, out argc, out value);
                case TokenType.Else: return EvelulateIfOnResultStack(out argc, out value);
            }
            int typeIndex = (int)tokenType;
            argc = Math.Abs(argumentInfos[typeIndex]);
            if (argc == 0 || result.Count < argc) {
                value = default;
                return false;
            }
            var fn = tokenProcessors[typeIndex];
            if (fn == null) {
                value = default;
                return false;
            }
            int valueStackOffset = valueStack.Count;
            try {
                var peekView = result.Peek(argc);
                for (int i = 0; i < argc; i++) {
                    if (peekView[i].type != TokenType.Primitive) {
                        value = default;
                        return false;
                    }
                    valueStack.Push(peekView[i].numberValue);
                }
                value = fn(new(tokenType));
            } finally {
                valueStack.Pop(valueStack.Count - valueStackOffset);
            }
            return true;
        }

        bool EvelulateIfOnResultStack(out int argc, out TNumber value) {
            argc = 0;
            value = default;
            int count = result.Count;
            var peekView = result.Peek(count); // [...condition][...true]<IF>[...false]<ELSE>
            int falseSlotIndex = FindLastInstructionBeginningOffset(peekView);
            if (falseSlotIndex <= 0 || peekView[falseSlotIndex - 1].type != TokenType.If)
                return false;
            int trueSlotIndex = FindLastInstructionBeginningOffset(peekView[..(falseSlotIndex - 1)]);
            if (trueSlotIndex <= 0 || peekView[trueSlotIndex - 1].type != TokenType.Primitive)
                return false;
            argc = -1;
            result.Pop(count - trueSlotIndex + 1);
            result.Push(IsTruely(peekView[trueSlotIndex - 1].numberValue) ?
                peekView[trueSlotIndex..(falseSlotIndex - 1)] :
                peekView[falseSlotIndex..]
            );
            return true;
        }

        int FindLastInstructionBeginningOffset(ReadOnlySpan<Token> tokens) {
            for (int i = tokens.Length - 1, left; i >= 0; i--) {
                if (backtraceStack.TryPop(out left) && left > 0)
                    backtraceStack.Push(left - 1);
                switch (tokens[i].type) {
                    case TokenType.External: backtraceStack.Push(-1); break;
                    case TokenType.LeftParenthesis: backtraceStack.Pop(); break;
                    default:
                        int argc = Math.Abs(argumentInfos[(int)tokens[i].type]);
                        if (argc > 0) backtraceStack.Push(argc);
                        break;
                }
                if (backtraceStack.Count <= 0) return i;
            }
            return -1;
        }

        bool EvaluateExternalOnResultStack(ushort identifierIndex, out int argc, out TNumber value) {
            argc = 0;
            if (isStaticFunction == null ||
                !(isStaticFunction.TryGetValue(identifierIndex, out var isStatic) && isStatic) ||
                !functionProcessors.TryGetValue(identifierIndex, out var fn)) {
                value = default;
                return false;
            }
            var peekView = result.Peek(result.Count);
            bool hasParenthesis = false;
            for (int i = peekView.Length - 1; i >= 0; i--) {
                if (peekView[i].type == TokenType.LeftParenthesis) {
                    i++;
                    peekView = i < peekView.Length ? peekView[i..] : ReadOnlySpan<Token>.Empty;
                    argc = peekView.Length + 1; // +1 for left parenthesis
                    hasParenthesis = true;
                    break;
                }
                if (peekView[i].type != TokenType.Primitive) {
                    value = default;
                    return false;
                }
            }
            if (!hasParenthesis) {
                value = default;
                return false;
            }
            foreach (var argToken in peekView) valueStack.Push(argToken.numberValue);
            value = fn(valueStack.Pop(peekView.Length));
            return true;
        }

        protected abstract TNumber ParseNumber(string value);

        enum ParseType : byte {
            Unknown, Number, NumberWithDot, Identifier, Operator,
        }

        [Serializable]
        public struct Token : IEquatable<Token> {
            public TokenType type;
            public TNumber numberValue;
            public ushort identifierIndex;
            public object data;

            public Token(TokenType type) {
                this.type = type;
                data = type == TokenType.Identifier || type == TokenType.External ? "" : null;
                identifierIndex = 0;
                numberValue = default;
            }

            public Token(TokenType type, object data, ushort identifierIndex = 0) {
                this.type = type;
                this.data = data;
                this.identifierIndex = identifierIndex;
                numberValue = default;
            }

            public Token(TNumber numberValue) {
                type = TokenType.Primitive;
                data = null;
                identifierIndex = 0;
                this.numberValue = numberValue;
            }

            public readonly bool Equals(Token other) =>
                type == other.type &&
                ((type != TokenType.Identifier && type != TokenType.External) || (identifierIndex != 0 && other.identifierIndex != 0 ?
                    identifierIndex == other.identifierIndex : data == other.data
                )) &&
                (type != TokenType.Primitive || EqualityComparer<TNumber>.Default.Equals(numberValue, other.numberValue));

            public override readonly bool Equals(object obj) => obj is Token other && Equals(other);

            public override readonly int GetHashCode() {
                var hashCode = new HashCode();
                hashCode.Add(type);
                switch (type) {
                    case TokenType.Identifier:
                    case TokenType.External:
                        if (identifierIndex != 0)
                            hashCode.Add(identifierIndex);
                        else
                            hashCode.Add(data);
                        break;
                    case TokenType.Primitive:
                        hashCode.Add(numberValue);
                        break;
                }
                return hashCode.ToHashCode();
            }

            public override readonly string ToString() => type switch {
                TokenType.Primitive => $"[{typeof(TNumber).Name}] {numberValue}",
                TokenType.Identifier => $"[Identifier] {data}",
                TokenType.External => $"[External] {data}",
                _ => $"[{type}]",
            };
        }
    }

    public enum TokenType : byte {
        Unknown, Primitive, Identifier, External,
        UnaryPlus, UnaryMinus,
        Add, Subtract, Multiply, Divide, Modulo, Power,
        LogicalAnd, LogicalOr, LogicalNot,
        BitwiseAnd, BitwiseOr, BitwiseXor, BitwiseNot,
        LeftShift, RightShift,
        GreaterThan, GreaterThanOrEquals,
        LessThan, LessThanOrEquals,
        Equals, NotEquals,
        If, Else,
        LeftParenthesis, RightParenthesis, Comma,

        MaxToken // Keep this at the end
    }
}