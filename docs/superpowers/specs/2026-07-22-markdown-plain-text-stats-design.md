# Markdown 纯文本统计设计

## 目标

修复主窗口正文统计直接计算 Markdown 源码的问题，使 Markdown 和所见即所得模式始终显示相同的纯文本行数与字数。

## 当前问题

`MarkdownBodyStatsDisplayConverter` 将便签正文交给 `NoteNavigationHelper.FormatBodyStats`。该方法直接遍历 Markdown 源字符串，因此 `#`、`**`、链接地址、图片语法等格式字符会被计入字数，Markdown 结构所需的空行也会影响行数。

## 统计规则

- 先使用完整 Markdown 管线将源码转换为纯文本，再统计纯文本。
- Markdown 语法标记、链接目标地址、图片目标地址不计入字数。
- 用户可见的标题、段落、链接文字、列表内容、引用内容、行内代码和代码块内容计入字数。
- Toast UI 为连续空段落生成的独立 `<br>` 标签保留逻辑行数，但标签本身不计入字数；代码中的字面 `<br>` 仍作为可见文字计数。
- 纯文本中的换行不计入字数；规范化为 LF 后按逻辑行统计。
- 空内容显示 `0 行 0 字`。
- Markdown 与所见即所得模式使用同一份便签 Markdown，因此统计结果不得因模式切换而变化。
- 快捷便签是普通文本编辑器，继续使用现有普通文本统计规则。

## 技术方案

在应用项目中引入 Markdig，并使用 `MarkdownPipelineBuilder.UseAdvancedExtensions()` 构建共享解析管线。`NoteNavigationHelper.FormatBodyStats` 使用与 `Markdown.ToPlainText` 相同的 HTML 文本渲染流程，并替换 HTML 块与内联渲染器：独立 `<br>` 输出零宽行占位符，内联 `<br>` 输出换行。随后去除解析器产生的尾部换行并计算行数和字数。

正则剥离不采用，因为无法可靠处理嵌套格式、转义、链接、图片、表格和代码。WebView 回传 DOM 文本也不采用，因为会让统计依赖异步编辑器生命周期，重新引入状态不同步风险。

## 验证

- 单元测试覆盖标题和强调、链接、图片、列表、引用、代码以及空内容。
- 运行完整 Release 测试和 Release 构建。
- 确认工作树干净且 `develop` 指针未变化。
