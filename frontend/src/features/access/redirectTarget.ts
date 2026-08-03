/** Where somebody lands who signs in without having been sent anywhere in particular. */
export const DEFAULT_DESTINATION = "/dashboard";

/**
 * An origin that exists nowhere, only to resolve a candidate address against something. Any host
 * would do — the point is that a value which stays on it is a value that names no host of its own.
 */
const PROBE_ORIGIN = "http://redirect.probe.invalid";

/**
 * The address somebody was heading for when the gate turned them away — a link to an Episode out of
 * an alert, a shared trace, anything under the app — carried across the sign-in as the `redirect`
 * search param of /login.
 *
 * It arrives from the URL, so it is whatever anyone cared to put there, and this is the one place
 * that decides whether to believe it. Only an address inside this app is honoured: without that,
 * /login?redirect=https://elsewhere would turn Bugler's own sign-in page into a springboard to
 * somebody else's, which is the shape phishing likes best.
 */
export function sanitizeRedirectTarget(value: unknown): string | undefined {
  if (typeof value !== "string" || !value.startsWith("/")) {
    return undefined;
  }

  // Resolved the way a browser would resolve it, rather than judged by how it reads. "//host" and
  // "/\host" are absolute addresses wearing a path's clothes, and a tab or newline spliced into
  // either one is dropped before resolution — so a rule written against the text can be out-thought,
  // and this one cannot: whatever lands on an origin we did not make up names a host of its own.
  let resolved: URL;
  try {
    resolved = new URL(value, PROBE_ORIGIN);
  } catch {
    return undefined;
  }

  if (resolved.origin !== PROBE_ORIGIN) {
    return undefined;
  }

  // Handed back as the parser read it, not as it was typed: what gets navigated to is then the very
  // string this function approved, with no room left between the two.
  return `${resolved.pathname}${resolved.search}${resolved.hash}`;
}
