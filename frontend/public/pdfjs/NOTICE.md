This file is vendored so PDF receipt rendering works fully self-hosted (no CDN calls at runtime):

- `pdf.worker.min.js` from the `pdfjs-dist` npm package (Apache-2.0), copied verbatim from
  `pdf.worker.min.mjs` and renamed to a `.js` extension. The content is untouched ESM only the
  extension changed, so that nginx's default `mime.types` (which maps `.js` but not `.mjs` to
  `application/javascript`) serves it with a MIME type browsers accept for module workers.

Re-generate by copying `node_modules/pdfjs-dist/build/pdf.worker.min.mjs` to `pdf.worker.min.js`
in this directory after upgrading the `pdfjs-dist` dependency.
