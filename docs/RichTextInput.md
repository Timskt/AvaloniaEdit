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

如果业务里有很多种组件样式，不建议在一个 `ElementFactory` 里写很长的 `if/else`。推荐给内容指定 `StyleKey`，然后按 key 注册工厂：

```csharp
richInput.RegisterElementFactory("order-card", context =>
    new OrderCardView
    {
        DataContext = context.Content.Value,
        MaxWidth = context.AvailableWidth
    });

richInput.RegisterElementFactory("mention-user", context =>
    new MentionUserChip((User)context.Content.Value));

richInput.InsertCustom(
    displayText: "订单 A001",
    value: order,
    styleKey: "order-card",
    metadata: new Dictionary<string, object>
    {
        ["status"] = "paid",
        ["compact"] = true
    });
```

需要全局接管时，用上下文工厂：

```csharp
richInput.ElementFactoryWithContext = context =>
{
    if (context.Metadata.TryGetValue("compact", out var compact) && compact is true)
        return new CompactCard(context.Content.Value);

    return null; // 返回 null 时继续走 StyleKey 注册工厂、ElementFactory、默认渲染。
};
```

`RichTextElementFactoryContext` 提供：

- `Manager`、`TextArea`
- `Item`、`Content`
- `StyleKey`、`Metadata`
- `AvailableWidth`
- `MaxImageWidth`、`MaxImageHeight`
- `IsSelected`

`RichTextContentItem.Tag` 可以放运行时状态，例如上传进度、临时错误信息或 UI 缓存对象。

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

## 中文 IME

默认使用 QQ 输入框式的 inline composition。输入法组合阶段的拼音、字母或候选前文本会作为临时 visual element 插入到光标位置，占住同一行布局，但不会写入 `TextDocument`、不会进入 undo stack，也不会参与复制/序列化。输入法确认后，提交文本仍走 AvaloniaEdit 原有 `TextInput` 路径。

```csharp
editor.TextArea.ImePreeditDisplayMode = ImePreeditDisplayMode.Inline; // 默认
```

如果业务确实需要旧的浮层绘制或完全隐藏 composition 显示，可以切换模式：

```csharp
editor.TextArea.ImePreeditDisplayMode = ImePreeditDisplayMode.Overlay;
editor.TextArea.ImePreeditDisplayMode = ImePreeditDisplayMode.Hidden;
```

## 选择样式

默认选择态只描边，不铺大块蓝色背景。`SuppressTextSelectionBackgroundForRichContent` 默认是 `true`，选中图片、文件、card 等富内容时会过滤掉对象占位符的普通文本 selection 背景，只保留富内容 wrapper 自己的选中样式。

需要恢复旧式整块文本 selection 背景时关闭它：

```csharp
richInput.SuppressTextSelectionBackgroundForRichContent = false;
```

需要完全自定义富内容选中态时使用 `InlineContentStyleSelector`：

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

## @ 人和指令弹窗

触发逻辑建议放在业务层：监听文本输入，检测当前 caret 前面的 `@query`，用 `GetCaretAnchorRect()` 把 Popup 放到光标附近；用户选中成员后，用 `ReplaceRangeWithContent` 替换掉 `@query`。

```csharp
var mentionStart = -1;

editor.TextArea.TextEntered += (_, e) =>
{
    var caret = editor.TextArea.Caret.Offset;
    var text = editor.Document.Text;
    mentionStart = FindMentionStart(text, caret);
    if (mentionStart < 0)
    {
        mentionPopup.IsOpen = false;
        return;
    }

    var query = text.Substring(mentionStart + 1, caret - mentionStart - 1);
    mentionList.ItemsSource = SearchMembers(query)
        .Prepend(Member.All); // @全体成员

    var anchor = richInput.GetCaretAnchorRect();
    mentionPopup.HorizontalOffset = anchor.X;
    mentionPopup.VerticalOffset = anchor.Bottom;
    mentionPopup.IsOpen = true;
};

void CommitMention(Member member)
{
    var caret = editor.TextArea.Caret.Offset;
    if (mentionStart < 0 || caret < mentionStart)
        return;

    richInput.ReplaceRangeWithContent(
        mentionStart,
        caret - mentionStart,
        RichTextContent.FromCustom("@" + member.DisplayName, member, "mention-user"));

    mentionPopup.IsOpen = false;
    mentionStart = -1;
}
```

## 粘贴和拖放导入

Ava12 使用 `IDataTransfer/IAsyncDataTransfer`：

```csharp
// 通用导入：粘贴和拖放都会走这里。
richInput.CanImportAsyncDataTransfer = data => data.Contains(DataFormat.Text);
richInput.AsyncDataTransferImporter = async data =>
{
    var text = await data.TryGetTextAsync();
    return new[] { RichTextContent.FromCustom("custom-payload", text) };
};

// 只接管 Ctrl+V/粘贴：可以按数据类型决定插入文本、图片、文件或业务组件。
richInput.PasteHandler = async context =>
{
    if (context.DataTransfer.Contains(DataFormat.Bitmap))
        return; // UseDefault：不设置 Handled 时继续走默认图片粘贴。

    var text = await context.DataTransfer.TryGetTextAsync();
    if (text?.StartsWith("order:", StringComparison.OrdinalIgnoreCase) == true)
    {
        context.InsertContents(new[]
        {
            RichTextContent.FromCustom(text, new OrderPayload(text), "order-card")
        });
        return;
    }

    context.InsertText(text);
};

// 只接管拖放：适合按不同文件类型生成不同业务组件。
richInput.DropHandler = async context =>
{
    var files = context.DataTransfer.TryGetFiles();
    if (files == null)
        return;

    var contents = new List<RichTextContent>();
    foreach (var file in files)
    {
        if (file.Name.EndsWith(".fig", StringComparison.OrdinalIgnoreCase))
            contents.Add(RichTextContent.FromCustom(file.Name, file, "design-file"));
        else if (file.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            contents.Add(RichTextContent.FromCustom(file.Name, file, "package-file"));
        else
            contents.Add(RichTextContent.FromFile(file));
    }

    context.InsertContents(contents);
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

richInput.PasteHandler = context =>
{
    var text = context.DataObject.Get(DataFormats.Text) as string;
    if (text?.StartsWith("order:", StringComparison.OrdinalIgnoreCase) == true)
    {
        context.InsertContents(new[]
        {
            RichTextContent.FromCustom(text, new OrderPayload(text), "order-card")
        });
    }

    return Task.CompletedTask;
};

richInput.DropHandler = context =>
{
    var fileNames = context.DataObject.GetFileNames();
    context.InsertContents(fileNames.Select(fileName =>
        fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)
            ? RichTextContent.FromCustom(Path.GetFileName(fileName), fileName, "package-file")
            : RichTextContent.FromFileName(fileName)));
    return Task.CompletedTask;
};
```

## 复制粘贴快照

复制时会同时写入普通文本和富内容快照。粘回支持 `RichTextInputManager` 的编辑器时会恢复富内容元数据；粘到普通输入框时仍是普通文本。

同应用内从一个富输入框赋值给另一个富输入框或会话展示框，优先使用 live value。它会保留 Bitmap、自定义 `Value`、`Metadata` 和 `StyleKey`，目标控件会按自己的最大宽度和渲染工厂重新布局：

```csharp
var value = inputRich.GetValue();
previewRich.SetValue(value);
```

手动序列化：

```csharp
var snapshotJson = richInput.SerializeSnapshot(editor.TextArea.Selection.SurroundingSegment);
otherRich.SetSerializedSnapshot(snapshotJson);
```

`SerializeSnapshot` 适合存储或跨进程传递；如果要完整保留内存对象、Bitmap 或业务对象引用，用 `GetValue/SetValue`。

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
