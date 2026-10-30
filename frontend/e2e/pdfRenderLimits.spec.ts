import { test, expect } from '@playwright/test';
import {
  PDF_RENDER_MAX_DIMENSION,
  PDF_RENDER_MAX_PIXELS,
  fitPdfRenderScale,
} from '../src/utils/pdfRenderLimits';

test.describe('fitPdfRenderScale', () => {
  test('keeps the desired scale for an A4 page', () => {
    expect(fitPdfRenderScale(595, 842, 1.5)).toBe(1.5);
  });

  test('keeps the desired scale for a small page', () => {
    expect(fitPdfRenderScale(200, 200, 1.5)).toBe(1.5);
  });

  for (const size of [3000, 10000, 12000, 50000, 1e9]) {
    test(`limits the canvas of a ${size} x ${size} page`, () => {
      const scale = fitPdfRenderScale(size, size, 1.5);

      expect(scale).toBeLessThan(1.5);
      expect(size * scale).toBeLessThanOrEqual(PDF_RENDER_MAX_DIMENSION);
      expect(size * scale * size * scale).toBeLessThanOrEqual(PDF_RENDER_MAX_PIXELS);
    });
  }

  test('limits a very long and narrow page by its longer side', () => {
    const scale = fitPdfRenderScale(100, 100_000, 1.5);

    expect(100_000 * scale).toBeLessThanOrEqual(PDF_RENDER_MAX_DIMENSION);
  });

  test('limits a page that is only wide by the pixel count', () => {
    const scale = fitPdfRenderScale(3000, 3000, 1.5);

    expect(3000 * scale * 3000 * scale).toBeLessThanOrEqual(PDF_RENDER_MAX_PIXELS);
  });

  for (const [width, height] of [
    [0, 100],
    [100, 0],
    [-1, 100],
    [Number.NaN, 100],
    [100, Number.POSITIVE_INFINITY],
  ]) {
    test(`rejects the invalid page size ${width} x ${height}`, () => {
      expect(() => fitPdfRenderScale(width, height, 1.5)).toThrow();
    });
  }
});
