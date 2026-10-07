using System;
using TUIKit;
using TUIKit.Input;
using TUIKit.Modals;

namespace TaskManager.Tui
{
    /// <summary>
    /// A maximized, live chart with axes, tick labels, and titles. Enter or Escape restores.
    /// </summary>
    public sealed class ChartModal : DialogModal
    {
        #region Private-Members

        private readonly Func<ChartData> _Data;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="data">Returns the current data on every render, so the chart stays live.</param>
        public ChartModal(Func<ChartData> data)
        {
            _Data = data ?? throw new ArgumentNullException(nameof(data));
            ChartData d = data();
            Title = " " + d.Title + " ";
            FooterHint = " Enter/Esc restore ";
            MinContentWidth = 30;
            MinContentHeight = 10;
        }

        #endregion

        #region Public-Methods

        public override bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Escape || key.Code == KeyCode.Enter || (key.Code == KeyCode.Character && (key.Rune == 'q' || key.Rune == 'Q')))
                RequestClose(null);
            return true;
        }

        #endregion

        #region Protected-Methods

        protected override int MeasureContentWidth(int availableWidth)
        {
            return availableWidth;
        }

        protected override int MeasureContentHeight(int contentWidth)
        {
            return 10000;
        }

        protected override void RenderContent(ISurface content)
        {
            AxisChart.RenderWithAxes(content, _Data());
        }

        #endregion
    }
}
