These files are vendored so OCR works fully self-hosted (no CDN calls at runtime):

- `worker.min.js` from the `tesseract.js` npm package (Apache-2.0)
- `tesseract-core-simd-lstm.wasm(.js)`, `tesseract-core-lstm.wasm(.js)` from the `tesseract.js-core` npm package (Apache-2.0)
- `deu.traineddata` German "fast" model from [tesseract-ocr/tessdata_fast](https://github.com/tesseract-ocr/tessdata_fast) (Apache-2.0)

Re-generate by re-running the copy/download steps in the Phase 2 plan/PR description after upgrading the `tesseract.js` dependency.
