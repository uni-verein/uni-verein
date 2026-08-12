import { createWorker } from 'tesseract.js';

export interface ReceiptOcrResult {
  amount?: number;
  date?: Date;
}

/**
 * Labels that introduce the final amount to be paid on German invoices
 * ("Rechnung"), receipts ("Beleg"), quittances ("Quittung") and till
 * receipts ("Kassenzettel"), roughly ordered from most specific/reliable to
 * most generic. Each keyword is tried against the whole text in order and
 * the first one that is found wins, this matters because a generic label
 * (e.g. "Betrag") often also appears earlier in the text as part of a
 * sub-total or a per-item row, and we don't want that to win over a more
 * specific "Gesamtbetrag"/"Endbetrag" that only shows up further down.
 *
 * Deliberately excludes sub-total/tax-only labels such as "Zwischensumme",
 * "Nettobetrag" or "Steuerbetrag", since those are not the amount that was
 * actually paid.
 */
const AMOUNT_KEYWORDS = [
  'gesamtbetrag',
  'gesamtsumme',
  'endbetrag',
  'endsumme',
  'rechnungsbetrag',
  'rechnungssumme',
  'zahlbetrag',
  'zahlungsbetrag',
  'bruttobetrag',
  'zu\\s*zahlend(?:er)?\\s*betrag',
  'fälliger\\s*betrag',
  'grand\\s*total',
  'amount\\s*due',
  'balance\\s*due',
  'gesamtpreis',
  'gesamt',
  'summe',
  'zu\\s*zahlen',
  'total',
  'fällig',
  'betrag',
];

// German-style amount: thousands separated by "." and decimals always by a
// ",". The comma-decimal is required deliberately: it's what actually
// distinguishes a printed money amount from other dotted/decimal-looking
// numbers OCR'd off a receipt (timestamps such as "12:39:35.000", version-ish
// IDs, ...) that would otherwise be misread as an amount - e.g. the ".000" in
// a TSE timestamp matching as "35.00" and turning into 3500 once "."
// thousands-separators are blindly stripped.
const AMOUNT_DECIMAL = '\\d{1,3}(?:\\.\\d{3})*,\\d{2}';
const AMOUNT_REGEX = new RegExp(`(${AMOUNT_DECIMAL})\\s*(?:€|eur)?`, 'gi');
const DATE_REGEX = /(\d{1,2})[.\/](\d{1,2})[.\/](\d{2,4})/;

const NON_AMOUNT_LINE = /\b(tse|bon-?nr|kassen(?:nummer)?|markt|seriennummer|barcode)\b/i;

function buildKeywordLineRegex(keyword: string): RegExp {
  // Capture the rest of the line the label appears on (bounded, in case the
  // OCR output is missing a line break where one would be expected) instead
  // of only the characters immediately after the label, so that tabular
  // breakdowns such as "Gesamtbetrag   8,47   1,13   9,60"
  // (Netto/Steuer/Brutto) can be resolved below instead of grabbing the
  // first - wrong - column.
  return new RegExp(`\\b(?:${keyword})\\b([^\\n]{0,80})`, 'i');
}

function parseGermanAmount(raw: string): number {
  return parseFloat(raw.replace(/\./g, '').replace(',', '.'));
}

function lastAmount(segment: string): number | undefined {
  const matches = [...segment.matchAll(AMOUNT_REGEX)]
    .map((m) => parseGermanAmount(m[1]))
    .filter((n) => !isNaN(n));
  return matches.length > 0 ? matches[matches.length - 1] : undefined;
}

function extractAmount(text: string): number | undefined {
  for (const keyword of AMOUNT_KEYWORDS) {
    const match = text.match(buildKeywordLineRegex(keyword));
    if (match) {
      const amount = lastAmount(match[1]);
      if (amount !== undefined) {
        return amount;
      }
    }
  }

  // Fallback: no recognizable "total" label was found. Assume the largest
  // amount printed on the receipt is the total. true for the vast
  // majority of invoices, receipts and till slips. Metadata lines (serial
  // numbers, TSE signatures, barcodes, ...) are skipped so they can't be
  // mistaken for a bigger "amount".
  const matches = text
    .split('\n')
    .filter((line) => !NON_AMOUNT_LINE.test(line))
    .flatMap((line) => [...line.matchAll(AMOUNT_REGEX)])
    .map((m) => parseGermanAmount(m[1]))
    .filter((n) => !isNaN(n));
  return matches.length > 0 ? Math.max(...matches) : undefined;
}

export async function runReceiptOcr(image: Blob): Promise<ReceiptOcrResult> {
  try {
    const worker = await createWorker('deu', 1, {
      workerPath: '/tesseract/worker.min.js',
      corePath: '/tesseract/',
      langPath: '/tesseract/',
      gzip: false,
      workerBlobURL: false,
    });

    let text = '';
    try {
      const { data } = await worker.recognize(image);
      text = data.text ?? '';
    } finally {
      await worker.terminate();
    }

    const result: ReceiptOcrResult = {};

    const amount = extractAmount(text);
    if (amount !== undefined) {
      result.amount = amount;
    }

    const dateMatch = text.match(DATE_REGEX);
    if (dateMatch) {
      const [, day, month, yearRaw] = dateMatch;
      const year = yearRaw.length === 2 ? 2000 + parseInt(yearRaw, 10) : parseInt(yearRaw, 10);
      const date = new Date(year, parseInt(month, 10) - 1, parseInt(day, 10));
      if (!isNaN(date.getTime())) {
        result.date = date;
      }
    }

    return result;
  } catch (e) {
    console.error('Receipt OCR failed', e);
    return {};
  }
}
