import { describe, expect, it } from "vitest";
import { mount } from "@vue/test-utils";
import type { ContentDocument, RichText } from "@databro/types";
import ContentRenderer from "./ContentRenderer.vue";
import { safeHref, toRichText, richTextToPlain } from "./rich-text";

function paragraph(content: RichText): ContentDocument {
  return { version: 1, blocks: [{ id: "p", type: "paragraph", data: { content } }] };
}

const text = (value: string, marks?: unknown[]) =>
  ({ type: "text", text: value, ...(marks ? { marks } : {}) }) as never;

// ADR-0009. Inline content is author-supplied and reaches the renderer straight out of JSONB, so
// the marks-to-elements mapping is a security boundary, not just formatting.
describe("inline rich text", () => {
  it("renders bold, italic, code and strike as elements", () => {
    const wrapper = mount(ContentRenderer, {
      props: {
        document: paragraph([
          text("b", [{ type: "bold" }]),
          text("i", [{ type: "italic" }]),
          text("c", [{ type: "code" }]),
          text("s", [{ type: "strike" }]),
        ]),
      },
    });

    expect(wrapper.get("strong").text()).toBe("b");
    expect(wrapper.get("em").text()).toBe("i");
    expect(wrapper.get("code").text()).toBe("c");
    expect(wrapper.get("s").text()).toBe("s");
  });

  it("nests marks, outermost last", () => {
    const wrapper = mount(ContentRenderer, {
      props: {
        document: paragraph([
          text("both", [{ type: "code" }, { type: "bold" }]),
        ]),
      },
    });

    // bold is applied last, so <strong> wraps <code>.
    expect(wrapper.get("strong > code").text()).toBe("both");
  });

  it("renders a link with rel protecting the referrer and ranking", () => {
    const wrapper = mount(ContentRenderer, {
      props: {
        document: paragraph([
          text("docs", [{ type: "link", attrs: { href: "https://example.com/x" } }]),
        ]),
      },
    });

    const anchor = wrapper.get("a");
    expect(anchor.attributes("href")).toBe("https://example.com/x");
    expect(anchor.attributes("rel")).toContain("noopener");
    expect(anchor.attributes("rel")).toContain("nofollow");
  });

  it("allows site-relative links so articles can link to each other", () => {
    const wrapper = mount(ContentRenderer, {
      props: {
        document: paragraph([
          text("related", [{ type: "link", attrs: { href: "/articles/other" } }]),
        ]),
      },
    });

    expect(wrapper.get("a").attributes("href")).toBe("/articles/other");
  });

  it("drops the anchor for a dangerous href but keeps the text", () => {
    for (const href of ["javascript:alert(1)", "data:text/html,<script>", "//evil.example"]) {
      const wrapper = mount(ContentRenderer, {
        props: { document: paragraph([text("click me", [{ type: "link", attrs: { href } }])]) },
      });

      expect(wrapper.find("a").exists()).toBe(false);
      // Losing the link must not lose the prose.
      expect(wrapper.text()).toContain("click me");
    }
  });

  it("never treats inline text as markup", () => {
    const hostile = '<img src=x onerror="alert(1)">';
    const wrapper = mount(ContentRenderer, {
      props: { document: paragraph([text(hostile, [{ type: "bold" }])]) },
    });

    expect(wrapper.find("img").exists()).toBe(false);
    expect(wrapper.get("strong").text()).toBe(hostile);
  });

  it("ignores an unknown mark rather than dropping the text", () => {
    const wrapper = mount(ContentRenderer, {
      props: { document: paragraph([text("kept", [{ type: "rainbow" }])]) },
    });

    expect(wrapper.text()).toContain("kept");
  });
});

describe("toRichText", () => {
  it("passes through a node array", () => {
    expect(toRichText([{ type: "text", text: "a" }])).toHaveLength(1);
  });

  it("wraps a legacy plain string", () => {
    expect(toRichText(undefined, "legacy")).toEqual([{ type: "text", text: "legacy" }]);
  });

  it("returns empty for missing or malformed content rather than throwing", () => {
    expect(toRichText(undefined, undefined)).toEqual([]);
    expect(toRichText(null, "")).toEqual([]);
    expect(toRichText([{ type: "bogus" }, { nope: true }, "raw"])).toEqual([]);
  });

  it("projects to plain text for attribute contexts", () => {
    expect(
      richTextToPlain([
        { type: "text", text: "a=" },
        { type: "mathInline", attrs: { latex: "x^2" } },
      ]),
    ).toBe("a=x^2");
  });
});

describe("safeHref", () => {
  it("accepts http, https and site-relative", () => {
    expect(safeHref("https://a.example")).toBe("https://a.example");
    expect(safeHref("http://a.example")).toBe("http://a.example");
    expect(safeHref("/articles/x")).toBe("/articles/x");
  });

  it("rejects other schemes, protocol-relative URLs and junk", () => {
    for (const href of ["javascript:alert(1)", "data:text/html,x", "//evil.example", "", "   ", 42]) {
      expect(safeHref(href as never)).toBeNull();
    }
  });
});
