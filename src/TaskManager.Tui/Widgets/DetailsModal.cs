using System;
using System.Collections.Generic;
using System.Linq;
using TUIKit;
using TUIKit.Input;
using TUIKit.Modals;
using TUIKit.Unicode;

namespace TaskManager.Tui
{
    /// <summary>
    /// Content of a details modal: labeled rows (with section headings) and optional charts.
    /// </summary>
    public sealed class DetailsContent
    {
        #region Public-Members

        public List<DetailRow> Rows { get; } = new List<DetailRow>();

        public List<ChartData> Charts { get; } = new List<ChartData>();

        public DetailsContent Section(string title)
        {
            Rows.Add(new DetailRow { Label = title, IsSection = true });
            return this;
        }

        public DetailsContent Row(string label, string value)
        {
            Rows.Add(new DetailRow { Label = label, Value = value ?? string.Empty });
            return this;
        }

        #endregion
    }

    /// <summary>
    /// One row of details.
    /// </summary>
    public sealed class DetailRow
    {
        public string Label { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        public bool IsSection { get; set; }
    }

    /// <summary>
    /// A large, live, scrollable details form (rows, then charts with axes). Up/Down/PgUp/PgDn/Home/End scroll;
    /// Enter or Escape closes.
    /// </summary>
    public sealed class DetailsModal : DialogModal
    {
        #region Private-Members

        private static readonly CellStyle _LabelStyle = CellStyle.Default.WithForeground(Color.FromPalette(8));
        private static readonly CellStyle _ValueStyle = CellStyle.Default;
        private static readonly CellStyle _SectionStyle = CellStyle.Default.WithAttribute(CellAttributes.Bold, true).WithForeground(Color.FromPalette(6));
        private readonly Func<DetailsContent> _Content;
        private int _Top = 0;
        private int _LastRows = 10;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="title">Title.</param>
        /// <param name="content">Returns current content on every render, so the details stay live.</param>
        public DetailsModal(string title, Func<DetailsContent> content)
        {
            _Content = content ?? throw new ArgumentNullException(nameof(content));
            Title = " " + title + " ";
            FooterHint = " ↑↓ PgUp PgDn scroll   Enter/Esc close ";
            MinContentWidth = 40;
            MinContentHeight = 8;
        }

        #endregion

        #region Public-Methods

        public override bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.Escape:
                case KeyCode.Enter:
                    RequestClose(null);
                    return true;
                case KeyCode.Up: _Top = Math.Max(0, _Top - 1); return true;
                case KeyCode.Down: _Top++; return true;
                case KeyCode.PageUp: _Top = Math.Max(0, _Top - _LastRows); return true;
                case KeyCode.PageDown: _Top += _LastRows; return true;
                case KeyCode.Home: _Top = 0; return true;
                case KeyCode.End: _Top = int.MaxValue / 2; return true;
                case KeyCode.Character:
                    if (key.Rune == 'q' || key.Rune == 'Q') RequestClose(null);
                    return true;
                default:
                    return true;
            }
        }

        #endregion

        #region Protected-Methods

        protected override int MeasureContentWidth(int availableWidth)
        {
            return Math.Min(availableWidth, 132);
        }

        protected override int MeasureContentHeight(int contentWidth)
        {
            return 10000;
        }

        protected override void RenderContent(ISurface content)
        {
            DetailsContent c = _Content();
            int width = content.Size.Width;
            int height = content.Size.Height;

            // Charts take the bottom part of the form when there is room. They sit side by side when each still gets
            // 40 columns (so every chart keeps its axes), otherwise stacked; either way at least 9 rows each.
            int chartCount = c.Charts.Count;
            bool sideBySide = chartCount > 1 && width / chartCount >= 40;
            int chartArea = chartCount == 0 ? 0 : sideBySide ? Math.Min(height / 2, 16) : Math.Min(height / 2, chartCount * 14);
            if (chartArea < (sideBySide ? 9 : 9 * chartCount) && !(chartCount == 1 && chartArea >= 9)) chartArea = 0;
            int rowArea = height - chartArea;

            int labelWidth = Math.Min(28, Math.Max(10, c.Rows.Where(r => !r.IsSection).Select(r => TextFit.Width(r.Label)).DefaultIfEmpty(10).Max() + 2));
            int valueWidth = Math.Max(10, width - labelWidth);

            List<Line> lines = new List<Line>();
            foreach (DetailRow row in c.Rows)
            {
                if (row.IsSection)
                {
                    if (lines.Count > 0) lines.Add(new Line());
                    lines.Add(new Line { Label = row.Label, IsSection = true });
                    continue;
                }

                List<string> chunks = Wrap(row.Value, valueWidth);
                for (int i = 0; i < chunks.Count; i++) lines.Add(new Line { Label = i == 0 ? row.Label : string.Empty, Value = chunks[i] });
            }

            _LastRows = Math.Max(1, rowArea);
            _Top = Math.Max(0, Math.Min(_Top, Math.Max(0, lines.Count - rowArea)));
            for (int r = 0; r < rowArea && _Top + r < lines.Count; r++)
            {
                Line line = lines[_Top + r];
                if (line.IsSection)
                {
                    content.DrawText(0, r, TextFit.Truncate(line.Label, width), _SectionStyle);
                    continue;
                }

                content.DrawText(0, r, TextFit.Truncate(line.Label, labelWidth - 1), _LabelStyle);
                content.DrawText(labelWidth, r, TextFit.Truncate(line.Value, valueWidth), _ValueStyle);
            }

            if (lines.Count > rowArea && rowArea > 0)
            {
                string more = " " + (_Top + 1) + "-" + Math.Min(lines.Count, _Top + rowArea) + " of " + lines.Count + " ";
                content.DrawText(Math.Max(0, width - more.Length), rowArea - 1, more, _LabelStyle);
            }

            if (chartArea > 0)
            {
                for (int i = 0; i < chartCount; i++)
                {
                    Rect area;
                    if (sideBySide)
                    {
                        int columnWidth = width / chartCount;
                        area = new Rect(i * columnWidth, rowArea, i == chartCount - 1 ? width - i * columnWidth : columnWidth - 1, chartArea);
                    }
                    else
                    {
                        int each = chartArea / chartCount;
                        area = new Rect(0, rowArea + i * each, width, each);
                    }

                    content.DrawText(area.X, area.Y, TextFit.Truncate(c.Charts[i].Title, area.Width), _SectionStyle);
                    AxisChart.RenderWithAxes(new TUIKit.SurfaceView(content, new Rect(area.X, area.Y + 1, area.Width, area.Height - 1)), c.Charts[i]);
                }
            }
        }

        #endregion

        #region Private-Methods

        private static List<string> Wrap(string text, int width)
        {
            List<string> ret = new List<string>();
            foreach (string paragraph in (text ?? string.Empty).Split('\n'))
            {
                string rest = paragraph;
                while (TextFit.Width(rest) > width)
                {
                    string head = TextFit.Truncate(rest, width);
                    int cut = head.LastIndexOf(' ');
                    if (cut > width / 2) head = head.Substring(0, cut);
                    ret.Add(head);
                    rest = rest.Substring(head.Length).TrimStart();
                }

                ret.Add(rest);
            }

            return ret;
        }

        #endregion

        #region Private-Classes

        private sealed class Line
        {
            public string Label = string.Empty;
            public string Value = string.Empty;
            public bool IsSection;
        }

        #endregion
    }
}
