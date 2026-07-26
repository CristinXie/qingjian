using System.Xml.Linq;
using Xunit;

namespace QingJian.App.Tests.Views;

public sealed class MainWindowXamlTests
{
    [Fact]
    public void Sidebar_ContainsDynamicTodoWidgetToggleButton()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());

        var toggleButton = FindNamedElement(xaml, "Button", "TodoWidgetToggleButton");

        Assert.Equal("ToggleTodoWidgetButton_OnClick", (string?)toggleButton.Attribute("Click"));
        Assert.Equal("{StaticResource SecondaryButtonStyle}", (string?)toggleButton.Attribute("Style"));
        Assert.NotEqual("显示/隐藏桌面待办", (string?)toggleButton.Attribute("Content"));
    }

    [Fact]
    public void SettingsButton_RaisesSettingsRequest()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());

        var settingsButton = FindNamedElement(xaml, "Button", "SettingsButton");

        Assert.Equal("SettingsButton_OnClick", (string?)settingsButton.Attribute("Click"));
        Assert.Equal("设置", (string?)settingsButton.Attribute("ToolTip"));
    }

    [Fact]
    public void FolderButton_RaisesFolderManagementRequest()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var folderButton = FindNamedElement(xaml, "Button", "FolderButton");

        Assert.Equal("FolderButton_OnClick", (string?)folderButton.Attribute("Click"));
        Assert.Equal("文件夹", (string?)folderButton.Attribute("ToolTip"));
    }

    [Fact]
    public void Sidebar_ContainsActiveFolderFilterAndSeparateEmptyState()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var filterPanel = FindNamedElement(xaml, "StackPanel", "ActiveFolderFilterPanel");
        var clearButton = FindNamedElement(xaml, "Button", "ClearFolderFilterButton");
        var emptyText = FindNamedElement(xaml, "TextBlock", "FolderEmptyTextBlock");

        Assert.Contains(filterPanel.Descendants(), element =>
            ((string?)element.Attribute("Text"))?.Contains("CurrentFolderDisplayName", StringComparison.Ordinal) == true);
        Assert.Equal("显示全部便签", (string?)clearButton.Attribute("ToolTip"));
        Assert.Equal("没有便签", (string?)emptyText.Attribute("Text"));
        Assert.Contains(emptyText.Descendants(), element =>
            element.Name.LocalName == "DataTrigger"
            && (string?)element.Attribute("Binding") == "{Binding ShowFolderEmptyState}"
            && (string?)element.Attribute("Value") == "True");
    }

    [Fact]
    public void Sidebar_SearchAndSortControlsShareTheRowBelowTodoAction()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var searchBox = FindNamedElement(xaml, "TextBox", "NoteSearchTextBox");
        var sortButton = FindNamedElement(xaml, "Button", "NavigationSortButton");

        Assert.Same(searchBox.Parent, sortButton.Parent);
        Assert.Equal("{Binding SearchText, UpdateSourceTrigger=PropertyChanged}", (string?)searchBox.Attribute("Text"));
        Assert.Equal("{Binding ToggleNavigationSortCommand}", (string?)sortButton.Attribute("Command"));
        Assert.Equal("32", (string?)sortButton.Attribute("Width"));
        Assert.Contains(sortButton.Descendants(), element =>
            element.Name.LocalName == "DataTrigger"
            && (string?)element.Attribute("Value") == "Favorite");
        Assert.Contains(sortButton.Descendants(), element =>
            element.Name.LocalName == "Setter"
            && (string?)element.Attribute("Value") == "按时间");
        Assert.Contains(sortButton.Descendants(), element =>
            element.Name.LocalName == "Setter"
            && (string?)element.Attribute("Value") == "按收藏");

        var style = sortButton.Descendants().Single(element =>
            element.Name.LocalName == "Style"
            && (string?)element.Attribute("TargetType") == "Button");
        var defaultGlyph = style.Elements().Single(element =>
            element.Name.LocalName == "Setter"
            && (string?)element.Attribute("Property") == "Content");
        var favoriteTrigger = style.Descendants().Single(element =>
            element.Name.LocalName == "DataTrigger"
            && (string?)element.Attribute("Value") == "Favorite");
        var favoriteGlyph = favoriteTrigger.Elements().Single(element =>
            element.Name.LocalName == "Setter"
            && (string?)element.Attribute("Property") == "Content");

        Assert.Equal("\uE823", (string?)defaultGlyph.Attribute("Value"));
        Assert.Equal("\uE734", (string?)favoriteGlyph.Attribute("Value"));
    }

    [Fact]
    public void NoteList_UsesThreeRowPreviewWithFavoriteTimeAndFolder()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var favoriteButton = FindNamedElement(xaml, "Button", "FavoriteButton");

        Assert.Equal("FavoriteButton_OnPreviewMouseLeftButtonDown", (string?)favoriteButton.Attribute("PreviewMouseLeftButtonDown"));
        Assert.Equal("0", (string?)FindAttributeByLocalName(favoriteButton, "ToolTipService.InitialShowDelay"));
        Assert.Contains(favoriteButton.Descendants(), element =>
            element.Name.LocalName == "DataTrigger"
            && (string?)element.Attribute("Binding") == "{Binding IsFavorite}"
            && (string?)element.Attribute("Value") == "True");
        Assert.Contains(favoriteButton.Descendants(), element =>
            element.Name.LocalName == "Setter"
            && (string?)element.Attribute("Value") == "#F5C542");
        Assert.Contains(xaml.Descendants(), element =>
            element.Name.LocalName == "TextBlock"
            && ((string?)element.Attribute("Text"))?.Contains("NoteUpdatedAtDisplayConverter", StringComparison.Ordinal) == true);
        Assert.Contains(xaml.Descendants(), element =>
            element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "{Binding FolderName}");

        var folderButton = FindNamedElement(xaml, "Button", "FolderAssignmentButton");
        Assert.Equal("FolderAssignmentButton_OnClick", (string?)folderButton.Attribute("Click"));
        Assert.Equal("移动到文件夹", (string?)folderButton.Attribute("ToolTip"));
        Assert.Equal("0", (string?)FindAttributeByLocalName(folderButton, "ToolTipService.InitialShowDelay"));
    }

    [Fact]
    public void NoteList_UsesBalancedThreeRowPreviewSpacing()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var styles = XDocument.Load(FindStylesXamlPath());
        var title = xaml.Descendants().Single(element =>
            element.Name.LocalName == "TextBlock"
            && (string?)element.Attribute("Text") == "{Binding Title}");
        var previewGrid = title.Parent ?? throw new InvalidOperationException("Note preview grid is missing.");
        var rowHeights = previewGrid.Elements()
            .Single(element => element.Name.LocalName == "Grid.RowDefinitions")
            .Elements()
            .Select(element => (string?)element.Attribute("Height"))
            .ToArray();
        var favoriteButton = FindNamedElement(xaml, "Button", "FavoriteButton");
        var updatedTime = xaml.Descendants().Single(element =>
            element.Name.LocalName == "TextBlock"
            && ((string?)element.Attribute("Text"))?.Contains("NoteUpdatedAtDisplayConverter", StringComparison.Ordinal) == true);
        var folderRow = xaml.Descendants().Single(element =>
            element.Name.LocalName == "Button"
            && (string?)FindAttributeByLocalName(element, "Name") == "FolderAssignmentButton"
            && element.Descendants().Any(child =>
                child.Name.LocalName == "TextBlock"
                && (string?)child.Attribute("Text") == "{Binding FolderName}"));
        var noteItemStyle = styles.Descendants().Single(element =>
            element.Name.LocalName == "Style"
            && (string?)FindAttributeByLocalName(element, "Key") == "NoteListBoxItemStyle");
        var paddingSetter = noteItemStyle.Elements().Single(element =>
            element.Name.LocalName == "Setter"
            && (string?)element.Attribute("Property") == "Padding");

        Assert.Equal(new[] { "24", "24", "24" }, rowHeights);
        Assert.Equal("12,8", (string?)paddingSetter.Attribute("Value"));
        Assert.Equal("24", (string?)favoriteButton.Attribute("Width"));
        Assert.Equal("24", (string?)favoriteButton.Attribute("Height"));
        Assert.Null(updatedTime.Attribute("Margin"));
        Assert.Equal("Center", (string?)updatedTime.Attribute("VerticalAlignment"));
        Assert.Equal("Transparent", (string?)folderRow.Attribute("BorderBrush"));
        Assert.Equal("Center", (string?)folderRow.Attribute("VerticalContentAlignment"));
    }

    [Fact]
    public void NoteList_PreservesEditorSelectionAndShowsSearchEmptyState()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var listBox = xaml.Descendants().Single(element => element.Name.LocalName == "ListBox");
        var emptyText = FindNamedElement(xaml, "TextBlock", "NoSearchResultsTextBlock");

        Assert.Contains(listBox.Descendants(), element =>
            element.Name.LocalName == "Setter"
            && (string?)element.Attribute("Property") == "SelectedItem"
            && (string?)element.Attribute("Value") == "{Binding SelectedNote, Mode=OneWay}");
        Assert.Equal("NoteListBox_OnSelectionChanged", (string?)listBox.Attribute("SelectionChanged"));
        Assert.Equal("没有匹配的便签", (string?)emptyText.Attribute("Text"));
        Assert.Contains(emptyText.Descendants(), element =>
            element.Name.LocalName == "DataTrigger"
            && (string?)element.Attribute("Binding") == "{Binding ShowNoSearchResults}"
            && (string?)element.Attribute("Value") == "True");
    }

    [Fact]
    public void Sidebar_FooterWiresSettingsAndKeepsOtherPlaceholderButtonsInOrder()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var expected = new[] { "SettingsButton", "FolderButton", "BatchManagementButton" };
        var buttons = xaml.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .Where(element => expected.Contains((string?)FindAttributeByLocalName(element, "Name")))
            .ToArray();

        Assert.Equal(expected, buttons.Select(button => (string?)FindAttributeByLocalName(button, "Name")));
        Assert.Equal("SettingsButton_OnClick", (string?)buttons[0].Attribute("Click"));
        Assert.Equal("FolderButton_OnClick", (string?)buttons[1].Attribute("Click"));
        Assert.Null(buttons[2].Attribute("Command"));
        Assert.Null(buttons[2].Attribute("Click"));
        Assert.All(buttons, button =>
        {
            Assert.Equal("{StaticResource IconButtonStyle}", (string?)button.Attribute("Style"));
            Assert.Equal("0", (string?)FindAttributeByLocalName(button, "ToolTipService.InitialShowDelay"));
        });
    }

    [Fact]
    public void NoteList_ShowsUpdatedTimeAsSecondaryMetadata()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());

        Assert.Contains(
            xaml.Descendants(),
            element => element.Name.LocalName == "TextBlock"
                && ((string?)element.Attribute("Text"))?.Contains("UpdatedAt", StringComparison.Ordinal) == true
                && (string?)element.Attribute("Foreground") == "{StaticResource MutedTextBrush}");
    }

    [Fact]
    public void NoteList_UsesDedicatedItemContainerStyleForSelectedAndHoverStates()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var listBox = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "ListBox"
                && ((string?)element.Attribute("ItemsSource"))?.Contains("Notes", StringComparison.Ordinal) == true);
        var styles = XDocument.Load(FindStylesXamlPath());

        Assert.Equal("{StaticResource NoteListBoxItemStyle}", (string?)listBox.Attribute("ItemContainerStyle"));
        Assert.Contains(
            styles.Descendants(),
            element => element.Name.LocalName == "Style"
                && (string?)FindAttributeByLocalName(element, "Key") == "NoteListBoxItemStyle");
    }

    [Fact]
    public void MainActions_UsePurposefulButtonStyles()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var newNoteButtons = xaml
            .Descendants()
            .Where(element => element.Name.LocalName == "Button"
                && (string?)element.Attribute("Content") == "新建便签")
            .ToArray();
        var deleteButton = FindNamedElement(xaml, "Button", "DeleteButton");

        Assert.NotEmpty(newNoteButtons);
        Assert.All(
            newNoteButtons,
            button => Assert.Equal("{StaticResource PrimaryButtonStyle}", GetAppliedStyle(button)));
        Assert.Equal("{StaticResource DangerIconButtonStyle}", (string?)deleteButton.Attribute("Style"));
    }

    [Fact]
    public void NoteList_UsesGroupedNavigationView()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var listBox = xaml.Descendants().Single(element => element.Name.LocalName == "ListBox");

        Assert.Equal("{Binding NotesView}", (string?)listBox.Attribute("ItemsSource"));
        Assert.Contains(
            listBox.Descendants(),
            element => element.Name.LocalName == "GroupStyle.HeaderTemplate");
    }

    [Fact]
    public void EditorActions_AppearInAgreedOrderWithSpacingAndImmediateTooltips()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var expectedNames = new[] { "ModeToggleButton", "UndoButton", "RedoButton", "DeleteButton" };
        var buttons = xaml.Descendants()
            .Where(element => element.Name.LocalName == "Button")
            .Where(element => expectedNames.Contains((string?)FindAttributeByLocalName(element, "Name")))
            .ToArray();

        Assert.Equal(expectedNames, buttons.Select(button => (string?)FindAttributeByLocalName(button, "Name")));
        Assert.Null(buttons[0].Attribute("Margin"));
        Assert.All(buttons.Skip(1), button => Assert.Equal("8,0,0,0", (string?)button.Attribute("Margin")));
        Assert.All(
            buttons,
            button => Assert.Equal("0", (string?)FindAttributeByLocalName(button, "ToolTipService.InitialShowDelay")));
        Assert.Equal("撤销", (string?)buttons[1].Attribute("ToolTip"));
        Assert.Equal("恢复", (string?)buttons[2].Attribute("ToolTip"));
        Assert.Equal("删除", (string?)buttons[3].Attribute("ToolTip"));
        Assert.Equal("DeleteButton_OnClick", (string?)buttons[3].Attribute("Click"));
        Assert.Null(buttons[3].Attribute("Command"));
    }

    [Fact]
    public void EditorHeaderAndFooter_ExposeTabFlowAndCreationDate()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var titleTextBox = FindNamedElement(xaml, "TextBox", "TitleTextBox");
        var createdAtTextBlock = FindNamedElement(xaml, "TextBlock", "CreatedAtTextBlock");

        Assert.Equal("TitleTextBox_OnPreviewKeyDown", (string?)titleTextBox.Attribute("PreviewKeyDown"));
        Assert.Contains("SelectedNote.CreatedAt", (string?)createdAtTextBlock.Attribute("Text"));
        Assert.Contains("NoteCreatedAtDisplayConverter", (string?)createdAtTextBlock.Attribute("Text"));
        Assert.Contains(
            xaml.Descendants(),
            element => element.Name.LocalName == "NoteCreatedAtDisplayConverter"
                && (string?)FindAttributeByLocalName(element, "Key") == "NoteCreatedAtDisplayConverter");

        var stats = FindNamedElement(xaml, "TextBlock", "BodyStatsTextBlock");
        var footer = stats.Parent;
        Assert.NotNull(footer);
        Assert.Equal("Grid", footer!.Name.LocalName);
        Assert.Contains("SelectedNote.Content", (string?)stats.Attribute("Text"));
        Assert.Contains("MarkdownBodyStatsDisplayConverter", (string?)stats.Attribute("Text"));
        Assert.Equal("Right", (string?)createdAtTextBlock.Attribute("HorizontalAlignment"));
        Assert.Null(footer.Attribute("BorderThickness"));
        Assert.Null(footer.Attribute("BorderBrush"));
    }

    [Fact]
    public void Editor_IsWrappedInReadableShell()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var editorShell = xaml
            .Descendants()
            .Single(element => element.Name.LocalName == "Border"
                && (string?)FindAttributeByLocalName(element, "Name") == "EditorShell");

        Assert.Equal("{StaticResource EditorShellBackgroundBrush}", (string?)editorShell.Attribute("Background"));
        Assert.Equal("{StaticResource BorderBrush}", (string?)editorShell.Attribute("BorderBrush"));
        Assert.Equal("1", (string?)editorShell.Attribute("BorderThickness"));
    }

    [Fact]
    public void SidebarActionsAndNoteItems_UseEightDipRoundedBorders()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var styles = XDocument.Load(FindStylesXamlPath());
        var roundedStyle = styles
            .Descendants()
            .Single(element => element.Name.LocalName == "Style"
                && (string?)FindAttributeByLocalName(element, "Key") == "RoundedButtonStyle");
        var noteItemStyle = styles
            .Descendants()
            .Single(element => element.Name.LocalName == "Style"
                && (string?)FindAttributeByLocalName(element, "Key") == "NoteListBoxItemStyle");
        var roundedButtonBorder = roundedStyle
            .Descendants()
            .Single(element => element.Name.LocalName == "Border"
                && (string?)element.Attribute("CornerRadius") == "8");
        var roundedNoteItemBorder = noteItemStyle
            .Descendants()
            .Single(element => element.Name.LocalName == "Border"
                && (string?)element.Attribute("CornerRadius") == "8");
        var sidebarButtons = xaml
            .Descendants()
            .Where(element => element.Name.LocalName == "Button"
                && ((string?)element.Attribute("Content") == "新建便签"
                    || (string?)FindAttributeByLocalName(element, "Name") == "TodoWidgetToggleButton"))
            .ToArray();

        Assert.NotEmpty(sidebarButtons);
        Assert.All(sidebarButtons, button =>
        {
            var style = GetAppliedStyle(button);
            Assert.Contains(style, new[]
            {
                "{StaticResource PrimaryButtonStyle}",
                "{StaticResource SecondaryButtonStyle}"
            });
        });
        Assert.NotNull(roundedButtonBorder);
        Assert.NotNull(roundedNoteItemBorder);
    }

    [Fact]
    public void NoteList_SwitchesToMultipleSelectionAndShowsBatchCheckboxes()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var listBox = FindNamedElement(xaml, "ListBox", "NoteListBox");
        var checkbox = FindNamedElement(xaml, "CheckBox", "BatchSelectionCheckBox");

        Assert.Contains(listBox.Descendants(), element =>
            element.Name.LocalName == "DataTrigger"
            && (string?)element.Attribute("Binding") == "{Binding IsBatchMode}"
            && (string?)element.Attribute("Value") == "True"
            && element.Descendants().Any(setter =>
                setter.Name.LocalName == "Setter"
                && (string?)setter.Attribute("Property") == "SelectionMode"
                && (string?)setter.Attribute("Value") == "Multiple"));
        Assert.Contains("IsSelected", (string?)checkbox.Attribute("IsChecked"));
        Assert.Contains(checkbox.Descendants(), element =>
            element.Name.LocalName == "DataTrigger"
            && (string?)element.Attribute("Binding") == "{Binding DataContext.IsBatchMode, RelativeSource={RelativeSource AncestorType={x:Type Window}}}"
            && (string?)element.Attribute("Value") == "True");
    }

    [Fact]
    public void Sidebar_ContainsTwoRowBatchActionBarWithImmediateIconTooltips()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var normalFooter = FindNamedElement(xaml, "StackPanel", "NormalSidebarFooter");
        var actionBar = FindNamedElement(xaml, "Grid", "BatchActionBar");
        var count = FindNamedElement(xaml, "TextBlock", "BatchSelectedCountTextBlock");
        var buttonNames = new[]
        {
            "BatchSelectAllButton",
            "BatchClearSelectionButton",
            "BatchExitButton",
            "BatchMoveButton",
            "BatchFavoriteButton",
            "BatchUnfavoriteButton",
            "BatchDeleteButton"
        };
        var expectedTooltips = new[]
        {
            "全选当前结果",
            "清空选择",
            "退出批量管理",
            "移动到文件夹",
            "批量收藏",
            "批量取消收藏",
            "批量删除"
        };

        Assert.Contains("BatchSelectedCount", (string?)count.Attribute("Text"));
        Assert.Contains(actionBar.Descendants(), element =>
            element.Name.LocalName == "DataTrigger"
            && (string?)element.Attribute("Binding") == "{Binding IsBatchMode}"
            && (string?)element.Attribute("Value") == "True");
        Assert.Contains(normalFooter.Descendants(), element =>
            element.Name.LocalName == "DataTrigger"
            && (string?)element.Attribute("Binding") == "{Binding IsBatchMode}"
            && (string?)element.Attribute("Value") == "True");

        var buttons = buttonNames.Select((name, index) =>
        {
            var button = FindNamedElement(xaml, "Button", name);
            Assert.Equal(expectedTooltips[index], (string?)button.Attribute("ToolTip"));
            Assert.Equal("0", (string?)FindAttributeByLocalName(button, "ToolTipService.InitialShowDelay"));
            Assert.Contains((string?)button.Attribute("Style"), new[]
            {
                "{StaticResource IconButtonStyle}",
                "{StaticResource DangerIconButtonStyle}"
            });
            return button;
        }).ToArray();

        Assert.All(buttons.Where(button =>
            (string?)FindAttributeByLocalName(button, "Name") is not "BatchSelectAllButton" and not "BatchMoveButton"),
            button => Assert.Equal("8,0,0,0", (string?)button.Attribute("Margin")));
    }

    [Fact]
    public void BatchMode_DisablesEditorAndConflictingNavigationControls()
    {
        var xaml = XDocument.Load(FindMainWindowXamlPath());
        var editor = FindNamedElement(xaml, "Grid", "EditorInteractionPanel");
        var newNote = FindNamedElement(xaml, "Button", "NewNoteButton");
        var search = FindNamedElement(xaml, "TextBox", "NoteSearchTextBox");
        var sort = FindNamedElement(xaml, "Button", "NavigationSortButton");

        foreach (var element in new[] { editor, newNote, search, sort })
        {
            Assert.Contains(element.Descendants(), descendant =>
                descendant.Name.LocalName == "DataTrigger"
                && (string?)descendant.Attribute("Binding") == "{Binding IsBatchMode}"
                && (string?)descendant.Attribute("Value") == "True"
                && descendant.Descendants().Any(setter =>
                    setter.Name.LocalName == "Setter"
                    && (string?)setter.Attribute("Property") == "IsEnabled"
                    && (string?)setter.Attribute("Value") == "False"));
        }
    }

    private static string FindMainWindowXamlPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var path = Path.Combine(
                directory.FullName,
                "src",
                "QingJian.App",
                "Views",
                "MainWindow.xaml");

            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find MainWindow.xaml from test output directory.");
    }

    private static string FindStylesXamlPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            var path = Path.Combine(
                directory.FullName,
                "src",
                "QingJian.App",
                "Resources",
                "Styles.xaml");

            if (File.Exists(path))
            {
                return path;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not find Styles.xaml from test output directory.");
    }

    private static XAttribute? FindAttributeByLocalName(XElement element, string localName)
    {
        return element.Attributes().SingleOrDefault(attribute => attribute.Name.LocalName == localName);
    }

    private static string? GetAppliedStyle(XElement element)
    {
        return (string?)element.Attribute("Style")
            ?? (string?)element.Descendants()
                .FirstOrDefault(descendant => descendant.Name.LocalName == "Style")?
                .Attribute("BasedOn");
    }

    private static XElement FindNamedElement(XDocument document, string elementName, string name)
    {
        return document.Descendants().Single(element =>
            element.Name.LocalName == elementName
            && (string?)FindAttributeByLocalName(element, "Name") == name);
    }
}
