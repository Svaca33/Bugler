import { LanguageSelect } from "./LanguageSelect";

/**
 * The language Bugler speaks to you. Its own label is the card's heading — a caption above it would
 * only say the same word twice.
 */
export function LanguageCard() {
  return (
    <div className="flex max-w-[620px] flex-col gap-4 rounded-[11px] border border-[#1E344C] bg-card p-4">
      <LanguageSelect />
    </div>
  );
}
