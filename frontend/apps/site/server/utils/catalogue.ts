import { createApiClient, type ApiClient } from "@databro/api-client";
import type { ArticleSummary, Category, TaxonomyTerm } from "@databro/types";
import type { H3Event } from "h3";

/**
 * Shared data access for the crawler-facing artifacts (sitemap, RSS, robots).
 *
 * These run on the Nitro server, so they use the *internal* API address — inside a container
 * `localhost` is this container, not the API (see nuxt.config).
 */
export function apiClient(event: H3Event): ApiClient {
  const config = useRuntimeConfig(event);
  const baseUrl = (config.apiInternalBaseUrl || config.public.apiBaseUrl) as string;
  return createApiClient({ baseUrl });
}

export function siteUrl(event: H3Event): string {
  return String(useRuntimeConfig(event).public.siteUrl).replace(/\/$/, "");
}

/** Hard ceiling so a runaway catalogue cannot make one request page forever. */
const MAX_PAGES = 50;
const PAGE_SIZE = 100;

/**
 * Every published article, paged through the public listing.
 *
 * A dedicated bulk endpoint would be better once the catalogue is large — this is N requests for N
 * hundred articles. Noted in STATUS rather than pretended away; at present it is one request.
 */
export async function allPublishedArticles(event: H3Event): Promise<ArticleSummary[]> {
  const client = apiClient(event);
  const collected: ArticleSummary[] = [];

  for (let page = 1; page <= MAX_PAGES; page++) {
    const result = await client.listArticles({ page, pageSize: PAGE_SIZE });
    collected.push(...result.items);

    if (page >= result.meta.totalPages || result.items.length === 0) break;
  }

  return collected;
}

export async function taxonomy(
  event: H3Event,
): Promise<{ categories: Category[]; tags: TaxonomyTerm[] }> {
  const client = apiClient(event);
  const [categories, tags] = await Promise.all([client.listCategories(), client.listTags()]);
  return { categories, tags };
}

/**
 * Escapes text for XML.
 *
 * Not optional: an article title containing `&` or `<` produces a malformed document that a crawler
 * rejects wholesale — one bad title would take the entire sitemap or feed offline.
 */
export function xml(value: string): string {
  return value
    .replace(/&/g, "&amp;")
    .replace(/</g, "&lt;")
    .replace(/>/g, "&gt;")
    .replace(/"/g, "&quot;")
    .replace(/'/g, "&apos;");
}

/** The locales the site publishes, mirroring nuxt.config's i18n block. */
export const LOCALES = [
  { code: "en", prefix: "" },
  { code: "id", prefix: "/id" },
] as const;
