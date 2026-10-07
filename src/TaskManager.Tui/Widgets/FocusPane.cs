using System;
using System.Collections.Generic;
using TUIKit;
using TUIKit.Content;
using TUIKit.Input;
using TUIKit.Unicode;
using TUIKit.Widgets;

namespace TaskManager.Tui
{
    /// <summary>
    /// A text pane that can take focus and opens details on Enter.
    /// </summary>
    public sealed class FocusPane : IWidget, IFocusable, IFocusAware, IKeyHintSource
    {
        #region Public-Members

        /// <summary>
        /// Raised on Enter.
        /// </summary>
        public event Action Activated;

        /// <summary>
        /// The pane to write into.
        /// </summary>
        public Pane Pane { get; private set; }

        #endregion

        #region Private-Members

        private static readonly CellStyle _Hint = CellStyle.Default.WithAttribute(CellAttributes.Dim, true);
        private readonly string _HintText;
        private bool _Focused = false;

        #endregion

        #region Constructors-and-Factories

        public FocusPane(string id, string hint = "details")
        {
            Pane = new Pane(id);
            _HintText = " Enter: " + hint + " ";
        }

        #endregion

        #region Public-Methods

        public Size Measure(Size available)
        {
            return available;
        }

        public void Render(ISurface surface)
        {
            Pane.Render(surface);
            if (_Focused && surface.Size.Width > TextFit.Width(_HintText) + 4)
                surface.DrawText(surface.Size.Width - TextFit.Width(_HintText), surface.Size.Height - 1, _HintText, _Hint);
        }

        public bool HandleKey(KeyEvent key)
        {
            if (key.Code == KeyCode.Enter && key.Modifiers == KeyModifiers.None)
            {
                Activated?.Invoke();
                return true;
            }

            return false;
        }

        public void OnFocusChanged(bool focused)
        {
            _Focused = focused;
        }

        public IReadOnlyList<KeyHint> GetKeyHints()
        {
            return new[] { new KeyHint("Enter", "Details") };
        }

        #endregion
    }
}
