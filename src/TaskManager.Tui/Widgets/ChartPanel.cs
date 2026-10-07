using System;
using System.Collections.Generic;
using TUIKit;
using TUIKit.Input;
using TUIKit.Unicode;
using TUIKit.Widgets;

namespace TaskManager.Tui
{
    /// <summary>
    /// A chart that can take focus (Tab) and maximizes on Enter. Shows the lines compactly; the maximized view
    /// (<see cref="ChartModal"/>) adds axes, tick labels, and titles.
    /// </summary>
    public sealed class ChartPanel : IWidget, IFocusable, IFocusAware, IKeyHintSource
    {
        #region Public-Members

        /// <summary>
        /// Raised when the user asks to maximize the chart.
        /// </summary>
        public event Action<ChartPanel> Activated;

        /// <summary>
        /// Latest data.
        /// </summary>
        public ChartData Data { get; private set; }

        /// <summary>
        /// True while focused.
        /// </summary>
        public bool IsFocused { get; private set; }

        #endregion

        #region Private-Members

        private static readonly CellStyle _Hint = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);
        private readonly LineChart _Chart = new LineChart(new double[] { 0, 0 });

        #endregion

        #region Constructors-and-Factories

        public ChartPanel(ChartData initial)
        {
            Data = initial ?? throw new ArgumentNullException(nameof(initial));
        }

        #endregion

        #region Public-Methods

        public void Update(ChartData data)
        {
            if (data != null) Data = data;
        }

        public Size Measure(Size available)
        {
            return available;
        }

        public void Render(ISurface surface)
        {
            AxisChart.RenderCompact(surface, Data, _Chart);
            if (IsFocused && surface.Size.Width > 24)
            {
                string hint = " Enter: maximize ";
                surface.DrawText(surface.Size.Width - TextFit.Width(hint), 0, hint, _Hint);
            }
        }

        public bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Enter && key.Modifiers == KeyModifiers.None)
            {
                Activated?.Invoke(this);
                return true;
            }

            return false;
        }

        public void OnFocusChanged(bool focused)
        {
            IsFocused = focused;
        }

        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            return new[] { new KeyHint("Enter", "Maximize chart") };
        }

        #endregion
    }
}
