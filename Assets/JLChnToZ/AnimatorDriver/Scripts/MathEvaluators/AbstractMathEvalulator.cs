using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;

namespace JLChnToZ.MathUtilities {
    public partial class AbstractMathEvalulator<TNumber> {
        public delegate TNumber FunctionProcessor(ReadOnlySpan<TNumber> arguments);
        public delegate TNumber UnaryOperatorProcessor(TNumber value);
        public delegate TNumber BinaryOperatorProcessor(TNumber value, TNumber value2);

        FastStack<TNumber> valueStack;
        FastStack<bool> conditionStack;
        FastStack<int> argumentStack;
        IDictionary<string, ushort> idTable;
        IDictionary<ushort, TNumber> variables;
        IDictionary<ushort, Func<TNumber>> functionProcessors;
        IDictionary<ushort, bool> isStaticFunction;
        Func<Token, TNumber>[] tokenProcessors;
        IComparer<TNumber> comparer;
        Func<string, TNumber> getVariableFunc;
        Func<ushort, TNumber> getVariableFuncById;
        ushort idCounter;

        public IComparer<TNumber> Comparer {
            get => comparer;
            set => comparer = value ?? Comparer<TNumber>.Default;
        }

        public Func<string, TNumber> GetVariableFunc {
            get => getVariableFunc;
            set {
                getVariableFunc = value;
                getVariableFuncById = null;
                if (value != null) variables = null;
            }
        }

        public Func<ushort, TNumber> GetVariableFuncById {
            get => getVariableFuncById;
            set {
                getVariableFuncById = value;
                getVariableFunc = null;
                if (value != null) variables = null;
            }
        }

        public IDictionary<string, ushort> IdTable {
            get => new ReadOnlyDictionary<string, ushort>(idTable);
            set {
                if (value == null) throw new ArgumentNullException(nameof(value));
                if (idTable == null) {
                    idTable = new Dictionary<string, ushort>(value, StringComparer.OrdinalIgnoreCase);
                    return;
                }
                var oldMapping = new Dictionary<ushort, string>();
                foreach (var kv in idTable) {
                    oldMapping[kv.Value] = kv.Key;
                    idCounter = Math.Min(idCounter, kv.Value);
                }
                var duplicateCheck = new HashSet<ushort>();
                foreach (var kv in value) {
                    ushort key = kv.Value;
                    while (!duplicateCheck.Add(key))
                        key = --idCounter;
                    idTable[kv.Key] = key;
                }
                var oldVariables = variables;
                if (oldVariables != null && oldVariables.Count > 0) {
                    variables = new Dictionary<ushort, TNumber>();
                    foreach (var kv in oldVariables) {
                        if (!idTable.TryGetValue(oldMapping[kv.Key], out var newId))
                            newId = kv.Key;
                        variables[newId] = kv.Value;
                    }
                }
                var oldFunctions = functionProcessors;
                if (oldFunctions != null && oldFunctions.Count > 0) {
                    functionProcessors = new Dictionary<ushort, Func<TNumber>>();
                    foreach (var kv in oldFunctions) {
                        if (!idTable.TryGetValue(oldMapping[kv.Key], out var newId))
                            newId = kv.Key;
                        functionProcessors[newId] = kv.Value;
                    }
                }
            }
        }

        protected abstract TNumber Truely { get; }

        protected virtual TNumber Falsy { get; } = default;

        protected virtual TNumber Error { get; } = default;

        public AbstractMathEvalulator() { }

        protected AbstractMathEvalulator(string[] defaultMapping) {
            if (defaultMapping != null && defaultMapping.Length > 0) {
                idTable = new Dictionary<string, ushort>(defaultMapping.Length, StringComparer.OrdinalIgnoreCase);
                var duplicateCheck = new HashSet<ushort>(idTable.Values);
                ushort i = 1;
                foreach (var id in defaultMapping) {
                    if (string.IsNullOrEmpty(id) || idTable.ContainsKey(id)) continue;
                    while (!duplicateCheck.Add(i)) i++;
                    idTable.Add(id, i++);
                }
            }
        }

        protected abstract bool IsTruely(TNumber value);

        public TNumber Evalulate(ReadOnlySpan<Token> tokens) {
            try {
                foreach (var token in tokens) EvalulateStep(token);
                return valueStack.Pop();
            } finally {
                valueStack.Clear();
                conditionStack.Clear();
                argumentStack.Clear();
            }
        }

        void EvalulateStep(Token token) {
            switch (token.type) {
                case TokenType.LeftParenthesis:
                    argumentStack.Push(valueStack.Count);
                    return;
                case TokenType.If:
                    var value = valueStack.Pop();
                    bool isTrue = IsTruely(valueStack.Pop());
                    if (isTrue) valueStack.Push(value);
                    conditionStack.Push(isTrue);
                    return;
                case TokenType.Else:
                    if (conditionStack.Pop()) valueStack.Pop();
                    return;
            }
            if (tokenProcessors == null) return;
            var processor = tokenProcessors[(int)token.type];
            if (processor != null) valueStack.Push(processor(token));
        }

        public TNumber GetVariable(string key) {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (getVariableFunc != null) return getVariableFunc(key);
            if (!TryGetId(key, out var id, false)) return default;
            return GetVariable(id);
        }

        public TNumber GetVariable(ushort id) {
            if (getVariableFuncById != null) return getVariableFuncById(id);
            variables ??= new Dictionary<ushort, TNumber>();
            if (variables.TryGetValue(id, out var value)) return value;
            return default;
        }

        public void SetVariable(string key, TNumber value) {
            if (string.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            TryGetId(key, out var id, true);
            if (getVariableFunc != null) throw new InvalidOperationException("Cannot set variable when GetVariableFunc is set");
            SetVariable(id, value);
        }

        public void SetVariable(ushort id, TNumber value) {
            if (getVariableFuncById != null) throw new InvalidOperationException("Cannot set variable when GetVariableFuncById is set");
            variables ??= new Dictionary<ushort, TNumber>();
            variables[id] = value;
            idCounter = Math.Min(idCounter, id);
        }

        public bool RegisterProcessor(string functionName, FunctionProcessor processor, bool overrideExisting = true, bool isStaticFunction = true) {
            if (processor == null) throw new ArgumentNullException(nameof(processor));
            TryGetId(functionName, out var id, true);
            return RegisterProcessor(id, processor, overrideExisting, isStaticFunction);
        }

        protected bool RegisterProcessor(ushort id, FunctionProcessor processor, bool overrideExisting = true, bool isStaticFunction = true) =>
            RegisterProcessor(id, () => {
                var valuePointer = valueStack.Count;
                if (valuePointer < 0) return Error;
                int marker = argumentStack.Pop();
                if (marker > valuePointer) return Error;
                return processor(valueStack.Pop(valuePointer - marker));
            }, overrideExisting, isStaticFunction);

        protected bool RegisterProcessor(string functionName, Func<TNumber> processor, bool overrideExisting = true, bool isStaticFunction = false) {
            if (string.IsNullOrEmpty(functionName)) throw new ArgumentNullException(nameof(functionName));
            TryGetId(functionName, out var id, true);
            return RegisterProcessor(id, processor, overrideExisting, isStaticFunction);
        }

        protected bool RegisterProcessor(ushort id, Func<TNumber> processor, bool overrideExisting = true, bool isStaticFunction = false) {
            if (processor == null) throw new ArgumentNullException(nameof(processor));
            functionProcessors ??= new Dictionary<ushort, Func<TNumber>>();
            if (functionProcessors.ContainsKey(id)) {
                if (!overrideExisting) return false;
                functionProcessors[id] = processor;
            } else
                functionProcessors.Add(id, processor);
            if (isStaticFunction) {
                this.isStaticFunction ??= new Dictionary<ushort, bool>();
                this.isStaticFunction[id] = isStaticFunction;
            }
            return true;
        }

        public bool RegisterProcessor(TokenType type, UnaryOperatorProcessor processor, bool overrideExisting = true) {
            if (processor == null) throw new ArgumentNullException(nameof(processor));
            operatorArgc[(int)type] = 1;
            return RegisterProcessor(type, (Token _) => {
                if (valueStack.Count < 1) return Error;
                return processor(valueStack.Pop());
            }, overrideExisting);
        }

        public bool RegisterProcessor(TokenType type, BinaryOperatorProcessor processor, bool overrideExisting = true) {
            if (processor == null) throw new ArgumentNullException(nameof(processor));
            operatorArgc[(int)type] = 2;
            return RegisterProcessor(type, (Token _) => {
                if (valueStack.Count < 2) return Error;
                var second = valueStack.Pop();
                var first = valueStack.Pop();
                return processor(first, second);
            }, overrideExisting);
        }

        protected bool RegisterProcessor(TokenType type, Func<Token, TNumber> processor, bool overrideExisting = true) {
            if (processor == null) throw new ArgumentNullException(nameof(processor));
            tokenProcessors ??= new Func<Token, TNumber>[(int)TokenType.MaxToken];
            if (tokenProcessors[(int)type] != null) {
                if (!overrideExisting) return false;
                tokenProcessors[(int)type] = processor;
            } else
                tokenProcessors[(int)type] = processor;
            return true;
        }

        public virtual void RegisterDefaultFunctions() {
            foreach (var methodInfo in GetType().GetMethods(
                BindingFlags.Static |
                BindingFlags.Instance |
                BindingFlags.NonPublic |
                BindingFlags.Public |
                BindingFlags.FlattenHierarchy
            )) {
                var attribute = methodInfo.GetCustomAttribute<ProcessorAttribute>();
                if (attribute == null) continue;
                var parameters = methodInfo.GetParameters();
                switch (parameters.Length) {
                    case 0:
                        if (methodInfo.IsStatic || attribute.Type != TokenType.External) goto default;
                        RegisterProcessor(attribute.FunctionName,
                            Delegate.CreateDelegate(typeof(Func<TNumber>), this, methodInfo) as Func<TNumber>,
                            attribute.IsStaticFunction
                        );
                        break;
                    case 1:
                        if (parameters[0].ParameterType == typeof(Token)) {
                            if (methodInfo.IsStatic) goto default;
                            operatorArgc[(int)attribute.Type] = 0;
                            RegisterProcessor(attribute.Type, Delegate.CreateDelegate(typeof(Func<Token, TNumber>), this, methodInfo) as Func<Token, TNumber>);
                        } else if (attribute.Type == TokenType.External)
                            RegisterProcessor(attribute.FunctionName, methodInfo.IsStatic ?
                                Delegate.CreateDelegate(typeof(FunctionProcessor), methodInfo) as FunctionProcessor :
                                Delegate.CreateDelegate(typeof(FunctionProcessor), this, methodInfo) as FunctionProcessor,
                                isStaticFunction: attribute.IsStaticFunction
                            );
                        else
                            RegisterProcessor(attribute.Type, methodInfo.IsStatic ?
                                Delegate.CreateDelegate(typeof(UnaryOperatorProcessor), methodInfo) as UnaryOperatorProcessor :
                                Delegate.CreateDelegate(typeof(UnaryOperatorProcessor), this, methodInfo) as UnaryOperatorProcessor
                            );
                        break;
                    case 2:
                        if (attribute.Type == TokenType.External) goto default;
                        RegisterProcessor(attribute.Type, methodInfo.IsStatic ?
                            Delegate.CreateDelegate(typeof(BinaryOperatorProcessor), methodInfo) as BinaryOperatorProcessor :
                            Delegate.CreateDelegate(typeof(BinaryOperatorProcessor), this, methodInfo) as BinaryOperatorProcessor
                        );
                        break;
                    default:
                        throw new NotSupportedException($"Method {methodInfo.Name} has unsupported signature");
                }
            }
        }

        public virtual bool TryGetId(string identifier, out ushort id, bool addIfNotExists = false) {
            if (string.IsNullOrEmpty(identifier)) throw new ArgumentNullException(nameof(identifier));
            if (idTable == null) {
                if (!addIfNotExists) {
                    id = 0;
                    return false;
                }
                idTable = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
            }
            if (!idTable.TryGetValue(identifier, out id)) {
                if (!addIfNotExists) return false;
                id = --idCounter;
                idTable.Add(identifier, id);
            }
            return true;
        }

        public void OptimizeTokens(Token[] tokens, bool addIdIfNotExists = false) {
            if (tokens == null) throw new ArgumentNullException(nameof(tokens));
            for (int i = 0; i < tokens.Length; i++) {
                var token = tokens[i];
                switch (token.type) {
                    case TokenType.External:
                        if (token.data is not Delegate &&
                            (token.identifierIndex != 0 || TryGetId(token.data as string, out token.identifierIndex, addIdIfNotExists))) {
                            if (functionProcessors != null && functionProcessors.TryGetValue(token.identifierIndex, out var externProc))
                                token.data = externProc;
                            tokens[i] = token;
                        }
                        break;
                    case TokenType.Identifier:
                        if (TryGetId(token.data as string, out token.identifierIndex, addIdIfNotExists))
                            tokens[i] = token;
                        break;
                }
            }
        }

        #region Operators
        [Processor("min")]
        protected TNumber Min(ReadOnlySpan<TNumber> args) {
            comparer ??= Comparer<TNumber>.Default;
            if (args.Length == 0) return Error;
            var min = args[0];
            for (int i = 1; i < args.Length; i++)
                if (comparer.Compare(args[i], min) < 0)
                    min = args[i];
            return min;
        }

        [Processor("max")]
        protected TNumber Max(ReadOnlySpan<TNumber> args) {
            comparer ??= Comparer<TNumber>.Default;
            if (args.Length == 0) return Error;
            var max = args[0];
            for (int i = 1; i < args.Length; i++)
                if (comparer.Compare(args[i], max) > 0)
                    max = args[i];
            return max;
        }

        [Processor("clamp")]
        protected TNumber Clamp(ReadOnlySpan<TNumber> args) {
            comparer ??= Comparer<TNumber>.Default;
            if (args.Length < 3) return Error;
            return comparer.Compare(args[2], args[0]) < 0 ? args[0] :
                comparer.Compare(args[2], args[1]) > 0 ? args[1] :
                args[2];
        }

        [Processor(TokenType.Equals)]
        protected virtual TNumber Equals(TNumber first, TNumber second) {
            comparer ??= Comparer<TNumber>.Default;
            return comparer.Compare(first, second) == 0 ? Truely : Falsy;
        }

        [Processor(TokenType.NotEquals)]
        protected virtual TNumber NotEquals(TNumber first, TNumber second) {
            comparer ??= Comparer<TNumber>.Default;
            return comparer.Compare(first, second) != 0 ? Truely : Falsy;
        }

        [Processor(TokenType.GreaterThan)]
        protected virtual TNumber GreaterThan(TNumber first, TNumber second) {
            comparer ??= Comparer<TNumber>.Default;
            return comparer.Compare(first, second) > 0 ? Truely : Falsy;
        }

        [Processor(TokenType.GreaterThanOrEquals)]
        protected virtual TNumber GreaterThanOrEquals(TNumber first, TNumber second) {
            comparer ??= Comparer<TNumber>.Default;
            return comparer.Compare(first, second) >= 0 ? Truely : Falsy;
        }

        [Processor(TokenType.LessThan)]
        protected virtual TNumber LessThan(TNumber first, TNumber second) {
            comparer ??= Comparer<TNumber>.Default;
            return comparer.Compare(first, second) < 0 ? Truely : Falsy;
        }

        [Processor(TokenType.LessThanOrEquals)]
        protected virtual TNumber LessThanOrEquals(TNumber first, TNumber second) {
            comparer ??= Comparer<TNumber>.Default;
            return comparer.Compare(first, second) <= 0 ? Truely : Falsy;
        }

        [Processor(TokenType.LogicalAnd)]
        protected virtual TNumber LogicalAnd(TNumber first, TNumber second) =>
            IsTruely(first) ? second : first;

        [Processor(TokenType.LogicalOr)]
        protected virtual TNumber LogicalOr(TNumber first, TNumber second) =>
            IsTruely(first) ? first : second;

        [Processor(TokenType.LogicalNot)]
        protected virtual TNumber LogicalNot(TNumber value) =>
            IsTruely(value) ? Falsy : Truely;

        [Processor(TokenType.Primitive)]
        protected virtual TNumber Primitive(Token token) => token.numberValue;

        [Processor(TokenType.Identifier)]
        protected virtual TNumber Identifier(Token token) => GetVariable(token.data as string);

        [Processor(TokenType.External)]
        protected virtual TNumber External(Token token) {
            if (token.data is not Func<TNumber> externProc &&
                (!(token.identifierIndex != 0 || TryGetId(token.data as string, out token.identifierIndex, false)) ||
                functionProcessors == null || !functionProcessors.TryGetValue(token.identifierIndex, out externProc)))
                throw new Exception($"External function {token.data} not found");
            return externProc();
        }
        #endregion
    }

#if UNITY_5_3_OR_NEWER
    public partial class ProcessorAttribute : UnityEngine.Scripting.PreserveAttribute { }
#else
    public partial class ProcessorAttribute : Attribute { }
#endif

    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public partial class ProcessorAttribute {
        public TokenType Type { get; private set; }
        public string FunctionName { get; private set; }
        public bool IsStaticFunction { get; private set; }

        public ProcessorAttribute(TokenType type) {
            Type = type;
        }

        public ProcessorAttribute(string functionName, bool isStaticFunction = true) {
            Type = TokenType.External;
            FunctionName = functionName;
            IsStaticFunction = isStaticFunction;
        }
    }
}