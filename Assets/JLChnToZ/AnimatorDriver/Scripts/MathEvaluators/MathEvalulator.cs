using System;

namespace JLChnToZ.MathUtilities {
    public class MathEvalulator : AbstractMathEvalulator<double> {
        Random randomProvider;

        public Random RandomProvider {
            get => randomProvider;
            set => randomProvider = value;
        }

        protected override double Truely => 1;

        protected override double Error => double.NaN;

        static bool IsSafeInteger(double value) => value >= int.MinValue && value <= int.MaxValue;

        protected override double ParseNumber(string value) => double.Parse(value);

        protected override bool IsTruely(double value) => value != 0 && !double.IsNaN(value);

        #region Operators
        [Processor(TokenType.Add)] double Add(double first, double second) => first + second;

        [Processor(TokenType.Subtract)] double Subtract(double first, double second) => first - second;

        [Processor(TokenType.Multiply)] double Multiply(double first, double second) => first * second;

        [Processor(TokenType.Divide)] double Divide(double first, double second) => first / second;

        [Processor(TokenType.Modulo)] double Modulo(double first, double second) => first % second;

        [Processor(TokenType.Power)] double Power(double first, double second) => Math.Pow(first, second);

        [Processor(TokenType.UnaryPlus)] double UnaryPlus(double value) => value;

        [Processor(TokenType.UnaryMinus)] double UnaryMinus(double value) => -value;

        [Processor(TokenType.BitwiseAnd)]
        double BitwiseAnd(double first, double second) =>
            IsSafeInteger(first) && IsSafeInteger(second) ? unchecked((int)first & (int)second) : double.NaN;

        [Processor(TokenType.BitwiseOr)]
        double BitwiseOr(double first, double second) =>
            IsSafeInteger(first) && IsSafeInteger(second) ? unchecked((int)first | (int)second) : double.NaN;

        [Processor(TokenType.BitwiseXor)]
        double BitwiseXor(double first, double second) =>
            IsSafeInteger(first) && IsSafeInteger(second) ? unchecked((int)first ^ (int)second) : double.NaN;

        [Processor(TokenType.BitwiseNot)]
        double BitwiseNot(double value) =>
            IsSafeInteger(value) ? unchecked((int)value) : double.NaN;

        [Processor(TokenType.LeftShift)]
        double LeftShift(double value, double shift) =>
            IsSafeInteger(value) && IsSafeInteger(shift) ? unchecked((int)value << (int)shift) : double.NaN;

        [Processor(TokenType.RightShift)]
        double RightShift(double value, double shift) =>
            IsSafeInteger(value) && IsSafeInteger(shift) ? unchecked((int)value >> (int)shift) : double.NaN;

        [Processor("abs")] static double Abs(ReadOnlySpan<double> args) => Math.Abs(args[0]);

        [Processor("sqrt")] static double Sqrt(ReadOnlySpan<double> args) => Math.Sqrt(args[0]);

        [Processor("cbrt")] static double Cbrt(ReadOnlySpan<double> args) => Math.Cbrt(args[0]);

        [Processor("pow")] static double Pow(ReadOnlySpan<double> args) => Math.Pow(args[0], args[1]);

        [Processor("lerp")] static double Lerp(ReadOnlySpan<double> args) => args[0] + (args[1] - args[0]) * args[2];

        [Processor("remap")] static double Remap(ReadOnlySpan<double> args) => args[3] + (args[4] - args[3]) * (args[0] - args[1]) / (args[2] - args[1]);

        [Processor("saturate")] static double Saturate(ReadOnlySpan<double> args) => Math.Clamp(args[0], 0, 1);

        [Processor("sign")] static double Sign(ReadOnlySpan<double> args) => Math.Sign(args[0]);

        [Processor("round")] static double Round(ReadOnlySpan<double> args) => Math.Round(args[0]);

        [Processor("floor")] static double Floor(ReadOnlySpan<double> args) => Math.Floor(args[0]);

        [Processor("ceil")] static double Ceil(ReadOnlySpan<double> args) => Math.Ceiling(args[0]);

        [Processor("trunc")] static double Trunc(ReadOnlySpan<double> args) => Math.Truncate(args[0]);

        [Processor("sin")] static double Sin(ReadOnlySpan<double> args) => Math.Sin(args[0]);

        [Processor("cos")] static double Cos(ReadOnlySpan<double> args) => Math.Cos(args[0]);

        [Processor("tan")] static double Tan(ReadOnlySpan<double> args) => Math.Tan(args[0]);

        [Processor("asin")] static double Asin(ReadOnlySpan<double> args) => Math.Asin(args[0]);

        [Processor("acos")] static double Acos(ReadOnlySpan<double> args) => Math.Acos(args[0]);

        [Processor("atan")]
        static double Atan(ReadOnlySpan<double> args) => args.Length switch {
            1 => Math.Atan(args[0]),
            2 => Math.Atan2(args[0], args[1]),
            _ => double.NaN,
        };

        [Processor("sinh")] static double Sinh(ReadOnlySpan<double> args) => Math.Sinh(args[0]);

        [Processor("cosh")] static double Cosh(ReadOnlySpan<double> args) => Math.Cosh(args[0]);

        [Processor("tanh")] static double Tanh(ReadOnlySpan<double> args) => Math.Tanh(args[0]);

        [Processor("asinh")] static double Asinh(ReadOnlySpan<double> args) => Math.Asinh(args[0]);

        [Processor("acosh")] static double Acosh(ReadOnlySpan<double> args) => Math.Acosh(args[0]);

        [Processor("atanh")] static double Atanh(ReadOnlySpan<double> args) => Math.Atanh(args[0]);

        [Processor("log")]
        static double Log(ReadOnlySpan<double> args) => args.Length switch {
            1 => Math.Log(args[0]),
            2 => Math.Log(args[0], args[1]),
            _ => double.NaN,
        };

        [Processor("exp")] static double Exp(ReadOnlySpan<double> args) => Math.Exp(args[0]);

        [Processor("log10")] static double Log10(ReadOnlySpan<double> args) => Math.Log10(args[0]);

        [Processor("log2")] static double Log2(ReadOnlySpan<double> args) => Math.Log(args[0], 2);

        [Processor("random", false)]
        double Random(ReadOnlySpan<double> args) {
            randomProvider ??= new Random();
            return args.Length switch {
                0 => randomProvider.NextDouble(),
                1 => randomProvider.Next((int)args[0]),
                2 => randomProvider.Next((int)args[0], (int)args[1]),
                _ => double.NaN,
            };
        }

        [Processor("isnan")] static double IsNaN(ReadOnlySpan<double> args) => double.IsNaN(args[0]) ? 1F : 0F;

        [Processor("switch")]
        static double Switch(ReadOnlySpan<double> args) {
            var index = args[0];
            return args.Length > 1 && double.IsFinite(index) ?
                args[(int)(index % (args.Length - 1) + (index < 0 ? args.Length : 1))] :
                double.NaN;
        }
        #endregion
    }
}