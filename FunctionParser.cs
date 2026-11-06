using System;
using System.Globalization;

namespace LocalMinimumGoldenSection
{
    public sealed class ExpressionException : Exception
    {
        public ExpressionException(string message) : base(message) { }
    }

    public sealed class CompiledFunction
    {
        private readonly ExpressionNode root;

        internal CompiledFunction(ExpressionNode rootNode)
        {
            root = rootNode;
        }

        public double Evaluate(double x)
        {
            if (double.IsNaN(x) || double.IsInfinity(x))
                throw new ExpressionException("Аргумент x должен быть конечным числом.");
            return ExpressionNode.EnsureFinite(root.Evaluate(x));
        }
    }

    internal abstract class ExpressionNode
    {
        public abstract double Evaluate(double x);

        public static double EnsureFinite(double value)
        {
            if (double.IsNaN(value))
                throw new ExpressionException("Значение функции не определено (NaN или нарушение области определения).");
            if (double.IsInfinity(value))
                throw new ExpressionException("Вычисление привело к бесконечности или переполнению.");
            return value;
        }
    }

    internal sealed class ConstantNode : ExpressionNode
    {
        private readonly double value;
        public ConstantNode(double number) { value = number; }
        public override double Evaluate(double x) { return value; }
    }

    internal sealed class VariableNode : ExpressionNode
    {
        public override double Evaluate(double x) { return x; }
    }

    internal sealed class UnaryNode : ExpressionNode
    {
        private readonly char operation;
        private readonly ExpressionNode operand;
        public UnaryNode(char op, ExpressionNode node) { operation = op; operand = node; }
        public override double Evaluate(double x)
        {
            double v = operand.Evaluate(x);
            return EnsureFinite(operation == '-' ? -v : v);
        }
    }

    internal sealed class BinaryNode : ExpressionNode
    {
        private readonly char operation;
        private readonly ExpressionNode left, right;
        public BinaryNode(char op, ExpressionNode a, ExpressionNode b)
        {
            operation = op; left = a; right = b;
        }
        public override double Evaluate(double x)
        {
            double a = left.Evaluate(x), b = right.Evaluate(x);
            double result;
            switch (operation)
            {
                case '+': result = a + b; break;
                case '-': result = a - b; break;
                case '*': result = a * b; break;
                case '/':
                    if (b == 0.0) throw new ExpressionException("Деление на ноль.");
                    result = a / b; break;
                case '^': result = Math.Pow(a, b); break;
                default: throw new ExpressionException("Неизвестная операция.");
            }
            return EnsureFinite(result);
        }
    }

    internal sealed class FunctionNode : ExpressionNode
    {
        private readonly string name;
        private readonly ExpressionNode argument;
        public FunctionNode(string functionName, ExpressionNode arg)
        {
            name = functionName; argument = arg;
        }
        public override double Evaluate(double x)
        {
            double a = argument.Evaluate(x);
            double result;
            switch (name)
            {
                case "sin": result = Math.Sin(a); break;
                case "cos": result = Math.Cos(a); break;
                case "tan":
                    // Math.Tan(pi/2) в double возвращает очень большое конечное число,
                    // хотя математически здесь вертикальная асимптота. Не считаем
                    // такое значение корректной точкой графика/расчёта.
                    if (Math.Abs(Math.Cos(a)) < 1e-14)
                        throw new ExpressionException("tan не определён в точках π/2 + k·π.");
                    result = Math.Tan(a);
                    break;
                case "asin":
                    if (Math.Abs(a) > 1) throw new ExpressionException("asin определён только для аргумента от -1 до 1.");
                    result = Math.Asin(a); break;
                case "acos":
                    if (Math.Abs(a) > 1) throw new ExpressionException("acos определён только для аргумента от -1 до 1.");
                    result = Math.Acos(a); break;
                case "atan": result = Math.Atan(a); break;
                case "sqrt":
                    if (a < 0) throw new ExpressionException("sqrt не определён для отрицательного аргумента.");
                    result = Math.Sqrt(a); break;
                case "abs": result = Math.Abs(a); break;
                case "exp": result = Math.Exp(a); break;
                case "ln":
                    if (a <= 0) throw new ExpressionException("ln определён только для положительного аргумента.");
                    result = Math.Log(a); break;
                case "log":
                    if (a <= 0) throw new ExpressionException("log определён только для положительного аргумента.");
                    result = Math.Log10(a); break;
                default: throw new ExpressionException("Неизвестная функция: " + name);
            }
            return EnsureFinite(result);
        }
    }

    /// <summary>
    /// Рекурсивный спуск. ^ правоассоциативен; ^ старше унарного знака:
    /// -x^2 == -(x^2), 2^-3 == 2^(-3), 2^3^2 == 2^(3^2).
    /// Умножение всегда явное: 2*x, а не 2x.
    /// </summary>
    public sealed class FunctionParser
    {
        private string source;
        private int position, nodeCount, depth;
        private const int MaxNodes = 512;
        private const int MaxDepth = 100;

        public CompiledFunction Compile(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                throw new ExpressionException("Введите формулу функции.");
            if (text.Length > 2048)
                throw new ExpressionException("Формула слишком длинная (максимум 2048 символов).");
            source = text;
            position = 0;
            nodeCount = 0;
            depth = 0;
            ExpressionNode root = ParseExpression();
            SkipSpaces();
            if (position != source.Length)
                throw SyntaxError("Лишний символ '" + source[position] + "'. Используйте явное умножение *.");
            return new CompiledFunction(root);
        }

        private ExpressionNode NewNode(ExpressionNode node)
        {
            nodeCount++;
            if (nodeCount > MaxNodes)
                throw SyntaxError("Слишком сложное выражение.");
            return node;
        }

        private ExpressionNode ParseExpression()
        {
            Enter();
            try { return ParseAddition(); }
            finally { Leave(); }
        }

        private ExpressionNode ParseAddition()
        {
            ExpressionNode left = ParseMultiplication();
            while (true)
            {
                if (Consume('+')) left = NewNode(new BinaryNode('+', left, ParseMultiplication()));
                else if (Consume('-')) left = NewNode(new BinaryNode('-', left, ParseMultiplication()));
                else return left;
            }
        }

        private ExpressionNode ParseMultiplication()
        {
            ExpressionNode left = ParseUnary();
            while (true)
            {
                if (Consume('*')) left = NewNode(new BinaryNode('*', left, ParseUnary()));
                else if (Consume('/')) left = NewNode(new BinaryNode('/', left, ParseUnary()));
                else return left;
            }
        }

        private ExpressionNode ParseUnary()
        {
            Enter();
            try
            {
                if (Consume('+')) return NewNode(new UnaryNode('+', ParseUnary()));
                if (Consume('-')) return NewNode(new UnaryNode('-', ParseUnary()));
                return ParsePower();
            }
            finally { Leave(); }
        }

        private ExpressionNode ParsePower()
        {
            ExpressionNode left = ParsePrimary();
            if (Consume('^'))
                left = NewNode(new BinaryNode('^', left, ParseUnary()));
            return left;
        }

        private ExpressionNode ParsePrimary()
        {
            SkipSpaces();
            if (Consume('('))
            {
                ExpressionNode inside = ParseExpression();
                if (!Consume(')')) throw SyntaxError("Ожидалась закрывающая скобка ')'.");
                return inside;
            }
            if (position >= source.Length) throw SyntaxError("Неожиданный конец выражения.");
            char ch = source[position];
            if (char.IsDigit(ch) || ch == '.' || ch == ',') return ParseNumber();
            if (char.IsLetter(ch))
            {
                int start = position;
                while (position < source.Length && char.IsLetter(source[position])) position++;
                string name = source.Substring(start, position - start).ToLowerInvariant();
                if (name == "x") return NewNode(new VariableNode());
                if (name == "pi") return NewNode(new ConstantNode(Math.PI));
                if (name == "e") return NewNode(new ConstantNode(Math.E));
                if (!IsFunction(name)) throw SyntaxError("Неизвестное имя '" + name + "'.");
                if (!Consume('(')) throw SyntaxError("После функции " + name + " ожидается '('.");
                ExpressionNode arg = ParseExpression();
                if (!Consume(')')) throw SyntaxError("Ожидалась ')' после " + name + ".");
                return NewNode(new FunctionNode(name, arg));
            }
            throw SyntaxError("Неожиданный символ '" + ch + "'.");
        }

        private ExpressionNode ParseNumber()
        {
            int start = position;
            bool hasDigits = false;
            while (position < source.Length && char.IsDigit(source[position]))
            { position++; hasDigits = true; }
            if (position < source.Length && (source[position] == '.' || source[position] == ','))
            {
                position++;
                while (position < source.Length && char.IsDigit(source[position]))
                { position++; hasDigits = true; }
            }
            if (!hasDigits) throw SyntaxError("Неверная запись числа.");
            if (position < source.Length && (source[position] == 'e' || source[position] == 'E'))
            {
                int exponentStart = position;
                position++;
                if (position < source.Length && (source[position] == '+' || source[position] == '-')) position++;
                int digitsStart = position;
                while (position < source.Length && char.IsDigit(source[position])) position++;
                if (position == digitsStart) position = exponentStart; // например, 2*e — константа
            }
            string token = source.Substring(start, position - start).Replace(',', '.');
            double value;
            if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || double.IsNaN(value) || double.IsInfinity(value))
                throw SyntaxError("Некорректное или слишком большое число '" + token + "'.");
            return NewNode(new ConstantNode(value));
        }

        private static bool IsFunction(string name)
        {
            switch (name)
            {
                case "sin": case "cos": case "tan": case "asin": case "acos":
                case "atan": case "sqrt": case "abs": case "exp": case "ln": case "log": return true;
                default: return false;
            }
        }

        private bool Consume(char ch)
        {
            SkipSpaces();
            if (position < source.Length && source[position] == ch)
            { position++; return true; }
            return false;
        }

        private void SkipSpaces()
        {
            while (position < source.Length && char.IsWhiteSpace(source[position])) position++;
        }

        private void Enter()
        {
            depth++;
            if (depth > MaxDepth) throw SyntaxError("Слишком глубокая вложенность выражения.");
        }
        private void Leave() { depth--; }
        private ExpressionException SyntaxError(string message)
        {
            return new ExpressionException("Ошибка формулы, позиция " + (position + 1).ToString(CultureInfo.InvariantCulture) + ": " + message);
        }
    }
}
