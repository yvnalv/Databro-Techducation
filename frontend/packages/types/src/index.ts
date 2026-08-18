// Shared types for the DataBro frontend. These mirror the backend API contracts
// (see backend docs/API_SPEC.md and docs/CONTENT_MODEL.md). Keep in lockstep with the backend.

// ---- API response envelope (docs/API_SPEC.md, docs/ERROR_HANDLING.md) ----

export interface ApiSuccess<TData> {
  success: true;
  data: TData;
  meta?: Record<string, unknown>;
}

export interface ApiFieldError {
  field: string;
  message: string;
}

export interface ApiError {
  code: string;
  message: string;
  details?: ApiFieldError[];
  traceId?: string;
}

export interface ApiFailure {
  success: false;
  error: ApiError;
}

export type ApiResponse<TData> = ApiSuccess<TData> | ApiFailure;

// ---- Inline rich text (ADR-0009) ----
//
// Shaped like ProseMirror's document model, which is what Tiptap uses natively, so the CMS editor
// needs no translation layer between what it edits and what is stored.
//
// Marks are never rendered as HTML strings: each maps to an element, and `link.href` is
// scheme-checked exactly like an embed URL.

export interface BoldMark {
  type: "bold";
}
export interface ItalicMark {
  type: "italic";
}
export interface CodeMark {
  type: "code";
}
export interface StrikeMark {
  type: "strike";
}
export interface LinkMark {
  type: "link";
  attrs: { href: string; title?: string };
}

export type InlineMark = BoldMark | ItalicMark | CodeMark | StrikeMark | LinkMark;
export type InlineMarkType = InlineMark["type"];

export interface TextNode {
  type: "text";
  text: string;
  marks?: InlineMark[];
}

/** Atomic inline node — has no text of its own. */
export interface MathInlineNode {
  type: "mathInline";
  attrs: { latex: string };
}

export type InlineNode = TextNode | MathInlineNode;

/**
 * Inline content, plus a legacy escape hatch.
 *
 * Documents written before ADR-0009 carry a plain `text` string. There is no production content to
 * migrate, so renderers accept either shape rather than a data migration being written. `text` is a
 * compatibility shim, not a supported authoring shape.
 */
export type RichText = InlineNode[];

// ---- Content blocks (docs/CONTENT_MODEL.md) ----

export type BlockType =
  | "heading"
  | "paragraph"
  | "code"
  | "callout"
  | "image"
  | "quote"
  | "list"
  | "divider"
  | "embed"
  | "table"
  | "math";

export interface BlockBase<TType extends BlockType, TData> {
  id: string;
  type: TType;
  data: TData;
}

/** A table cell is inline content only — blocks do not nest into cells. */
export type TableCell = RichText;

/**
 * A list item: inline content, optionally followed by nested blocks so a tutorial step can carry a
 * code sample (ADR-0009). Nesting is depth-capped at render time.
 */
export interface ListItem {
  content: RichText;
  blocks?: ContentBlock[];
}

// `heading` stays plain text on purpose: emphasis or links inside a heading hurt both the document
// outline and anchor generation.
export type HeadingBlock = BlockBase<"heading", { level: 2 | 3 | 4; text: string }>;

export type ParagraphBlock = BlockBase<"paragraph", { content?: RichText; text?: string }>;

export type CodeBlock = BlockBase<
  "code",
  {
    language: string;
    code: string;
    runnable?: boolean;
    filename?: string;
    /** Result of running the sample — the "run this, get that" teaching pattern. */
    output?: string;
  }
>;

export type CalloutBlock = BlockBase<
  "callout",
  { variant: "info" | "tip" | "warning" | "danger"; content?: RichText; text?: string }
>;

export type ImageBlock = BlockBase<"image", { mediaId: string; alt: string; caption?: string }>;

export type QuoteBlock = BlockBase<
  "quote",
  { content?: RichText; text?: string; attribution?: string }
>;

export type ListBlock = BlockBase<
  "list",
  { ordered: boolean; items: Array<ListItem | string> }
>;

export type DividerBlock = BlockBase<"divider", Record<string, never>>;
export type EmbedBlock = BlockBase<"embed", { provider: string; url: string }>;

export type TableBlock = BlockBase<
  "table",
  { headers: Array<TableCell | string>; rows: Array<Array<TableCell | string>> }
>;

/** Block-level LaTeX, rendered by KaTeX. Inline math is the `mathInline` node. */
export type MathBlock = BlockBase<"math", { latex: string }>;

export type ContentBlock =
  | HeadingBlock
  | ParagraphBlock
  | CodeBlock
  | CalloutBlock
  | ImageBlock
  | QuoteBlock
  | ListBlock
  | DividerBlock
  | EmbedBlock
  | TableBlock
  | MathBlock;

export interface ContentDocument {
  version: number;
  blocks: ContentBlock[];
}

// ---- Article DTOs (docs/API_SPEC.md, docs/DATABASE.md) ----

export type Visibility = "public" | "premium";
export type ArticleStatus =
  | "draft"
  | "scheduled"
  | "published"
  | "unpublished"
  | "archived";

export interface SeoMetadata {
  metaTitle?: string;
  metaDescription?: string;
  canonicalUrl?: string;
  robots?: string;
  ogImageMediaId?: string;
}

/**
 * Resolved byline. The backend stores only an author id and resolves the display name through
 * the cross-module IUserDirectory contract (ADR-0008), so this is null when the author can no
 * longer be resolved - render a localized fallback rather than assuming it is present.
 */
export interface AuthorSummary {
  id: string;
  displayName: string;
  avatarUrl?: string | null;
}

/**
 * Byline plus bio. Only the article *detail* response carries it — a list of summaries has no use
 * for a bio per item, and that is the cached, read-heavy public path.
 */
export interface AuthorProfile extends AuthorSummary {
  bio?: string | null;
}

// ---- Taxonomy (docs/BUSINESS_RULES.md TX-1 … TX-3, CT-11) ----

/** A category or tag as the read surface exposes it. */
export interface TaxonomyTerm {
  id: string;
  slug: string;
  name: string;
}

export interface Category extends TaxonomyTerm {
  description?: string | null;
  parentId?: string | null;
  order: number;
  /** Published articles only — a tile must never promise drafts a reader cannot open. */
  articleCount: number;
}

/** A category plus its ancestors, root first — the breadcrumb trail for a category page. */
export interface CategoryWithAncestors {
  category: Category;
  ancestors: Category[];
}

export interface ArticleSummary {
  id: string;
  slug: string;
  title: string;
  summary: string;
  status: ArticleStatus;
  visibility: Visibility;
  locale: string;
  author: AuthorSummary | null;
  readingTimeMinutes: number;
  publishedAt?: string;
  /** Null when uncategorised, or when the category was deleted (CT-11: at most one). */
  category: TaxonomyTerm | null;
  /** Excludes deleted tags; empty rather than absent when untagged. */
  tags: TaxonomyTerm[];
}

// ---- Media (ADR-0011) ----

export type MediaProcessingStatus = "pending" | "ready" | "failed";

export interface MediaVariant {
  /** The variant's width as a string — "640". Also its name in storage. */
  name: string;
  url: string;
  width: number;
  height: number;
}

/**
 * A media reference resolved for rendering.
 *
 * `variants` is empty while an asset is still processing: the original renders at full size and the
 * `srcset` is simply omitted, never a broken image.
 */
export interface MediaRef {
  url: string;
  altText: string;
  width: number;
  height: number;
  variants: MediaVariant[];
}

/** The full asset as the CMS library sees it. The public site only ever needs {@link MediaRef}. */
export interface MediaAsset extends MediaRef {
  id: string;
  fileName: string;
  mimeType: string;
  byteSize: number;
  processingStatus: MediaProcessingStatus;
  /** Why variant generation failed. Null unless `processingStatus` is `failed`. */
  processingError: string | null;
  createdAt: string;
}

// ---- Learning (ADR-0012, ADR-0013) ----

export type CourseStatus = "draft" | "published" | "unpublished";
export type Difficulty = "beginner" | "intermediate" | "advanced";

/**
 * A lesson body — the same content engine an article uses, with none of the standalone-page
 * furniture: no author, taxonomy, SEO or locale. Reached through its course, never on its own.
 */
export interface LessonContent {
  id: string;
  slug: string;
  title: string;
  summary: string;
  status: ArticleStatus;
  currentVersion: number;
  readingTimeMinutes: number;
  content: ContentDocument;
  publishedAt?: string;
}

/** A row in the picker when attaching a body to a course. No blocks — a list is a list. */
export interface LessonContentSummary {
  id: string;
  slug: string;
  title: string;
  summary: string;
  status: ArticleStatus;
  readingTimeMinutes: number;
  publishedAt?: string;
}

/**
 * A lesson in a curriculum: its position and learning metadata, joined to the body Content owns.
 *
 * `isPublished` refers to the *body*. A course can go live before every lesson is written
 * (ADR-0013), so the authoring view shows unpublished ones — with empty `blocks` — and the public
 * course page omits them entirely.
 */
export interface CourseLesson {
  id: string;
  contentUnitId: string;
  slug: string;
  title: string;
  summary: string;
  order: number;
  estimatedMinutes: number;
  difficulty: Difficulty;
  objectives: string[];
  prerequisiteLessonIds: string[];
  isPublished: boolean;
  blocks: ContentBlock[];
}

export interface CourseModule {
  id: string;
  title: string;
  summary: string;
  order: number;
  lessons: CourseLesson[];
}

export interface Course {
  id: string;
  slug: string;
  title: string;
  summary: string;
  status: CourseStatus;
  difficulty: Difficulty;
  lessonCount: number;
  /** Summed from the lessons, so it cannot drift from the curriculum it describes. */
  estimatedMinutes: number;
  publishedAt?: string;
  modules: CourseModule[];
}

/**
 * A curated sequence of courses — "Become an LLM Engineer".
 *
 * `courses` are resolved cards in the curated order, not ids: the sequence is the point of a path.
 * On the public read, courses that are not published are **absent** — a path may be curated ahead of
 * the courses in it, the same way a course may go live before every lesson is written.
 */
export interface LearningPath {
  id: string;
  slug: string;
  title: string;
  summary: string;
  status: CourseStatus;
  difficulty: Difficulty;
  publishedAt?: string;
  courses: CourseSummary[];
}

/** A neighbouring lesson, as a prev/next link needs it. No body — it is a link. */
export interface LessonLink {
  id: string;
  slug: string;
  title: string;
}

/**
 * One lesson as its own page: the body, plus enough curriculum around it to navigate.
 *
 * Fetched separately from {@link Course} rather than picked out of it — the course response carries
 * every body it has, which is right for rendering a whole curriculum and wrong as the cost of
 * reading lesson three of fifty.
 */
export interface LessonPage {
  courseId: string;
  courseSlug: string;
  courseTitle: string;
  moduleTitle: string;
  /** Position in the whole course, 1-based. Not the position within its module. */
  position: number;
  totalLessons: number;
  lesson: CourseLesson;
  /** Neighbours cross module boundaries: a learner moves through one sequence. */
  previous?: LessonLink | null;
  next?: LessonLink | null;
}

/** A course card. No curriculum, because a card does not render one. */
export interface CourseSummary {
  id: string;
  slug: string;
  title: string;
  summary: string;
  status: CourseStatus;
  difficulty: Difficulty;
  lessonCount: number;
  estimatedMinutes: number;
  publishedAt?: string;
}

// ---- Assessment (ADR-0018, AS-1 … AS-9) ----

export type QuestionType = "singlechoice" | "multiplechoice" | "truefalse";
export type QuizStatus = "draft" | "published" | "unpublished";

/**
 * A choice as the person answering sees it.
 *
 * **There is no correctness field, and that is the design** (AS-1): the API has a separate shape for
 * authoring, so this one has nothing to leak into. Do not add one here.
 */
export interface QuizChoice {
  id: string;
  text: string;
}

export interface QuizQuestion {
  id: string;
  prompt: string;
  type: QuestionType;
  points: number;
  choices: QuizChoice[];
}

/** A quiz as a learner receives it. Published only, and carrying no answers. */
export interface Quiz {
  id: string;
  lessonId: string;
  title: string;
  passingScore: number;
  totalPoints: number;
  questions: QuizQuestion[];
}

/** The authoring counterpart. Carries the answer key; never fetched by learner-facing code. */
export interface AuthoringQuizChoice extends QuizChoice {
  isCorrect: boolean;
}

export interface AuthoringQuizQuestion {
  id: string;
  prompt: string;
  type: QuestionType;
  points: number;
  explanation?: string | null;
  choices: AuthoringQuizChoice[];
}

export interface AuthoringQuiz {
  id: string;
  lessonId: string;
  title: string;
  status: QuizStatus;
  passingScore: number;
  totalPoints: number;
  publishedAt?: string;
  questions: AuthoringQuizQuestion[];
}

/**
 * One question's outcome. Present only on a **submitted** attempt (AS-2) — before that the answers
 * would simply be the answers.
 */
export interface AttemptAnswerResult {
  questionId: string;
  selectedChoiceIds: string[];
  correctChoiceIds: string[];
  pointsEarned: number;
  explanation?: string | null;
}

export interface QuizAttempt {
  id: string;
  quizId: string;
  startedAt: string;
  submittedAt?: string | null;
  score: number;
  totalPoints: number;
  percentage: number;
  passed: boolean;
  /** Empty until submitted. */
  results: AttemptAnswerResult[];
}

// ---- Enrollment & progress (LN-6 … LN-11) ----

/**
 * A learner's progress in one course.
 *
 * `completedLessons` can sit **below** `totalLessons` on a course that has `completedAt` set: the
 * course grew after the learner finished it, and completion is a moment that is never revoked
 * (LN-6). Render both rather than trying to reconcile them.
 */
export interface Enrollment {
  id: string;
  courseId: string;
  courseSlug: string;
  courseTitle: string;
  enrolledAt: string;
  /** Set once, never cleared. */
  completedAt?: string | null;
  /** The lesson last *opened*, which is not the lesson last completed. */
  lastLessonId?: string | null;
  /**
   * The resume point as a URL segment. Null when that lesson has since been unpublished or removed
   * — a Resume button is only worth offering if it leads somewhere, and the id alone cannot say.
   */
  lastLessonSlug?: string | null;
  lastAccessedAt?: string | null;
  /** Published lessons right now — moves as the curriculum grows. */
  totalLessons: number;
  completedLessons: number;
  /** Derived per request and capped at 100. */
  percentComplete: number;
  completedLessonIds: string[];
}

// ---- Version history (CT-8) ----

/**
 * One entry in an article's history. Without `content` on purpose: the list is a navigation aid, and
 * shipping every snapshot's body would make it the heaviest response in the CMS.
 */
export interface ArticleVersionSummary {
  version: number;
  title: string;
  summary: string;
  readingTimeMinutes: number;
  createdAt: string;
  /** True for the version currently published — what a reader sees right now. */
  isCurrent: boolean;
}

/** A single snapshot with its content, for previewing before a restore. */
export interface ArticleVersion {
  version: number;
  title: string;
  summary: string;
  createdAt: string;
  isCurrent: boolean;
  content: ContentDocument;
}

export interface Article extends ArticleSummary {
  /** Narrows the summary's author to the richer detail shape. */
  author: AuthorProfile | null;
  content: ContentDocument;
  seo: SeoMetadata;
  currentVersion: number;
  scheduledFor?: string;
  /**
   * Every media id this article references — image blocks and `og:image` — resolved to URLs and
   * keyed by id. Shipped with the article so the renderer needs no second request. An id missing
   * from the map is one whose asset is gone; render a placeholder, not a broken image.
   */
  media: Record<string, MediaRef>;
}

/**
 * What the editor sends when creating or saving an article.
 *
 * `slug` is create-only: it is immutable once published (CT-2) and moves through the dedicated
 * slug-change endpoint, which pairs it with a 301 (CT-3).
 */
export interface ArticleDraftInput {
  title: string;
  summary: string;
  content: ContentDocument;
  slug?: string;
  visibility?: Visibility;
  locale?: string;
  seo?: SeoMetadata;
  categoryId?: string | null;
  tagIds?: string[];
}

// ---- Auth (docs/API_SPEC.md §5 Auth, docs/SECURITY.md §1) ----

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
  expiresInSeconds: number;
}

export interface UserProfile {
  id: string;
  email: string;
  displayName: string;
  emailConfirmed: boolean;
  /** Role names. Permissions live in the JWT; the UI branches on roles for coarse affordances. */
  roles: string[];
}

// ---- Paging ----

/**
 * Offset paging for indexable listings. docs/API_SPEC.md §3 prefers cursors for public lists, but
 * category and tag pages exist to be crawled and a cursor has no stable URL a crawler can
 * enumerate. See docs/SEO.md.
 */
export interface PageMeta {
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
}

export interface Paged<T> {
  items: T[];
  meta: PageMeta;
}

/**
 * How a set of search results was matched (ADR-0010).
 *
 * `fuzzy` means full-text found nothing and the API fell back to trigram similarity over titles.
 * The UI must say so — presenting approximate results as if they were exact is how a search box
 * loses a reader's trust.
 */
export type SearchMatchMode = "exact" | "fuzzy";

/** One result. `path` is site-relative and locale-agnostic; the owning module decides its shape. */
export interface SearchHit {
  id: string;
  slug: string;
  path: string;
  title: string;
  summary: string;
}

/**
 * Results from one module (ADR-0014).
 *
 * Segments are never merged into a single ranked list: relevance scores from two corpora are not
 * comparable, so any blend would be a fabricated ordering. `matchMode` is per segment because two
 * modules can legitimately disagree — an exact course hit beside a typo-corrected article one.
 */
export interface SearchSegment {
  /** `courses`, `articles`. Stable — the UI keys its section headings off it. */
  kind: string;
  hits: SearchHit[];
  /** The true total, which may exceed `hits.length`: segments are capped for display. */
  total: number;
  matchMode: SearchMatchMode;
}

export interface SearchResults {
  query: string;
  /** Ordered by the API: courses before articles. */
  segments: SearchSegment[];
}

/**
 * A resolved URL redirect (docs/SEO.md §4). Returned by the redirect-lookup endpoint when a moved
 * slug should resolve to a `statusCode` (301) redirect rather than a 404.
 */
export interface Redirect {
  fromPath: string;
  toPath: string;
  statusCode: number;
}
