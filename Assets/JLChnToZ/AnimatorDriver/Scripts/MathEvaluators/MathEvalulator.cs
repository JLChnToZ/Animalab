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

        [Processor("abs")] static double Abs(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Abs(args[0]) : double.NaN;

        [Processor("sqrt")] static double Sqrt(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Sqrt(args[0]) : double.NaN;

        [Processor("cbrt")] static double Cbrt(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Cbrt(args[0]) : double.NaN;

        [Processor("pow")] static double Pow(ReadOnlySpan<double> args) => args.Length >= 2 ? Math.Pow(args[0], args[1]) : double.NaN;

        [Processor("lerp")] static double Lerp(ReadOnlySpan<double> args) => args.Length >= 3 ? args[0] + (args[1] - args[0]) * args[2] : double.NaN;

        [Processor("remap")] static double Remap(ReadOnlySpan<double> args) => args.Length >= 5 ? args[3] + (args[4] - args[3]) * (args[0] - args[1]) / (args[2] - args[1]) : double.NaN;

        [Processor("saturate")] static double Saturate(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Clamp(args[0], 0, 1) : double.NaN;

        [Processor("sign")] static double Sign(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Sign(args[0]) : double.NaN;

        [Processor("round")] static double Round(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Round(args[0]) : double.NaN;

        [Processor("floor")] static double Floor(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Floor(args[0]) : double.NaN;

        [Processor("ceil")] static double Ceil(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Ceiling(args[0]) : double.NaN;

        [Processor("trunc")] static double Trunc(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Truncate(args[0]) : double.NaN;

        [Processor("sin")] static double Sin(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Sin(args[0]) : double.NaN;

        [Processor("cos")] static double Cos(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Cos(args[0]) : double.NaN;

        [Processor("tan")] static double Tan(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Tan(args[0]) : double.NaN;

        [Processor("asin")] static double Asin(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Asin(args[0]) : double.NaN;

        [Processor("acos")] static double Acos(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Acos(args[0]) : double.NaN;

        [Processor("atan")]
        static double Atan(ReadOnlySpan<double> args) => args.Length switch {
            1 => Math.Atan(args[0]),
            2 => Math.Atan2(args[0], args[1]),
            _ => double.NaN,
        };

        [Processor("sinh")] static double Sinh(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Sinh(args[0]) : double.NaN;

        [Processor("cosh")] static double Cosh(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Cosh(args[0]) : double.NaN;

        [Processor("tanh")] static double Tanh(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Tanh(args[0]) : double.NaN;

        [Processor("asinh")] static double Asinh(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Asinh(args[0]) : double.NaN;

        [Processor("acosh")] static double Acosh(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Acosh(args[0]) : double.NaN;

        [Processor("atanh")] static double Atanh(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Atanh(args[0]) : double.NaN;

        [Processor("log")]
        static double Log(ReadOnlySpan<double> args) => args.Length switch {
            1 => Math.Log(args[0]),
            2 => Math.Log(args[0], args[1]),
            _ => double.NaN,
        };

        [Processor("exp")] static double Exp(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Exp(args[0]) : double.NaN;

        [Processor("log10")] static double Log10(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Log10(args[0]) : double.NaN;

        [Processor("log2")] static double Log2(ReadOnlySpan<double> args) => args.Length > 0 ? Math.Log(args[0], 2) : double.NaN;

        [Processor("random", false)]
        double Random(ReadOnlySpan<double> args) {
            randomProvider ??= new Random();
            return args.Length switch {
                0 => randomProvider.NextDouble(),
                1 => randomProvider.NextDouble() * args[0],
                2 => randomProvider.NextDouble() * (args[1] - args[0]) + args[0],
                _ => double.NaN,
            };
        }

        [Processor("isnan")] static double IsNaN(ReadOnlySpan<double> args) => args.Length > 0 ? double.IsNaN(args[0]) ? 1F : 0F : double.NaN;

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