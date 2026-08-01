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

export interface Article extends ArticleSummary {
  /** Narrows the summary's author to the richer detail shape. */
  author: AuthorProfile | null;
  content: ContentDocument;
  seo: SeoMetadata;
  currentVersion: number;
  scheduledFor?: string;
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
