using System;
using System.Collections.Generic;

namespace LocalMinimumGoldenSection
{
    public enum GoldenSectionStatus
    {
        Success,
        PrecisionLimit,
        IterationLimit,
        CalculationError,
        MethodNotApplicable
    }

    public sealed class GoldenSectionStep
    {
        public int Iteration { get; set; }
        public double A { get; set; }
        public double B { get; set; }
        public double X1 { get; set; }
        public double X2 { get; set; }
        public double F1 { get; set; }
        public double F2 { get; set; }
        public double BestX { get; set; }
        public double BestY { get; set; }
        public double Width { get; set; }
        public bool HasPair { get; set; }
        public bool IsFinalState { get; set; }
    }

    public sealed class GoldenSectionResult
    {
        private readonly List<GoldenSectionStep> steps = new List<GoldenSectionStep>();

        public List<GoldenSectionStep> Steps { get { return steps; } }
        public GoldenSectionStatus Status { get; set; }
        public string Message { get; set; }
        public double A { get; set; }
        public double B { get; set; }
        public double Width { get; set; }
        public double X { get; set; }
        public double FX { get; set; }
        public int Iterations { get; set; }
        public bool HasPoint { get; set; }

        public GoldenSectionResult()
        {
            Status = GoldenSectionStatus.CalculationError;
            Message = String.Empty;
        }
    }

    public static class GoldenSectionMethod
    {
        // Защитный предел задаётся разработчиком и не показывается пользователю.
        public const int MaxIterations = 10000;
        private static readonly double Tau = (Math.Sqrt(5.0) - 1.0) / 2.0;

        public static GoldenSectionResult FindMinimum(CompiledFunction function,
            double a, double b, double epsilon)
        {
            GoldenSectionResult result = new GoldenSectionResult();
            result.A = a;
            result.B = b;
            result.Width = b - a;

            try
            {
                Validate(function, a, b, epsilon);

                // Проверяем оба конца: заданный интервал должен лежать в области определения.
                function.Evaluate(a);
                function.Evaluate(b);

                string suitabilityMessage;
                if (!CheckIntervalSuitability(function, a, b, out suitabilityMessage))
                {
                    result.Status = GoldenSectionStatus.MethodNotApplicable;
                    result.Message = suitabilityMessage;
                    result.A = a;
                    result.B = b;
                    result.Width = b - a;
                    return result;
                }

                double width = b - a;
                double x1 = a + (1.0 - Tau) * width;
                double x2 = a + Tau * width;
                double f1 = function.Evaluate(x1);
                double f2 = function.Evaluate(x2);

                AddPairStep(result, 0, a, b, x1, x2, f1, f2);

                if (width <= epsilon)
                {
                    FinishSuccessfully(function, result, a, b, 0, true);
                    return result;
                }

                int iteration = 0;
                while (width > epsilon)
                {
                    if (iteration >= MaxIterations)
                    {
                        FinishWithBestPoint(result, a, b, x1, x2, f1, f2,
                            GoldenSectionStatus.IterationLimit,
                            "Достигнут внутренний предел вычислений. Заданная точность не достигнута.");
                        return result;
                    }

                    // Если пробные точки совпали с границами или друг с другом,
                    // double уже не позволяет продолжить деление интервала.
                    if (!(a < x1 && x1 < x2 && x2 < b))
                    {
                        FinishWithBestPoint(result, a, b, x1, x2, f1, f2,
                            GoldenSectionStatus.PrecisionLimit,
                            "Дальнейшее уточнение невозможно из-за точности чисел.");
                        return result;
                    }

                    bool keepLeftPart = f1 <= f2;
                    if (keepLeftPart)
                    {
                        b = x2;
                        x2 = x1;
                        f2 = f1;
                    }
                    else
                    {
                        a = x1;
                        x1 = x2;
                        f1 = f2;
                    }

                    iteration++;
                    width = b - a;
                    if (!NumericValidation.IsFinite(width) || width < 0)
                        throw new ExpressionException("Получена недопустимая длина интервала.");

                    if (width <= epsilon)
                    {
                        FinishSuccessfully(function, result, a, b, iteration, true);
                        return result;
                    }

                    if (keepLeftPart)
                    {
                        double nextX1 = a + (1.0 - Tau) * width;
                        if (!(a < nextX1 && nextX1 < x2))
                        {
                            result.Iterations = iteration;
                            FinishWithBestPoint(result, a, b, x1, x2, f1, f2,
                                GoldenSectionStatus.PrecisionLimit,
                                "Дальнейшее уточнение невозможно из-за точности чисел.");
                            return result;
                        }
                        x1 = nextX1;
                        f1 = function.Evaluate(x1);
                    }
                    else
                    {
                        double nextX2 = a + Tau * width;
                        if (!(x1 < nextX2 && nextX2 < b))
                        {
                            result.Iterations = iteration;
                            FinishWithBestPoint(result, a, b, x1, x2, f1, f2,
                                GoldenSectionStatus.PrecisionLimit,
                                "Дальнейшее уточнение невозможно из-за точности чисел.");
                            return result;
                        }
                        x2 = nextX2;
                        f2 = function.Evaluate(x2);
                    }

                    AddPairStep(result, iteration, a, b, x1, x2, f1, f2);
                }

                // Защитный возврат, хотя нормальный выход происходит выше.
                FinishSuccessfully(function, result, a, b, iteration, true);
            }
            catch (ExpressionException ex)
            {
                result.Status = GoldenSectionStatus.CalculationError;
                result.Message = "Не удалось вычислить функцию: " + ex.Message;
                result.A = a;
                result.B = b;
                result.Width = b - a;

                // Если ошибка возникла после нескольких шагов, сохраняем последнее
                // известное приближение и добавляем завершающее состояние для интерфейса.
                if (result.Steps.Count > 0)
                {
                    GoldenSectionStep last = result.Steps[result.Steps.Count - 1];
                    result.X = last.BestX;
                    result.FX = last.BestY;
                    result.HasPoint = NumericValidation.IsFinite(result.X) && NumericValidation.IsFinite(result.FX);
                    GoldenSectionStep errorStep = new GoldenSectionStep();
                    errorStep.Iteration = last.Iteration + 1;
                    errorStep.A = a;
                    errorStep.B = b;
                    errorStep.BestX = last.BestX;
                    errorStep.BestY = last.BestY;
                    errorStep.Width = b - a;
                    errorStep.HasPair = false;
                    errorStep.IsFinalState = true;
                    result.Steps.Add(errorStep);
                    result.Iterations = errorStep.Iteration;
                }
            }
            catch (ArgumentException ex)
            {
                result.Status = GoldenSectionStatus.CalculationError;
                result.Message = ex.Message;
            }
            return result;
        }

        private static bool CheckIntervalSuitability(CompiledFunction function,
            double a, double b, out string message)
        {
            const int sampleCount = 512;
            double[] values = new double[sampleCount + 1];
            List<double> absoluteValues = new List<double>(sampleCount + 1);
            double maxAbsolute = 0;

            message = String.Empty;
            for (int i = 0; i <= sampleCount; i++)
            {
                double x = a + (b - a) * (i / (double)sampleCount);
                try
                {
                    values[i] = function.Evaluate(x);
                }
                catch (ExpressionException)
                {
                    message = "Функция не определена на всём интервале. Проверьте область определения и точки разрыва.";
                    return false;
                }

                double absolute = Math.Abs(values[i]);
                absoluteValues.Add(absolute);
                if (absolute > maxAbsolute) maxAbsolute = absolute;
            }

            absoluteValues.Sort();
            double medianAbsolute = MedianOfSorted(absoluteValues);

            // Вертикальные асимптоты (например, tan(x)) обычно дают смену знака
            // между двумя очень большими значениями, намного превосходящими
            // типичный масштаб функции на интервале.
            if (medianAbsolute > 0)
            {
                for (int i = 1; i <= sampleCount; i++)
                {
                    double left = values[i - 1];
                    double right = values[i];
                    bool oppositeSigns = (left < 0 && right > 0) || (left > 0 && right < 0);
                    if (oppositeSigns && Math.Abs(left) > 8.0 * medianAbsolute &&
                        Math.Abs(right) > 8.0 * medianAbsolute)
                    {
                        message = "На интервале обнаружен возможный разрыв или вертикальная асимптота. " +
                            "Выберите участок без разрывов.";
                        return false;
                    }
                }

                // Обнаруживаем узкую особенность, например ln((x-c)^2), которая
                // уходит к -∞, или 1/(x-c)^2 с бесконечным пиком. Это не обычная
                // гладкая впадина/вершина: значения резко выбиваются из масштаба.
                for (int i = 1; i < sampleCount; i++)
                {
                    double current = values[i];
                    bool localMinimum = current < values[i - 1] && current < values[i + 1];
                    bool localMaximum = current > values[i - 1] && current > values[i + 1];
                    if ((localMinimum || localMaximum) && Math.Abs(current) > 8.0 * medianAbsolute)
                    {
                        message = "На интервале обнаружена узкая особенность или возможная точка разрыва. " +
                            "Уменьшите интервал и проверьте функцию.";
                        return false;
                    }
                }
            }

            // Метод золотого сечения надёжен на участке с одной впадиной:
            // сначала функция не возрастает, затем не убывает. Монотонные
            // функции тоже допустимы (минимум находится на границе интервала).
            double tolerance = Math.Max(1.0, maxAbsolute) * 1e-10;
            bool hasStartedIncreasing = false;
            for (int i = 1; i <= sampleCount; i++)
            {
                double delta = values[i] - values[i - 1];
                if (delta > tolerance)
                    hasStartedIncreasing = true;
                else if (delta < -tolerance && hasStartedIncreasing)
                {
                    message = "На выбранном интервале несколько подъёмов и спадов. " +
                        "Выберите участок с одной впадиной или монотонный участок.";
                    return false;
                }
            }

            return true;
        }

        private static double MedianOfSorted(List<double> values)
        {
            if (values == null || values.Count == 0) return 0;
            int middle = values.Count / 2;
            if (values.Count % 2 == 1) return values[middle];
            return values[middle - 1] + (values[middle] - values[middle - 1]) / 2.0;
        }

        private static void Validate(CompiledFunction function, double a, double b, double epsilon)
        {
            if (function == null)
                throw new ArgumentException("Введите формулу функции.");
            if (!NumericValidation.IsFinite(a) || !NumericValidation.IsFinite(b))
                throw new ArgumentException("Границы интервала должны быть конечными числами.");
            if (!(a < b))
                throw new ArgumentException("Левая граница a должна быть меньше правой границы b.");
            if (!NumericValidation.IsFinite(b - a))
                throw new ArgumentException("Слишком широкий интервал.");
            if (!NumericValidation.IsFinite(epsilon) || epsilon <= 0)
                throw new ArgumentException("Точность ε должна быть положительным конечным числом.");
        }

        private static void AddPairStep(GoldenSectionResult result, int iteration,
            double a, double b, double x1, double x2, double f1, double f2)
        {
            bool firstIsBetter = f1 <= f2;
            GoldenSectionStep step = new GoldenSectionStep();
            step.Iteration = iteration;
            step.A = a;
            step.B = b;
            step.X1 = x1;
            step.X2 = x2;
            step.F1 = f1;
            step.F2 = f2;
            step.BestX = firstIsBetter ? x1 : x2;
            step.BestY = firstIsBetter ? f1 : f2;
            step.Width = b - a;
            step.HasPair = true;
            step.IsFinalState = false;
            result.Steps.Add(step);
            result.A = a;
            result.B = b;
            result.Width = b - a;
            result.Iterations = iteration;
        }

        private static void FinishSuccessfully(CompiledFunction function,
            GoldenSectionResult result, double a, double b, int iterations, bool addFinalStep)
        {
            double x = a + (b - a) / 2.0;
            double y = function.Evaluate(x);

            result.Status = GoldenSectionStatus.Success;
            result.Message = "Заданная точность достигнута.";
            result.A = a;
            result.B = b;
            result.Width = b - a;
            result.X = x;
            result.FX = y;
            result.Iterations = iterations;
            result.HasPoint = true;

            if (addFinalStep)
            {
                GoldenSectionStep last = result.Steps.Count > 0
                    ? result.Steps[result.Steps.Count - 1] : null;
                if (last != null && last.Iteration == iterations && last.A == a && last.B == b)
                {
                    last.BestX = x;
                    last.BestY = y;
                    last.Width = b - a;
                    last.IsFinalState = true;
                }
                else
                {
                    GoldenSectionStep finalStep = new GoldenSectionStep();
                    finalStep.Iteration = iterations;
                    finalStep.A = a;
                    finalStep.B = b;
                    finalStep.BestX = x;
                    finalStep.BestY = y;
                    finalStep.Width = b - a;
                    finalStep.HasPair = false;
                    finalStep.IsFinalState = true;
                    result.Steps.Add(finalStep);
                }
            }
        }

        private static void FinishWithBestPoint(GoldenSectionResult result,
            double a, double b, double x1, double x2, double f1, double f2,
            GoldenSectionStatus status, string message)
        {
            bool firstIsBetter = f1 <= f2;
            double bestX = firstIsBetter ? x1 : x2;
            double bestY = firstIsBetter ? f1 : f2;

            result.Status = status;
            result.Message = message;
            result.A = a;
            result.B = b;
            result.Width = b - a;
            result.X = bestX;
            result.FX = bestY;
            result.HasPoint = NumericValidation.IsFinite(bestX) && NumericValidation.IsFinite(bestY);

            GoldenSectionStep terminalStep = new GoldenSectionStep();
            terminalStep.Iteration = result.Iterations;
            terminalStep.A = a;
            terminalStep.B = b;
            terminalStep.BestX = bestX;
            terminalStep.BestY = bestY;
            terminalStep.Width = b - a;
            terminalStep.HasPair = false;
            terminalStep.IsFinalState = true;
            result.Steps.Add(terminalStep);
        }
    }

    internal static class NumericValidation
    {
        public static bool IsFinite(double value)
        {
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
