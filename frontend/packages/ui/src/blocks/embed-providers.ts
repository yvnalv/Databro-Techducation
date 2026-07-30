/**
 * Embed allowlist.
 *
 * An EmbedBlock carries an author-supplied URL. Turning that straight into an `<iframe src>` would
 * let anyone with Content.Create frame arbitrary origins inside a DataBro page - a stored XSS and
 * clickjacking vector, and a privacy leak to third parties on a public page. So embeds are
 * allowlisted by host, normalized into the provider's documented embed URL, and anything
 * unrecognised degrades to a plain link instead of being framed.
 *
 * Adding a provider is a deliberate act: it must appear here, with a known-safe embed URL shape.
 */

export interface EmbedTarget {
  /** Provider key, for styling and analytics. */
  provider: string;
  /** Fully-normalized URL safe to place in an iframe src. */
  embedUrl: string;
  /** Accessible name for the iframe. */
  title: string;
}

type Normalizer = (url: URL) => string | null;

const PROVIDERS: Record<string, { hosts: string[]; normalize: Normalizer; title: string }> = {
  youtube: {
    hosts: ["youtube.com", "www.youtube.com", "youtu.be", "m.youtube.com"],
    title: "YouTube video",
    normalize: (url) => {
      const id = url.hostname.endsWith("youtu.be")
        ? url.pathname.slice(1)
        : (url.searchParams.get("v") ?? (url.pathname.startsWith("/embed/") ? url.pathname.slice(7) : null));
      return id && /^[\w-]{6,20}$/.test(id) ? `https://www.youtube-nocookie.com/embed/${id}` : null;
    },
  },
  vimeo: {
    hosts: ["vimeo.com", "www.vimeo.com", "player.vimeo.com"],
    title: "Vimeo video",
    normalize: (url) => {
      const id = url.pathname.split("/").filter(Boolean).pop();
      return id && /^\d{6,12}$/.test(id) ? `https://player.vimeo.com/video/${id}` : null;
    },
  },
  codepen: {
    hosts: ["codepen.io", "www.codepen.io"],
    title: "CodePen embed",
    normalize: (url) => {
      const [user, type, id] = url.pathname.split("/").filter(Boolean);
      return user && type === "pen" && id && /^[\w-]{4,20}$/.test(id)
        ? `https://codepen.io/${user}/embed/${id}`
        : null;
    },
  },
};

/**
 * Resolves an embed to a safe iframe target, or null when the provider is not allowlisted or the
 * URL does not match its expected shape. Callers render a link fallback on null.
 */
export function resolveEmbed(rawUrl: string): EmbedTarget | null {
  let url: URL;
  try {
    url = new URL(rawUrl);
  } catch {
    return null;
  }

  // Only https. An http embed would break the page's security context.
  if (url.protocol !== "https:") return null;

  for (const [provider, config] of Object.entries(PROVIDERS)) {
    if (!config.hosts.includes(url.hostname.toLowerCase())) continue;
    const embedUrl = config.normalize(url);
    return embedUrl ? { provider, embedUrl, title: config.title } : null;
  }

  return null;
}

/** True when the URL is safe to render as a plain link (the fallback path). */
export function isSafeLink(rawUrl: string): boolean {
  try {
    const { protocol } = new URL(rawUrl);
    return protocol === "https:" || protocol === "http:";
  } catch {
    return false;
  }
}
