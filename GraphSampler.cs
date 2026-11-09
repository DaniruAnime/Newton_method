using System;
using System.Collections.Generic;

namespace LocalMinimumGoldenSection
{
    /// <summary>Подготовленные данные для построения графика.</summary>
    public sealed class GraphData
    {
        public double[] XValues { get; internal set; }
        public double[] YValues { get; internal set; }
        public bool[] Valid { get; internal set; }
        public bool[] ConnectFromPrevious { get; internal set; }
        public double YMin { get; internal set; }
        public double YMax { get; internal set; }
        public double MedianAbsoluteY { get; internal set; }
        public int ValidCount { get; internal set; }
    }

    /// <summary>
    /// Отдельная от WinForms логика выборки графика. Учитывает недопустимые точки,
    /// вертикальные асимптоты и резкие разрывы, чтобы не соединять разные ветви.
    /// </summary>
    public static class GraphSampler
    {
        public static GraphData Sample(CompiledFunction function, double a, double b, int sampleCount)
        {
            if (function == null) throw new ArgumentNullException("function");
            if (!NumericValidation.IsFinite(a) || !NumericValidation.IsFinite(b) || !(a < b))
                throw new ArgumentException("Для построения графика нужен корректный интервал.");
            if (sampleCount < 2) throw new ArgumentOutOfRangeException("sampleCount");

            GraphData data = new GraphData();
            data.XValues = new double[sampleCount + 1];
            data.YValues = new double[sampleCount + 1];
            data.Valid = new bool[sampleCount + 1];
            data.ConnectFromPrevious = new bool[sampleCount + 1];

            List<double> finiteValues = new List<double>();
            List<double> absoluteValues = new List<double>();
            List<double> adjacentDifferences = new List<double>();

            for (int i = 0; i <= sampleCount; i++)
            {
                double x = a + (b - a) * (i / (double)sampleCount);
                data.XValues[i] = x;
                try
                {
                    double y = function.Evaluate(x);
                    if (NumericValidation.IsFinite(y))
                    {
                        data.YValues[i] = y;
                        data.Valid[i] = true;
                        finiteValues.Add(y);
                        absoluteValues.Add(Math.Abs(y));
                        data.ValidCount++;
                    }
                }
                catch (ExpressionException)
                {
                    data.Valid[i] = false;
                }
            }

            if (finiteValues.Count == 0)
            {
                data.YMin = -1;
                data.YMax = 1;
                data.MedianAbsoluteY = 0;
                return data;
            }

            for (int i = 1; i <= sampleCount; i++)
            {
                if (data.Valid[i - 1] && data.Valid[i])
                    adjacentDifferences.Add(Math.Abs(data.YValues[i] - data.YValues[i - 1]));
            }

            finiteValues.Sort();
            absoluteValues.Sort();
            adjacentDifferences.Sort();

            // Квантильный диапазон не позволяет нескольким точкам около tan(x)
            // с огромными значениями сжать весь остальной график в одну линию.
            if (finiteValues.Count >= 40)
            {
                data.YMin = Quantile(finiteValues, 0.01);
                data.YMax = Quantile(finiteValues, 0.99);
            }
            else
            {
                data.YMin = finiteValues[0];
                data.YMax = finiteValues[finiteValues.Count - 1];
            }

            if (!NumericValidation.IsFinite(data.YMin) || !NumericValidation.IsFinite(data.YMax) ||
                !(data.YMax > data.YMin))
            {
                data.YMin = finiteValues[0];
                data.YMax = finiteValues[finiteValues.Count - 1];
            }

            if (!(data.YMax > data.YMin))
            {
                double center = data.YMin;
                double padding = Math.Max(1.0, Math.Abs(center)) * 0.1;
                data.YMin = center - padding;
                data.YMax = center + padding;
            }

            data.MedianAbsoluteY = Median(absoluteValues);
            double medianDifference = adjacentDifferences.Count > 0 ? Median(adjacentDifferences) : 0;
            double robustRange = data.YMax - data.YMin;
            if (!NumericValidation.IsFinite(robustRange) || !(robustRange > 0))
                robustRange = Math.Max(1.0, Math.Max(Math.Abs(data.YMin), Math.Abs(data.YMax)));

            bool[] extremePoint = new bool[sampleCount + 1];
            if (data.MedianAbsoluteY > 0)
            {
                for (int i = 1; i < sampleCount; i++)
                {
                    if (!data.Valid[i - 1] || !data.Valid[i] || !data.Valid[i + 1])
                        continue;
                    double current = data.YValues[i];
                    bool localMinimum = current < data.YValues[i - 1] && current < data.YValues[i + 1];
                    bool localMaximum = current > data.YValues[i - 1] && current > data.YValues[i + 1];
                    if ((localMinimum || localMaximum) &&
                        Math.Abs(current) > 8.0 * data.MedianAbsoluteY)
                        extremePoint[i] = true;
                }
            }

            for (int i = 1; i <= sampleCount; i++)
            {
                if (!data.Valid[i - 1] || !data.Valid[i] || extremePoint[i - 1] || extremePoint[i])
                {
                    data.ConnectFromPrevious[i] = false;
                    continue;
                }

                double y1 = data.YValues[i - 1];
                double y2 = data.YValues[i];
                double jump = Math.Abs(y2 - y1);

                // Резкий скачок на масштабе графика — вероятный разрыв/асимптота.
                if (jump > Math.Max(2.0 * robustRange, 80.0 * medianDifference))
                {
                    data.ConnectFromPrevious[i] = false;
                    continue;
                }

                // У tan(x) около полюса значения меняют знак, оставаясь большими
                // относительно типичного масштаба функции.
                bool oppositeSigns = (y1 < 0 && y2 > 0) || (y1 > 0 && y2 < 0);
                if (oppositeSigns && data.MedianAbsoluteY > 0 &&
                    Math.Abs(y1) > 8.0 * data.MedianAbsoluteY &&
                    Math.Abs(y2) > 8.0 * data.MedianAbsoluteY)
                {
                    data.ConnectFromPrevious[i] = false;
                    continue;
                }

                double midX = data.XValues[i - 1] + (data.XValues[i] - data.XValues[i - 1]) / 2.0;
                double midY;
                try
                {
                    midY = function.Evaluate(midX);
                }
                catch (ExpressionException)
                {
                    data.ConnectFromPrevious[i] = false;
                    continue;
                }

                if (!NumericValidation.IsFinite(midY) ||
                    midY < data.YMin - 0.75 * robustRange ||
                    midY > data.YMax + 0.75 * robustRange)
                {
                    data.ConnectFromPrevious[i] = false;
                    continue;
                }

                double linearMid = y1 + (y2 - y1) / 2.0;
                if (Math.Abs(midY - linearMid) > 1.5 * robustRange)
                {
                    data.ConnectFromPrevious[i] = false;
                    continue;
                }

                data.ConnectFromPrevious[i] = true;
            }

            return data;
        }

        private static double Quantile(List<double> sorted, double fraction)
        {
            if (sorted == null || sorted.Count == 0) return 0;
            int index = (int)Math.Round(fraction * (sorted.Count - 1));
            index = Math.Max(0, Math.Min(sorted.Count - 1, index));
            return sorted[index];
        }

        private static double Median(List<double> sorted)
        {
            if (sorted == null || sorted.Count == 0) return 0;
            int middle = sorted.Count / 2;
            if (sorted.Count % 2 == 1) return sorted[middle];
            return sorted[middle - 1] + (sorted[middle] - sorted[middle - 1]) / 2.0;
        }
    }
}
