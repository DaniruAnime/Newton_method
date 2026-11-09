using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace LocalMinimumGoldenSection
{
    public sealed class MainForm : Form
    {
        private readonly TextBox formulaBox;
        private readonly TextBox aBox;
        private readonly TextBox bBox;
        private readonly TextBox epsilonBox;
        private readonly PlotPanel plotPanel;
        private readonly DataGridView stepsGrid;
        private readonly Label resultLabel;
        private readonly ToolStripStatusLabel statusLabel;
        private readonly Timer playbackTimer;
        private ToolStripMenuItem firstItem;
        private ToolStripMenuItem previousItem;
        private ToolStripMenuItem nextItem;
        private ToolStripMenuItem playItem;
        private ToolStripMenuItem pauseItem;

        private CompiledFunction compiledFunction;
        private GoldenSectionResult calculation;
        private List<GoldenSectionStep> steps;
        private int currentStepIndex;
        private bool updatingGrid;
        private bool calculationReady;

        public MainForm()
        {
            Text = "Поиск локального минимума — метод золотого сечения";
            StartPosition = FormStartPosition.CenterScreen;
            Width = 1240;
            Height = 760;
            MinimumSize = new Size(900, 640);
            AutoScaleMode = AutoScaleMode.None;
            Font = SystemFonts.MessageBoxFont;

            formulaBox = new TextBox();
            aBox = new TextBox();
            bBox = new TextBox();
            epsilonBox = new TextBox();
            formulaBox.Text = "(x - 2)^2 + 1";
            aBox.Text = "0";
            bBox.Text = "5";
            epsilonBox.Text = "0.0001";

            plotPanel = new PlotPanel();
            stepsGrid = CreateStepsGrid();
            resultLabel = new Label();
            statusLabel = new ToolStripStatusLabel();
            playbackTimer = new Timer();
            playbackTimer.Interval = 350;

            MenuStrip menu = BuildMenu();
            Panel inputPanel = BuildInputPanel();
            SplitContainer split = BuildMainSplit();
            StatusStrip statusStrip = new StatusStrip();
            statusStrip.SizingGrip = true;
            statusLabel.Spring = true;
            statusLabel.TextAlign = ContentAlignment.MiddleLeft;
            statusLabel.Text = "Введите параметры и выберите «Вычисление → Рассчитать».";
            statusStrip.Items.Add(statusLabel);

            // Явная табличная компоновка исключает зависимость видимости
            // панели ввода от порядка Dock-контролов в Mono WinForms.
            TableLayoutPanel root = new TableLayoutPanel();
            root.Name = "rootLayout";
            root.Dock = DockStyle.Fill;
            root.Margin = Padding.Empty;
            root.Padding = Padding.Empty;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 27f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 104f));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 24f));

            menu.Dock = DockStyle.Fill;
            inputPanel.Dock = DockStyle.Fill;
            split.Dock = DockStyle.Fill;
            statusStrip.Dock = DockStyle.Fill;
            root.Controls.Add(menu, 0, 0);
            root.Controls.Add(inputPanel, 0, 1);
            root.Controls.Add(split, 0, 2);
            root.Controls.Add(statusStrip, 0, 3);
            Controls.Add(root);
            MainMenuStrip = menu;

            playbackTimer.Tick += PlaybackTimerTick;
            stepsGrid.CellClick += StepsGridCellClick;
            formulaBox.TextChanged += InputChanged;
            aBox.TextChanged += InputChanged;
            bBox.TextChanged += InputChanged;
            epsilonBox.TextChanged += InputChanged;

            UpdateMenuState();
        }

        private MenuStrip BuildMenu()
        {
            MenuStrip menu = new MenuStrip();

            ToolStripMenuItem fileMenu = new ToolStripMenuItem("Файл");
            ToolStripMenuItem clearItem = new ToolStripMenuItem("Очистить результаты");
            clearItem.ShortcutKeys = Keys.Control | Keys.L;
            clearItem.Click += delegate { ClearResults(); };
            ToolStripMenuItem exitItem = new ToolStripMenuItem("Выход");
            exitItem.Click += delegate { Close(); };
            fileMenu.DropDownItems.Add(clearItem);
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(exitItem);

            ToolStripMenuItem calculationMenu = new ToolStripMenuItem("Вычисление");
            ToolStripMenuItem calculateItem = new ToolStripMenuItem("Рассчитать");
            calculateItem.ShortcutKeys = Keys.F5;
            calculateItem.Click += delegate { Calculate(); };
            calculationMenu.DropDownItems.Add(calculateItem);

            ToolStripMenuItem viewMenu = new ToolStripMenuItem("Пошаговый просмотр");
            firstItem = new ToolStripMenuItem("В начало");
            firstItem.ShortcutKeys = Keys.Alt | Keys.Home;
            firstItem.Click += delegate { GoToFirstStep(); };
            previousItem = new ToolStripMenuItem("Предыдущий шаг");
            previousItem.ShortcutKeys = Keys.Alt | Keys.Left;
            previousItem.Click += delegate { PreviousStep(); };
            nextItem = new ToolStripMenuItem("Следующий шаг");
            nextItem.ShortcutKeys = Keys.Alt | Keys.Right;
            nextItem.Click += delegate { NextStep(); };
            playItem = new ToolStripMenuItem("Автовоспроизведение");
            playItem.ShortcutKeys = Keys.Control | Keys.Space;
            playItem.Click += delegate { StartPlayback(); };
            pauseItem = new ToolStripMenuItem("Пауза");
            pauseItem.ShortcutKeys = Keys.Escape;
            pauseItem.Click += delegate { StopPlayback(); };
            viewMenu.DropDownItems.Add(firstItem);
            viewMenu.DropDownItems.Add(previousItem);
            viewMenu.DropDownItems.Add(nextItem);
            viewMenu.DropDownItems.Add(new ToolStripSeparator());
            viewMenu.DropDownItems.Add(playItem);
            viewMenu.DropDownItems.Add(pauseItem);

            menu.Items.Add(fileMenu);
            menu.Items.Add(calculationMenu);
            menu.Items.Add(viewMenu);
            return menu;
        }

        private Panel BuildInputPanel()
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.Padding = new Padding(10, 5, 10, 5);
            panel.BackColor = SystemColors.Control;

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.Margin = Padding.Empty;
            layout.Padding = Padding.Empty;
            layout.ColumnCount = 1;
            layout.RowCount = 2;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 61f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            TableLayoutPanel fields = new TableLayoutPanel();
            fields.Dock = DockStyle.Fill;
            fields.Margin = Padding.Empty;
            fields.Padding = Padding.Empty;
            fields.ColumnCount = 4;
            fields.RowCount = 1;
            fields.GrowStyle = TableLayoutPanelGrowStyle.FixedSize;
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 46f));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f));
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f));
            fields.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            fields.Controls.Add(CreateInputField("Функция f(x)", formulaBox), 0, 0);
            fields.Controls.Add(CreateInputField("Левая граница a", aBox), 1, 0);
            fields.Controls.Add(CreateInputField("Правая граница b", bBox), 2, 0);
            fields.Controls.Add(CreateInputField("Точность ε", epsilonBox), 3, 0);

            Label hint = new Label();
            hint.Dock = DockStyle.Fill;
            hint.Margin = new Padding(3, 0, 3, 0);
            hint.TextAlign = ContentAlignment.MiddleLeft;
            hint.ForeColor = Color.FromArgb(95, 101, 108);
            hint.Text = "Подходит для участка с одной впадиной: слева функция убывает, справа возрастает. Команды — в меню.";

            layout.Controls.Add(fields, 0, 0);
            layout.Controls.Add(hint, 0, 1);
            panel.Controls.Add(layout);
            return panel;
        }

        private static Control CreateInputField(string caption, TextBox textBox)
        {
            TableLayoutPanel field = new TableLayoutPanel();
            field.Dock = DockStyle.Fill;
            field.Margin = new Padding(3, 0, 8, 0);
            field.Padding = Padding.Empty;
            field.ColumnCount = 1;
            field.RowCount = 2;
            field.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            field.RowStyles.Add(new RowStyle(SizeType.Absolute, 23f));
            field.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Label label = new Label();
            label.Text = caption;
            label.Dock = DockStyle.Fill;
            label.Margin = Padding.Empty;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Visible = true;

            textBox.Dock = DockStyle.Fill;
            textBox.Margin = new Padding(0, 1, 0, 0);
            textBox.MinimumSize = new Size(0, 26);
            textBox.Font = new Font(SystemFonts.MessageBoxFont.FontFamily, 10f);
            textBox.Visible = true;

            field.Controls.Add(label, 0, 0);
            field.Controls.Add(textBox, 0, 1);
            return field;
        }

        private SplitContainer BuildMainSplit()
        {
            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Size = new Size(1200, 600);
            split.Orientation = Orientation.Horizontal;
            split.SplitterWidth = 6;
            split.Panel1MinSize = 230;
            split.Panel2MinSize = 190;
            split.SplitterDistance = 370;
            split.Panel1.Controls.Add(plotPanel);
            plotPanel.Dock = DockStyle.Fill;

            TableLayoutPanel lower = new TableLayoutPanel();
            lower.Dock = DockStyle.Fill;
            lower.RowCount = 2;
            lower.ColumnCount = 1;
            lower.RowStyles.Add(new RowStyle(SizeType.Absolute, 46f));
            lower.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            resultLabel.Dock = DockStyle.Fill;
            resultLabel.Padding = new Padding(9, 5, 9, 4);
            resultLabel.TextAlign = ContentAlignment.MiddleLeft;
            resultLabel.BackColor = Color.FromArgb(246, 248, 250);
            resultLabel.ForeColor = Color.FromArgb(50, 58, 65);
            resultLabel.Text = "Результат появится после расчёта. Для просмотра сходимости используйте меню «Пошаговый просмотр».";

            stepsGrid.Dock = DockStyle.Fill;
            lower.Controls.Add(resultLabel, 0, 0);
            lower.Controls.Add(stepsGrid, 0, 1);
            split.Panel2.Controls.Add(lower);
            return split;
        }

        private static DataGridView CreateStepsGrid()
        {
            DataGridView grid = new DataGridView();
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.MultiSelect = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            grid.BackgroundColor = Color.White;
            grid.BorderStyle = BorderStyle.None;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            grid.Columns.Add("iteration", "Шаг");
            grid.Columns.Add("a", "a");
            grid.Columns.Add("b", "b");
            grid.Columns.Add("x1", "x₁");
            grid.Columns.Add("f1", "f(x₁)");
            grid.Columns.Add("x2", "x₂");
            grid.Columns.Add("f2", "f(x₂)");
            grid.Columns.Add("width", "Длина интервала");
            grid.Columns.Add("bestX", "Текущий x");
            grid.Columns.Add("bestY", "Текущий y");
            grid.Columns[0].FillWeight = 45;
            grid.Columns[1].FillWeight = 85;
            grid.Columns[2].FillWeight = 85;
            grid.Columns[3].FillWeight = 85;
            grid.Columns[4].FillWeight = 90;
            grid.Columns[5].FillWeight = 85;
            grid.Columns[6].FillWeight = 90;
            grid.Columns[7].FillWeight = 95;
            grid.Columns[8].FillWeight = 95;
            grid.Columns[9].FillWeight = 95;
            return grid;
        }

        private void Calculate()
        {
            StopPlayback();
            try
            {
                double a = ParseDouble(aBox.Text, "a");
                double b = ParseDouble(bBox.Text, "b");
                double epsilon = ParseDouble(epsilonBox.Text, "ε");
                if (!(a < b))
                    throw new ArgumentException("Левая граница a должна быть меньше правой границы b.");
                if (epsilon <= 0)
                    throw new ArgumentException("Точность ε должна быть больше нуля.");

                FunctionParser parser = new FunctionParser();
                compiledFunction = parser.Compile(formulaBox.Text);
                calculation = GoldenSectionMethod.FindMinimum(compiledFunction, a, b, epsilon);
                steps = calculation.Steps;

                if (steps == null || steps.Count == 0)
                {
                    calculationReady = false;
                    stepsGrid.Rows.Clear();
                    // Даже если метод нельзя применить к интервалу, пользователь
                    // всё равно видит график (например, отдельные ветви tan(x)).
                    plotPanel.SetFunctionOnly(compiledFunction, formulaBox.Text, a, b);
                    resultLabel.ForeColor = Color.DarkRed;
                    resultLabel.Text = "Расчёт не выполнен: " + calculation.Message;
                    statusLabel.Text = calculation.Status == GoldenSectionStatus.MethodNotApplicable
                        ? "Метод не подходит для выбранного интервала."
                        : "Расчёт не выполнен.";
                    MessageBox.Show(this, calculation.Message, "Расчёт не выполнен",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    UpdateMenuState();
                    return;
                }

                calculationReady = true;
                LoadStepsToGrid();
                currentStepIndex = steps.Count - 1;
                RenderCurrentStep();
                statusLabel.Text = "Расчёт подготовлен. Просмотрите шаги или включите автовоспроизведение.";
                UpdateMenuState();
            }
            catch (ArgumentException ex)
            {
                ShowInputError(ex.Message);
            }
            catch (ExpressionException ex)
            {
                ShowInputError(ex.Message);
            }
            catch (Exception ex)
            {
                ShowInputError("Не удалось выполнить расчёт: " + ex.Message);
            }
        }

        private static double ParseDouble(string text, string fieldName)
        {
            string normalized = (text ?? String.Empty).Trim().Replace(',', '.');
            double value;
            if (normalized.Length == 0 || !double.TryParse(normalized,
                NumberStyles.Float, CultureInfo.InvariantCulture, out value) ||
                !NumericValidation.IsFinite(value))
                throw new ArgumentException("Поле «" + fieldName + "» должно содержать конечное вещественное число.");
            return value;
        }

        private void ShowInputError(string message)
        {
            calculationReady = false;
            calculation = null;
            steps = null;
            compiledFunction = null;
            stepsGrid.Rows.Clear();
            plotPanel.ClearPlot();
            resultLabel.Text = "Ошибка: " + message;
            statusLabel.Text = "Проверьте введённые данные.";
            UpdateMenuState();
            MessageBox.Show(this, message, "Некорректные данные",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void LoadStepsToGrid()
        {
            updatingGrid = true;
            stepsGrid.Rows.Clear();
            for (int i = 0; i < steps.Count; i++)
            {
                GoldenSectionStep step = steps[i];
                string x1 = step.HasPair ? Format(step.X1) : "—";
                string f1 = step.HasPair ? Format(step.F1) : "—";
                string x2 = step.HasPair ? Format(step.X2) : "—";
                string f2 = step.HasPair ? Format(step.F2) : "—";
                stepsGrid.Rows.Add(step.Iteration.ToString(CultureInfo.CurrentCulture),
                    Format(step.A), Format(step.B), x1, f1, x2, f2,
                    Format(step.Width), Format(step.BestX), Format(step.BestY));
            }
            if (stepsGrid.Rows.Count > 0)
            {
                stepsGrid.ClearSelection();
                stepsGrid.Rows[0].Selected = true;
                stepsGrid.CurrentCell = stepsGrid.Rows[0].Cells[0];
            }
            updatingGrid = false;
        }

        private void RenderCurrentStep()
        {
            if (!calculationReady || steps == null || steps.Count == 0)
                return;

            currentStepIndex = Math.Max(0, Math.Min(currentStepIndex, steps.Count - 1));
            GoldenSectionStep step = steps[currentStepIndex];
            double initialA = ParseDouble(aBox.Text, "a");
            double initialB = ParseDouble(bBox.Text, "b");
            plotPanel.SetData(compiledFunction, formulaBox.Text, initialA, initialB, steps, currentStepIndex);

            bool atLastStep = currentStepIndex == steps.Count - 1;
            if (!atLastStep)
            {
                resultLabel.ForeColor = Color.FromArgb(50, 58, 65);
                resultLabel.Text = "Шаг " + currentStepIndex.ToString(CultureInfo.CurrentCulture) +
                    " из " + (steps.Count - 1).ToString(CultureInfo.CurrentCulture) +
                    ". Интервал: [" + Format(step.A) + "; " + Format(step.B) +
                    "]. Приближение: x = " + Format(step.BestX) + "; y = " + Format(step.BestY) + ".";
                statusLabel.Text = "Пошаговый просмотр: интервал сужается к минимуму.";
            }
            else
            {
                ShowFinalStatus();
            }

            updatingGrid = true;
            if (currentStepIndex < stepsGrid.Rows.Count)
            {
                stepsGrid.ClearSelection();
                stepsGrid.Rows[currentStepIndex].Selected = true;
                stepsGrid.CurrentCell = stepsGrid.Rows[currentStepIndex].Cells[0];
                stepsGrid.FirstDisplayedScrollingRowIndex = Math.Max(0,
                    Math.Min(currentStepIndex, Math.Max(0, stepsGrid.Rows.Count - 1)));
            }
            updatingGrid = false;
            UpdateMenuState();
        }

        private void ShowFinalStatus()
        {
            if (calculation.Status == GoldenSectionStatus.Success && calculation.HasPoint)
            {
                resultLabel.ForeColor = Color.DarkGreen;
                resultLabel.Text = "Минимум найден: x_min = " + Format(calculation.X) +
                    "; y_min = f(x_min) = " + Format(calculation.FX) +
                    ". Конечный интервал: [" + Format(calculation.A) + "; " +
                    Format(calculation.B) + "]; ширина = " + Format(calculation.Width) + ".";
                statusLabel.Text = "Точность достигнута. Просмотр последовательности завершён.";
            }
            else
            {
                resultLabel.ForeColor = Color.DarkRed;
                string details = calculation.Message;
                if (calculation.HasPoint)
                    details += " Лучшее приближение: x = " + Format(calculation.X) +
                        "; y = " + Format(calculation.FX) + ".";
                resultLabel.Text = "Расчёт остановлен: " + details;
                statusLabel.Text = "Требуемая точность не подтверждена.";
            }
        }

        private void NextStep()
        {
            if (!calculationReady || steps == null || currentStepIndex >= steps.Count - 1)
            {
                StopPlayback();
                return;
            }
            currentStepIndex++;
            RenderCurrentStep();
        }

        private void PreviousStep()
        {
            if (!calculationReady || steps == null || currentStepIndex <= 0)
                return;
            currentStepIndex--;
            RenderCurrentStep();
        }

        private void GoToFirstStep()
        {
            if (!calculationReady || steps == null || steps.Count == 0)
                return;
            StopPlayback();
            currentStepIndex = 0;
            RenderCurrentStep();
        }

        private void StartPlayback()
        {
            if (!calculationReady || steps == null || currentStepIndex >= steps.Count - 1)
                return;
            playbackTimer.Start();
            statusLabel.Text = "Автовоспроизведение шагов...";
            UpdateMenuState();
        }

        private void StopPlayback()
        {
            playbackTimer.Stop();
            UpdateMenuState();
        }

        private void PlaybackTimerTick(object sender, EventArgs e)
        {
            if (currentStepIndex >= steps.Count - 1)
            {
                StopPlayback();
                return;
            }
            NextStep();
            if (currentStepIndex >= steps.Count - 1)
                StopPlayback();
        }

        private void StepsGridCellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (updatingGrid || e.RowIndex < 0 || !calculationReady || steps == null)
                return;
            StopPlayback();
            currentStepIndex = e.RowIndex;
            RenderCurrentStep();
        }

        private void InputChanged(object sender, EventArgs e)
        {
            // Очищаем и обычный результат, и диагностический график, если
            // метод был неприменим к предыдущему интервалу.
            if (!calculationReady && compiledFunction == null && calculation == null)
                return;
            StopPlayback();
            calculationReady = false;
            calculation = null;
            steps = null;
            compiledFunction = null;
            stepsGrid.Rows.Clear();
            plotPanel.ClearPlot();
            resultLabel.ForeColor = Color.FromArgb(50, 58, 65);
            resultLabel.Text = "Параметры изменены. Выполните расчёт заново.";
            statusLabel.Text = "Ожидание новых параметров.";
            UpdateMenuState();
        }

        private void ClearResults()
        {
            StopPlayback();
            calculationReady = false;
            calculation = null;
            steps = null;
            compiledFunction = null;
            currentStepIndex = 0;
            stepsGrid.Rows.Clear();
            plotPanel.ClearPlot();
            resultLabel.ForeColor = Color.FromArgb(50, 58, 65);
            resultLabel.Text = "Результаты очищены. Введённые параметры сохранены.";
            statusLabel.Text = "Готово к новому расчёту.";
            UpdateMenuState();
        }

        private void UpdateMenuState()
        {
            bool ready = calculationReady && steps != null && steps.Count > 0;
            firstItem.Enabled = ready && currentStepIndex > 0;
            previousItem.Enabled = ready && currentStepIndex > 0;
            nextItem.Enabled = ready && currentStepIndex < steps.Count - 1;
            playItem.Enabled = ready && currentStepIndex < steps.Count - 1 && !playbackTimer.Enabled;
            pauseItem.Enabled = playbackTimer.Enabled;
        }

        private static string Format(double value)
        {
            return value.ToString("G10", CultureInfo.CurrentCulture);
        }
    }
}
