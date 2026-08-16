import { describe, expect, it } from "vitest";
import { mount } from "@vue/test-utils";
import type { ContentDocument } from "@databro/types";
import ContentRenderer from "./ContentRenderer.vue";
import { buildToc, headingAnchor } from "./headings";

const heading = (level: number, text: string, id = `h${text.length}`) =>
  ({ id, type: "heading", data: { level, text } }) as never;

describe("headingAnchor", () => {
  it("slugifies to a URL-safe id", () => {
    expect(headingAnchor("A Minimal Pipeline")).toBe("a-minimal-pipeline");
  });

  it("strips punctuation and collapses whitespace", () => {
    expect(headingAnchor("What RAG *Actually* Solves?  Really!")).toBe(
      "what-rag-actually-solves-really",
    );
  });

  it("returns empty for text with nothing slugifiable", () => {
    expect(headingAnchor("!!! ???")).toBe("");
    expect(headingAnchor("")).toBe("");
  });
});

// The contract that matters: the id the TOC links to must be the id the renderer stamps. Two
// implementations would drift on the first odd heading and every link would scroll nowhere.
describe("anchor / renderer agreement", () => {
  it("matches the id rendered onto the heading element", () => {
    const text = "Chunking: Where It *Actually* Goes Wrong";
    const doc: ContentDocument = { version: 1, blocks: [heading(2, text)] };

    const wrapper = mount(ContentRenderer, { props: { document: doc } });

    expect(wrapper.get("h2").attributes("id")).toBe(headingAnchor(text));
    expect(buildToc(doc)[0].id).toBe(wrapper.get("h2").attributes("id"));
  });
});

describe("buildToc", () => {
  const doc: ContentDocument = {
    version: 1,
    blocks: [
      heading(2, "First Section", "a"),
      { id: "p", type: "paragraph", data: { text: "body" } } as never,
      heading(3, "A Subsection", "b"),
      heading(4, "Too Deep", "c"),
      heading(2, "Second Section", "d"),
    ],
  };

  it("collects h2 and h3 in document order", () => {
    expect(buildToc(doc).map((e) => e.text)).toEqual([
      "First Section",
      "A Subsection",
      "Second Section",
    ]);
  });

  it("excludes h4, which outlines rather than navigates", () => {
    expect(buildToc(doc).some((e) => e.text === "Too Deep")).toBe(false);
  });

  it("records the level so the list can indent", () => {
    expect(buildToc(doc).map((e) => e.level)).toEqual([2, 3, 2]);
  });

  it("drops headings that would produce a dead link", () => {
    const bad: ContentDocument = { version: 1, blocks: [heading(2, "???")] };
    expect(buildToc(bad)).toEqual([]);
  });

  it("returns empty for a missing or blockless document", () => {
    expect(buildToc(undefined)).toEqual([]);
    expect(buildToc({ version: 1, blocks: [] })).toEqual([]);
  });
});
