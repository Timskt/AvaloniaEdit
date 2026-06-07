using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.RichTextInput;
using NUnit.Framework;
using Assert = NUnit.Framework.Legacy.ClassicAssert;

namespace AvaloniaEdit.Tests.RichTextInput
{
    [TestFixture]
    public class RichTextInputManagerTests
    {
        [AvaloniaTest]
        public void InsertContentAddsObjectReplacementCharacter()
        {
            var textArea = CreateTextArea("hello");
            var manager = RichTextInputManager.Install(textArea);
            textArea.Caret.Offset = 5;

            manager.InsertContent(RichTextContent.FromCustom("demo.bin", new object()));

            Assert.AreEqual("hello" + RichTextInputManager.ObjectReplacementString, textArea.Document.Text);
            Assert.AreEqual(1, manager.Items.Count);
            Assert.AreEqual(5, manager.Items[0].Offset);
            Assert.AreEqual("demo.bin", manager.Items[0].Content.DisplayText);
        }

        [AvaloniaTest]
        public void RemovingObjectReplacementCharacterRemovesRichContentItem()
        {
            var textArea = CreateTextArea("ab");
            var manager = RichTextInputManager.Install(textArea);
            var item = manager.InsertContent(1, RichTextContent.FromCustom("demo.bin", new object()));

            textArea.Document.Remove(item.Offset, item.Length);

            Assert.AreEqual("ab", textArea.Document.Text);
            Assert.AreEqual(0, manager.Items.Count);
        }

        [AvaloniaTest]
        public void UndoRedoRestoresInsertedRichContentMetadata()
        {
            var textArea = CreateTextArea("ab");
            var manager = RichTextInputManager.Install(textArea);

            manager.InsertContent(1, RichTextContent.FromCustom("demo.bin", new object()));
            textArea.Document.UndoStack.Undo();

            Assert.AreEqual("ab", textArea.Document.Text);
            Assert.AreEqual(0, manager.Items.Count);

            textArea.Document.UndoStack.Redo();

            Assert.AreEqual("a" + RichTextInputManager.ObjectReplacementString + "b", textArea.Document.Text);
            Assert.AreEqual(1, manager.Items.Count);
            Assert.AreEqual("demo.bin", manager.Items[0].Content.DisplayText);
        }

        [AvaloniaTest]
        public void UndoRedoRestoresDeletedRichContentMetadata()
        {
            var textArea = CreateTextArea("ab");
            var manager = RichTextInputManager.Install(textArea);
            var item = manager.InsertContent(1, RichTextContent.FromCustom("demo.bin", new object()));

            textArea.Document.Remove(item.Offset, item.Length);
            textArea.Document.UndoStack.Undo();

            Assert.AreEqual("a" + RichTextInputManager.ObjectReplacementString + "b", textArea.Document.Text);
            Assert.AreEqual(1, manager.Items.Count);
            Assert.AreEqual("demo.bin", manager.Items[0].Content.DisplayText);

            textArea.Document.UndoStack.Redo();

            Assert.AreEqual("ab", textArea.Document.Text);
            Assert.AreEqual(0, manager.Items.Count);
        }

        [AvaloniaTest]
        public void RemoveContentRemovesPlaceholderAndItem()
        {
            var textArea = CreateTextArea("ab");
            var manager = RichTextInputManager.Install(textArea);
            var item = manager.InsertCustom("demo.bin", new object());

            var removed = manager.RemoveContent(item);

            Assert.IsTrue(removed);
            Assert.AreEqual("ab", textArea.Document.Text);
            Assert.AreEqual(0, manager.Items.Count);
        }

        [AvaloniaTest]
        public void CustomElementFactoryIsUsedForInlineElement()
        {
            var textArea = CreateTextArea("");
            var manager = RichTextInputManager.Install(textArea);
            manager.ElementFactory = item => new Button { Content = item.Content.DisplayText };
            manager.InsertContent(RichTextContent.FromCustom("styled.file", new object()));

            Assert.IsTrue(manager.TryGetItem(0, out var item));
            var control = manager.CreateElement(item);

            Assert.IsInstanceOf<Border>(control);
            Assert.IsInstanceOf<Button>(((Border)control).Child);
            Assert.AreEqual("styled.file", ((Button)((Border)control).Child).Content);
        }

        [AvaloniaTest]
        public async Task InsertDataAsyncAddsBitmapContentFromDataTransfer()
        {
            var textArea = CreateTextArea("ab");
            var manager = RichTextInputManager.Install(textArea);
            var dataTransfer = new DataTransfer();
            var item = new DataTransferItem();
            item.SetBitmap(new WriteableBitmap(new PixelSize(1, 1), new Vector(96, 96), null, null));
            dataTransfer.Add(item);

            var inserted = await manager.InsertDataAsync((IDataTransfer)dataTransfer, 1, false);

            Assert.IsTrue(inserted);
            Assert.AreEqual("a" + RichTextInputManager.ObjectReplacementString + "b", textArea.Document.Text);
            Assert.AreEqual(1, manager.Items.Count);
            Assert.AreEqual(RichTextContentKind.Image, manager.Items[0].Content.Kind);
        }

        [AvaloniaTest]
        public async Task InsertDataAsyncUsesCustomDataTransferImporter()
        {
            var textArea = CreateTextArea("ab");
            var manager = RichTextInputManager.Install(textArea);
            manager.CanImportDataTransfer = data => data.Contains(DataFormat.Text);
            manager.DataTransferImporter = data => Task.FromResult<IEnumerable<RichTextContent>>(new[]
            {
                RichTextContent.FromCustom("business-card", data.TryGetText())
            });
            var dataTransfer = new DataTransfer();
            dataTransfer.Add(DataTransferItem.CreateText("42"));

            var inserted = await manager.InsertDataAsync((IDataTransfer)dataTransfer, 1, false);

            Assert.IsTrue(inserted);
            Assert.AreEqual("a" + RichTextInputManager.ObjectReplacementString + "b", textArea.Document.Text);
            Assert.AreEqual(1, manager.Items.Count);
            Assert.AreEqual(RichTextContentKind.Custom, manager.Items[0].Content.Kind);
            Assert.AreEqual("business-card", manager.Items[0].Content.DisplayText);
        }

        [AvaloniaTest]
        public void SelectContentThenRemoveSelectedTextDeletesInlineContent()
        {
            var textArea = CreateTextArea("ab");
            var manager = RichTextInputManager.Install(textArea);
            var item = manager.InsertContent(1, RichTextContent.FromCustom("chip", 42));

            manager.SelectContent(item);
            textArea.RemoveSelectedText();

            Assert.AreEqual("ab", textArea.Document.Text);
            Assert.AreEqual(0, manager.Items.Count);
        }

        [AvaloniaTest]
        public void RemovingTextBetweenInlineContentKeepsDocumentOrder()
        {
            var textArea = CreateTextArea("ab");
            var manager = RichTextInputManager.Install(textArea);
            manager.InsertContent(0, RichTextContent.FromCustom("first", 1));
            manager.InsertContent(textArea.Document.TextLength, RichTextContent.FromCustom("second", 2));

            textArea.Document.Remove(1, 1);

            Assert.AreEqual(RichTextInputManager.ObjectReplacementString + "b" + RichTextInputManager.ObjectReplacementString, textArea.Document.Text);
            var items = manager.GetItemsInDocumentOrder();
            Assert.AreEqual("first", items[0].Content.DisplayText);
            Assert.AreEqual(0, items[0].Offset);
            Assert.AreEqual("second", items[1].Content.DisplayText);
            Assert.AreEqual(2, items[1].Offset);
        }

        [AvaloniaTest]
        public async Task InsertDataAsyncRestoresSerializedRichTextSnapshot()
        {
            var sourceTextArea = CreateTextArea("hi ");
            var source = RichTextInputManager.Install(sourceTextArea);
            source.InsertContent(sourceTextArea.Document.TextLength, new RichTextContent(RichTextContentKind.File, "report.pdf", null, "/tmp/report.pdf"));
            sourceTextArea.Document.Insert(sourceTextArea.Document.TextLength, " ok");
            var payload = source.SerializeSnapshot();
            var dataTransfer = new DataTransfer();
            var item = new DataTransferItem();
            item.Set(RichTextInputManager.RichTextClipboardFormat, payload);
            dataTransfer.Add(item);

            var targetTextArea = CreateTextArea("");
            var target = RichTextInputManager.Install(targetTextArea);
            var inserted = await target.InsertDataAsync((IDataTransfer)dataTransfer, 0, false);

            Assert.IsTrue(inserted);
            Assert.AreEqual("hi " + RichTextInputManager.ObjectReplacementString + " ok", targetTextArea.Document.Text);
            Assert.AreEqual(1, target.Items.Count);
            Assert.AreEqual(RichTextContentKind.File, target.Items[0].Content.Kind);
            Assert.AreEqual("report.pdf", target.Items[0].Content.DisplayText);
            Assert.AreEqual("/tmp/report.pdf", target.Items[0].Content.Source);
        }

        [AvaloniaTest]
        public void GetPlainTextReplacesInlineContentWithDisplayText()
        {
            var textArea = CreateTextArea("send ");
            var manager = RichTextInputManager.Install(textArea);
            manager.InsertContent(textArea.Document.TextLength, RichTextContent.FromCustom("card", 1));
            textArea.Document.Insert(textArea.Document.TextLength, " now");

            Assert.AreEqual("send card now", manager.GetPlainText());
            Assert.AreEqual("send [Custom:card] now", manager.GetPlainText(null, item => $"[{item.Content.Kind}:{item.Content.DisplayText}]"));
        }

        [AvaloniaTest]
        public void ContentRemovingCanCancelExplicitRemoval()
        {
            var textArea = CreateTextArea("");
            var manager = RichTextInputManager.Install(textArea);
            var item = manager.InsertCustom("locked", 1);
            manager.ContentRemoving += (sender, args) => args.Cancel = true;

            var removed = manager.RemoveContent(item);

            Assert.IsFalse(removed);
            Assert.AreEqual(RichTextInputManager.ObjectReplacementString, textArea.Document.Text);
            Assert.AreEqual(1, manager.Items.Count);
        }

        [AvaloniaTest]
        public void RichTextInputDoesNotBlockChineseTextInput()
        {
            var textArea = CreateTextArea("");
            RichTextInputManager.Install(textArea);

            textArea.PerformTextInput("中文输入");

            Assert.AreEqual("中文输入", textArea.Document.Text);
        }

        [AvaloniaTest]
        public void CaretHeightUsesTextMetricsWhenInlineContentMakesLineTall()
        {
            var textArea = CreateTextArea("ab");
            var manager = RichTextInputManager.Install(textArea);
            manager.ElementFactory = item => new Border
            {
                Width = 24,
                Height = 100
            };
            manager.InsertContent(1, RichTextContent.FromCustom("tall", 1));
            textArea.Caret.Offset = 0;

            var caretRectangle = textArea.Caret.CalculateCaretRectangle();

            Assert.Less(caretRectangle.Height, 40);
        }

        private static TextArea CreateTextArea(string text)
        {
            return new TextArea
            {
                Document = new TextDocument(text)
            };
        }

    }
}
