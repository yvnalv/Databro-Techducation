import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiClientError, createApiClient } from "./index";

const ok = (data: unknown, meta?: unknown) =>
  new Response(JSON.stringify({ success: true, data, ...(meta ? { meta } : {}) }), { status: 200 });

const fail = (code: string, message: string, status: number) =>
  new Response(JSON.stringify({ success: false, error: { code, message } }), { status });

const realFetch = globalThis.fetch;
afterEach(() => {
  globalThis.fetch = realFetch;
  vi.restoreAllMocks();
});

describe("fetch binding", () => {
  /**
   * Regression test for a bug that only ever appeared in a browser.
   *
   * The client stores `globalThis.fetch` and later calls it as `this.fetchImpl(...)`, which makes
   * `this` the ApiClient. A browser's `fetch` requires the global as its receiver and throws
   * `TypeError: Illegal invocation`; Node's does not care. Every server-rendered call therefore
   * worked while the first real browser call — signing in to the CMS — failed.
   *
   * This fake reproduces the browser's rule, so the binding is now covered where it can be tested.
   */
  it("calls the global fetch with the global as its receiver", async () => {
    const strictGlobalFetch = function (this: unknown) {
      if (this !== globalThis && this !== undefined) {
        throw new TypeError("Illegal invocation");
      }
      return Promise.resolve(ok({ ok: true }));
    };

    globalThis.fetch = strictGlobalFetch as unknown as typeof globalThis.fetch;

    const client = createApiClient({ baseUrl: "https://api.example" });

    await expect(client.listCategories()).resolves.toEqual({ ok: true });
  });

  it("uses an injected fetch when one is supplied", async () => {
    const injected = vi.fn().mockResolvedValue(ok([]));
    const client = createApiClient({ baseUrl: "https://api.example", fetch: injected });

    await client.listTags();

    expect(injected).toHaveBeenCalledOnce();
    expect(String(injected.mock.calls[0][0])).toBe("https://api.example/api/v1/tags");
  });
});

describe("requests", () => {
  it("sends the bearer token when one is available", async () => {
    const fetchMock = vi.fn().mockResolvedValue(ok({ id: "1" }));
    const client = createApiClient({
      baseUrl: "https://api.example",
      getToken: () => "tok-123",
      fetch: fetchMock,
    });

    await client.me();

    const headers = fetchMock.mock.calls[0][1].headers as Headers;
    expect(headers.get("Authorization")).toBe("Bearer tok-123");
  });

  it("omits the header entirely when there is no token", async () => {
    const fetchMock = vi.fn().mockResolvedValue(ok([]));
    const client = createApiClient({ baseUrl: "https://api.example", fetch: fetchMock });

    await client.listTags();

    expect((fetchMock.mock.calls[0][1].headers as Headers).has("Authorization")).toBe(false);
  });

  it("surfaces an envelope failure as ApiClientError carrying code and status", async () => {
    const fetchMock = vi.fn().mockResolvedValue(fail("validation_failed", "Bad input", 400));
    const client = createApiClient({ baseUrl: "https://api.example", fetch: fetchMock });

    await expect(client.me()).rejects.toMatchObject({
      code: "validation_failed",
      status: 400,
    });
  });

  it("surfaces a non-JSON 401 as an unauthenticated error rather than a parse crash", async () => {
    // ASP.NET's auth middleware returns a bare 401 with no body. A fresh Response per call: a
    // body can only be read once.
    const fetchMock = vi.fn().mockImplementation(() => Promise.resolve(new Response("", { status: 401 })));
    const client = createApiClient({ baseUrl: "https://api.example", fetch: fetchMock });

    await expect(client.me()).rejects.toBeInstanceOf(ApiClientError);
    await expect(client.me()).rejects.toMatchObject({ code: "unauthenticated", status: 401 });
  });

  it("reads paging out of meta on list endpoints", async () => {
    const meta = { page: 2, pageSize: 20, total: 41, totalPages: 3 };
    const fetchMock = vi.fn().mockResolvedValue(ok([{ id: "a" }], meta));
    const client = createApiClient({ baseUrl: "https://api.example", fetch: fetchMock });

    const page = await client.listArticles({ page: 2 });

    expect(page.meta).toEqual(meta);
    expect(String(fetchMock.mock.calls[0][0])).toContain("page=2");
  });

  it("treats a 404 from the redirect lookup as 'no redirect', not an error", async () => {
    const fetchMock = vi.fn().mockResolvedValue(fail("not_found", "none", 404));
    const client = createApiClient({ baseUrl: "https://api.example", fetch: fetchMock });

    await expect(client.resolveRedirect("/articles/gone")).resolves.toBeNull();
  });
});

describe("search", () => {
  // Segmented since ADR-0014: the API returns one segment per module rather than one ranked page.
  const segmented = (segments: unknown[]) => ok({ query: "q", segments });

  it("encodes the query and passes the locale scope through", async () => {
    const fetchMock = vi.fn().mockResolvedValue(segmented([]));
    const client = createApiClient({ baseUrl: "https://api.example", fetch: fetchMock });

    await client.search({ q: "rag & agents", locale: "id", limit: 5 });

    const url = String(fetchMock.mock.calls[0][0]);
    expect(url).toContain("q=rag+%26+agents");
    expect(url).toContain("locale=id");
    expect(url).toContain("limit=5");
  });

  it("returns each module's segment separately", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      segmented([
        { kind: "courses", total: 1, matchMode: "exact", hits: [{ id: "c", path: "/courses/rag" }] },
        { kind: "articles", total: 9, matchMode: "exact", hits: [{ id: "a", path: "/articles/rag" }] },
      ]),
    );
    const client = createApiClient({ baseUrl: "https://api.example", fetch: fetchMock });

    const results = await client.search({ q: "rag" });

    expect(results.segments.map((s) => s.kind)).toEqual(["courses", "articles"]);
    // The true total, not the number of hits shown — segments are capped for display.
    expect(results.segments[1]?.total).toBe(9);
  });

  it("keeps match modes per segment rather than collapsing them", async () => {
    // Two modules can legitimately disagree, and one flag for both would misreport whichever lost.
    const fetchMock = vi.fn().mockResolvedValue(
      segmented([
        { kind: "courses", total: 1, matchMode: "exact", hits: [{ id: "c" }] },
        { kind: "articles", total: 2, matchMode: "fuzzy", hits: [{ id: "a" }] },
      ]),
    );
    const client = createApiClient({ baseUrl: "https://api.example", fetch: fetchMock });

    const results = await client.search({ q: "kubernettes" });

    expect(results.segments.map((s) => s.matchMode)).toEqual(["exact", "fuzzy"]);
  });
});
