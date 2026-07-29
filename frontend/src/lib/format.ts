export function formatTime(timestamp: string): string {
  const date = new Date(timestamp);
  return `${date.toLocaleDateString()} ${date.toLocaleTimeString()}.${date
    .getMilliseconds()
    .toString()
    .padStart(3, "0")}`;
}
