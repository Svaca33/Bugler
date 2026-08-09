import { SettingsCard } from "@/components/ui/settings-layout";

import { LanguageSelect } from "./LanguageSelect";

/**
 * The language Bugler speaks to you. Its own label is the card's heading — a caption above it would
 * only say the same word twice.
 */
export function LanguageCard() {
  return (
    <SettingsCard>
      <LanguageSelect />
    </SettingsCard>
  );
}
