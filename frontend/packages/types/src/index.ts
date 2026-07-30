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
  | "table";

export interface BlockBase<TType extends BlockType, TData> {
  id: string;
  type: TType;
  data: TData;
}

export type HeadingBlock = BlockBase<"heading", { level: 2 | 3 | 4; text: string }>;
export type ParagraphBlock = BlockBase<"paragraph", { text: string; marks?: unknown[] }>;
export type CodeBlock = BlockBase<
  "code",
  { language: string; code: string; runnable?: boolean; filename?: string }
>;
export type CalloutBlock = BlockBase<
  "callout",
  { variant: "info" | "tip" | "warning" | "danger"; text: string }
>;
export type ImageBlock = BlockBase<"image", { mediaId: string; alt: string; caption?: string }>;
export type QuoteBlock = BlockBase<"quote", { text: string; attribution?: string }>;
export type ListBlock = BlockBase<"list", { ordered: boolean; items: string[] }>;
export type DividerBlock = BlockBase<"divider", Record<string, never>>;
export type EmbedBlock = BlockBase<"embed", { provider: string; url: string }>;
export type TableBlock = BlockBase<"table", { headers: string[]; rows: string[][] }>;

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
  | TableBlock;

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
