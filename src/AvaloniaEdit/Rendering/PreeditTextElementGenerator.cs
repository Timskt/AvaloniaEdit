using System;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Utils;

namespace AvaloniaEdit.Rendering
{
    internal sealed class PreeditTextElementGenerator : VisualLineElementGenerator
    {
        private readonly TextArea _textArea;
        private string _text;

        public PreeditTextElementGenerator(TextArea textArea)
        {
            _textArea = textArea ?? throw new ArgumentNullException(nameof(textArea));
        }

        public bool HasPreedit => !string.IsNullOrEmpty(_text);

        public string Text => _text;

        public int? CursorOffset { get; private set; }

        public void SetPreedit(string text, int? cursorOffset)
        {
            text = string.IsNullOrEmpty(text) ? null : text;
            if (_text == text && CursorOffset == cursorOffset)
                return;

            _text = text;
            CursorOffset = cursorOffset;
            _textArea.TextView.Redraw();
        }

        public void Clear(bool redraw = true)
        {
            if (_text == null && CursorOffset == null)
                return;

            _text = null;
            CursorOffset = null;
            if (redraw)
                _textArea.TextView.Redraw();
        }

        public override int GetFirstInterestedOffset(int startOffset)
        {
            if (string.IsNullOrEmpty(_text) || _textArea.Document == null)
                return -1;

            var offset = Math.Max(0, Math.Min(_textArea.Caret.Offset, _textArea.Document.TextLength));
            return offset >= startOffset ? offset : -1;
        }

        public override VisualLineElement ConstructElement(int offset)
        {
            return !string.IsNullOrEmpty(_text) && offset == _textArea.Caret.Offset
                ? new PreeditTextElement(_text, CursorOffset)
                : null;
        }
    }

    internal sealed class PreeditTextElement : VisualLineElement
    {
        public PreeditTextElement(string text, int? cursorOffset)
            : base(Math.Max(1, text?.Length ?? 0), 0)
        {
            Text = string.IsNullOrEmpty(text) ? " " : text;
            CursorOffset = Math.Max(0, Math.Min(cursorOffset ?? Text.Length, Text.Length));
        }

        public string Text { get; }

        public int CursorOffset { get; }

        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
        {
            var relativeOffset = Math.Max(0, startVisualColumn - VisualColumn);
            relativeOffset = Math.Min(relativeOffset, Text.Length);
            return new PreeditTextRun(this, relativeOffset, context.TextView, TextRunProperties);
        }

        public override ReadOnlyMemory<char> GetPrecedingText(int visualColumnLimit, ITextRunConstructionContext context)
        {
            var length = Math.Max(0, Math.Min(visualColumnLimit - VisualColumn, Text.Length));
            return Text.AsMemory(0, length);
        }
    }

    internal sealed class PreeditTextRun : DrawableTextRun
    {
        private readonly TextLine _textLine;
        private readonly TextLine _prefixLine;
        private readonly int _relativeOffset;

        public PreeditTextRun(PreeditTextElement element, int relativeOffset, TextView textView, TextRunProperties properties)
        {
            Element = element ?? throw new ArgumentNullException(nameof(element));
            Properties = properties ?? throw new ArgumentNullException(nameof(properties));
            _relativeOffset = Math.Max(0, Math.Min(relativeOffset, element.Text.Length));
            Text = element.Text.AsMemory(_relativeOffset);

            var formatter = TextFormatterFactory.Create(textView);
            _textLine = FormattedTextElement.PrepareText(formatter, Text.ToString(), properties);
            var cursorOffset = Math.Max(_relativeOffset, element.CursorOffset) - _relativeOffset;
            cursorOffset = Math.Max(0, Math.Min(cursorOffset, Text.Length));
            _prefixLine = FormattedTextElement.PrepareText(formatter, Text.Slice(0, cursorOffset).ToString(), properties);
        }

        public PreeditTextElement Element { get; }

        public override ReadOnlyMemory<char> Text { get; }

        public override TextRunProperties Properties { get; }

        public override double Baseline => _textLine.Baseline;

        public override Size Size => new Size(_textLine.WidthIncludingTrailingWhitespace, _textLine.Height);

        public override void Draw(DrawingContext drawingContext, Point origin)
        {
            _textLine.Draw(drawingContext, origin);
            var foreground = Properties.ForegroundBrush ?? Brushes.Black;
            var underlineY = origin.Y + _textLine.Height - 1;
            drawingContext.DrawLine(new Pen(foreground, 1),
                new Point(origin.X, underlineY),
                new Point(origin.X + _textLine.WidthIncludingTrailingWhitespace, underlineY));

            var cursorX = origin.X + _prefixLine.WidthIncludingTrailingWhitespace;
            drawingContext.DrawLine(new Pen(foreground, 1),
                new Point(cursorX, origin.Y),
                new Point(cursorX, origin.Y + _textLine.Height));
        }
    }
}
