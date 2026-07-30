// Typed client over the DataBro API. Framework-agnostic (uses the Fetch API), so both Nuxt apps
// can share it. It normalizes the success/failure envelope (see docs/API_SPEC.md).

import type {
  ApiResponse,
  Article,
  ArticleSummary,
  Category,
  CategoryWithAncestors,
  Paged,
  PageMeta,
  TaxonomyTerm,
} from "@databro/types";

export class ApiClientError extends Error {
  constructor(
    public readonly code: string,
    message: string,
    public readonly status: number,
    public readonly details?: unknown,
    public readonly traceId?: string,
  ) {
    super(message);
    this.name = "ApiClientError";
  }
}

export interface ApiClientOptions {
  baseUrl: string;
  /** Returns the bearer token for authenticated requests, if any. */
  getToken?: () => string | null | undefined;
  fetch?: typeof globalThis.fetch;
}

export class ApiClient {
  private readonly baseUrl: string;
  private readonly getToken?: () => string | null | undefined;
  private readonly fetchImpl: typeof globalThis.fetch;

  constructor(options: ApiClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/$/, "");
    this.getToken = options.getToken;
    this.fetchImpl = options.fetch ?? globalThis.fetch;
  }

  private async request<TData>(path: string, init?: RequestInit): Promise<TData> {
    return (await this.envelope<TData>(path, init)).data;
  }

  /** Like {@link request} but keeps `meta`, which carries paging on list endpoints. */
  private async envelope<TData>(
    path: string,
    init?: RequestInit,
  ): Promise<{ data: TData; meta?: Record<string, unknown> }> {
    const headers = new Headers(init?.headers);
    headers.set("Accept", "application/json");
    const token = this.getToken?.();
    if (token) headers.set("Authorization", `Bearer ${token}`);

    const response = await this.fetchImpl(`${this.baseUrl}${path}`, { ...init, headers });

    // Not every failure comes back in the envelope: ASP.NET's auth middleware returns a bare 401,
    // and an infrastructure fault can return HTML. Surface those as ApiClientError too rather
    // than letting a JSON parse error escape.
    const raw = await response.text();
    let body: ApiResponse<TData>;
    try {
      body = JSON.parse(raw) as ApiResponse<TData>;
    } catch {
      throw new ApiClientError(
        response.status === 401 ? "unauthenticated" : "unexpected_response",
        response.statusText || `Request to ${path} failed.`,
        response.status,
      );
    }

    if (!body.success) {
      throw new ApiClientError(
        body.error.code,
        body.error.message,
        response.status,
        body.error.details,
        body.error.traceId,
      );
    }
    return { data: body.data, meta: body.meta };
  }

  // ---- Public read surface ----
  // Only endpoints the API actually serves. Search arrives with the Search module; adding it here
  // early would ship a method that 404s at runtime.

  /**
   * Published articles, newest first, optionally narrowed by category or tag slug.
   *
   * Offset-paged: these listings are indexable, and a crawler needs stable page URLs it can
   * enumerate (docs/SEO.md). An unmatched `category`/`tag` slug yields an empty page rather than
   * the unfiltered catalogue.
   */
  async listArticles(params?: {
    page?: number;
    pageSize?: number;
    category?: string;
    tag?: string;
  }): Promise<Paged<ArticleSummary>> {
    const query = new URLSearchParams();
    if (params?.page != null) query.set("page", String(params.page));
    if (params?.pageSize != null) query.set("pageSize", String(params.pageSize));
    if (params?.category) query.set("category", params.category);
    if (params?.tag) query.set("tag", params.tag);

    const suffix = query.size > 0 ? `?${query}` : "";
    const { data, meta } = await this.envelope<ArticleSummary[]>(`/api/v1/articles${suffix}`);

    return {
      items: data,
      meta: (meta as unknown as PageMeta) ?? {
        page: 1,
        pageSize: data.length,
        total: data.length,
        totalPages: 1,
      },
    };
  }

  getArticle(slug: string): Promise<Article> {
    return this.request<Article>(`/api/v1/articles/${encodeURIComponent(slug)}`);
  }

  // ---- Taxonomy ----

  listCategories(): Promise<Category[]> {
    return this.request<Category[]>("/api/v1/categories");
  }

  /** The category plus its ancestor trail (root first) for breadcrumbs. */
  getCategory(slug: string): Promise<CategoryWithAncestors> {
    return this.request<CategoryWithAncestors>(`/api/v1/categories/${encodeURIComponent(slug)}`);
  }

  listTags(): Promise<TaxonomyTerm[]> {
    return this.request<TaxonomyTerm[]>("/api/v1/tags");
  }

  getTag(slug: string): Promise<TaxonomyTerm> {
    return this.request<TaxonomyTerm>(`/api/v1/tags/${encodeURIComponent(slug)}`);
  }
}

export function createApiClient(options: ApiClientOptions): ApiClient {
  return new ApiClient(options);
}

export type { ApiResponse } from "@databro/types";
