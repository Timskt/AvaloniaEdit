using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.Rendering;
using AvaloniaEdit.Utils;

namespace AvaloniaEdit.RichTextInput
{
    /// <summary>
    /// Well-known rich content categories supported by <see cref="RichTextInputManager"/>.
    /// </summary>
    public enum RichTextContentKind
    {
        Text,
        Emoji,
        Image,
        File,
        Custom
    }

    /// <summary>
    /// Describes one rich content payload embedded in a text document.
    /// </summary>
    public sealed class RichTextContent
    {
        public RichTextContent(
            RichTextContentKind kind,
            string displayText,
            object value = null,
            string source = null,
            string styleKey = null,
            IReadOnlyDictionary<string, object> metadata = null)
        {
            Kind = kind;
            DisplayText = displayText ?? string.Empty;
            Value = value;
            Source = source;
            StyleKey = styleKey;
            Metadata = metadata ?? new Dictionary<string, object>();
        }

        public RichTextContentKind Kind { get; }

        public string DisplayText { get; }

        public object Value { get; }

        public string Source { get; }

        public string StyleKey { get; }

        public IReadOnlyDictionary<string, object> Metadata { get; }

        public static RichTextContent FromText(string text)
        {
            return new RichTextContent(RichTextContentKind.Text, text, text);
        }

        public static RichTextContent FromEmoji(string emoji)
        {
            return new RichTextContent(RichTextContentKind.Emoji, emoji, emoji);
        }

        public static RichTextContent FromImage(Bitmap bitmap, string displayText = null, string source = null)
        {
            return new RichTextContent(RichTextContentKind.Image, displayText ?? "Image", bitmap, source);
        }

        public static RichTextContent FromFile(IStorageItem file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            return new RichTextContent(RichTextContentKind.File, file.Name, file, file.Path?.ToString());
        }

        public static RichTextContent FromFileName(string fileName)
        {
            if (fileName == null)
                throw new ArgumentNullException(nameof(fileName));

            return new RichTextContent(RichTextContentKind.File, Path.GetFileName(fileName), fileName, fileName);
        }

        public static RichTextContent FromCustom(string displayText, object value, string styleKey = null, IReadOnlyDictionary<string, object> metadata = null)
        {
            return new RichTextContent(RichTextContentKind.Custom, displayText, value, null, styleKey, metadata);
        }
    }

    public sealed class RichTextInputSnapshot
    {
        public RichTextInputSnapshot()
        {
            Text = string.Empty;
            Items = Array.Empty<RichTextInputSnapshotItem>();
        }

        public RichTextInputSnapshot(string text, IReadOnlyList<RichTextInputSnapshotItem> items)
        {
            Text = text ?? string.Empty;
            Items = items ?? Array.Empty<RichTextInputSnapshotItem>();
        }

        public string Text { get; set; }

        public IReadOnlyList<RichTextInputSnapshotItem> Items { get; set; }
    }

    public sealed class RichTextInputSnapshotItem
    {
        public int Offset { get; set; }

        public RichTextContentKind Kind { get; set; }

        public string DisplayText { get; set; }

        public string Source { get; set; }

        public string StyleKey { get; set; }
    }

    public sealed class RichTextInputValue
    {
        public RichTextInputValue()
        {
            Text = string.Empty;
            Items = Array.Empty<RichTextInputValueItem>();
        }

        public RichTextInputValue(string text, IReadOnlyList<RichTextInputValueItem> items)
        {
            Text = text ?? string.Empty;
            Items = items ?? Array.Empty<RichTextInputValueItem>();
        }

        public string Text { get; set; }

        public IReadOnlyList<RichTextInputValueItem> Items { get; set; }
    }

    public sealed class RichTextInputValueItem
    {
        public int Offset { get; set; }

        public RichTextContent Content { get; set; }
    }

    public sealed class RichTextPasteContext
    {
        internal RichTextPasteContext(IAsyncDataTransfer dataTransfer, int offset, bool replaceSelection)
        {
            DataTransfer = dataTransfer ?? throw new ArgumentNullException(nameof(dataTransfer));
            Offset = offset;
            ReplaceSelection = replaceSelection;
            Contents = new List<RichTextContent>();
        }

        public IAsyncDataTransfer DataTransfer { get; }

        public int Offset { get; }

        public bool ReplaceSelection { get; }

        public IList<RichTextContent> Contents { get; }

        public string Text { get; private set; }

        public bool Handled { get; private set; }

        public void InsertContents(IEnumerable<RichTextContent> contents)
        {
            Contents.Clear();
            if (contents != null)
            {
                foreach (var content in contents)
                {
                    if (content != null)
                        Contents.Add(content);
                }
            }

            Text = null;
            Handled = true;
        }

        public void InsertText(string text)
        {
            Contents.Clear();
            Text = text ?? string.Empty;
            Handled = true;
        }

        public void UseDefault()
        {
            Contents.Clear();
            Text = null;
            Handled = false;
        }
    }

    public sealed class RichTextDropContext
    {
        internal RichTextDropContext(IDataTransfer dataTransfer, int offset, bool replaceSelection)
        {
            DataTransfer = dataTransfer ?? throw new ArgumentNullException(nameof(dataTransfer));
            Offset = offset;
            ReplaceSelection = replaceSelection;
            Contents = new List<RichTextContent>();
        }

        public IDataTransfer DataTransfer { get; }

        public int Offset { get; }

        public bool ReplaceSelection { get; }

        public IList<RichTextContent> Contents { get; }

        public string Text { get; private set; }

        public bool Handled { get; private set; }

        public void InsertContents(IEnumerable<RichTextContent> contents)
        {
            Contents.Clear();
            if (contents != null)
            {
                foreach (var content in contents)
                {
                    if (content != null)
                        Contents.Add(content);
                }
            }

            Text = null;
            Handled = true;
        }

        public void InsertText(string text)
        {
            Contents.Clear();
            Text = text ?? string.Empty;
            Handled = true;
        }

        public void UseDefault()
        {
            Contents.Clear();
            Text = null;
            Handled = false;
        }
    }

    public sealed class RichTextTextTriggerOptions
    {
        public RichTextTextTriggerOptions()
        {
            Trigger = '@';
            MaxQueryLength = 64;
            StopAtRichContent = true;
            AllowEmptyQuery = true;
        }

        public char Trigger { get; set; }

        public int MaxQueryLength { get; set; }

        public bool StopAtRichContent { get; set; }

        public bool AllowEmptyQuery { get; set; }

        public Func<char, bool> IsBoundary { get; set; }

        public Func<char, bool> IsQueryCharacter { get; set; }
    }

    public sealed class RichTextTextTriggerMatch
    {
        public RichTextTextTriggerMatch(char trigger, int triggerOffset, int caretOffset, string query)
        {
            Trigger = trigger;
            TriggerOffset = triggerOffset;
            CaretOffset = caretOffset;
            Query = query ?? string.Empty;
        }

        public char Trigger { get; }

        public int TriggerOffset { get; }

        public int CaretOffset { get; }

        public int Length => CaretOffset - TriggerOffset;

        public string Query { get; }
    }

    public sealed class RichTextContentChangedEventArgs : EventArgs
    {
        public RichTextContentChangedEventArgs(RichTextContentItem item)
        {
            Item = item;
        }

        public RichTextContentItem Item { get; }
    }

    public sealed class RichTextContentRemovingEventArgs : EventArgs
    {
        public RichTextContentRemovingEventArgs(RichTextContentItem item)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
        }

        public RichTextContentItem Item { get; }

        public bool Cancel { get; set; }
    }

    public enum RichTextContentPointerEventKind
    {
        PointerPressed,
        PointerReleased,
        DoubleTapped,
        ContextRequested
    }

    public enum RichTextContentPointerSelectionBehavior
    {
        None,
        SelectContent,
        PreserveSelection,
        ExtendSelection
    }

    public enum RichTextSelectedContentEnterBehavior
    {
        KeepDefault,
        MoveCaretAfterContent,
        InsertNewLineBeforeContent,
        InsertNewLineAfterContent
    }

    public sealed class RichTextContentPointerEventArgs : EventArgs
    {
        public RichTextContentPointerEventArgs(RichTextContentItem item, RoutedEventArgs routedEventArgs)
            : this(null, item, routedEventArgs, RichTextContentPointerEventKind.PointerPressed)
        {
        }

        internal RichTextContentPointerEventArgs(
            RichTextInputManager manager,
            RichTextContentItem item,
            RoutedEventArgs routedEventArgs,
            RichTextContentPointerEventKind eventKind)
        {
            Manager = manager;
            Item = item ?? throw new ArgumentNullException(nameof(item));
            RoutedEventArgs = routedEventArgs;
            EventKind = eventKind;
        }

        public RichTextInputManager Manager { get; }

        public RichTextContentItem Item { get; }

        public RoutedEventArgs RoutedEventArgs { get; }

        public RichTextContentPointerEventKind EventKind { get; }

        public TextArea TextArea => Manager?.TextArea;

        public PointerEventArgs PointerEventArgs => RoutedEventArgs as PointerEventArgs;

        public TappedEventArgs TappedEventArgs => RoutedEventArgs as TappedEventArgs;

        public ContextRequestedEventArgs ContextRequestedEventArgs => RoutedEventArgs as ContextRequestedEventArgs;

        public bool IsSelected => Manager?.IsContentSelected(Item) == true;

        public KeyModifiers KeyModifiers => PointerEventArgs?.KeyModifiers ?? KeyModifiers.None;

        public bool IsLeftButtonPressed => PointerEventArgs?
            .GetCurrentPoint(TextArea)
            .Properties
            .IsLeftButtonPressed == true;

        public bool IsRightButtonPressed => PointerEventArgs?
            .GetCurrentPoint(TextArea)
            .Properties
            .IsRightButtonPressed == true;

        public bool Handled { get; set; }

        public bool TryGetPosition(Control relativeTo, out Point position)
        {
            if (PointerEventArgs == null)
            {
                position = default;
                return false;
            }

            position = PointerEventArgs.GetPosition(relativeTo ?? TextArea);
            return true;
        }

        public IReadOnlyList<RichTextContentItem> GetSelectedItems()
        {
            return Manager?.GetSelectedItems() ?? Array.Empty<RichTextContentItem>();
        }

        public RichTextInputValue GetSelectionValue()
        {
            return Manager?.GetSelectionValue() ?? new RichTextInputValue();
        }

        public string GetSelectedPlainText(Func<RichTextContentItem, string> contentTextFactory = null)
        {
            return Manager?.GetSelectedPlainText(contentTextFactory) ?? string.Empty;
        }
    }

    public sealed class RichTextInlineContentStyle
    {
        public IBrush Background { get; set; } = Brushes.Transparent;

        public IBrush BorderBrush { get; set; } = Brushes.Transparent;

        public Thickness BorderThickness { get; set; } = new Thickness(1);

        public CornerRadius CornerRadius { get; set; } = new CornerRadius(4);

        public Thickness Padding { get; set; } = new Thickness(0);
    }

    public sealed class RichTextElementFactoryContext
    {
        internal RichTextElementFactoryContext(
            RichTextInputManager manager,
            RichTextContentItem item,
            double availableWidth,
            bool isSelected)
        {
            Manager = manager;
            Item = item;
            Content = item.Content;
            TextArea = manager.TextArea;
            AvailableWidth = availableWidth;
            IsSelected = isSelected;
        }

        public RichTextInputManager Manager { get; }

        public RichTextContentItem Item { get; }

        public RichTextContent Content { get; }

        public TextArea TextArea { get; }

        public double AvailableWidth { get; }

        public double MaxImageWidth => Manager.MaxImageWidth;

        public double MaxImageHeight => Manager.MaxImageHeight;

        public bool IsSelected { get; }

        public string StyleKey => Content.StyleKey;

        public IReadOnlyDictionary<string, object> Metadata => Content.Metadata;
    }

    /// <summary>
    /// A rich content instance attached to a single object-replacement character in the document.
    /// </summary>
    public sealed class RichTextContentItem
    {
        internal RichTextContentItem(RichTextContent content, AnchorSegment segment)
        {
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Segment = segment ?? throw new ArgumentNullException(nameof(segment));
        }

        public RichTextContent Content { get; }

        public int Offset => Segment.Offset;

        public int Length => Segment.Length;

        public int EndOffset => Segment.EndOffset;

        public object Tag { get; set; }

        internal AnchorSegment Segment { get; }
    }

    /// <summary>
    /// Handles rich clipboard and drag-and-drop payloads for a text area.
    /// </summary>
    public interface IRichTextInputDataHandler
    {
        bool CanInsert(IDataTransfer dataTransfer);

        bool CanInsert(IAsyncDataTransfer dataTransfer);

        Task<bool> InsertDataAsync(IDataTransfer dataTransfer, int offset, bool replaceSelection);

        Task<bool> InsertDataAsync(IAsyncDataTransfer dataTransfer, int offset, bool replaceSelection);
    }

    /// <summary>
    /// Configures rich content insertion and inline rendering for a <see cref="TextArea"/>.
    /// </summary>
    public sealed class RichTextInputManager : IDisposable, IRichTextInputDataHandler, ISelectionBackgroundSegmentTransformer
    {
        public const char ObjectReplacementCharacter = '\uFFFC';
        public const string ObjectReplacementString = "\uFFFC";
        public static readonly DataFormat<string> RichTextClipboardFormat =
            DataFormat.CreateStringApplicationFormat("AvaloniaEdit.RichTextInput");

        private static readonly HashSet<string> ImageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".bmp", ".gif", ".jpg", ".jpeg", ".png", ".webp"
        };

        private readonly TextArea _textArea;
        private readonly RichTextInlineObjectGenerator _generator;
        private readonly List<RichTextContentItem> _items = new List<RichTextContentItem>();
        private readonly Dictionary<string, Func<RichTextElementFactoryContext, Control>> _elementFactories =
            new Dictionary<string, Func<RichTextElementFactoryContext, Control>>(StringComparer.Ordinal);
        private bool _suppressTextSelectionBackgroundForRichContent = true;
        private bool _isDisposed;

        public RichTextInputManager(TextArea textArea)
        {
            _textArea = textArea ?? throw new ArgumentNullException(nameof(textArea));
            _generator = new RichTextInlineObjectGenerator(this);
            ElementFactory = CreateDefaultElement;
            ConvertImageFilesToImages = true;

            _textArea.TextView.ElementGenerators.Add(_generator);
            _textArea.TextView.Services.AddService<IRichTextInputDataHandler>(this);
            _textArea.TextView.Services.AddService<ISelectionBackgroundSegmentTransformer>(this);
            _textArea.TextView.SizeChanged += TextView_SizeChanged;
            _textArea.DocumentChanged += TextArea_DocumentChanged;
            _textArea.SelectionChanged += TextArea_SelectionChanged;
            _textArea.AddHandler(InputElement.KeyDownEvent, TextArea_KeyDown, RoutingStrategies.Tunnel);
            AttachToDocument(_textArea.Document);
        }

        public IReadOnlyList<RichTextContentItem> Items => _items;

        public TextArea TextArea => _textArea;

        public bool ConvertImageFilesToImages { get; set; }

        public Func<RichTextContentItem, Control> ElementFactory { get; set; }

        public Func<RichTextElementFactoryContext, Control> ElementFactoryWithContext { get; set; }

        public double MinInlineElementWidth { get; set; } = 48;

        public double MaxInlineElementWidth { get; set; } = 220;

        public double MaxImageWidth { get; set; } = 180;

        public double MaxImageHeight { get; set; } = 120;

        public Func<IDataTransfer, bool> CanImportDataTransfer { get; set; }

        public Func<IDataTransfer, Task<IEnumerable<RichTextContent>>> DataTransferImporter { get; set; }

        public Func<IAsyncDataTransfer, bool> CanImportAsyncDataTransfer { get; set; }

        public Func<IAsyncDataTransfer, Task<IEnumerable<RichTextContent>>> AsyncDataTransferImporter { get; set; }

        public Func<RichTextPasteContext, Task> PasteHandler { get; set; }

        public Func<RichTextDropContext, Task> DropHandler { get; set; }

        public InlineObjectVerticalAlignment InlineObjectAlignment { get; set; } = InlineObjectVerticalAlignment.Bottom;

        public Func<RichTextContentItem, InlineObjectVerticalAlignment> InlineObjectAlignmentSelector { get; set; }

        public LineContentVerticalAlignment LineContentAlignment
        {
            get => _textArea.Options.LineContentVerticalAlignment;
            set => _textArea.Options.LineContentVerticalAlignment = value;
        }

        public bool SelectContentOnPointerPressed { get; set; } = true;

        public bool EnableContentPointerInteractions { get; set; } = true;

        public bool HighlightSelectedContent { get; set; } = true;

        public bool SuppressTextSelectionBackgroundForRichContent
        {
            get => _suppressTextSelectionBackgroundForRichContent;
            set
            {
                if (_suppressTextSelectionBackgroundForRichContent == value)
                    return;

                _suppressTextSelectionBackgroundForRichContent = value;
                _textArea.TextView.InvalidateLayer(KnownLayer.Selection);
            }
        }

        public Func<RichTextContentItem, bool, RichTextInlineContentStyle> InlineContentStyleSelector { get; set; }

        public Func<RichTextContentItem, bool> CanRemoveContent { get; set; }

        public RichTextContentPointerSelectionBehavior ContentPointerSelectionBehavior { get; set; } =
            RichTextContentPointerSelectionBehavior.SelectContent;

        public Func<RichTextContentPointerEventArgs, RichTextContentPointerSelectionBehavior> ContentPointerSelectionBehaviorSelector { get; set; }

        public bool HandleContentPointerEvents { get; set; } = true;

        public Func<RichTextContentPointerEventArgs, bool> ContentPointerHandledSelector { get; set; }

        public RichTextSelectedContentEnterBehavior SelectedContentEnterBehavior { get; set; } =
            RichTextSelectedContentEnterBehavior.InsertNewLineAfterContent;

        public event EventHandler<RichTextContentChangedEventArgs> ContentInserted;

        public event EventHandler<RichTextContentRemovingEventArgs> ContentRemoving;

        public event EventHandler<RichTextContentChangedEventArgs> ContentRemoved;

        public event EventHandler<RichTextContentChangedEventArgs> ContentSelectionChanged;

        public event EventHandler<RichTextContentPointerEventArgs> ContentPointerPressed;

        public event EventHandler<RichTextContentPointerEventArgs> ContentPointerReleased;

        public event EventHandler<RichTextContentPointerEventArgs> ContentDoubleTapped;

        public event EventHandler<RichTextContentPointerEventArgs> ContentContextRequested;

        public static RichTextInputManager Install(TextArea textArea)
        {
            if (textArea == null)
                throw new ArgumentNullException(nameof(textArea));

            var existing = textArea.GetService(typeof(IRichTextInputDataHandler)) as RichTextInputManager;
            return existing ?? new RichTextInputManager(textArea);
        }

        public RichTextContentItem InsertContent(RichTextContent content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            var document = _textArea.Document ?? throw ThrowUtil.NoDocumentAssigned();
            RichTextContentItem item;
            using (document.RunUpdate())
            {
                if (!_textArea.Selection.IsEmpty)
                    _textArea.RemoveSelectedText();

                var offset = _textArea.Caret.Offset;
                item = InsertContent(offset, content);
                _textArea.Caret.Offset = offset + ObjectReplacementString.Length;
                _textArea.ClearSelection();
            }

            FinalizeCaretAfterInsertion();
            return item;
        }

        public RichTextContentItem InsertEmoji(string emoji)
        {
            return InsertContent(RichTextContent.FromEmoji(emoji));
        }

        public RichTextContentItem InsertImage(Bitmap bitmap, string displayText = null, string source = null)
        {
            return InsertContent(RichTextContent.FromImage(bitmap, displayText, source));
        }

        public RichTextContentItem InsertFile(IStorageItem file)
        {
            return InsertContent(RichTextContent.FromFile(file));
        }

        public RichTextContentItem InsertFileName(string fileName)
        {
            return InsertContent(RichTextContent.FromFileName(fileName));
        }

        public RichTextContentItem InsertCustom(string displayText, object value)
        {
            return InsertContent(RichTextContent.FromCustom(displayText, value));
        }

        public RichTextContentItem InsertCustom(string displayText, object value, string styleKey, IReadOnlyDictionary<string, object> metadata = null)
        {
            return InsertContent(RichTextContent.FromCustom(displayText, value, styleKey, metadata));
        }

        public IReadOnlyList<RichTextContentItem> GetItemsInDocumentOrder()
        {
            RemoveInvalidItems();
            return _items.OrderBy(item => item.Offset).ToArray();
        }

        public IReadOnlyList<RichTextContentItem> GetSelectedItems()
        {
            if (_textArea.Selection.IsEmpty)
                return Array.Empty<RichTextContentItem>();

            var segments = GetOrderedSelectionSegments();
            return GetItemsInDocumentOrder()
                .Where(item => segments.Any(segment =>
                    item.Offset >= segment.StartOffset && item.EndOffset <= segment.EndOffset))
                .ToArray();
        }

        public bool RemoveContent(RichTextContentItem item)
        {
            if (item == null || !_items.Contains(item) || _textArea.Document == null)
                return false;
            if (!CanRemove(item))
                return false;

            _textArea.Document.Remove(item.Offset, item.Length);
            return true;
        }

        public RichTextContentItem InsertContent(int offset, RichTextContent content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));

            var document = _textArea.Document ?? throw ThrowUtil.NoDocumentAssigned();
            using (document.RunUpdate())
            {
                document.Insert(offset, ObjectReplacementString, AnchorMovementType.BeforeInsertion);
                var item = AddItem(offset, content);
                if (document.UndoStack.AcceptChanges)
                    document.UndoStack.Push(new RichTextContentUndoOperation(this, content, offset, true, item));
                return item;
            }
        }

        public RichTextContentItem ReplaceRangeWithContent(int offset, int length, RichTextContent content)
        {
            if (content == null)
                throw new ArgumentNullException(nameof(content));
            if (offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length));

            var document = _textArea.Document ?? throw ThrowUtil.NoDocumentAssigned();
            if (offset + length > document.TextLength)
                throw new ArgumentOutOfRangeException(nameof(length));

            using (document.RunUpdate())
            {
                document.Replace(offset, length, ObjectReplacementString, OffsetChangeMappingType.KeepAnchorBeforeInsertion);
                var item = AddItem(offset, content);
                if (document.UndoStack.AcceptChanges)
                    document.UndoStack.Push(new RichTextContentUndoOperation(this, content, offset, true, item));

                _textArea.Caret.Offset = offset + ObjectReplacementString.Length;
                _textArea.ClearSelection();
                FinalizeCaretAfterInsertion();
                return item;
            }
        }

        public Rect GetCaretAnchorRect()
        {
            var rect = _textArea.Caret.CalculateCaretRectangle();
            return rect.WithX(rect.X - _textArea.TextView.HorizontalOffset)
                .WithY(rect.Y - _textArea.TextView.VerticalOffset);
        }

        public bool TryGetTextTriggerRange(char trigger, out int triggerOffset, out string query)
        {
            if (TryGetTextTrigger(new RichTextTextTriggerOptions { Trigger = trigger }, out var match))
            {
                triggerOffset = match.TriggerOffset;
                query = match.Query;
                return true;
            }

            triggerOffset = -1;
            query = null;
            return false;
        }

        public bool TryGetTextTrigger(RichTextTextTriggerOptions options, out RichTextTextTriggerMatch match)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            match = null;

            var document = _textArea.Document;
            if (document == null)
                return false;

            var caretOffset = Math.Max(0, Math.Min(_textArea.Caret.Offset, document.TextLength));
            var minOffset = options.MaxQueryLength > 0
                ? Math.Max(0, caretOffset - options.MaxQueryLength - 1)
                : 0;

            for (var offset = caretOffset - 1; offset >= minOffset; offset--)
            {
                var c = document.GetCharAt(offset);
                if (c == options.Trigger)
                {
                    var query = document.GetText(offset + 1, caretOffset - offset - 1);
                    if (!options.AllowEmptyQuery && query.Length == 0)
                        return false;

                    match = new RichTextTextTriggerMatch(options.Trigger, offset, caretOffset, query);
                    return true;
                }

                if (options.StopAtRichContent && c == ObjectReplacementCharacter)
                    break;

                if (options.IsBoundary?.Invoke(c) == true || (options.IsBoundary == null && char.IsWhiteSpace(c)))
                    break;

                if (options.IsQueryCharacter != null && !options.IsQueryCharacter(c))
                    break;
            }

            return false;
        }

        public bool TryGetItem(int offset, out RichTextContentItem item)
        {
            RemoveInvalidItems();
            item = _items.FirstOrDefault(i => i.Offset == offset);
            return item != null;
        }

        public int GetFirstInterestedOffset(int startOffset)
        {
            RemoveInvalidItems();
            var offset = int.MaxValue;
            foreach (var item in _items)
            {
                if (item.Offset >= startOffset && item.Offset < offset)
                    offset = item.Offset;
            }

            return offset == int.MaxValue ? -1 : offset;
        }

        public Control CreateElement(RichTextContentItem item)
        {
            var context = CreateElementFactoryContext(item);
            Control element = null;

            if (ElementFactoryWithContext != null)
                element = ElementFactoryWithContext(context);

            if (element == null
                && !string.IsNullOrEmpty(item.Content.StyleKey)
                && _elementFactories.TryGetValue(item.Content.StyleKey, out var keyedFactory))
            {
                element = keyedFactory(context);
            }

            var factory = ElementFactory;
            element ??= factory?.Invoke(item);
            element ??= CreateDefaultElement(item, context.AvailableWidth, MaxImageWidth, MaxImageHeight);
            return EnableContentPointerInteractions ? new RichTextInlineContentControl(this, item, element) : element;
        }

        public RichTextElementFactoryContext CreateElementFactoryContext(RichTextContentItem item)
        {
            if (item == null)
                throw new ArgumentNullException(nameof(item));

            return new RichTextElementFactoryContext(this, item, GetConstrainedInlineWidth(), IsContentSelected(item));
        }

        public void RegisterElementFactory(string styleKey, Func<RichTextElementFactoryContext, Control> factory)
        {
            if (string.IsNullOrEmpty(styleKey))
                throw new ArgumentException("A style key is required.", nameof(styleKey));
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _elementFactories[styleKey] = factory;
            _textArea.TextView.Redraw();
        }

        public bool UnregisterElementFactory(string styleKey)
        {
            if (string.IsNullOrEmpty(styleKey))
                return false;

            var removed = _elementFactories.Remove(styleKey);
            if (removed)
                _textArea.TextView.Redraw();
            return removed;
        }

        public void ClearElementFactories()
        {
            if (_elementFactories.Count == 0)
                return;

            _elementFactories.Clear();
            _textArea.TextView.Redraw();
        }

        public InlineObjectVerticalAlignment GetInlineObjectAlignment(RichTextContentItem item)
        {
            return InlineObjectAlignmentSelector?.Invoke(item) ?? InlineObjectAlignment;
        }

        public RichTextInlineContentStyle GetInlineContentStyle(RichTextContentItem item, bool selected)
        {
            var style = InlineContentStyleSelector?.Invoke(item, selected);
            if (style != null)
                return style;

            return selected && HighlightSelectedContent
                ? new RichTextInlineContentStyle
                {
                    Background = Brushes.Transparent,
                    BorderBrush = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(4)
                }
                : new RichTextInlineContentStyle();
        }

        public bool CanInsert(IDataTransfer dataTransfer)
        {
            if (dataTransfer == null)
                return false;

            return CanImportDataTransfer?.Invoke(dataTransfer) == true
                || dataTransfer.Contains(DataFormat.Bitmap)
                || dataTransfer.Contains(DataFormat.File)
                || dataTransfer.Contains(RichTextClipboardFormat);
        }

        public bool CanInsert(IAsyncDataTransfer dataTransfer)
        {
            if (dataTransfer == null)
                return false;

            return CanImportAsyncDataTransfer?.Invoke(dataTransfer) == true
                || dataTransfer.Contains(DataFormat.Bitmap)
                || dataTransfer.Contains(DataFormat.File)
                || dataTransfer.Contains(RichTextClipboardFormat);
        }

        public bool CanPaste(IAsyncDataTransfer dataTransfer)
        {
            return dataTransfer != null && (PasteHandler != null || CanInsert(dataTransfer));
        }

        public bool CanDrop(IDataTransfer dataTransfer)
        {
            return dataTransfer != null && (DropHandler != null || CanInsert(dataTransfer));
        }

        public Task<bool> InsertDataAsync(IDataTransfer dataTransfer, int offset, bool replaceSelection)
        {
            if (dataTransfer == null)
                throw new ArgumentNullException(nameof(dataTransfer));

            return InsertDataCoreAsync(dataTransfer, offset, replaceSelection);
        }

        public Task<bool> InsertDataAsync(IAsyncDataTransfer dataTransfer, int offset, bool replaceSelection)
        {
            if (dataTransfer == null)
                throw new ArgumentNullException(nameof(dataTransfer));

            return InsertDataCoreAsync(dataTransfer, offset, replaceSelection);
        }

        public async Task<bool> InsertPasteDataAsync(IAsyncDataTransfer dataTransfer, int offset, bool replaceSelection)
        {
            if (dataTransfer == null)
                throw new ArgumentNullException(nameof(dataTransfer));

            if (PasteHandler != null)
            {
                var context = new RichTextPasteContext(dataTransfer, offset, replaceSelection);
                await PasteHandler(context);
                if (context.Handled)
                {
                    if (context.Contents.Count > 0)
                        return InsertContents(offset, replaceSelection, context.Contents);

                    return InsertText(offset, replaceSelection, context.Text);
                }
            }

            return await InsertDataCoreAsync(dataTransfer, offset, replaceSelection);
        }

        public async Task<bool> InsertDropDataAsync(IDataTransfer dataTransfer, int offset, bool replaceSelection)
        {
            if (dataTransfer == null)
                throw new ArgumentNullException(nameof(dataTransfer));

            if (DropHandler != null)
            {
                var context = new RichTextDropContext(dataTransfer, offset, replaceSelection);
                await DropHandler(context);
                if (context.Handled)
                {
                    if (context.Contents.Count > 0)
                        return InsertContents(offset, replaceSelection, context.Contents);

                    return InsertText(offset, replaceSelection, context.Text);
                }
            }

            return await InsertDataCoreAsync(dataTransfer, offset, replaceSelection);
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            DetachFromDocument(_textArea.Document);
            _textArea.TextView.SizeChanged -= TextView_SizeChanged;
            _textArea.DocumentChanged -= TextArea_DocumentChanged;
            _textArea.SelectionChanged -= TextArea_SelectionChanged;
            _textArea.RemoveHandler(InputElement.KeyDownEvent, TextArea_KeyDown);
            _textArea.TextView.ElementGenerators.Remove(_generator);
            if (_textArea.GetService(typeof(IRichTextInputDataHandler)) == this)
                _textArea.TextView.Services.RemoveService<IRichTextInputDataHandler>();
            if (_textArea.GetService(typeof(ISelectionBackgroundSegmentTransformer)) == this)
                _textArea.TextView.Services.RemoveService<ISelectionBackgroundSegmentTransformer>();
        }

        public IEnumerable<ISegment> TransformSelectionBackgroundSegments(IEnumerable<SelectionSegment> segments)
        {
            if (segments == null)
                yield break;

            if (!SuppressTextSelectionBackgroundForRichContent || _items.Count == 0)
            {
                foreach (var segment in segments)
                    yield return segment;
                yield break;
            }

            RemoveInvalidItems();
            var items = GetItemsInDocumentOrder();
            foreach (var segment in segments)
            {
                var currentOffset = segment.StartOffset;
                foreach (var item in items)
                {
                    if (item.EndOffset <= currentOffset)
                        continue;
                    if (item.Offset >= segment.EndOffset)
                        break;

                    if (item.Offset > currentOffset)
                        yield return new SimpleSegment(currentOffset, item.Offset - currentOffset);

                    currentOffset = Math.Max(currentOffset, Math.Min(item.EndOffset, segment.EndOffset));
                    if (currentOffset >= segment.EndOffset)
                        break;
                }

                if (currentOffset < segment.EndOffset)
                    yield return new SimpleSegment(currentOffset, segment.EndOffset - currentOffset);
            }
        }

        private async Task<bool> InsertDataCoreAsync(IDataTransfer dataTransfer, int offset, bool replaceSelection)
        {
            var contents = new List<RichTextContent>();
            var richTextPayload = dataTransfer.TryGetValue(RichTextClipboardFormat);
            if (!string.IsNullOrEmpty(richTextPayload))
                return InsertSerializedSnapshot(richTextPayload, offset, replaceSelection);

            if (DataTransferImporter != null && CanImportDataTransfer?.Invoke(dataTransfer) == true)
            {
                var imported = await DataTransferImporter(dataTransfer);
                if (imported != null)
                    contents.AddRange(imported.Where(content => content != null));
            }

            if (contents.Count > 0)
                return InsertContents(offset, replaceSelection, contents);

            var files = dataTransfer.TryGetFiles();
            if (files != null && files.Any())
            {
                foreach (var file in files)
                {
                    var content = await CreateContentForFileAsync(file);
                    if (content != null)
                        contents.Add(content);
                }
            }
            else
            {
                var bitmap = dataTransfer.TryGetBitmap();
                if (bitmap != null)
                    contents.Add(RichTextContent.FromImage(bitmap));
            }

            return InsertContents(offset, replaceSelection, contents);
        }

        private async Task<bool> InsertDataCoreAsync(IAsyncDataTransfer dataTransfer, int offset, bool replaceSelection)
        {
            var contents = new List<RichTextContent>();
            var richTextPayload = await dataTransfer.TryGetValueAsync(RichTextClipboardFormat);
            if (!string.IsNullOrEmpty(richTextPayload))
                return InsertSerializedSnapshot(richTextPayload, offset, replaceSelection);

            if (AsyncDataTransferImporter != null && CanImportAsyncDataTransfer?.Invoke(dataTransfer) == true)
            {
                var imported = await AsyncDataTransferImporter(dataTransfer);
                if (imported != null)
                    contents.AddRange(imported.Where(content => content != null));
            }

            if (contents.Count > 0)
                return InsertContents(offset, replaceSelection, contents);

            var files = await dataTransfer.TryGetFilesAsync();
            if (files != null && files.Any())
            {
                foreach (var file in files)
                {
                    var content = await CreateContentForFileAsync(file);
                    if (content != null)
                        contents.Add(content);
                }
            }
            else
            {
                var bitmap = await dataTransfer.TryGetBitmapAsync();
                if (bitmap != null)
                    contents.Add(RichTextContent.FromImage(bitmap));
            }

            return InsertContents(offset, replaceSelection, contents);
        }

        public RichTextInputSnapshot CreateSnapshot(ISegment segment = null, bool removeObjectReplacementCharacters = false)
        {
            var document = _textArea.Document ?? throw ThrowUtil.NoDocumentAssigned();
            segment ??= new SimpleSegment(0, document.TextLength);
            var text = document.GetText(segment);
            var items = GetItemsInDocumentOrder()
                .Where(item => item.Offset >= segment.Offset && item.Offset < segment.EndOffset)
                .Select(item => new RichTextInputSnapshotItem
                {
                    Offset = item.Offset - segment.Offset,
                    Kind = item.Content.Kind,
                    DisplayText = item.Content.DisplayText,
                    Source = item.Content.Source,
                    StyleKey = item.Content.StyleKey
                })
                .ToArray();

            if (!removeObjectReplacementCharacters)
                return new RichTextInputSnapshot(text, items);

            var adjusted = text;
            foreach (var item in items.OrderByDescending(item => item.Offset))
            {
                if (item.Offset >= 0 && item.Offset < adjusted.Length && adjusted[item.Offset] == ObjectReplacementCharacter)
                    adjusted = adjusted.Remove(item.Offset, 1);
            }

            var removedBefore = 0;
            foreach (var item in items.OrderBy(item => item.Offset))
            {
                item.Offset -= removedBefore;
                removedBefore++;
            }

            return new RichTextInputSnapshot(adjusted, items);
        }

        public RichTextInputSnapshot GetSnapshot(bool removeObjectReplacementCharacters = false)
        {
            return CreateSnapshot(null, removeObjectReplacementCharacters);
        }

        public string SerializeSnapshot(ISegment segment = null)
        {
            return JsonSerializer.Serialize(CreateSnapshot(segment));
        }

        public string SerializeValue()
        {
            return SerializeSnapshot();
        }

        public RichTextInputValue GetValue(ISegment segment = null)
        {
            var document = _textArea.Document ?? throw ThrowUtil.NoDocumentAssigned();
            segment ??= new SimpleSegment(0, document.TextLength);
            var text = document.GetText(segment);
            var items = GetItemsInDocumentOrder()
                .Where(item => item.Offset >= segment.Offset && item.Offset < segment.EndOffset)
                .Select(item => new RichTextInputValueItem
                {
                    Offset = item.Offset - segment.Offset,
                    Content = item.Content
                })
                .ToArray();

            return new RichTextInputValue(text, items);
        }

        public RichTextInputValue GetSelectionValue()
        {
            var document = _textArea.Document ?? throw ThrowUtil.NoDocumentAssigned();
            if (_textArea.Selection.IsEmpty)
                return new RichTextInputValue();

            var text = new System.Text.StringBuilder();
            var items = new List<RichTextInputValueItem>();
            foreach (var segment in GetOrderedSelectionSegments())
            {
                var baseOffset = text.Length;
                var value = GetValue(new SimpleSegment(segment.StartOffset, segment.EndOffset - segment.StartOffset));
                text.Append(value.Text);
                foreach (var item in value.Items)
                {
                    items.Add(new RichTextInputValueItem
                    {
                        Offset = baseOffset + item.Offset,
                        Content = item.Content
                    });
                }
            }

            return new RichTextInputValue(text.ToString(), items);
        }

        public RichTextInputValue GetSelectedValue()
        {
            return GetSelectionValue();
        }

        public RichTextInputSnapshot GetSelectionSnapshot(bool removeObjectReplacementCharacters = false)
        {
            var value = GetSelectionValue();
            var snapshot = new RichTextInputSnapshot(
                value.Text,
                value.Items.Select(item => new RichTextInputSnapshotItem
                {
                    Offset = item.Offset,
                    Kind = item.Content.Kind,
                    DisplayText = item.Content.DisplayText,
                    Source = item.Content.Source,
                    StyleKey = item.Content.StyleKey
                }).ToArray());

            if (!removeObjectReplacementCharacters)
                return snapshot;

            var adjusted = snapshot.Text ?? string.Empty;
            var snapshotItems = snapshot.Items.ToArray();
            foreach (var item in snapshotItems.OrderByDescending(item => item.Offset))
            {
                if (item.Offset >= 0 && item.Offset < adjusted.Length && adjusted[item.Offset] == ObjectReplacementCharacter)
                    adjusted = adjusted.Remove(item.Offset, 1);
            }

            var removedBefore = 0;
            foreach (var item in snapshotItems.OrderBy(item => item.Offset))
            {
                item.Offset -= removedBefore;
                removedBefore++;
            }

            return new RichTextInputSnapshot(adjusted, snapshotItems);
        }

        public void SetValue(RichTextInputValue value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var document = _textArea.Document ?? throw ThrowUtil.NoDocumentAssigned();
            using (document.RunUpdate())
            {
                _items.Clear();
                document.Text = value.Text ?? string.Empty;
                foreach (var valueItem in value.Items.OrderBy(item => item.Offset))
                {
                    if (valueItem?.Content == null)
                        continue;

                    if (valueItem.Offset < 0 || valueItem.Offset >= document.TextLength)
                        continue;

                    if (document.GetCharAt(valueItem.Offset) == ObjectReplacementCharacter)
                        AddItem(valueItem.Offset, valueItem.Content);
                }

                _textArea.Caret.Offset = document.TextLength;
                _textArea.ClearSelection();
            }

            _textArea.TextView.Redraw();
        }

        public bool SetSnapshot(RichTextInputSnapshot snapshot)
        {
            if (snapshot == null)
                return false;

            var document = _textArea.Document ?? throw ThrowUtil.NoDocumentAssigned();
            using (document.RunUpdate())
            {
                _items.Clear();
                document.Text = snapshot.Text ?? string.Empty;
                foreach (var snapshotItem in (snapshot.Items ?? Array.Empty<RichTextInputSnapshotItem>()).OrderBy(item => item.Offset))
                {
                    if (snapshotItem.Offset < 0
                        || snapshotItem.Offset >= document.TextLength
                        || document.GetCharAt(snapshotItem.Offset) != ObjectReplacementCharacter)
                    {
                        continue;
                    }

                    AddItem(snapshotItem.Offset, CreateContentFromSnapshotItem(snapshotItem));
                }

                _textArea.Caret.Offset = document.TextLength;
                _textArea.ClearSelection();
            }

            _textArea.TextView.Redraw();
            FinalizeCaretAfterInsertion();
            return true;
        }

        public bool SetSerializedSnapshot(string payload)
        {
            RichTextInputSnapshot snapshot;
            try
            {
                snapshot = JsonSerializer.Deserialize<RichTextInputSnapshot>(payload);
            }
            catch
            {
                return false;
            }

            return SetSnapshot(snapshot);
        }

        public string GetPlainText(ISegment segment = null, Func<RichTextContentItem, string> contentTextFactory = null)
        {
            var document = _textArea.Document ?? throw ThrowUtil.NoDocumentAssigned();
            segment ??= new SimpleSegment(0, document.TextLength);
            var text = document.GetText(segment);
            var builder = new System.Text.StringBuilder(text);
            var items = GetItemsInDocumentOrder()
                .Where(item => item.Offset >= segment.Offset && item.Offset < segment.EndOffset)
                .OrderByDescending(item => item.Offset)
                .ToArray();

            foreach (var item in items)
            {
                var relativeOffset = item.Offset - segment.Offset;
                var replacement = contentTextFactory?.Invoke(item) ?? item.Content.DisplayText ?? string.Empty;
                if (relativeOffset >= 0 && relativeOffset < builder.Length && builder[relativeOffset] == ObjectReplacementCharacter)
                {
                    builder.Remove(relativeOffset, 1);
                    builder.Insert(relativeOffset, replacement);
                }
            }

            return builder.ToString();
        }

        public string GetSelectedPlainText(Func<RichTextContentItem, string> contentTextFactory = null)
        {
            var document = _textArea.Document ?? throw ThrowUtil.NoDocumentAssigned();
            if (_textArea.Selection.IsEmpty)
                return string.Empty;

            var builder = new System.Text.StringBuilder();
            foreach (var segment in GetOrderedSelectionSegments())
            {
                builder.Append(GetPlainText(
                    new SimpleSegment(segment.StartOffset, segment.EndOffset - segment.StartOffset),
                    contentTextFactory));
            }

            return builder.ToString();
        }

        public bool TrySetRichClipboardData(DataTransfer dataTransfer, ISegment segment)
        {
            if (dataTransfer == null)
                throw new ArgumentNullException(nameof(dataTransfer));
            if (segment == null)
                return false;

            var snapshot = CreateSnapshot(segment);
            if (snapshot.Items.Count == 0)
                return false;

            var item = new DataTransferItem();
            item.Set(RichTextClipboardFormat, JsonSerializer.Serialize(snapshot));
            dataTransfer.Add(item);
            return true;
        }

        private bool InsertSerializedSnapshot(string payload, int offset, bool replaceSelection)
        {
            RichTextInputSnapshot snapshot;
            try
            {
                snapshot = JsonSerializer.Deserialize<RichTextInputSnapshot>(payload);
            }
            catch
            {
                return false;
            }

            if (snapshot == null)
                return false;

            var document = _textArea.Document ?? throw ThrowUtil.NoDocumentAssigned();
            using (document.RunUpdate())
            {
                if (replaceSelection && !_textArea.Selection.IsEmpty)
                {
                    _textArea.RemoveSelectedText();
                    offset = _textArea.Caret.Offset;
                }

                document.Insert(offset, snapshot.Text ?? string.Empty, AnchorMovementType.BeforeInsertion);
                foreach (var snapshotItem in snapshot.Items.OrderBy(item => item.Offset))
                {
                    var itemOffset = offset + snapshotItem.Offset;
                    if (itemOffset >= 0
                        && itemOffset < document.TextLength
                        && document.GetCharAt(itemOffset) == ObjectReplacementCharacter)
                    {
                        var content = CreateContentFromSnapshotItem(snapshotItem);
                        var item = AddItem(itemOffset, content);
                        if (document.UndoStack.AcceptChanges)
                            document.UndoStack.Push(new RichTextContentUndoOperation(this, content, itemOffset, true, item));
                    }
                }

                _textArea.Caret.Offset = offset + (snapshot.Text?.Length ?? 0);
                _textArea.ClearSelection();
            }

            FinalizeCaretAfterInsertion();
            return true;
        }

        private static RichTextContent CreateContentFromSnapshotItem(RichTextInputSnapshotItem snapshotItem)
        {
            return new RichTextContent(snapshotItem.Kind, snapshotItem.DisplayText, null, snapshotItem.Source, snapshotItem.StyleKey);
        }

        private bool InsertContents(int offset, bool replaceSelection, IList<RichTextContent> contents)
        {
            if (contents == null || contents.Count == 0)
                return false;

            var document = _textArea.Document ?? throw ThrowUtil.NoDocumentAssigned();
            using (document.RunUpdate())
            {
                if (replaceSelection && !_textArea.Selection.IsEmpty)
                {
                    _textArea.RemoveSelectedText();
                    offset = _textArea.Caret.Offset;
                }

                foreach (var content in contents)
                {
                    InsertContent(offset, content);
                    offset += ObjectReplacementString.Length;
                }
            }

            _textArea.Caret.Offset = offset;
            _textArea.ClearSelection();
            FinalizeCaretAfterInsertion();
            return true;
        }

        private bool InsertText(int offset, bool replaceSelection, string text)
        {
            if (text == null)
                return false;

            var document = _textArea.Document ?? throw ThrowUtil.NoDocumentAssigned();
            using (document.RunUpdate())
            {
                if (replaceSelection && !_textArea.Selection.IsEmpty)
                {
                    _textArea.RemoveSelectedText();
                    offset = _textArea.Caret.Offset;
                }

                document.Insert(offset, text);
                _textArea.Caret.Offset = offset + text.Length;
                _textArea.ClearSelection();
            }

            FinalizeCaretAfterInsertion();
            return true;
        }

        private void FinalizeCaretAfterInsertion()
        {
            _textArea.Caret.ResetVisualColumn();
            _textArea.Focus();
            _textArea.Caret.BringCaretToView();
        }

        private async Task<RichTextContent> CreateContentForFileAsync(IStorageItem file)
        {
            if (file == null)
                return null;

            if (ConvertImageFilesToImages && file is IStorageFile storageFile && IsImageFile(file))
            {
                var bitmap = await TryLoadBitmapAsync(storageFile);
                if (bitmap != null)
                    return RichTextContent.FromImage(bitmap, file.Name, file.Path?.ToString());
            }

            return RichTextContent.FromFile(file);
        }

        private static bool IsImageFile(IStorageItem file)
        {
            var extension = Path.GetExtension(file.Name);
            return !string.IsNullOrEmpty(extension) && ImageExtensions.Contains(extension);
        }

        private static async Task<Bitmap> TryLoadBitmapAsync(IStorageFile file)
        {
            try
            {
                await using var stream = await file.OpenReadAsync();
                return new Bitmap(stream);
            }
            catch
            {
                return null;
            }
        }

        private void TextArea_DocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            DetachFromDocument(e.OldDocument);
            _items.Clear();
            AttachToDocument(e.NewDocument);
            _textArea.TextView.Redraw();
        }

        private void TextView_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_items.Count > 0)
                _textArea.TextView.Redraw();
        }

        private void TextArea_SelectionChanged(object sender, EventArgs e)
        {
            if (_items.Count > 0)
                ContentSelectionChanged?.Invoke(this, new RichTextContentChangedEventArgs(null));
        }

        private void TextArea_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Handled || e.Key != Key.Enter)
                return;

            if ((e.KeyModifiers & ~KeyModifiers.Shift) != KeyModifiers.None)
                return;

            e.Handled = HandleSelectedContentEnterKey();
        }

        internal bool HandleSelectedContentEnterKey()
        {
            if (SelectedContentEnterBehavior == RichTextSelectedContentEnterBehavior.KeepDefault
                || !TryGetSingleSelectedContent(out var item))
            {
                return false;
            }

            switch (SelectedContentEnterBehavior)
            {
                case RichTextSelectedContentEnterBehavior.MoveCaretAfterContent:
                    _textArea.ClearSelection();
                    _textArea.Caret.Offset = item.EndOffset;
                    FinalizeCaretAfterInsertion();
                    return true;
                case RichTextSelectedContentEnterBehavior.InsertNewLineBeforeContent:
                    _textArea.ClearSelection();
                    _textArea.Caret.Offset = item.Offset;
                    _textArea.PerformTextInput("\n");
                    FinalizeCaretAfterInsertion();
                    return true;
                case RichTextSelectedContentEnterBehavior.InsertNewLineAfterContent:
                    _textArea.ClearSelection();
                    _textArea.Caret.Offset = item.EndOffset;
                    _textArea.PerformTextInput("\n");
                    FinalizeCaretAfterInsertion();
                    return true;
                default:
                    return false;
            }
        }

        private bool TryGetSingleSelectedContent(out RichTextContentItem selectedItem)
        {
            selectedItem = null;
            if (_textArea.Selection.IsEmpty)
                return false;

            var segment = _textArea.Selection.SurroundingSegment;
            var items = GetSelectedItems();
            if (items.Count != 1)
                return false;

            var item = items[0];
            if (segment.Offset != item.Offset || segment.EndOffset != item.EndOffset)
                return false;

            selectedItem = item;
            return true;
        }

        private double GetConstrainedInlineWidth()
        {
            var textViewWidth = _textArea.TextView.Bounds.Width;
            if (double.IsNaN(textViewWidth) || double.IsInfinity(textViewWidth) || textViewWidth <= 0)
                return MaxInlineElementWidth;

            var available = Math.Max(MinInlineElementWidth, textViewWidth - 40);
            return Math.Min(MaxInlineElementWidth, available);
        }

        private void AttachToDocument(TextDocument document)
        {
            if (document != null)
            {
                TextDocumentWeakEventManager.Changing.AddHandler(document, Document_Changing);
                TextDocumentWeakEventManager.Changed.AddHandler(document, Document_Changed);
            }
        }

        private void DetachFromDocument(TextDocument document)
        {
            if (document != null)
            {
                TextDocumentWeakEventManager.Changing.RemoveHandler(document, Document_Changing);
                TextDocumentWeakEventManager.Changed.RemoveHandler(document, Document_Changed);
            }
        }

        private void Document_Changing(object sender, DocumentChangeEventArgs e)
        {
            if (e.RemovalLength == 0 || _items.Count == 0)
                return;

            var document = _textArea.Document;
            var removedItems = GetItemsInRange(e.Offset, e.Offset + e.RemovalLength);
            foreach (var item in removedItems)
            {
                RemoveItem(item);
                if (document?.UndoStack.AcceptChanges == true)
                    document.UndoStack.Push(new RichTextContentUndoOperation(this, item.Content, item.Offset, false, item));
            }
        }

        private void Document_Changed(object sender, DocumentChangeEventArgs e)
        {
            if (RemoveInvalidItems())
                _textArea.TextView.Redraw();
        }

        private bool RemoveInvalidItems()
        {
            var document = _textArea.Document;
            if (document == null || _items.Count == 0)
                return false;

            var removed = false;
            for (var i = _items.Count - 1; i >= 0; i--)
            {
                var item = _items[i];
                if (item.Length != ObjectReplacementString.Length
                    || item.Offset < 0
                    || item.Offset >= document.TextLength
                    || document.GetCharAt(item.Offset) != ObjectReplacementCharacter)
                {
                    _items.RemoveAt(i);
                    removed = true;
                }
            }

            return removed;
        }

        private IReadOnlyList<RichTextContentItem> GetItemsInRange(int startOffset, int endOffset)
        {
            return _items
                .Where(item => item.Offset >= startOffset && item.Offset < endOffset)
                .ToArray();
        }

        private IReadOnlyList<SelectionSegment> GetOrderedSelectionSegments()
        {
            return _textArea.Selection.Segments
                .OrderBy(segment => segment.StartOffset)
                .ToArray();
        }

        private RichTextContentItem AddItem(int offset, RichTextContent content)
        {
            var document = _textArea.Document ?? throw ThrowUtil.NoDocumentAssigned();
            var item = new RichTextContentItem(content, new AnchorSegment(document, offset, ObjectReplacementString.Length));
            _items.Add(item);
            ContentInserted?.Invoke(this, new RichTextContentChangedEventArgs(item));
            _textArea.TextView.Redraw();
            return item;
        }

        private bool CanRemove(RichTextContentItem item)
        {
            if (CanRemoveContent?.Invoke(item) == false)
                return false;

            var args = new RichTextContentRemovingEventArgs(item);
            ContentRemoving?.Invoke(this, args);
            return !args.Cancel;
        }

        private void RemoveItem(RichTextContentItem item)
        {
            if (item != null && _items.Remove(item))
            {
                ContentRemoved?.Invoke(this, new RichTextContentChangedEventArgs(item));
                _textArea.TextView.Redraw();
            }
        }

        public void SelectContent(RichTextContentItem item)
        {
            if (item == null || !_items.Contains(item) || _textArea.Document == null)
                return;

            _textArea.Focus();
            _textArea.Selection = Selection.Create(_textArea, item.Offset, item.Offset + item.Length);
            _textArea.Caret.Offset = item.Offset + item.Length;
        }

        public bool IsContentSelected(RichTextContentItem item)
        {
            if (item == null || _textArea.Selection.IsEmpty)
                return false;

            return _textArea.Selection.Segments.Any(segment =>
                item.Offset >= segment.StartOffset && item.Offset + item.Length <= segment.EndOffset);
        }

        internal RichTextContentPointerEventArgs RaiseContentPointerPressed(RichTextContentItem item, RoutedEventArgs routedEventArgs)
        {
            var args = new RichTextContentPointerEventArgs(this, item, routedEventArgs, RichTextContentPointerEventKind.PointerPressed);
            ContentPointerPressed?.Invoke(this, args);
            return args;
        }

        internal RichTextContentPointerEventArgs RaiseContentPointerReleased(RichTextContentItem item, RoutedEventArgs routedEventArgs)
        {
            var args = new RichTextContentPointerEventArgs(this, item, routedEventArgs, RichTextContentPointerEventKind.PointerReleased);
            ContentPointerReleased?.Invoke(this, args);
            return args;
        }

        internal RichTextContentPointerEventArgs RaiseContentDoubleTapped(RichTextContentItem item, RoutedEventArgs routedEventArgs)
        {
            var args = new RichTextContentPointerEventArgs(this, item, routedEventArgs, RichTextContentPointerEventKind.DoubleTapped);
            ContentDoubleTapped?.Invoke(this, args);
            return args;
        }

        internal RichTextContentPointerEventArgs RaiseContentContextRequested(RichTextContentItem item, RoutedEventArgs routedEventArgs)
        {
            var args = new RichTextContentPointerEventArgs(this, item, routedEventArgs, RichTextContentPointerEventKind.ContextRequested);
            ContentContextRequested?.Invoke(this, args);
            return args;
        }

        internal bool ShouldHandleContentPointerEvent(RichTextContentPointerEventArgs args)
        {
            return ContentPointerHandledSelector?.Invoke(args) ?? HandleContentPointerEvents;
        }

        internal void ApplyContentPointerSelection(RichTextContentPointerEventArgs args)
        {
            if (args == null || args.EventKind != RichTextContentPointerEventKind.PointerPressed)
                return;

            var behavior = ContentPointerSelectionBehaviorSelector?.Invoke(args)
                ?? (SelectContentOnPointerPressed
                    ? ContentPointerSelectionBehavior
                    : RichTextContentPointerSelectionBehavior.None);

            switch (behavior)
            {
                case RichTextContentPointerSelectionBehavior.SelectContent:
                    SelectContent(args.Item);
                    break;
                case RichTextContentPointerSelectionBehavior.PreserveSelection:
                    if (!IsContentSelected(args.Item))
                        SelectContent(args.Item);
                    break;
                case RichTextContentPointerSelectionBehavior.ExtendSelection:
                    ExtendSelectionToContent(args.Item);
                    break;
            }
        }

        private void ExtendSelectionToContent(RichTextContentItem item)
        {
            if (item == null || !_items.Contains(item) || _textArea.Document == null)
                return;

            var anchorOffset = _textArea.Selection.IsEmpty
                ? _textArea.Caret.Offset
                : _textArea.Selection.SurroundingSegment.Offset;
            _textArea.Focus();
            _textArea.Selection = Selection.Create(_textArea, anchorOffset, item.EndOffset);
            _textArea.Caret.Offset = item.EndOffset;
        }

        private sealed class RichTextContentUndoOperation : IUndoableOperation
        {
            private readonly RichTextInputManager _manager;
            private readonly RichTextContent _content;
            private readonly int _offset;
            private readonly bool _isInsertion;
            private RichTextContentItem _item;

            public RichTextContentUndoOperation(RichTextInputManager manager, RichTextContent content, int offset, bool isInsertion, RichTextContentItem item)
            {
                _manager = manager;
                _content = content;
                _offset = offset;
                _isInsertion = isInsertion;
                _item = item;
            }

            public void Undo()
            {
                if (_isInsertion)
                    _manager.RemoveItem(_item);
                else
                    _item = _manager.AddItem(_offset, _content);
            }

            public void Redo()
            {
                if (_isInsertion)
                    _item = _manager.AddItem(_offset, _content);
                else
                    _manager.RemoveItem(_item);
            }
        }

        public static Control CreateDefaultElement(RichTextContentItem item)
        {
            return CreateDefaultElement(item, 220, 180, 120);
        }

        private static Control CreateDefaultElement(RichTextContentItem item, double maxInlineWidth, double maxImageWidth, double maxImageHeight)
        {
            if (item.Content.Kind == RichTextContentKind.Image && item.Content.Value is Bitmap bitmap)
                return CreateImageElement(bitmap, item.Content.DisplayText, Math.Min(maxImageWidth, maxInlineWidth), maxImageHeight);

            if (item.Content.Kind == RichTextContentKind.Emoji)
                return CreateEmojiElement(item.Content.DisplayText);

            return CreateFileLikeElement(item.Content, maxInlineWidth);
        }

        private static Control CreateImageElement(Bitmap bitmap, string displayText, double maxWidth, double maxHeight)
        {
            var width = Math.Min(maxWidth, Math.Max(48, bitmap.Size.Width));
            var height = Math.Min(maxHeight, Math.Max(48, bitmap.Size.Height));
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(248, 250, 252)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(3),
                Margin = new Thickness(2, 1),
                Child = new Image
                {
                    Source = bitmap,
                    Width = width,
                    Height = height,
                    Stretch = Stretch.Uniform,
                    [ToolTip.TipProperty] = displayText
                }
            };
        }

        private static Control CreateEmojiElement(string emoji)
        {
            return new TextBlock
            {
                Text = emoji,
                FontSize = 18,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(1, 0)
            };
        }

        private static Control CreateFileLikeElement(RichTextContent content, double maxInlineWidth)
        {
            var icon = content.Kind == RichTextContentKind.File ? "FILE" : content.Kind.ToString().ToUpperInvariant();
            var title = string.IsNullOrWhiteSpace(content.DisplayText) ? content.Kind.ToString() : content.DisplayText;
            return new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(7, 4),
                Margin = new Thickness(2, 1),
                Child = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 6,
                    VerticalAlignment = VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock
                        {
                            Text = icon,
                            FontSize = 10,
                            FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85)),
                            VerticalAlignment = VerticalAlignment.Center
                        },
                        new TextBlock
                        {
                            Text = title,
                            FontSize = 12,
                            Foreground = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                            TextTrimming = TextTrimming.CharacterEllipsis,
                            MaxWidth = maxInlineWidth,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                }
            };
        }
    }

    internal sealed class RichTextInlineObjectGenerator : VisualLineElementGenerator
    {
        private readonly RichTextInputManager _manager;

        public RichTextInlineObjectGenerator(RichTextInputManager manager)
        {
            _manager = manager ?? throw new ArgumentNullException(nameof(manager));
        }

        public override int GetFirstInterestedOffset(int startOffset)
        {
            return _manager.GetFirstInterestedOffset(startOffset);
        }

        public override VisualLineElement ConstructElement(int offset)
        {
            return _manager.TryGetItem(offset, out var item)
                ? new InlineObjectElement(RichTextInputManager.ObjectReplacementString.Length, _manager.CreateElement(item), _manager.GetInlineObjectAlignment(item))
                : null;
        }
    }

    internal sealed class RichTextInlineContentControl : Border
    {
        private readonly RichTextInputManager _manager;
        private readonly RichTextContentItem _item;

        public RichTextInlineContentControl(RichTextInputManager manager, RichTextContentItem item, Control content)
        {
            _manager = manager;
            _item = item;
            Child = content;
            Focusable = false;
            Classes.Add("rich-text-inline-content");
            UpdateSelection();
            _manager.ContentSelectionChanged += Manager_ContentSelectionChanged;
            AddHandler(ContextRequestedEvent, OnContextRequested);
            DetachedFromVisualTree += OnDetachedFromVisualTree;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            var args = _manager.RaiseContentPointerPressed(_item, e);
            if (args.Handled)
            {
                e.Handled = true;
                return;
            }

            _manager.ApplyContentPointerSelection(args);

            e.Handled = _manager.ShouldHandleContentPointerEvent(args);
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            var args = _manager.RaiseContentPointerReleased(_item, e);
            e.Handled = args.Handled || _manager.ShouldHandleContentPointerEvent(args);
        }

        protected override void OnDoubleTapped(TappedEventArgs e)
        {
            base.OnDoubleTapped(e);
            var args = _manager.RaiseContentDoubleTapped(_item, e);
            e.Handled = args.Handled || _manager.ShouldHandleContentPointerEvent(args);
        }

        private void OnContextRequested(object sender, ContextRequestedEventArgs e)
        {
            var args = _manager.RaiseContentContextRequested(_item, e);
            e.Handled = args.Handled || _manager.ShouldHandleContentPointerEvent(args);
        }

        private void Manager_ContentSelectionChanged(object sender, RichTextContentChangedEventArgs e)
        {
            UpdateSelection();
        }

        private void OnDetachedFromVisualTree(object sender, VisualTreeAttachmentEventArgs e)
        {
            _manager.ContentSelectionChanged -= Manager_ContentSelectionChanged;
            RemoveHandler(ContextRequestedEvent, OnContextRequested);
            DetachedFromVisualTree -= OnDetachedFromVisualTree;
        }

        private void UpdateSelection()
        {
            var selected = _manager.IsContentSelected(_item);
            var style = _manager.GetInlineContentStyle(_item, selected);
            Background = style.Background;
            BorderBrush = style.BorderBrush;
            BorderThickness = style.BorderThickness;
            CornerRadius = style.CornerRadius;
            Padding = style.Padding;
            Classes.Set("selected", selected);
        }
    }
}
