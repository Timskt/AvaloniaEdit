# RichTextInput 使用说明

`RichTextInputManager` 给 `TextArea` 增加类似聊天输入框的富内容能力：文本、emoji、图片、文件卡片和业务自定义控件可以混排、复制粘贴、拖放、选中删除，并且保留普通文本降级能力。

## 最小接入

```csharp
using AvaloniaEdit.RichTextInput;

var richInput = RichTextInputManager.Install(editor.TextArea);
```

安装后会自动接管富内容拖放和粘贴。普通文本输入、中文 IME、撤销重做、普通复制粘贴仍走原有编辑器路径。

## 插入内容

```csharp
richInput.InsertEmoji("😀");
richInput.InsertImage(bitmap, "screenshot.png");
richInput.InsertFile(file);
richInput.InsertFileName("/tmp/report.pdf");
richInput.InsertCustom("order-card", order);
```

富内容在文档中用 `\uFFFC` 占位。业务数据保存在 `RichTextContentItem.Content` 中，可以通过 `GetItemsInDocumentOrder()` 按文档顺序读取。

## 自定义渲染

```csharp
richInput.ElementFactory = item =>
{
    if (item.Content.Kind == RichTextContentKind.File)
        return new MyFileChip(item.Content.DisplayText);

    if (item.Content.Kind == RichTextContentKind.Custom)
        return new MyBusinessCard(item.Content.Value);

    return RichTextInputManager.CreateDefaultElement(item);
};
```

`ElementFactory` 只负责返回业务控件。默认外层 wrapper 负责选中、事件、高亮和删除，所以自定义控件不用自己处理编辑器 selection。

## 对齐配置

默认是底部对齐，适合一行里同时存在文字、图片和卡片的聊天输入场景。

```csharp
// 控制文字在被高内容撑高后的行内位置。
richInput.LineContentAlignment = LineContentVerticalAlignment.Bottom;

// 控制所有富内容控件的 inline 对齐。
richInput.InlineObjectAlignment = InlineObjectVerticalAlignment.Bottom;

// 按 item 细分，比如图片底部、文件卡片居中。
richInput.InlineObjectAlignmentSelector = item =>
    item.Content.Kind == RichTextContentKind.Image
        ? InlineObjectVerticalAlignment.Bottom
        : InlineObjectVerticalAlignment.Center;
```

## 选择样式

默认选择态只描边，不铺大块蓝色背景。需要完全自定义时使用 `InlineContentStyleSelector`：

```csharp
richInput.InlineContentStyleSelector = (item, selected) => new RichTextInlineContentStyle
{
    Background = selected ? Brushes.Transparent : Brushes.Transparent,
    BorderBrush = selected ? Brushes.DodgerBlue : Brushes.Transparent,
    BorderThickness = new Thickness(selected ? 1 : 0),
    CornerRadius = new CornerRadius(6),
    Padding = new Thickness(0)
};
```

如果想让业务控件自己显示选中态，可以关闭默认高亮：

```csharp
richInput.HighlightSelectedContent = false;
```

## 交互事件

```csharp
richInput.ContentPointerPressed += (_, e) =>
{
    // 默认行为是单击选中。设置 Handled=true 可接管。
};

richInput.ContentDoubleTapped += (_, e) => OpenPreview(e.Item);
richInput.ContentContextRequested += (_, e) => ShowMenu(e.Item);
richInput.ContentRemoving += (_, e) =>
{
    if (IsUploading(e.Item))
        e.Cancel = true;
};
```

## 粘贴和拖放导入

Ava12 使用 `IDataTransfer/IAsyncDataTransfer`：

```csharp
richInput.CanImportAsyncDataTransfer = data => data.Contains(DataFormat.Text);
richInput.AsyncDataTransferImporter = async data =>
{
    var text = await data.TryGetTextAsync();
    return new[] { RichTextContent.FromCustom("custom-payload", text) };
};
```

Ava11 使用 `IDataObject`：

```csharp
richInput.CanImportDataObject = data => data.Contains(DataFormats.Text);
richInput.DataObjectImporter = data =>
{
    var text = data.Get(DataFormats.Text) as string;
    return Task.FromResult<IEnumerable<RichTextContent>>(
        new[] { RichTextContent.FromCustom("custom-payload", text) });
};
```

## 复制粘贴快照

复制时会同时写入普通文本和富内容快照。粘回支持 `RichTextInputManager` 的编辑器时会恢复富内容元数据；粘到普通输入框时仍是普通文本。

手动序列化：

```csharp
var snapshotJson = richInput.SerializeSnapshot(editor.TextArea.Selection.SurroundingSegment);
```

## 纯文本降级

发送消息、搜索索引或提交到不支持富内容的 API 时，可以把富内容替换成显示文本：

```csharp
var plain = richInput.GetPlainText(null, item =>
    item.Content.Kind == RichTextContentKind.Image
        ? "[图片]"
        : item.Content.DisplayText);
```

## 尺寸和 resize

```csharp
richInput.MinInlineElementWidth = 48;
richInput.MaxInlineElementWidth = 240;
richInput.MaxImageWidth = 190;
richInput.MaxImageHeight = 130;
```

`TextView` resize 后会重绘富内容，文件卡片和图片应使用 `MaxWidth`、`TextTrimming`、`Stretch.Uniform` 等响应式布局，避免输入框被用户拖大拖小时溢出。
