import { GlobalWorkerOptions, getDocument } from 'pdfjs-dist';
import { PDF_RENDER_MAX_PIXELS, fitPdfRenderScale } from './pdfRenderLimits';

GlobalWorkerOptions.workerSrc = '/pdfjs/pdf.worker.min.js';

export const RECEIPT_MAX_PDF_BYTES = 10 * 1024 * 1024;

export function isPdfFile(file: File | Blob): boolean {
  return file.type === 'application/pdf';
}

/**
 * Renders the first page of a PDF file to a JPEG blob, suitable both as an
 * `<img>` preview source and as input for the existing image-based OCR
 * pipeline (`runReceiptOcr`), since neither of those can decode raw PDF
 * bytes directly. The canvas is limited in size (see `pdfRenderLimits`), so a PDF declaring a huge page
 * is rendered at a lower scale instead of exhausting the browser.
 */
export async function renderPdfFirstPageToBlob(file: Blob, scale = 1.5): Promise<Blob> {
  const buffer = await file.arrayBuffer();
  const pdf = await getDocument({ data: buffer, maxImageSize: PDF_RENDER_MAX_PIXELS }).promise;
  try {
    const page = await pdf.getPage(1);
    const pageSize = page.getViewport({ scale: 1 });
    const viewport = page.getViewport({
      scale: fitPdfRenderScale(pageSize.width, pageSize.height, scale),
    });

    const canvas = document.createElement('canvas');
    canvas.width = Math.floor(viewport.width);
    canvas.height = Math.floor(viewport.height);
    const context = canvas.getContext('2d');
    if (!context) {
      throw new Error('Canvas 2D context could not be created');
    }

    await page.render({ canvasContext: context, viewport }).promise;

    const blob = await new Promise<Blob | null>((resolve) =>
      canvas.toBlob(resolve, 'image/jpeg', 0.85),
    );
    if (!blob) {
      throw new Error('PDF page could not be rendered');
    }
    return blob;
  } finally {
    await pdf.destroy();
  }
}
