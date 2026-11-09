using System;

namespace LocalMinimumGoldenSection
{
    internal static class SelfTests
    {
        public static int Run()
        {
            int passed = 0;
            int failed = 0;
            Test("Минимум параболы", delegate
            {
                GoldenSectionResult result = Solve("(x - 2)^2 + 1", 0, 5, 0.000001);
                Require(result.Status == GoldenSectionStatus.Success, result.Message);
                Require(Math.Abs(result.X - 2.0) <= 0.000002, "x_min вне ожидаемой точности: " + result.X);
                Require(Math.Abs(result.FX - 1.0) <= 1e-10, "Неверное значение минимума: " + result.FX);
                Require(result.Width <= 0.000001, "Конечный интервал шире ε.");
            }, ref passed, ref failed);

            Test("Минимум x^2 на симметричном интервале", delegate
            {
                GoldenSectionResult result = Solve("x^2", -3, 4, 0.00001);
                Require(result.Status == GoldenSectionStatus.Success, result.Message);
                Require(Math.Abs(result.X) <= 0.00002, "x_min не близок к нулю.");
                Require(Math.Abs(result.FX) <= 1e-8, "f(x_min) не близко к нулю.");
            }, ref passed, ref failed);

            Test("Минимум монотонной функции у левой границы", delegate
            {
                GoldenSectionResult result = Solve("x", -2, 5, 0.00001);
                Require(result.Status == GoldenSectionStatus.Success, result.Message);
                Require(result.X >= -2 && result.X <= -1.99999, "Минимум не найден около левой границы.");
            }, ref passed, ref failed);

            Test("Тригонометрическая функция в радианах", delegate
            {
                GoldenSectionResult result = Solve("sin(x)", -3, 0, 0.00001);
                Require(result.Status == GoldenSectionStatus.Success, result.Message);
                Require(Math.Abs(result.X + Math.PI / 2.0) <= 0.00002, "Минимум sin(x) найден неточно.");
                Require(Math.Abs(result.FX + 1.0) <= 1e-9, "Значение sin(x_min) неверно.");
            }, ref passed, ref failed);

            Test("Функция с запятой в десятичной дроби", delegate
            {
                GoldenSectionResult result = Solve("(x - 1,5)^2", 0, 3, 0.00001);
                Require(result.Status == GoldenSectionStatus.Success, result.Message);
                Require(Math.Abs(result.X - 1.5) <= 0.00002, "Не обработана десятичная запятая.");
            }, ref passed, ref failed);

            Test("Точность больше длины интервала", delegate
            {
                GoldenSectionResult result = Solve("(x - 1)^2", 0, 2, 3);
                Require(result.Status == GoldenSectionStatus.Success, result.Message);
                Require(result.Iterations == 0, "Неожиданно выполнены итерации.");
                Require(Math.Abs(result.Width - 2.0) < 1e-12, "Не сохранён исходный интервал.");
            }, ref passed, ref failed);

            Test("Недопустимое значение на границе", delegate
            {
                GoldenSectionResult result = Solve("ln(x)", -1, 4, 0.0001);
                Require(result.Status == GoldenSectionStatus.CalculationError, "Ожидалась ошибка области определения.");
            }, ref passed, ref failed);

            Test("Очень малая точность и ограничение double", delegate
            {
                GoldenSectionResult result = Solve("(x - 1)^2", 0, 2, 1e-300);
                Require(result.Status == GoldenSectionStatus.PrecisionLimit,
                    "Ожидалась остановка из-за ограниченной точности double, получен статус " + result.Status);
            }, ref passed, ref failed);

            Test("tan(x) на непрерывной ветви", delegate
            {
                GoldenSectionResult result = Solve("tan(x)", -1, 1, 0.00001);
                Require(result.Status == GoldenSectionStatus.Success, result.Message);
                Require(Math.Abs(result.X + 1.0) <= 0.00002, "Минимум монотонной ветви tan(x) должен быть около левой границы.");
                Require(Math.Abs(result.FX - Math.Tan(-1.0)) <= 0.0001, "Неверное значение tan(x) в найденной точке.");
            }, ref passed, ref failed);

            Test("tan(x) с асимптотами на интервале", delegate
            {
                GoldenSectionResult result = Solve("tan(x)", -2, 10, 0.00001);
                Require(result.Status == GoldenSectionStatus.MethodNotApplicable,
                    "Метод не должен выдавать минимум через вертикальные асимптоты.");
                Require(result.Message.Contains("разрыв") || result.Message.Contains("асимптот"),
                    "Пользователю должна быть объяснена причина отказа.");
            }, ref passed, ref failed);

            Test("График tan(x) разрывается около асимптот", delegate
            {
                CompiledFunction function = new FunctionParser().Compile("tan(x)");
                GraphData graph = GraphSampler.Sample(function, -2, 10, 1600);
                int breaks = 0;
                for (int i = 1; i < graph.ConnectFromPrevious.Length; i++)
                    if (!graph.ConnectFromPrevious[i] && graph.Valid[i - 1] && graph.Valid[i]) breaks++;
                Require(breaks >= 3, "График должен иметь разрывы на ветвях tan(x).");
                Require(graph.YMax < 1000 && graph.YMin > -1000,
                    "Единичные огромные значения около асимптот не должны сжимать масштаб графика.");
            }, ref passed, ref failed);

            Test("График tan(x) на непрерывной ветви не разрывается", delegate
            {
                CompiledFunction function = new FunctionParser().Compile("tan(x)");
                GraphData graph = GraphSampler.Sample(function, -1, 1, 1600);
                int connected = 0;
                for (int i = 1; i < graph.ConnectFromPrevious.Length; i++)
                    if (graph.ConnectFromPrevious[i]) connected++;
                Require(connected > 1500, "Непрерывная ветвь tan(x) не должна искусственно разрываться.");
            }, ref passed, ref failed);

            Test("Несколько экстремумов на интервале", delegate
            {
                GoldenSectionResult result = Solve("sin(x)", 0, 6.283185307179586, 0.00001);
                Require(result.Status == GoldenSectionStatus.MethodNotApplicable,
                    "Метод не должен обещать корректный минимум на интервале с несколькими впадинами.");
            }, ref passed, ref failed);

            Test("Узкая логарифмическая особенность", delegate
            {
                GoldenSectionResult result = Solve("ln((x - 0.1249)^2)", -1, 1, 0.00001);
                Require(result.Status == GoldenSectionStatus.MethodNotApplicable,
                    "Метод не должен принимать логарифмическую особенность за конечный минимум.");
            }, ref passed, ref failed);

            Test("Монотонная экспонента", delegate
            {
                GoldenSectionResult result = Solve("exp(x)", -5, 5, 0.00001);
                Require(result.Status == GoldenSectionStatus.Success, result.Message);
                Require(Math.Abs(result.X + 5.0) <= 0.00002,
                    "Минимум возрастающей экспоненты должен находиться у левой границы.");
            }, ref passed, ref failed);

            Test("tan в точке вертикальной асимптоты", delegate
            {
                bool thrown = false;
                try { new FunctionParser().Compile("tan(x)").Evaluate(Math.PI / 2.0); }
                catch (ExpressionException) { thrown = true; }
                Require(thrown, "tan(pi/2) не должен возвращать огромное конечное значение.");
            }, ref passed, ref failed);

            Test("Некорректная формула", delegate
            {
                bool thrown = false;
                try { new FunctionParser().Compile("sin("); }
                catch (ExpressionException) { thrown = true; }
                Require(thrown, "Парсер не отклонил незакрытую скобку.");
            }, ref passed, ref failed);

            Console.WriteLine("ИТОГО: пройдено " + passed + ", ошибок " + failed + ".");
            return failed == 0 ? 0 : 1;
        }

        private static GoldenSectionResult Solve(string formula, double a, double b, double epsilon)
        {
            CompiledFunction function = new FunctionParser().Compile(formula);
            return GoldenSectionMethod.FindMinimum(function, a, b, epsilon);
        }

        private static void Test(string name, Action action, ref int passed, ref int failed)
        {
            try
            {
                action();
                passed++;
                Console.WriteLine("[OK] " + name);
            }
            catch (Exception ex)
            {
                failed++;
                Console.WriteLine("[ОШИБКА] " + name + ": " + ex.Message);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
        }
    }
}
