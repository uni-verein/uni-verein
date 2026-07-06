const COUNTRY_FORMATS: Record<string, { length: number; bbanPattern: string }> = {
  DE: { length: 22, bbanPattern: 'nnnnnnnnnnnnnnnnnn' }, // 18 digits
  AT: { length: 20, bbanPattern: 'nnnnnnnnnnnnnnnn' }, // 16 digits
  CH: { length: 21, bbanPattern: 'nnnnnnnnnnnnnnn n' }, // 17 digits (formatted)
  GB: { length: 22, bbanPattern: 'aaaannnnnnnnnnnnnn' }, // 4 letters + 14 digits
  FR: { length: 27, bbanPattern: 'nnnnnnnnnnnnnnnnnnnnnnn' }, // 23 digits
  ES: { length: 24, bbanPattern: 'nnnnnnnnnnnnnnnnnnnn' }, // 20 digits
  IT: { length: 27, bbanPattern: 'annnnnnnnnnnnnnnnnnnnnnn' }, // 1 letter + 22 digits + check
  NL: { length: 18, bbanPattern: 'aaaannnnnnnnnn' }, // 4 letters + 10 digits
  BE: { length: 16, bbanPattern: 'nnnnnnnnnnnn' }, // 12 digits
  PL: { length: 28, bbanPattern: 'nnnnnnnnnnnnnnnnnnnnnnnn' }, // 24 digits
  SE: { length: 24, bbanPattern: 'nnnnnnnnnnnnnnnnnnnn' }, // 20 digits
  DK: { length: 18, bbanPattern: 'nnnnnnnnnnnnnn' }, // 14 digits
  NO: { length: 15, bbanPattern: 'nnnnnnnnnnn' }, // 11 digits
  FI: { length: 18, bbanPattern: 'nnnnnnnnnnnnnn' }, // 14 digits
  LU: { length: 20, bbanPattern: 'nnnaaaaaaaaaaaaa' }, // 3 digits + 13 alphanum
  PT: { length: 25, bbanPattern: 'nnnnnnnnnnnnnnnnnnnnnnn' }, // actually 21 digits
};

function letterToNum(c: string): string {
  return (c.charCodeAt(0) - 55).toString();
}

function mod97(numeric: string): number {
  let remainder = numeric;
  while (remainder.length > 2) {
    const block = remainder.slice(0, 9);
    remainder = (parseInt(block, 10) % 97).toString() + remainder.slice(block.length);
  }
  return parseInt(remainder, 10) % 97;
}

function randomLetter(): string {
  return String.fromCharCode(65 + Math.floor(Math.random() * 26));
}

function randomDigit(): string {
  return Math.floor(Math.random() * 10).toString();
}

function generateBBAN(totalLength: number, pattern: string): string {
  let bban = '';
  for (let i = 0; i < totalLength; i++) {
    const type = pattern[i] ?? 'n';
    bban += type === 'a' ? randomLetter() : randomDigit();
  }
  return bban;
}

function calculateCheckDigits(countryCode: string, bban: string): string {
  const rearranged = bban + countryCode + '00';
  const numeric = rearranged.replace(/[A-Z]/g, letterToNum);
  const check = 98 - mod97(numeric);
  return check.toString().padStart(2, '0');
}

export interface GenerateIBANOptions {
  /** ISO 3166-1 alpha-2 country code (default: "DE"). */
  countryCode?: string;
  /** Provide a fixed BBAN instead of generating a random one. */
  bban?: string;
  /** Return the IBAN with spaces every 4 characters (default: false). */
  formatted?: boolean;
}

export function generateIBAN(options: GenerateIBANOptions = {}): string {
  const countryCode = (options.countryCode ?? 'DE').toUpperCase();

  const format = COUNTRY_FORMATS[countryCode];
  if (!format) {
    throw new Error(
      `Country "${countryCode}" is not supported. ` +
        `Supported: ${Object.keys(COUNTRY_FORMATS).join(', ')}`,
    );
  }

  // BBAN length = total IBAN length − 4 (country + check digits)
  const bbanLength = format.length - 4;

  const bban = options.bban
    ? options.bban.toUpperCase()
    : generateBBAN(bbanLength, format.bbanPattern);

  if (bban.length !== bbanLength) {
    throw new Error(
      `Provided BBAN has length ${bban.length}, expected ${bbanLength} for ${countryCode}.`,
    );
  }

  const checkDigits = calculateCheckDigits(countryCode, bban);
  const iban = countryCode + checkDigits + bban;

  if (options.formatted) {
    return iban.match(/.{1,4}/g)!.join(' ');
  }

  return iban;
}
