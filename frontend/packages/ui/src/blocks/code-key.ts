/**
 * Stable key for a code sample, shared by the server that highlights it and the client that looks
 * the result up.
 *
 * **Deliberately in its own file with zero imports**, and exported as `@databro/ui/code-key`. The
 * site's Nitro server needs this function; importing it from the package root would pull the entire
 * Vue component library into the server bundle, where Rollup cannot parse `.vue` at all and the
 * build fails outright.
 *
 * Hashed rather than keyed by the code itself so a page payload does not carry every sample twice —
 * once inside its block and again as a map key. FNV-1a: not cryptographic, and it does not need to
 * be. A collision would mean two samples highlighting as one, and the inputs are an author's own
 * code rather than anything adversarial.
 */
export function codeKey(code: string, language: string): string {
  const input = `${language} ${code}`;
  let hash = 0x811c9dc5;

  for (let i = 0; i < input.length; i++) {
    hash ^= input.charCodeAt(i);
    // The usual FNV prime multiply, written as shifts to stay in 32-bit integer range.
    hash = (hash + ((hash << 1) + (hash << 4) + (hash << 7) + (hash << 8) + (hash << 24))) >>> 0;
  }

  return hash.toString(36);
}
