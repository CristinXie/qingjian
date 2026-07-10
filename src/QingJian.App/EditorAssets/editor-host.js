(function () {
  let editor = null;
  let isSettingMarkdown = false;

  function postMarkdownChanged() {
    if (isSettingMarkdown || !editor || !window.chrome || !window.chrome.webview) {
      return;
    }

    window.chrome.webview.postMessage({
      type: "markdownChanged",
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
        initialValue: ""
      });

      editor.on("change", postMarkdownChanged);
      return true;
    },

    setMarkdown: function (markdown) {
      if (!editor) {
        return false;
      }

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
