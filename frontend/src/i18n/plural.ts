import { FORMAT_LOCALES, type LanguageCode } from "./language";

export interface PluralForms {
  one: string;
  /** Czech 2–4; a language without the category falls through to `other`. */
  few?: string;
  many?: string;
  other: string;
}

/**
 * CLDR plural selection, so "1 den / 2 dny / 5 dní" is the grammar's decision rather than an
 * if-chain's. Catalogs call this with their own language code; the count is not interpolated
 * here — each form arrives as the full phrase.
 */
export function plural(code: LanguageCode, count: number, forms: PluralForms): string {
  const category = new Intl.PluralRules(FORMAT_LOCALES[code]).select(count);
  return (
    (category === "one" && forms.one) ||
    (category === "few" && forms.few) ||
    (category === "many" && forms.many) ||
    forms.other
  );
}
