# Editor Vendor Assets

This folder contains local Toast UI Editor runtime assets used by the WebView2 editor.

Runtime files:

- `toastui-editor-all.min.js`
- `toastui-editor.min.css`

Sources:

- `toastui-editor-all.min.js` is generated from `@toast-ui/editor@3.2.2` with esbuild as a minified, self-contained browser IIFE that exposes `window.toastui.Editor`.
- `toastui-editor.min.css` comes from `@toast-ui/editor@3.2.2`.

Runtime editing must not depend on a CDN.
