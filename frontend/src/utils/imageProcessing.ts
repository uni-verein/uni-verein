export const RECEIPT_MAX_UPLOAD_BYTES = 100 * 1024;

// Long-edge px levels tried in order, largest first. Falling back to a smaller
// size is only needed when quality reduction alone can't hit the target
// which, for grayscale receipt photos, should be rare.
const DIMENSION_LEVELS = [1800, 1350, 1012, 759, 640];

// JPEG quality bounds for the binary search. Below ~0.4 artifacts get harsh
// enough to hurt readability (both for the human eye and OCR), so that's
// treated as a hard floor rather than searched past.
const QUALITY_HIGH = 0.92;
const QUALITY_LOW = 0.4;
const QUALITY_SEARCH_STEPS = 6;

async function loadImage(file: File | Blob): Promise<HTMLImageElement> {
  const dataUrl = await new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(reader.result as string);
    reader.onerror = () => reject(reader.error);
    reader.readAsDataURL(file);
  });

  return new Promise<HTMLImageElement>((resolve, reject) => {
    const image = new Image();
    image.onload = () => resolve(image);
    image.onerror = () => reject(new Error('Image could not be loaded'));
    image.src = dataUrl;
  });
}

function encodeAt(
  img: HTMLImageElement,
  maxDimension: number,
  quality: number,
): Promise<Blob | null> {
  let width = img.width;
  let height = img.height;

  if (width > height) {
    if (width > maxDimension) {
      height *= maxDimension / width;
      width = maxDimension;
    }
  } else if (height > maxDimension) {
    width *= maxDimension / height;
    height = maxDimension;
  }

  const canvas = document.createElement('canvas');
  canvas.width = Math.round(width);
  canvas.height = Math.round(height);
  const ctx = canvas.getContext('2d')!;
  ctx.filter = 'grayscale(1)';
  ctx.drawImage(img, 0, 0, canvas.width, canvas.height);

  return new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/jpeg', quality));
}

export async function compressImageFile(
  file: File | Blob,
  maxBytes = RECEIPT_MAX_UPLOAD_BYTES,
): Promise<Blob> {
  const img = await loadImage(file);

  let best: Blob | null = null;

  for (const maxDimension of DIMENSION_LEVELS) {
    const highBlob = await encodeAt(img, maxDimension, QUALITY_HIGH);
    if (highBlob && (!best || highBlob.size < best.size)) best = highBlob;
    if (highBlob && highBlob.size <= maxBytes) return highBlob;

    const lowBlob = await encodeAt(img, maxDimension, QUALITY_LOW);
    if (lowBlob && (!best || lowBlob.size < best.size)) best = lowBlob;
    if (!lowBlob || lowBlob.size > maxBytes) {
      continue;
    }

    let low = QUALITY_LOW;
    let high = QUALITY_HIGH;
    let fits = lowBlob;

    for (let i = 0; i < QUALITY_SEARCH_STEPS; i++) {
      const mid = (low + high) / 2;
      const blob = await encodeAt(img, maxDimension, mid);
      if (!blob) break;
      if (blob.size < best!.size) best = blob;

      if (blob.size <= maxBytes) {
        fits = blob;
        low = mid;
      } else {
        high = mid;
      }
    }

    return fits;
  }

  return best ?? (file as Blob);
}
