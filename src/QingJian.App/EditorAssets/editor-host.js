(function () {
  let editor = null;
  let isSettingMarkdown = false;
  let activeNoteId = "";

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
          addImageBlobHook: function () {
            return false;
          }
        }
      });

      editor.on("change", postMarkdownChanged);
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
    }
  };

  window.addEventListener("DOMContentLoaded", function () {
    window.qingjianEditor.initialize();
  });
})();
