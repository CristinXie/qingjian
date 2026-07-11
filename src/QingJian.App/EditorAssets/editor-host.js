(function () {
  let editor = null;
  let isSettingMarkdown = false;
  let activeNoteId = "";
  let imageRequestCounter = 0;
  const pendingImageCallbacks = new Map();

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

    editor.insertText(`![image](${url})`);
    return true;
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
    const files = event.dataTransfer ? Array.from(event.dataTransfer.files || []) : [];
    if (files.some((file) => /^image\//i.test(file.type || ""))) {
      event.preventDefault();
      event.stopPropagation();
      files.filter((file) => /^image\//i.test(file.type || "")).forEach((file) => {
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

  function handlePaste(event) {
    const files = event.clipboardData ? Array.from(event.clipboardData.files || []) : [];
    if (files.some((file) => /^image\//i.test(file.type || ""))) {
      event.preventDefault();
      event.stopPropagation();
      files.filter((file) => /^image\//i.test(file.type || "")).forEach((file) => {
        requestLocalImageUpload(file, insertImageMarkdown);
      });
      return;
    }

    const text = event.clipboardData ? event.clipboardData.getData("text/plain") : "";
    if (insertImageMarkdown((text || "").trim())) {
      event.preventDefault();
      event.stopPropagation();
    }
  }

  window.qingjianEditor = {
    initialize: function () {
      if (editor || !window.toastui || !window.toastui.Editor) {
        return false;
      }

      editor = new window.toastui.Editor({
        el: document.querySelector("#editor"),
        height: "100%",
        initialEditType: "wysiwyg",
        previewStyle: "vertical",
        usageStatistics: false,
        initialValue: "",
        hooks: {
          addImageBlobHook: function (blob, callback) {
            return requestLocalImageUpload(blob, function (url) {
              callback(url, blob.name || "image");
            });
          }
        }
      });

      editor.on("change", postMarkdownChanged);
      editor.on("changeMode", postEditorModeChanged);
      document.addEventListener("click", handleDocumentClick, true);
      document.addEventListener("drop", preventLocalImageDrop, true);
      document.addEventListener("paste", handlePaste, true);
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

    completeImageUpload: function (requestId, url) {
      const callback = pendingImageCallbacks.get(requestId);
      if (!callback) {
        return false;
      }

      pendingImageCallbacks.delete(requestId);
      callback(url);
      return true;
    }
  };

  window.addEventListener("DOMContentLoaded", function () {
    window.qingjianEditor.initialize();
  });
})();
