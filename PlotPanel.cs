using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;
using System.Collections.Generic;

namespace LocalMinimumGoldenSection
{
    public sealed class PlotPanel : Panel
    {
        private CompiledFunction function;
        private string formulaText;
        private double startX;
        private double endX;
        private IList<GoldenSectionStep> steps;
        private int currentIndex;

        private const int Samples = 1600;

        public PlotPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.White;
            BorderStyle = BorderStyle.FixedSingle;
        }

        public void SetData(CompiledFunction compiled, string formula,
            double a, double b, IList<GoldenSectionStep> calculationSteps, int selectedIndex)
        {
            function = compiled;
            formulaText = formula;
            startX = a;
            endX = b;
            steps = calculationSteps;
            currentIndex = selectedIndex;
            Invalidate();
        }

        /// <summary>Показывает график даже тогда, когда метод поиска не подходит интервалу.</summary>
        public void SetFunctionOnly(CompiledFunction compiled, string formula, double a, double b)
        {
            function = compiled;
            formulaText = formula;
            startX = a;
            endX = b;
            steps = null;
            currentIndex = -1;
            Invalidate();
        }

        public void ClearPlot()
        {
            function = null;
            formulaText = null;
            steps = null;
            currentIndex = -1;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Rectangle plot = new Rectangle(74, 42, Math.Max(1, Width - 103), Math.Max(1, Height - 91));
            if (plot.Width < 100 || plot.Height < 80)
                return;

            if (function == null)
            {
                using (Brush brush = new SolidBrush(Color.Gray))
                    g.DrawString("Введите функцию и выберите «Вычисление → Рассчитать».", Font, brush, 16, 16);
                return;
            }

            try
            {
                DrawPlot(g, plot);
            }
            catch (Exception ex)
            {
                using (Brush brush = new SolidBrush(Color.DarkRed))
                    g.DrawString("Не удалось построить график: " + ex.Message, Font, brush, 12, 12);
            }
        }

        private void DrawPlot(Graphics g, Rectangle r)
        {
            if (!(endX > startX) || !NumericValidation.IsFinite(endX - startX))
                return;

            GraphData graph = GraphSampler.Sample(function, startX, endX, Samples);
            if (graph.ValidCount == 0)
            {
                g.DrawString("На выбранном интервале нет допустимых значений функции.",
                    Font, Brushes.DarkRed, r.Left, r.Top);
                return;
            }

            int lastIndex = steps != null && steps.Count > 0
                ? Math.Max(0, Math.Min(currentIndex, steps.Count - 1)) : -1;
            double yMin = graph.YMin;
            double yMax = graph.YMax;

            // При успешном расчёте обязательно показываем найденную точку,
            // даже если она находится около края квантильного диапазона графика.
            if (lastIndex >= 0)
            {
                for (int i = 0; i <= lastIndex; i++)
                {
                    double stepY = steps[i].BestY;
                    if (NumericValidation.IsFinite(stepY))
                    {
                        if (stepY < yMin) yMin = stepY;
                        if (stepY > yMax) yMax = stepY;
                    }
                }
            }

            double range = SafeRange(yMin, yMax);
            double padding = range * 0.10;
            if (NumericValidation.IsFinite(padding) && padding > 0)
            {
                yMin -= padding;
                yMax += padding;
            }
            if (!NumericValidation.IsFinite(yMin) || !NumericValidation.IsFinite(yMax) || !(yMax > yMin))
                return;

            using (Pen gridPen = new Pen(Color.FromArgb(229, 234, 239), 1f))
            using (Pen axisPen = new Pen(Color.FromArgb(85, 96, 107), 1.4f))
            using (Pen curvePen = new Pen(Color.FromArgb(36, 105, 210), 2f))
            using (Pen intervalPen = new Pen(Color.FromArgb(205, 135, 40), 1.5f))
            using (Pen probePen = new Pen(Color.FromArgb(125, 137, 150), 1f))
            using (Pen trailPen = new Pen(Color.FromArgb(30, 150, 115), 1.4f))
            using (Pen pointPen = new Pen(Color.DarkRed, 1.4f))
            using (Brush labelBrush = new SolidBrush(Color.FromArgb(65, 73, 82)))
            using (Brush bestBrush = new SolidBrush(Color.DarkRed))
            using (Brush trailBrush = new SolidBrush(Color.FromArgb(30, 150, 115)))
            using (Brush headerBrush = new SolidBrush(Color.FromArgb(45, 52, 60)))
            {
                intervalPen.DashStyle = DashStyle.Dash;
                probePen.DashStyle = DashStyle.Dot;

                string header = "f(x) = " + (formulaText ?? String.Empty);
                g.DrawString(header, Font, headerBrush, 74, 5);
                g.DrawString("График обрывается в точках разрыва; оранжевые линии — границы интервала.",
                    Font, headerBrush, 74, 22);
                DrawTicks(g, r, yMin, yMax, gridPen, labelBrush);

                float xZero = ToPixelX(0, r);
                float yZero = ToPixelY(0, yMin, yMax, r);
                if (xZero >= r.Left && xZero <= r.Right)
                    g.DrawLine(axisPen, xZero, r.Top, xZero, r.Bottom);
                if (yZero >= r.Top && yZero <= r.Bottom)
                    g.DrawLine(axisPen, r.Left, yZero, r.Right, yZero);

                GraphicsState state = g.Save();
                g.SetClip(r);

                // Соединяем только те соседние точки, которые прошли проверку
                // разрыва и поведения в середине сегмента.
                for (int i = 1; i < graph.XValues.Length; i++)
                {
                    if (!graph.ConnectFromPrevious[i])
                        continue;
                    float x1 = ToPixelX(graph.XValues[i - 1], r);
                    float x2 = ToPixelX(graph.XValues[i], r);
                    float y1 = ToPixelY(graph.YValues[i - 1], yMin, yMax, r);
                    float y2 = ToPixelY(graph.YValues[i], yMin, yMax, r);
                    g.DrawLine(curvePen, x1, y1, x2, y2);
                }

                if (lastIndex >= 0)
                {
                    GoldenSectionStep current = steps[lastIndex];
                    DrawVerticalMarker(g, current.A, r, intervalPen);
                    DrawVerticalMarker(g, current.B, r, intervalPen);
                    if (current.HasPair)
                    {
                        DrawVerticalMarker(g, current.X1, r, probePen);
                        DrawVerticalMarker(g, current.X2, r, probePen);
                    }

                    PointF previous = PointF.Empty;
                    bool hasPrevious = false;
                    for (int i = 0; i <= lastIndex; i++)
                    {
                        GoldenSectionStep step = steps[i];
                        if (!NumericValidation.IsFinite(step.BestX) || !NumericValidation.IsFinite(step.BestY))
                            continue;
                        PointF point = new PointF(ToPixelX(step.BestX, r), ToPixelY(step.BestY, yMin, yMax, r));
                        if (hasPrevious)
                            g.DrawLine(trailPen, previous, point);
                        g.FillEllipse(trailBrush, point.X - 3f, point.Y - 3f, 6f, 6f);
                        previous = point;
                        hasPrevious = true;
                    }

                    float bestPixelX = ToPixelX(current.BestX, r);
                    float bestPixelY = ToPixelY(current.BestY, yMin, yMax, r);
                    g.DrawLine(pointPen, bestPixelX - 8, bestPixelY, bestPixelX + 8, bestPixelY);
                    g.DrawLine(pointPen, bestPixelX, bestPixelY - 8, bestPixelX, bestPixelY + 8);
                    g.FillEllipse(bestBrush, bestPixelX - 4f, bestPixelY - 4f, 8f, 8f);
                }
                else
                {
                    // При неподходящем интервале график всё равно доступен для анализа.
                    DrawVerticalMarker(g, startX, r, intervalPen);
                    DrawVerticalMarker(g, endX, r, intervalPen);
                }

                g.Restore(state);
                g.DrawRectangle(Pens.DarkGray, r);
                g.DrawString("f(x)", Font, labelBrush, 12, r.Top - 5);
                SizeF xLabelSize = g.MeasureString("x", Font);
                g.DrawString("x", Font, labelBrush, r.Right - xLabelSize.Width, r.Bottom + 24);

                if (lastIndex >= 0)
                {
                    GoldenSectionStep current = steps[lastIndex];
                    string intervalText = "Текущий интервал: [" + Format(current.A) + "; " + Format(current.B) + "]";
                    g.DrawString(intervalText, Font, headerBrush, r.Left + 8, r.Top + 7);
                    string pointText = "Точка: (" + Format(current.BestX) + "; " + Format(current.BestY) + ")";
                    float bestPixelX = ToPixelX(current.BestX, r);
                    float bestPixelY = ToPixelY(current.BestY, yMin, yMax, r);
                    float textX = Math.Min(r.Right - g.MeasureString(pointText, Font).Width - 6, bestPixelX + 12);
                    float textY = Math.Max(r.Top + 25, bestPixelY - 22);
                    g.DrawString(pointText, Font, headerBrush, textX, textY);
                }
                else
                {
                    string intervalText = "Интервал: [" + Format(startX) + "; " + Format(endX) + "]";
                    g.DrawString(intervalText, Font, headerBrush, r.Left + 8, r.Top + 7);
                }
            }
        }

        private static double SafeRange(double min, double max)
        {
            double range = max - min;
            if (NumericValidation.IsFinite(range) && range > 0)
                return range;
            return Math.Max(1.0, Math.Max(Math.Abs(min), Math.Abs(max)));
        }

        private void DrawVerticalMarker(Graphics g, double x, Rectangle plot, Pen pen)
        {
            if (!NumericValidation.IsFinite(x)) return;
            float px = ToPixelX(x, plot);
            if (px >= plot.Left && px <= plot.Right)
                g.DrawLine(pen, px, plot.Top, px, plot.Bottom);
        }

        private void DrawTicks(Graphics g, Rectangle r, double yMin, double yMax, Pen grid, Brush label)
        {
            double dx = NiceStep((endX - startX) / 7.0);
            double yRange = SafeRange(yMin, yMax);
            double dy = NiceStep(yRange / 6.0);
            if (dx > 0)
            {
                double first = Math.Ceiling(startX / dx) * dx;
                for (int k = 0; k < 20; k++)
                {
                    double x = first + k * dx;
                    if (x > endX || !NumericValidation.IsFinite(x)) break;
                    float px = ToPixelX(x, r);
                    g.DrawLine(grid, px, r.Top, px, r.Bottom);
                    string value = FormatTick(x);
                    SizeF size = g.MeasureString(value, Font);
                    float labelX = Math.Max(r.Left - 2, Math.Min(r.Right - size.Width + 2, px - size.Width / 2f));
                    g.DrawString(value, Font, label, labelX, r.Bottom + 4);
                }
            }
            if (dy > 0)
            {
                double first = Math.Ceiling(yMin / dy) * dy;
                for (int k = 0; k < 20; k++)
                {
                    double y = first + k * dy;
                    if (y > yMax || !NumericValidation.IsFinite(y)) break;
                    float py = ToPixelY(y, yMin, yMax, r);
                    g.DrawLine(grid, r.Left, py, r.Right, py);
                    string value = FormatTick(y);
                    SizeF size = g.MeasureString(value, Font);
                    g.DrawString(value, Font, label, r.Left - size.Width - 6, py - size.Height / 2f);
                }
            }
        }

        private float ToPixelX(double x, Rectangle r)
        {
            double width = endX - startX;
            if (!(width > 0) || !NumericValidation.IsFinite(width)) return r.Left;
            double relative = (x - startX) / width;
            if (double.IsNaN(relative) || double.IsInfinity(relative)) return r.Left;
            relative = Math.Max(-1000.0, Math.Min(1000.0, relative));
            return (float)(r.Left + r.Width * relative);
        }

        private static float ToPixelY(double y, double min, double max, Rectangle r)
        {
            if (y <= min) return r.Bottom;
            if (y >= max) return r.Top;

            double scale = Math.Max(1.0, Math.Max(Math.Abs(min), Math.Abs(max)));
            double normalizedMin = min / scale;
            double normalizedMax = max / scale;
            double normalizedY = y / scale;
            double denominator = normalizedMax - normalizedMin;
            if (!(denominator > 0) || !NumericValidation.IsFinite(denominator))
                return r.Top + r.Height / 2f;
            double relative = (normalizedY - normalizedMin) / denominator;
            if (!NumericValidation.IsFinite(relative)) return r.Top + r.Height / 2f;
            relative = Math.Max(0.0, Math.Min(1.0, relative));
            return (float)(r.Bottom - r.Height * relative);
        }

        private static double NiceStep(double approximate)
        {
            if (approximate <= 0 || !NumericValidation.IsFinite(approximate)) return 0;
            double power = Math.Pow(10.0, Math.Floor(Math.Log10(approximate)));
            if (power <= 0 || !NumericValidation.IsFinite(power)) return 0;
            double scaled = approximate / power;
            return (scaled <= 1 ? 1 : scaled <= 2 ? 2 : scaled <= 5 ? 5 : 10) * power;
        }

        private static string FormatTick(double value)
        {
            if (Math.Abs(value) < 1e-14) value = 0;
            return value.ToString("G4", CultureInfo.CurrentCulture);
        }

        private static string Format(double value)
        {
            return value.ToString("G8", CultureInfo.CurrentCulture);
        }
    }
}
