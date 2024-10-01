using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityRandom = UnityEngine.Random;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JLChnToZ.MathUtilities {
    using RawToken = AbstractMathEvalulator<float>.Token;

    public class UnityMathEvalulator : AbstractMathEvalulator<float> {
        public const int MAX_SAFE_INTEGER = 0x7FFFFF;
        static readonly string[] defaultMappings = new[] {
            "abs", "sqrt", "cbrt", "pow", "lerp", "remap", "saturate", "sign", "round", "floor", "ceil", "trunc",
            "sin", "cos", "tan", "asin", "acos", "atan", "sinh", "cosh", "tanh", "asinh", "acosh", "atanh",
            "log", "exp", "log10", "log2", "random", "isnan", "switch", "min", "max", "clamp"
        };

        static UnityMathEvalulator instance;

        internal static UnityMathEvalulator Instance {
            get {
                if (instance == null) {
                    instance = new UnityMathEvalulator();
                    instance.RegisterDefaultFunctions();
                }
                return instance;
            }
        }

        protected override float Truely => 1F;

        protected override float Error => float.NaN;

        public UnityMathEvalulator() : base(defaultMappings) { }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DefaultEvalulate(string expression) {
            var instance = Instance;
            return instance.Evalulate(instance.Parse(expression));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Evaluate(UnityMathExpression source) =>
            source.Evaluate(this);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DefaultParse(ref UnityMathExpression source) =>
            source.Parse();

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float DefaultEvalulate(UnityMathExpression source) =>
            source.Evaluate();

        protected override float ParseNumber(string value) => float.Parse(value);

        protected override bool IsTruely(float value) => value != 0 && !float.IsNaN(value);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsSafeInteger(float value) => value >= int.MinValue && value <= int.MaxValue;

        #region Operators
        protected override float Equals(float first, float second) => Mathf.Approximately(first, second) ? 1F : 0F;

        protected override float NotEquals(float first, float second) => Mathf.Approximately(first, second) ? 0F : 1F;

        protected override float GreaterThan(float first, float second) => first > second ? 1F : 0F;

        protected override float GreaterThanOrEquals(float first, float second) => first >= second ? 1F : 0F;

        protected override float LessThan(float first, float second) => first < second ? 1F : 0F;

        protected override float LessThanOrEquals(float first, float second) => first <= second ? 1F : 0F;

        [Processor(TokenType.Add)] float Add(float first, float second) => first + second;

        [Processor(TokenType.Subtract)] float Subtract(float first, float second) => first - second;

        [Processor(TokenType.Multiply)] float Multiply(float first, float second) => first * second;

        [Processor(TokenType.Divide)] float Divide(float first, float second) => first / second;

        [Processor(TokenType.Modulo)] float Modulo(float first, float second) => first % second;

        [Processor(TokenType.Power)] float Power(float first, float second) => Mathf.Pow(first, second);

        [Processor(TokenType.UnaryPlus)] float UnaryPlus(float value) => value;

        [Processor(TokenType.UnaryMinus)] float UnaryMinus(float value) => -value;

        [Processor(TokenType.BitwiseAnd)]
        float BitwiseAnd(float first, float second) =>
            IsSafeInteger(first) && IsSafeInteger(second) ? unchecked((int)first & (int)second) : float.NaN;

        [Processor(TokenType.BitwiseOr)]
        float BitwiseOr(float first, float second) =>
            IsSafeInteger(first) && IsSafeInteger(second) ? unchecked((int)first | (int)second) : float.NaN;

        [Processor(TokenType.BitwiseXor)]
        float BitwiseXor(float first, float second) =>
            IsSafeInteger(first) && IsSafeInteger(second) ? unchecked((int)first ^ (int)second) : float.NaN;

        [Processor(TokenType.BitwiseNot)]
        float BitwiseNot(float value) =>
            IsSafeInteger(value) ? ~unchecked((int)value) & MAX_SAFE_INTEGER : float.NaN;

        [Processor(TokenType.LeftShift)]
        float LeftShift(float value, float shift) =>
            IsSafeInteger(value) && IsSafeInteger(shift) ? unchecked((int)value << (int)shift) : float.NaN;

        [Processor(TokenType.RightShift)]
        float RightShift(float value, float shift) =>
            IsSafeInteger(value) && IsSafeInteger(shift) ? unchecked((int)value >> (int)shift) : float.NaN;

        [Processor("abs")] static float Abs(ReadOnlySpan<float> args) => args.Length > 0 ? Mathf.Abs(args[0]) : float.NaN;

        [Processor("sqrt")] static float Sqrt(ReadOnlySpan<float> args) => args.Length > 0 ? Mathf.Sqrt(args[0]) : float.NaN;

        [Processor("cbrt")] static float Cbrt(ReadOnlySpan<float> args) => args.Length > 0 ? (float)Math.Cbrt(args[0]) : float.NaN;

        [Processor("pow")] static float Pow(ReadOnlySpan<float> args) => args.Length >= 2 ? Mathf.Pow(args[0], args[1]) : float.NaN;

        [Processor("lerp")] static float Lerp(ReadOnlySpan<float> args) => args.Length >= 3 ? Mathf.LerpUnclamped(args[0], args[1], args[2]) : float.NaN;

        [Processor("remap")] static float Remap(ReadOnlySpan<float> args) => args.Length >= 5 ? Mathf.LerpUnclamped(args[0], args[1], Mathf.InverseLerp(args[3], args[4], args[2])) : float.NaN;

        [Processor("saturate")] static float Saturate(ReadOnlySpan<float> args) => args.Length > 0 ? Mathf.Clamp01(args[0]) : float.NaN;

        [Processor("sign")] static float Sign(ReadOnlySpan<float> args) => args.Length > 0 ? Mathf.Sign(args[0]) : float.NaN;

        [Processor("round")] static float Round(ReadOnlySpan<float> args) => args.Length > 0 ? Mathf.Round(args[0]) : float.NaN;

        [Processor("floor")] static float Floor(ReadOnlySpan<float> args) => args.Length > 0 ? Mathf.Floor(args[0]) : float.NaN;

        [Processor("ceil")] static float Ceil(ReadOnlySpan<float> args) => args.Length > 0 ? Mathf.Ceil(args[0]) : float.NaN;

        [Processor("trunc")] static float Trunc(ReadOnlySpan<float> args) => args.Length > 0 ? (float)Math.Truncate(args[0]) : float.NaN;

        [Processor("sin")] static float Sin(ReadOnlySpan<float> args) => args.Length > 0 ? Mathf.Sin(args[0]) : float.NaN;

        [Processor("cos")] static float Cos(ReadOnlySpan<float> args) => args.Length > 0 ? Mathf.Cos(args[0]) : float.NaN;

        [Processor("tan")] static float Tan(ReadOnlySpan<float> args) => args.Length > 0 ? Mathf.Tan(args[0]) : float.NaN;

        [Processor("asin")] static float Asin(ReadOnlySpan<float> args) => args.Length > 0 ? Mathf.Asin(args[0]) : float.NaN;

        [Processor("acos")] static float Acos(ReadOnlySpan<float> args) => args.Length > 0 ? Mathf.Acos(args[0]) : float.NaN;

        [Processor("atan")]
        static float Atan(ReadOnlySpan<float> args) => args.Length switch {
            1 => Mathf.Atan(args[0]),
            2 => Mathf.Atan2(args[0], args[1]),
            _ => float.NaN,
        };

        [Processor("sinh")] static float Sinh(ReadOnlySpan<float> args) => args.Length > 0 ? (float)Math.Sinh(args[0]) : float.NaN;

        [Processor("cosh")] static float Cosh(ReadOnlySpan<float> args) => args.Length > 0 ? (float)Math.Cosh(args[0]) : float.NaN;

        [Processor("tanh")] static float Tanh(ReadOnlySpan<float> args) => args.Length > 0 ? (float)Math.Tanh(args[0]) : float.NaN;

        [Processor("asinh")] static float Asinh(ReadOnlySpan<float> args) => args.Length > 0 ? (float)Math.Asinh(args[0]) : float.NaN;

        [Processor("acosh")] static float Acosh(ReadOnlySpan<float> args) => args.Length > 0 ? (float)Math.Acosh(args[0]) : float.NaN;

        [Processor("atanh")] static float Atanh(ReadOnlySpan<float> args) => args.Length > 0 ? (float)Math.Atanh(args[0]) : float.NaN;

        [Processor("log")]
        static float Log(ReadOnlySpan<float> args) => args.Length switch {
            1 => Mathf.Log(args[0]),
            2 => Mathf.Log(args[0], args[1]),
            _ => float.NaN,
        };

        [Processor("exp")] static float Exp(ReadOnlySpan<float> args) => args.Length > 0 ? Mathf.Exp(args[0]) : float.NaN;

        [Processor("log10")] static float Log10(ReadOnlySpan<float> args) => args.Length > 0 ? (float)Math.Log10(args[0]) : float.NaN;

        [Processor("log2")] static float Log2(ReadOnlySpan<float> args) => args.Length > 0 ? (float)Math.Log(args[0], 2) : float.NaN;

        [Processor("random", false)]
        static float Random(ReadOnlySpan<float> args) => args.Length switch {
            0 => UnityRandom.value,
            1 => UnityRandom.value * args[0],
            2 => UnityRandom.Range(args[0], args[1]),
            _ => float.NaN,
        };

        [Processor("isnan")] static float IsNaN(ReadOnlySpan<float> args) => args.Length > 0 ? float.IsNaN(args[0]) ? 1F : 0F : float.NaN;

        [Processor("switch")]
        static float Switch(ReadOnlySpan<float> args) {
            var index = args[0];
            return args.Length > 1 && float.IsFinite(index) ?
                args[(int)(index % (args.Length - 1) + (index < 0 ? args.Length : 1))] :
                float.NaN;
        }
        #endregion
    }

    [Serializable]
    public struct UnityMathExpression : ISerializationCallbackReceiver {
        [SerializeField] string expression;
        [SerializeField] SeralizableTokens[] tokens;
        internal RawToken[] convertedTokens;
        string prevExpression;
        bool optimized;

        public string Expression {
            readonly get => expression;
            set {
                expression = value;
                convertedTokens = null;
                optimized = false;
            }
        }

        public RawToken[] Tokens {
            readonly get => convertedTokens;
            set {
                convertedTokens = value;
                expression = "";
                optimized = false;
            }
        }

        public RawToken[] GetOptimizedTokens(UnityMathEvalulator evalulator = null, bool addIdIfNotExists = false) {
            evalulator ??= UnityMathEvalulator.Instance;
            if (convertedTokens == null || convertedTokens.Length == 0)
                convertedTokens = evalulator.Parse(expression, true);
            if (!optimized) {
                evalulator.OptimizeTokens(convertedTokens, addIdIfNotExists);
                optimized = true;
            }
            return convertedTokens;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Parse(UnityMathEvalulator evalulator = null, bool preEvaluate = true) {
            evalulator ??= UnityMathEvalulator.Instance;
            convertedTokens = evalulator.Parse(expression, preEvaluate);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float Evaluate(UnityMathEvalulator evalulator = null) {
            evalulator ??= UnityMathEvalulator.Instance;
            if (convertedTokens == null || convertedTokens.Length == 0)
                convertedTokens = evalulator.Parse(expression, true);
            return evalulator.Evalulate(convertedTokens);
        }

        public override readonly string ToString() => expression;

        void ISerializationCallbackReceiver.OnBeforeSerialize() {
            if (prevExpression != expression)
                try {
                    convertedTokens = UnityMathEvalulator.Instance.Parse(expression, true);
                } catch (Exception ex) {
                    Debug.LogError($"Failed to parse expression: {expression}\n{ex}");
                    convertedTokens = null;
                } finally {
                    prevExpression = expression;
                    optimized = false;
                }
            if (optimized) return; // Don't serialize optimized tokens
            if (convertedTokens == null || convertedTokens.Length == 0) {
                tokens = Array.Empty<SeralizableTokens>();
                return;
            }
            if (tokens == null || tokens.Length != convertedTokens.Length)
                tokens = new SeralizableTokens[convertedTokens.Length];
            for (var i = 0; i < convertedTokens.Length; i++)
                tokens[i] = convertedTokens[i];
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize() {
            if (tokens == null || tokens.Length == 0)
                convertedTokens = null;
            else {
                if (convertedTokens == null || convertedTokens.Length != tokens.Length)
                    convertedTokens = new RawToken[tokens.Length];
                for (var i = 0; i < tokens.Length; i++)
                    convertedTokens[i] = tokens[i];
            }
            optimized = false;
        }

        public static implicit operator string(UnityMathExpression expression) => expression.expression;

        [Serializable]
        struct SeralizableTokens {
            public TokenType type;
            public string identifierValue;
            public float numberValue;

            public static implicit operator RawToken(SeralizableTokens token) =>
                new RawToken {
                    type = token.type,
                    data = token.identifierValue,
                    numberValue = token.numberValue
                };

            public static implicit operator SeralizableTokens(RawToken token) =>
                new SeralizableTokens {
                    type = token.type,
                    identifierValue = token.data as string,
                    numberValue = token.numberValue
                };

            public override readonly string ToString() => ((RawToken)this).ToString();
        }

#if UNITY_EDITOR
        [CustomPropertyDrawer(typeof(UnityMathExpression))]
        class Drawer : PropertyDrawer {
            public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
                using (new EditorGUI.PropertyScope(position, label, property)) {
                    var expressionProperty = property.FindPropertyRelative(nameof(expression));
                    var tokenProperty = property.FindPropertyRelative(nameof(tokens));
                    var orgColor = GUI.color;
                    if (tokenProperty.arraySize == 0) GUI.color = Color.red;
                    expressionProperty.stringValue = EditorGUI.TextField(position, label, expressionProperty.stringValue);
                    GUI.color = orgColor;
                }
            }

            public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
                EditorGUIUtility.singleLineHeight;
        }
#endif
    }
}