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
