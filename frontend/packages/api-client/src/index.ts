// Typed client over the DataBro API. Framework-agnostic (uses the Fetch API), so both Nuxt apps
// can share it. It normalizes the success/failure envelope (see docs/API_SPEC.md).

import type {
  ApiResponse,
  Article,
  ArticleSummary,
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
    const headers = new Headers(init?.headers);
    headers.set("Accept", "application/json");
    const token = this.getToken?.();
    if (token) headers.set("Authorization", `Bearer ${token}`);

    const response = await this.fetchImpl(`${this.baseUrl}${path}`, { ...init, headers });
    const body = (await response.json()) as ApiResponse<TData>;

    if (!body.success) {
      throw new ApiClientError(
        body.error.code,
        body.error.message,
        response.status,
        body.error.details,
        body.error.traceId,
      );
    }
    return body.data;
  }

  // ---- Public read surface (Phase 1) ----

  listArticles(params?: { category?: string; tag?: string; q?: string }): Promise<ArticleSummary[]> {
    const query = new URLSearchParams(
      Object.entries(params ?? {}).filter(([, v]) => v != null) as [string, string][],
    ).toString();
    return this.request<ArticleSummary[]>(`/api/v1/articles${query ? `?${query}` : ""}`);
  }

  getArticle(slug: string): Promise<Article> {
    return this.request<Article>(`/api/v1/articles/${encodeURIComponent(slug)}`);
  }

  search(q: string): Promise<ArticleSummary[]> {
    return this.request<ArticleSummary[]>(`/api/v1/search?q=${encodeURIComponent(q)}`);
  }
}

export function createApiClient(options: ApiClientOptions): ApiClient {
  return new ApiClient(options);
}

export type { ApiResponse } from "@databro/types";
