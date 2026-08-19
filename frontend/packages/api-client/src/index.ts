// Typed client over the DataBro API. Framework-agnostic (uses the Fetch API), so both Nuxt apps
// can share it. It normalizes the success/failure envelope (see docs/API_SPEC.md).

import type {
  ApiResponse,
  Article,
  ArticleDraftInput,
  ArticleSummary,
  ArticleVersion,
  ArticleVersionSummary,
  AuthTokens,
  Category,
  CategoryWithAncestors,
  ContentDocument,
  Course,
  CourseSummary,
  Difficulty,
  Enrollment,
  LessonContent,
  LessonContentSummary,
  LessonPage,
  LearningPath,
  AuthoringQuiz,
  Quiz,
  QuizAttempt,
  QuizAttemptSummary,
  Paged,
  PageMeta,
  MediaAsset,
  Redirect,
  SearchResults,
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

    // `.bind` is load-bearing, not tidiness. Stored on the instance, `globalThis.fetch` would be
    // invoked as `this.fetchImpl(...)` — making `this` the ApiClient. Browsers require `fetch` to be
    // called on the global and throw `TypeError: Illegal invocation`; Node's fetch does not care.
    // That difference is why this survived every SSR path and only failed in the browser.
    this.fetchImpl = options.fetch ?? globalThis.fetch.bind(globalThis);
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
  // Only endpoints the API actually serves — a method that 404s at runtime is worse than a missing
  // one.

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

  /**
   * Search across every module, returned as one segment each (ADR-0014).
   *
   * Locale-scoped, because an index stems per locale and an English query cannot meaningfully rank
   * Indonesian text. Each segment carries its own `matchMode`, since two modules can legitimately
   * disagree about whether they had to fall back to fuzzy matching.
   */
  async search(params: { q: string; locale?: string; limit?: number }): Promise<SearchResults> {
    const query = new URLSearchParams({ q: params.q });
    if (params.locale) query.set("locale", params.locale);
    if (params.limit != null) query.set("limit", String(params.limit));

    // Segments rather than a page (ADR-0014): each module searches what it owns and the results
    // stay separate, because relevance scores from two corpora cannot be meaningfully merged.
    return this.request<SearchResults>(`/api/v1/search?${query}`);
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

  /**
   * Confirms an email address from the link in a verification message.
   *
   * Deliberately unauthenticated: the token in the link *is* the proof, and requiring a session
   * would mean a person who followed a link from their inbox had to sign in before they were
   * allowed to finish signing up.
   */
  confirmEmail(userId: string, token: string): Promise<Record<string, never>> {
    return this.json<Record<string, never>>("/api/v1/auth/confirm-email", "POST", { userId, token });
  }

  /**
   * Starts password recovery.
   *
   * Resolves whether or not the address belongs to an account — the API answers identically by
   * design, so a client cannot tell either, and must not imply it can. Say "if that address has an
   * account", never "sent".
   */
  forgotPassword(email: string): Promise<Record<string, never>> {
    return this.json<Record<string, never>>("/api/v1/auth/forgot-password", "POST", { email });
  }

  resetPassword(userId: string, token: string, password: string): Promise<Record<string, never>> {
    return this.json<Record<string, never>>("/api/v1/auth/reset-password", "POST", {
      userId,
      token,
      password,
    });
  }

  /** Re-sends the confirmation email. Same non-committal answer as {@link forgotPassword}. */
  resendConfirmation(email: string): Promise<Record<string, never>> {
    return this.json<Record<string, never>>("/api/v1/auth/resend-confirmation", "POST", { email });
  }

  /**
   * Revokes a refresh token server-side.
   *
   * Clearing cookies is not signing out: the refresh token stays valid for a fortnight, so a copy
   * taken off a shared machine outlives the sign-out that was meant to end it.
   */
  logout(refreshToken: string): Promise<Record<string, never>> {
    return this.json<Record<string, never>>("/api/v1/auth/logout", "POST", { refreshToken });
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

  createArticle(input: ArticleDraftInput): Promise<Article> {
    return this.request<Article>("/api/v1/authoring/articles", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(input),
    });
  }

  /**
   * Saves the draft. The API treats omitted taxonomy as "leave unchanged", so the editor always
   * sends both fields — otherwise clearing a category would be indistinguishable from not touching
   * it.
   */
  updateArticle(id: string, input: ArticleDraftInput): Promise<Article> {
    return this.request<Article>(`/api/v1/authoring/articles/${encodeURIComponent(id)}`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(input),
    });
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

  /** Schedules a future publish (CT-7). `scheduledFor` is an ISO timestamp and must be in the future. */
  scheduleArticle(id: string, scheduledFor: string): Promise<Article> {
    return this.request<Article>(`/api/v1/authoring/articles/${encodeURIComponent(id)}/schedule`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ scheduledFor }),
    });
  }

  /** Cancels a pending schedule, returning the article to draft. Leaves the draft untouched. */
  unscheduleArticle(id: string): Promise<Article> {
    return this.request<Article>(`/api/v1/authoring/articles/${encodeURIComponent(id)}/unschedule`, {
      method: "POST",
    });
  }

  // ---- Version history (CT-8) ----
  // Reading and restoring are draft operations behind `Content.Edit`, not publishing acts: a restore
  // copies a snapshot into the draft and changes nothing a reader sees until someone publishes.

  listArticleVersions(id: string): Promise<ArticleVersionSummary[]> {
    return this.request<ArticleVersionSummary[]>(
      `/api/v1/authoring/articles/${encodeURIComponent(id)}/versions`,
    );
  }

  getArticleVersion(id: string, version: number): Promise<ArticleVersion> {
    return this.request<ArticleVersion>(
      `/api/v1/authoring/articles/${encodeURIComponent(id)}/versions/${version}`,
    );
  }

  restoreArticleVersion(id: string, version: number): Promise<Article> {
    return this.request<Article>(
      `/api/v1/authoring/articles/${encodeURIComponent(id)}/versions/${version}/restore`,
      { method: "POST" },
    );
  }

  // ---- Taxonomy authoring (Taxonomy.Manage) ----
  // Slug changes are a *separate* endpoint from the general update on purpose: a term's slug is a
  // live public URL, so moving it is an explicit act the API pairs with a 301 (CT-3).

  createCategory(input: {
    name: string;
    slug?: string;
    parentId?: string | null;
    description?: string;
    order?: number;
  }): Promise<Category> {
    return this.request<Category>("/api/v1/authoring/categories", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(input),
    });
  }

  updateCategory(
    id: string,
    input: { name: string; description?: string; order?: number; parentId?: string | null },
  ): Promise<Category> {
    return this.request<Category>(`/api/v1/authoring/categories/${encodeURIComponent(id)}`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(input),
    });
  }

  changeCategorySlug(id: string, slug: string): Promise<Category> {
    return this.request<Category>(`/api/v1/authoring/categories/${encodeURIComponent(id)}/slug`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ slug }),
    });
  }

  /** Refused with a conflict while the category still classifies articles or has children (TX-2). */
  deleteCategory(id: string): Promise<unknown> {
    return this.request<unknown>(`/api/v1/authoring/categories/${encodeURIComponent(id)}`, {
      method: "DELETE",
    });
  }

  createTag(input: { name: string; slug?: string }): Promise<TaxonomyTerm> {
    return this.request<TaxonomyTerm>("/api/v1/authoring/tags", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(input),
    });
  }

  updateTag(id: string, input: { name: string }): Promise<TaxonomyTerm> {
    return this.request<TaxonomyTerm>(`/api/v1/authoring/tags/${encodeURIComponent(id)}`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(input),
    });
  }

  changeTagSlug(id: string, slug: string): Promise<TaxonomyTerm> {
    return this.request<TaxonomyTerm>(`/api/v1/authoring/tags/${encodeURIComponent(id)}/slug`, {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ slug }),
    });
  }

  deleteTag(id: string): Promise<unknown> {
    return this.request<unknown>(`/api/v1/authoring/tags/${encodeURIComponent(id)}`, {
      method: "DELETE",
    });
  }

  // ---- Learning: public read ----

  /**
   * Published courses, newest first. Offset-paged like the article listings, for the same reason:
   * these are indexable and a crawler needs stable page URLs it can enumerate.
   */
  async listCourses(params?: { page?: number; pageSize?: number }): Promise<Paged<CourseSummary>> {
    const query = new URLSearchParams();
    if (params?.page != null) query.set("page", String(params.page));
    if (params?.pageSize != null) query.set("pageSize", String(params.pageSize));

    const suffix = query.size > 0 ? `?${query}` : "";
    const { data, meta } = await this.envelope<CourseSummary[]>(`/api/v1/courses${suffix}`);

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

  /**
   * A published course with its curriculum. Lessons whose bodies are unpublished are omitted by the
   * API, so what arrives here is exactly what a learner may see (ADR-0013).
   */
  getCourse(slug: string): Promise<Course> {
    return this.request<Course>(`/api/v1/courses/${encodeURIComponent(slug)}`);
  }

  /** Published learning paths, each with its resolved course cards. */
  async listLearningPaths(params?: {
    page?: number;
    pageSize?: number;
  }): Promise<Paged<LearningPath>> {
    const query = new URLSearchParams();
    if (params?.page != null) query.set("page", String(params.page));
    if (params?.pageSize != null) query.set("pageSize", String(params.pageSize));

    const suffix = query.size > 0 ? `?${query}` : "";
    const { data, meta } = await this.envelope<LearningPath[]>(`/api/v1/learning-paths${suffix}`);

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

  getLearningPath(slug: string): Promise<LearningPath> {
    return this.request<LearningPath>(`/api/v1/learning-paths/${encodeURIComponent(slug)}`);
  }

  /**
   * One lesson of a published course, with its neighbours. 404s when either the course or the
   * lesson body is unpublished — the same rule the course page applies, so the two reads cannot
   * disagree about what a learner may see.
   */
  getLessonPage(courseSlug: string, lessonSlug: string): Promise<LessonPage> {
    return this.request<LessonPage>(
      `/api/v1/courses/${encodeURIComponent(courseSlug)}/lessons/${encodeURIComponent(lessonSlug)}`,
    );
  }

  // ---- Assessment: taking a quiz (ADR-0018) ----
  //
  // Nothing on this path carries the answer key. `Quiz` has no correctness field to deserialise
  // into, and `QuizAttempt.results` is empty until the attempt is submitted (AS-1, AS-2).

  /** The published quiz for a lesson, without answers. 404s when the lesson has none. */
  getLessonQuiz(lessonId: string): Promise<Quiz> {
    return this.request<Quiz>(`/api/v1/lessons/${encodeURIComponent(lessonId)}/quiz`);
  }

  /** Starts an attempt, or resumes the open one — a reload is not a decision to discard answers. */
  startAttempt(lessonId: string): Promise<QuizAttempt> {
    return this.json<QuizAttempt>(
      `/api/v1/lessons/${encodeURIComponent(lessonId)}/quiz/attempts`, "POST");
  }

  listAttempts(lessonId: string): Promise<QuizAttempt[]> {
    return this.request<QuizAttempt[]>(
      `/api/v1/lessons/${encodeURIComponent(lessonId)}/quiz/attempts`);
  }

  /**
   * Submits selections. Carries **no score** — scoring happens server-side from the stored answer
   * key (AS-3), so there is nothing here to fabricate.
   */
  submitAttempt(
    attemptId: string,
    answers: Record<string, string[]>,
  ): Promise<QuizAttempt> {
    return this.json<QuizAttempt>(
      `/api/v1/me/attempts/${encodeURIComponent(attemptId)}/submit`, "POST", { answers });
  }

  // ---- Assessment: authoring (Content.Edit / Content.Publish) ----

  async listAuthoringQuizzes(params?: {
    page?: number;
    pageSize?: number;
  }): Promise<Paged<AuthoringQuiz>> {
    const query = new URLSearchParams();
    if (params?.page != null) query.set("page", String(params.page));
    if (params?.pageSize != null) query.set("pageSize", String(params.pageSize));

    const suffix = query.size > 0 ? `?${query}` : "";
    const { data, meta } = await this.envelope<AuthoringQuiz[]>(
      `/api/v1/authoring/quizzes${suffix}`);

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

  getAuthoringQuiz(id: string): Promise<AuthoringQuiz> {
    return this.request<AuthoringQuiz>(`/api/v1/authoring/quizzes/${encodeURIComponent(id)}`);
  }

  /**
   * The quiz for a lesson, whatever its status. 404s when there is none.
   *
   * Exists so a curriculum builder can answer "does this lesson have a quiz" in one request when the
   * author clicks, rather than one request per lesson on load.
   */
  getAuthoringQuizForLesson(lessonId: string): Promise<AuthoringQuiz> {
    return this.request<AuthoringQuiz>(
      `/api/v1/authoring/quizzes/by-lesson/${encodeURIComponent(lessonId)}`);
  }

  createQuiz(input: { lessonId: string; title: string; passingScore?: number }): Promise<AuthoringQuiz> {
    return this.json<AuthoringQuiz>("/api/v1/authoring/quizzes", "POST", input);
  }

  updateQuiz(id: string, input: { title: string; passingScore: number }): Promise<AuthoringQuiz> {
    return this.json<AuthoringQuiz>(`/api/v1/authoring/quizzes/${encodeURIComponent(id)}`, "PATCH", input);
  }

  // Every mutation returns the whole quiz, so the builder replaces its state rather than patching.

  addQuestion(id: string, input: { prompt: string; type: string; points?: number }): Promise<AuthoringQuiz> {
    return this.json<AuthoringQuiz>(
      `/api/v1/authoring/quizzes/${encodeURIComponent(id)}/questions`, "POST", input);
  }

  updateQuestion(
    id: string,
    questionId: string,
    input: { prompt: string; points: number; explanation?: string | null },
  ): Promise<AuthoringQuiz> {
    return this.json<AuthoringQuiz>(
      `/api/v1/authoring/quizzes/${encodeURIComponent(id)}/questions/${encodeURIComponent(questionId)}`,
      "PATCH", input);
  }

  removeQuestion(id: string, questionId: string): Promise<AuthoringQuiz> {
    return this.json<AuthoringQuiz>(
      `/api/v1/authoring/quizzes/${encodeURIComponent(id)}/questions/${encodeURIComponent(questionId)}`,
      "DELETE");
  }

  addChoice(id: string, questionId: string, text: string): Promise<AuthoringQuiz> {
    return this.json<AuthoringQuiz>(
      `/api/v1/authoring/quizzes/${encodeURIComponent(id)}/questions/${encodeURIComponent(questionId)}/choices`,
      "POST", { text });
  }

  removeChoice(id: string, questionId: string, choiceId: string): Promise<AuthoringQuiz> {
    return this.json<AuthoringQuiz>(
      `/api/v1/authoring/quizzes/${encodeURIComponent(id)}/questions/${encodeURIComponent(questionId)}/choices/${encodeURIComponent(choiceId)}`,
      "DELETE");
  }

  /** Sets the answer key **as a whole** — the API refuses two correct answers on a single-choice question. */
  setCorrectChoices(id: string, questionId: string, correctChoiceIds: string[]): Promise<AuthoringQuiz> {
    return this.json<AuthoringQuiz>(
      `/api/v1/authoring/quizzes/${encodeURIComponent(id)}/questions/${encodeURIComponent(questionId)}/answer`,
      "PUT", { correctChoiceIds });
  }

  publishQuiz(id: string): Promise<AuthoringQuiz> {
    return this.json<AuthoringQuiz>(`/api/v1/authoring/quizzes/${encodeURIComponent(id)}/publish`, "POST");
  }

  unpublishQuiz(id: string): Promise<AuthoringQuiz> {
    return this.json<AuthoringQuiz>(`/api/v1/authoring/quizzes/${encodeURIComponent(id)}/unpublish`, "POST");
  }

  /** Submitted attempts at a quiz, newest first — the author's review screen (U-1). */
  listQuizAttempts(id: string): Promise<QuizAttemptSummary[]> {
    return this.request<QuizAttemptSummary[]>(
      `/api/v1/authoring/quizzes/${encodeURIComponent(id)}/attempts`);
  }

  // ---- Learning: the signed-in learner's own progress (LN-6 … LN-11) ----
  //
  // Addressed as `/me` throughout: the learner comes from the bearer token, never from an argument,
  // so there is no call shape here that can read or write someone else's progress (LN-8). That is
  // why none of these takes a user id.

  /** The dashboard: everything the learner has joined, most recently touched first. */
  async listMyEnrollments(params?: { page?: number; pageSize?: number }): Promise<Paged<Enrollment>> {
    const query = new URLSearchParams();
    if (params?.page != null) query.set("page", String(params.page));
    if (params?.pageSize != null) query.set("pageSize", String(params.pageSize));

    const suffix = query.size > 0 ? `?${query}` : "";
    const { data, meta } = await this.envelope<Enrollment[]>(`/api/v1/me/enrollments${suffix}`);

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

  /** Progress in one course. 404s when the learner is not enrolled. */
  getMyEnrollment(courseSlug: string): Promise<Enrollment> {
    return this.request<Enrollment>(`/api/v1/me/enrollments/${encodeURIComponent(courseSlug)}`);
  }

  /** Joins a course. Idempotent — a second call returns the existing enrollment (LN-9). */
  enrol(courseSlug: string): Promise<Enrollment> {
    return this.json<Enrollment>(`/api/v1/me/enrollments/${encodeURIComponent(courseSlug)}`, "POST");
  }

  /**
   * Moves the resume point. Distinct from {@link completeLesson} because opening a lesson and
   * finishing it are different claims.
   */
  visitLesson(courseSlug: string, lessonId: string): Promise<Enrollment> {
    return this.json<Enrollment>(
      `/api/v1/me/enrollments/${encodeURIComponent(courseSlug)}/lessons/${encodeURIComponent(lessonId)}/visit`,
      "POST",
    );
  }

  /** Marks a lesson finished. Idempotent; keeps the original timestamp (LN-10). */
  completeLesson(courseSlug: string, lessonId: string): Promise<Enrollment> {
    return this.json<Enrollment>(
      `/api/v1/me/enrollments/${encodeURIComponent(courseSlug)}/lessons/${encodeURIComponent(lessonId)}/complete`,
      "POST",
    );
  }

  /** Un-marks a lesson. Does not revoke a completed course (LN-6). */
  reopenLesson(courseSlug: string, lessonId: string): Promise<Enrollment> {
    return this.json<Enrollment>(
      `/api/v1/me/enrollments/${encodeURIComponent(courseSlug)}/lessons/${encodeURIComponent(lessonId)}/complete`,
      "DELETE",
    );
  }

  // ---- Learning paths: curation (Content.Edit) and publishing (Content.Publish) ----

  async listAuthoringLearningPaths(params?: {
    page?: number;
    pageSize?: number;
  }): Promise<Paged<LearningPath>> {
    const query = new URLSearchParams();
    if (params?.page != null) query.set("page", String(params.page));
    if (params?.pageSize != null) query.set("pageSize", String(params.pageSize));

    const suffix = query.size > 0 ? `?${query}` : "";
    const { data, meta } = await this.envelope<LearningPath[]>(
      `/api/v1/authoring/learning-paths${suffix}`,
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

  getAuthoringLearningPath(id: string): Promise<LearningPath> {
    return this.request<LearningPath>(`/api/v1/authoring/learning-paths/${encodeURIComponent(id)}`);
  }

  createLearningPath(input: {
    title: string;
    summary: string;
    slug?: string;
    difficulty?: Difficulty;
  }): Promise<LearningPath> {
    return this.json<LearningPath>("/api/v1/authoring/learning-paths", "POST", input);
  }

  updateLearningPath(
    id: string,
    input: { title: string; summary: string; difficulty?: Difficulty },
  ): Promise<LearningPath> {
    return this.json<LearningPath>(
      `/api/v1/authoring/learning-paths/${encodeURIComponent(id)}`,
      "PATCH",
      input,
    );
  }

  // Every mutation returns the whole path, so the builder replaces its state rather than
  // reconciling a patch against it — the same contract the course builder relies on.

  addCourseToPath(id: string, courseId: string): Promise<LearningPath> {
    return this.json<LearningPath>(
      `/api/v1/authoring/learning-paths/${encodeURIComponent(id)}/courses/${encodeURIComponent(courseId)}`,
      "POST",
    );
  }

  removeCourseFromPath(id: string, courseId: string): Promise<LearningPath> {
    return this.json<LearningPath>(
      `/api/v1/authoring/learning-paths/${encodeURIComponent(id)}/courses/${encodeURIComponent(courseId)}`,
      "DELETE",
    );
  }

  reorderPathCourses(id: string, orderedIds: string[]): Promise<LearningPath> {
    return this.json<LearningPath>(
      `/api/v1/authoring/learning-paths/${encodeURIComponent(id)}/courses/order`,
      "PUT",
      { orderedIds },
    );
  }

  publishLearningPath(id: string): Promise<LearningPath> {
    return this.json<LearningPath>(
      `/api/v1/authoring/learning-paths/${encodeURIComponent(id)}/publish`,
      "POST",
    );
  }

  unpublishLearningPath(id: string): Promise<LearningPath> {
    return this.json<LearningPath>(
      `/api/v1/authoring/learning-paths/${encodeURIComponent(id)}/unpublish`,
      "POST",
    );
  }

  // ---- Learning: curriculum authoring (ADR-0013) ----
  //
  // Structure sits behind Content.Edit and publishing behind Content.Publish, the same split
  // articles use — so an Author can build a curriculum but not put it live.

  async listAuthoringCourses(params?: { page?: number; pageSize?: number }): Promise<Paged<CourseSummary>> {
    const query = new URLSearchParams();
    if (params?.page != null) query.set("page", String(params.page));
    if (params?.pageSize != null) query.set("pageSize", String(params.pageSize));

    const suffix = query.size > 0 ? `?${query}` : "";
    const { data, meta } = await this.envelope<CourseSummary[]>(`/api/v1/authoring/courses${suffix}`);

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

  getAuthoringCourse(id: string): Promise<Course> {
    return this.request<Course>(`/api/v1/authoring/courses/${encodeURIComponent(id)}`);
  }

  createCourse(input: {
    title: string;
    summary: string;
    slug?: string;
    difficulty?: Difficulty;
  }): Promise<Course> {
    return this.json<Course>("/api/v1/authoring/courses", "POST", input);
  }

  updateCourse(
    id: string,
    input: { title: string; summary: string; difficulty?: Difficulty },
  ): Promise<Course> {
    return this.json<Course>(`/api/v1/authoring/courses/${encodeURIComponent(id)}`, "PATCH", input);
  }

  publishCourse(id: string): Promise<Course> {
    return this.request<Course>(`/api/v1/authoring/courses/${encodeURIComponent(id)}/publish`, {
      method: "POST",
    });
  }

  unpublishCourse(id: string): Promise<Course> {
    return this.request<Course>(`/api/v1/authoring/courses/${encodeURIComponent(id)}/unpublish`, {
      method: "POST",
    });
  }

  // ---- Curriculum structure. Every one of these returns the whole course, because they all
  // mutate one aggregate and the client should never have to reassemble it. ----

  addCourseModule(courseId: string, title: string): Promise<Course> {
    return this.json<Course>(`/api/v1/authoring/courses/${encodeURIComponent(courseId)}/modules`, "POST", { title });
  }

  updateCourseModule(
    courseId: string,
    moduleId: string,
    input: { title: string; summary?: string },
  ): Promise<Course> {
    return this.json<Course>(
      `/api/v1/authoring/courses/${encodeURIComponent(courseId)}/modules/${encodeURIComponent(moduleId)}`,
      "PATCH",
      input,
    );
  }

  removeCourseModule(courseId: string, moduleId: string): Promise<Course> {
    return this.request<Course>(
      `/api/v1/authoring/courses/${encodeURIComponent(courseId)}/modules/${encodeURIComponent(moduleId)}`,
      { method: "DELETE" },
    );
  }

  /**
   * Sends the whole desired order in one call. Not a move-per-row: a drag that half-applies leaves
   * the curriculum in an order nobody chose, and the API models this as one transaction against one
   * aggregate.
   */
  reorderCourseModules(courseId: string, orderedIds: string[]): Promise<Course> {
    return this.json<Course>(
      `/api/v1/authoring/courses/${encodeURIComponent(courseId)}/modules/order`,
      "PUT",
      { orderedIds },
    );
  }

  addCourseLesson(courseId: string, moduleId: string, contentUnitId: string): Promise<Course> {
    return this.json<Course>(
      `/api/v1/authoring/courses/${encodeURIComponent(courseId)}/modules/${encodeURIComponent(moduleId)}/lessons`,
      "POST",
      { contentUnitId },
    );
  }

  updateCourseLesson(
    courseId: string,
    moduleId: string,
    lessonId: string,
    input: {
      estimatedMinutes: number;
      difficulty?: Difficulty;
      objectives?: string[];
      prerequisiteLessonIds?: string[];
    },
  ): Promise<Course> {
    return this.json<Course>(
      `/api/v1/authoring/courses/${encodeURIComponent(courseId)}/modules/${encodeURIComponent(moduleId)}/lessons/${encodeURIComponent(lessonId)}`,
      "PATCH",
      input,
    );
  }

  removeCourseLesson(courseId: string, moduleId: string, lessonId: string): Promise<Course> {
    return this.request<Course>(
      `/api/v1/authoring/courses/${encodeURIComponent(courseId)}/modules/${encodeURIComponent(moduleId)}/lessons/${encodeURIComponent(lessonId)}`,
      { method: "DELETE" },
    );
  }

  reorderCourseLessons(courseId: string, moduleId: string, orderedIds: string[]): Promise<Course> {
    return this.json<Course>(
      `/api/v1/authoring/courses/${encodeURIComponent(courseId)}/modules/${encodeURIComponent(moduleId)}/lessons/order`,
      "PUT",
      { orderedIds },
    );
  }

  // ---- Lesson bodies (ADR-0012). Authoring only: there is no public endpoint, because a lesson is
  // reached through its course. ----

  async listLessonContent(params?: { page?: number; pageSize?: number }): Promise<Paged<LessonContentSummary>> {
    const query = new URLSearchParams();
    if (params?.page != null) query.set("page", String(params.page));
    if (params?.pageSize != null) query.set("pageSize", String(params.pageSize));

    const suffix = query.size > 0 ? `?${query}` : "";
    const { data, meta } = await this.envelope<LessonContentSummary[]>(`/api/v1/authoring/lessons${suffix}`);

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

  getLessonContent(id: string): Promise<LessonContent> {
    return this.request<LessonContent>(`/api/v1/authoring/lessons/${encodeURIComponent(id)}`);
  }

  createLessonContent(input: {
    title: string;
    summary: string;
    content: ContentDocument;
    slug?: string;
  }): Promise<LessonContent> {
    return this.json<LessonContent>("/api/v1/authoring/lessons", "POST", input);
  }

  updateLessonContent(
    id: string,
    input: { title: string; summary: string; content: ContentDocument },
  ): Promise<LessonContent> {
    return this.json<LessonContent>(`/api/v1/authoring/lessons/${encodeURIComponent(id)}`, "PATCH", input);
  }

  publishLessonContent(id: string): Promise<LessonContent> {
    return this.request<LessonContent>(`/api/v1/authoring/lessons/${encodeURIComponent(id)}/publish`, {
      method: "POST",
    });
  }

  unpublishLessonContent(id: string): Promise<LessonContent> {
    return this.request<LessonContent>(`/api/v1/authoring/lessons/${encodeURIComponent(id)}/unpublish`, {
      method: "POST",
    });
  }

  listLessonContentVersions(id: string): Promise<ArticleVersionSummary[]> {
    return this.request<ArticleVersionSummary[]>(
      `/api/v1/authoring/lessons/${encodeURIComponent(id)}/versions`,
    );
  }

  restoreLessonContentVersion(id: string, version: number): Promise<LessonContent> {
    return this.request<LessonContent>(
      `/api/v1/authoring/lessons/${encodeURIComponent(id)}/versions/${version}/restore`,
      { method: "POST" },
    );
  }

  /**
   * JSON body helper — these differ only in verb and payload, and repeating the ceremony hid that.
   *
   * `body` is optional because several progress endpoints carry their whole request in the URL. They
   * are sent with no body and no `Content-Type` rather than an empty `{}`: declaring a JSON body and
   * then not having one is the sort of small dishonesty a proxy or a strict server is entitled to
   * reject.
   */
  private json<T>(path: string, method: string, body?: unknown): Promise<T> {
    return this.request<T>(path, {
      method,
      ...(body === undefined
        ? {}
        : { headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) }),
    });
  }

  // ---- Media (Media.Upload; ADR-0011) ----

  /**
   * Uploads an image.
   *
   * `Content-Type` is deliberately **not** set: the browser must set it itself so it can append the
   * multipart boundary, and setting it by hand produces a body the server cannot parse. The API
   * ignores the declared type anyway and identifies the file by its magic bytes.
   *
   * Resolves as soon as the original is stored. `processingStatus` is `pending` until the variant
   * job finishes, so a caller that needs a `srcset` polls {@link getMediaAsset}.
   */
  uploadMedia(file: File, altText?: string): Promise<MediaAsset> {
    const form = new FormData();
    form.append("file", file);
    if (altText) form.append("altText", altText);

    return this.request<MediaAsset>("/api/v1/media", { method: "POST", body: form });
  }

  async listMedia(params?: { page?: number; pageSize?: number }): Promise<Paged<MediaAsset>> {
    const query = new URLSearchParams();
    if (params?.page != null) query.set("page", String(params.page));
    if (params?.pageSize != null) query.set("pageSize", String(params.pageSize));

    const suffix = query.size > 0 ? `?${query}` : "";
    const { data, meta } = await this.envelope<MediaAsset[]>(`/api/v1/media${suffix}`);

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

  getMediaAsset(id: string): Promise<MediaAsset> {
    return this.request<MediaAsset>(`/api/v1/media/${encodeURIComponent(id)}`);
  }

  updateMedia(id: string, altText: string): Promise<MediaAsset> {
    return this.request<MediaAsset>(`/api/v1/media/${encodeURIComponent(id)}`, {
      method: "PATCH",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ altText }),
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
