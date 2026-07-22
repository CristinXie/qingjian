(function () {
  let editor = null;
  let isSettingMarkdown = false;
  let activeNoteId = "";
  let imageRequestCounter = 0;
  const pendingImageCallbacks = new Map();

  const zhCnLanguage = {
    Markdown: "Markdown",
    WYSIWYG: "所见即所得",
    Write: "编辑",
    Preview: "预览",
    Headings: "标题",
    Paragraph: "正文",
    Bold: "粗体",
    Italic: "斜体",
    Strike: "删除线",
    Code: "行内代码",
    Line: "分隔线",
    Blockquote: "引用",
    "Unordered list": "无序列表",
    "Ordered list": "有序列表",
    Task: "任务列表",
    Indent: "增加缩进",
    Outdent: "减少缩进",
    "Insert link": "插入链接",
    "Insert CodeBlock": "插入代码块",
    "Insert table": "插入表格",
    "Insert image": "插入图片",
    Heading: "标题",
    "Image URL": "图片地址",
    "Select image file": "选择图片文件",
    "Choose a file": "选择文件",
    "No file": "未选择文件",
    Description: "描述",
    OK: "确定",
    More: "更多",
    Cancel: "取消",
    File: "文件",
    URL: "地址",
    "Link text": "链接文字",
    "Add row to up": "在上方添加行",
    "Add row to down": "在下方添加行",
    "Add column to left": "在左侧添加列",
    "Add column to right": "在右侧添加列",
    "Remove row": "删除行",
    "Remove column": "删除列",
    "Align column to left": "列左对齐",
    "Align column to center": "列居中对齐",
    "Align column to right": "列右对齐",
    "Remove table": "删除表格",
    "Would you like to paste as table?": "是否粘贴为表格？",
    "Text color": "文字颜色",
    "Auto scroll enabled": "已开启自动滚动",
    "Auto scroll disabled": "已关闭自动滚动",
    "Choose language": "选择语言",
    "Switch to WYSIWYG": "切换到所见即所得",
    "Switch to Markdown": "切换到 Markdown"
  };

  function postMarkdownChanged() {
    if (isSettingMarkdown || !editor || !window.chrome || !window.chrome.webview) {
      return;
    }

    window.chrome.webview.postMessage({
      type: "markdownChanged",
      noteId: activeNoteId,
      markdown: editor.getMarkdown()
    });
  }

  function isHttpUrl(url) {
    return /^https?:\/\//i.test(url || "");
  }

  function isImageUrl(url) {
    return isHttpUrl(url) && /\.(png|jpe?g|gif|webp|bmp|svg)(\?.*)?$/i.test(url);
  }

  function insertImageMarkdown(url) {
    if (!editor || !isImageUrl(url)) {
      return false;
    }

    if (typeof editor.exec === "function") {
      try {
        editor.exec("addImage", { imageUrl: url, altText: "图片" });
        postMarkdownChanged();
        return true;
      } catch {
        // Fall back to markdown text insertion below.
      }
    }

    editor.insertText(`![图片](${url})`);
    postMarkdownChanged();
    return true;
  }

  function getImageFiles(data) {
    if (!data) {
      return [];
    }

    const files = Array.from(data.files || []).filter((file) => /^image\//i.test(file.type || ""));
    const itemFiles = Array.from(data.items || [])
      .filter((item) => item.kind === "file" && /^image\//i.test(item.type || ""))
      .map((item) => item.getAsFile())
      .filter((file) => file && /^image\//i.test(file.type || ""));

    const seen = new Set();
    return [...files, ...itemFiles].filter((file) => {
      const key = `${file.name || ""}:${file.type || ""}:${file.size || 0}:${file.lastModified || 0}`;
      if (seen.has(key)) {
        return false;
      }

      seen.add(key);
      return true;
    });
  }

  function postEditorModeChanged(editorMode) {
    if (!window.chrome || !window.chrome.webview || !editorMode) {
      return;
    }

    window.chrome.webview.postMessage({
      type: "editorModeChanged",
      editorMode: editorMode
    });
  }

  function postNativePasteRequested() {
    if (!window.chrome || !window.chrome.webview) {
      return;
    }

    window.chrome.webview.postMessage({
      type: "nativePasteRequested"
    });
  }

  function requestLocalImageUpload(file, onUploaded) {
    if (!file || !/^image\//i.test(file.type || "")) {
      return false;
    }

    const requestId = `image-${Date.now()}-${++imageRequestCounter}`;
    pendingImageCallbacks.set(requestId, onUploaded);

    const reader = new FileReader();
    reader.onload = function () {
      if (!window.chrome || !window.chrome.webview) {
        pendingImageCallbacks.delete(requestId);
        return;
      }

      window.chrome.webview.postMessage({
        type: "localImageRequested",
        requestId: requestId,
        fileName: file.name || "image",
        dataUrl: String(reader.result || "")
      });
    };

    reader.onerror = function () {
      pendingImageCallbacks.delete(requestId);
    };

    reader.readAsDataURL(file);
    return true;
  }

  function postExternalLinkRequested(url) {
    if (!window.chrome || !window.chrome.webview || !isHttpUrl(url)) {
      return;
    }

    window.chrome.webview.postMessage({
      type: "externalLinkRequested",
      url: url
    });
  }

  function handleDocumentClick(event) {
    const link = event.target && event.target.closest ? event.target.closest("a[href]") : null;
    if (!link) {
      return;
    }

    event.preventDefault();
    event.stopPropagation();

    if (event.ctrlKey || event.metaKey) {
      postExternalLinkRequested(link.href);
    }
  }

  function preventLocalImageDrop(event) {
    const files = getImageFiles(event.dataTransfer);
    if (files.length > 0) {
      event.preventDefault();
      event.stopPropagation();
      files.forEach((file) => {
        requestLocalImageUpload(file, insertImageMarkdown);
      });
      return;
    }

    const url = event.dataTransfer ? event.dataTransfer.getData("text/uri-list") || event.dataTransfer.getData("text/plain") : "";
    if (insertImageMarkdown((url || "").trim())) {
      event.preventDefault();
      event.stopPropagation();
    }
  }

  function handleDragOver(event) {
    const files = getImageFiles(event.dataTransfer);
    const hasImageUrl = event.dataTransfer &&
      Array.from(event.dataTransfer.types || []).some((type) => type === "text/uri-list" || type === "text/plain");

    if (files.length > 0 || hasImageUrl) {
      event.preventDefault();
      event.stopPropagation();
      if (event.dataTransfer) {
        event.dataTransfer.dropEffect = "copy";
      }
    }
  }

  function handlePaste(event) {
    const files = getImageFiles(event.clipboardData);
    if (files.length > 0) {
      event.preventDefault();
      event.stopPropagation();
      event.stopImmediatePropagation();
      files.forEach((file) => {
        requestLocalImageUpload(file, insertImageMarkdown);
      });
      return;
    }

    const text = event.clipboardData ? event.clipboardData.getData("text/plain") : "";
    if (!text) {
      event.preventDefault();
      event.stopPropagation();
      postNativePasteRequested();
      return;
    }

    if (insertImageMarkdown((text || "").trim())) {
      event.preventDefault();
      event.stopPropagation();
    }
  }

  function executeHistoryCommand(command) {
    if (!editor || typeof editor.exec !== "function") {
      return false;
    }

    editor.exec(command);
    postMarkdownChanged();
    return true;
  }

  function handleEditorKeyDown(event) {
    if ((event.ctrlKey || event.metaKey) && !event.altKey && event.key.toLowerCase() === "y") {
      event.preventDefault();
      event.stopPropagation();
      executeHistoryCommand("redo");
    }
  }

  window.qingjianEditor = {
    initialize: function () {
      if (editor || !window.toastui || !window.toastui.Editor) {
        return false;
      }

      window.toastui.Editor.setLanguage(["zh-CN"], zhCnLanguage);

      editor = new window.toastui.Editor({
        el: document.querySelector("#editor"),
        height: "100%",
        initialEditType: "wysiwyg",
        previewStyle: "vertical",
        hideModeSwitch: true,
        language: "zh-CN",
        usageStatistics: false,
        initialValue: "",
        hooks: {
          addImageBlobHook: function (blob, callback) {
            return requestLocalImageUpload(blob, function (url) {
              callback(url, blob.name || "图片");
            });
          }
        }
      });

      editor.on("change", postMarkdownChanged);
      editor.on("changeMode", postEditorModeChanged);
      document.addEventListener("click", handleDocumentClick, true);
      document.addEventListener("dragover", handleDragOver, true);
      document.addEventListener("drop", preventLocalImageDrop, true);
      document.addEventListener("paste", handlePaste, true);
      document.addEventListener("keydown", handleEditorKeyDown, true);
      return true;
    },

    setMarkdown: function (noteId, markdown) {
      if (!editor) {
        return false;
      }

      activeNoteId = noteId || "";
      isSettingMarkdown = true;
      editor.setMarkdown(markdown || "", false);
      isSettingMarkdown = false;
      return true;
    },

    getMarkdown: function () {
      return editor ? editor.getMarkdown() : "";
    },

    setEditorMode: function (editorMode) {
      if (!editor || !editorMode || (editorMode !== "wysiwyg" && editorMode !== "markdown")) {
        return false;
      }

      if ((editorMode === "wysiwyg" && editor.isWysiwygMode()) ||
          (editorMode === "markdown" && editor.isMarkdownMode())) {
        return true;
      }

      editor.changeMode(editorMode);
      return true;
    },

    undo: function () {
      return executeHistoryCommand("undo");
    },

    redo: function () {
      return executeHistoryCommand("redo");
    },

    toggleMode: function () {
      if (!editor) {
        return false;
      }

      const editorMode = editor.isMarkdownMode() ? "wysiwyg" : "markdown";
      editor.changeMode(editorMode);
      return editorMode;
    },

    focus: function () {
      if (!editor || typeof editor.focus !== "function") {
        return false;
      }

      editor.focus();
      return true;
    },

    completeImageUpload: function (requestId, url) {
      const callback = pendingImageCallbacks.get(requestId);
      if (!callback) {
        return false;
      }

      pendingImageCallbacks.delete(requestId);
      callback(url);
      return true;
    },

    insertImage: function (url) {
      return insertImageMarkdown(url);
    }
  };

  window.addEventListener("DOMContentLoaded", function () {
    window.qingjianEditor.initialize();
  });
})();
