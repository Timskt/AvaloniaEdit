using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Headless.NUnit;
using Avalonia.Input;
using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;
using AvaloniaEdit.RichTextInput;
using NUnit.Framework;

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
            var dataObject = new DataObject();
            source.TrySetRichClipboardData(dataObject, new SimpleSegment(0, sourceTextArea.Document.TextLength));

            var targetTextArea = CreateTextArea("");
            var target = RichTextInputManager.Install(targetTextArea);
            var inserted = await target.InsertDataAsync(dataObject, 0, false);

            Assert.IsTrue(inserted);
            Assert.AreEqual("hi " + RichTextInputManager.ObjectReplacementString + " ok", targetTextArea.Document.Text);
            Assert.AreEqual(1, target.Items.Count);
            Assert.AreEqual("report.pdf", target.Items[0].Content.DisplayText);
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

        [AvaloniaTest]
        public void CustomElementFactoryIsWrappedForSelection()
        {
            var textArea = CreateTextArea("");
            var manager = RichTextInputManager.Install(textArea);
            manager.ElementFactory = item => new Button { Content = item.Content.DisplayText };
            manager.InsertCustom("styled.file", new object());

            Assert.IsTrue(manager.TryGetItem(0, out var item));
            var control = manager.CreateElement(item);

            Assert.IsInstanceOf<Border>(control);
            Assert.IsInstanceOf<Button>(((Border)control).Child);
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
