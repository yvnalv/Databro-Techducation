// Typed client over the DataBro API. Framework-agnostic (uses the Fetch API), so both Nuxt apps
// can share it. It normalizes the success/failure envelope (see docs/API_SPEC.md).

import type {
  ApiResponse,
  Article,
  ArticleSummary,
  AuthTokens,
  Category,
  CategoryWithAncestors,
  Paged,
  PageMeta,
  Redirect,
  TaxonomyTerm,
  UserProfile,
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

  // ---- Auth ----
  // Deliberately not wrapped in the token-bearing paths below: login and refresh are how a token is
  // obtained, so they must work without one.

  login(email: string, password: string): Promise<AuthTokens> {
    return this.request<AuthTokens>("/api/v1/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ email, password }),
    });
  }

  refresh(refreshToken: string): Promise<AuthTokens> {
    return this.request<AuthTokens>("/api/v1/auth/refresh", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ refreshToken }),
    });
  }

  /** The signed-in user. Requires a bearer token, so it doubles as a session probe. */
  me(): Promise<UserProfile> {
    return this.request<UserProfile>("/api/v1/me");
  }

  // ---- Authoring ----
  // Everything here requires a permission (docs/SECURITY.md §2); an unauthenticated call is a 401
  // and an under-privileged one a 403, both surfaced as ApiClientError with those statuses.

  /**
   * Articles across every status — the CMS list. Distinct from the public `listArticles`, which
   * serves only published content and is the cached, indexable surface.
   */
  async listAuthoringArticles(params?: {
    page?: number;
    pageSize?: number;
  }): Promise<Paged<ArticleSummary>> {
    const query = new URLSearchParams();
    if (params?.page != null) query.set("page", String(params.page));
    if (params?.pageSize != null) query.set("pageSize", String(params.pageSize));

    const suffix = query.size > 0 ? `?${query}` : "";
    const { data, meta } = await this.envelope<ArticleSummary[]>(
      `/api/v1/authoring/articles${suffix}`,
    );

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

  /** Full article by id, including the *draft* blocks — what the editor loads. */
  getAuthoringArticle(id: string): Promise<Article> {
    return this.request<Article>(`/api/v1/authoring/articles/${encodeURIComponent(id)}`);
  }

  publishArticle(id: string): Promise<Article> {
    return this.request<Article>(`/api/v1/authoring/articles/${encodeURIComponent(id)}/publish`, {
      method: "POST",
    });
  }

  unpublishArticle(id: string): Promise<Article> {
    return this.request<Article>(`/api/v1/authoring/articles/${encodeURIComponent(id)}/unpublish`, {
      method: "POST",
    });
  }

  // ---- Redirects ----

  /**
   * Resolves a path to its redirect target, or null when none exists. The site calls this on a 404
   * to honor a moved slug with a 301 rather than serving a dead page (docs/SEO.md §4). A 404 from
   * the endpoint is the normal "no redirect" answer, so it maps to null rather than throwing.
   */
  async resolveRedirect(path: string): Promise<Redirect | null> {
    const query = new URLSearchParams({ from: path });
    try {
      return await this.request<Redirect>(`/api/v1/redirects?${query}`);
    } catch (error) {
      if (error instanceof ApiClientError && error.status === 404) return null;
      throw error;
    }
  }
}

export function createApiClient(options: ApiClientOptions): ApiClient {
  return new ApiClient(options);
}

export type { ApiResponse } from "@databro/types";
