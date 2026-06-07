using System;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;
using AvaloniaEdit.Editing;

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

        public void Clear()
        {
            if (_text == null && CursorOffset == null)
                return;

            _text = null;
            CursorOffset = null;
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
                ? new PreeditTextElement(_text)
                : null;
        }
    }

    internal sealed class PreeditTextElement : VisualLineElement
    {
        public PreeditTextElement(string text)
            : base(Math.Max(1, text?.Length ?? 0), 0)
        {
            Text = string.IsNullOrEmpty(text) ? " " : text;
        }

        public string Text { get; }

        public override TextRun CreateTextRun(int startVisualColumn, ITextRunConstructionContext context)
        {
            TextRunProperties.SetTextDecorations(TextDecorations.Underline);
            var relativeOffset = Math.Max(0, startVisualColumn - VisualColumn);
            relativeOffset = Math.Min(relativeOffset, Text.Length);
            return new TextCharacters(Text.AsMemory(relativeOffset), TextRunProperties);
        }

        public override ReadOnlyMemory<char> GetPrecedingText(int visualColumnLimit, ITextRunConstructionContext context)
        {
            var length = Math.Max(0, Math.Min(visualColumnLimit - VisualColumn, Text.Length));
            return Text.AsMemory(0, length);
        }
    }
}
