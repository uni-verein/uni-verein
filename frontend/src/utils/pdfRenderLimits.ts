export const PDF_RENDER_MAX_DIMENSION = 4096;
export const PDF_RENDER_MAX_PIXELS = 16_000_000;

export function fitPdfRenderScale(
  pageWidth: number,
  pageHeight: number,
  desiredScale: number,
): number {
  if (
    !Number.isFinite(pageWidth) ||
    !Number.isFinite(pageHeight) ||
    pageWidth <= 0 ||
    pageHeight <= 0
  ) {
    throw new Error('PDF page has an invalid size');
  }

  return Math.min(
    desiredScale,
    PDF_RENDER_MAX_DIMENSION / Math.max(pageWidth, pageHeight),
    Math.sqrt(PDF_RENDER_MAX_PIXELS / (pageWidth * pageHeight)),
  );
}
