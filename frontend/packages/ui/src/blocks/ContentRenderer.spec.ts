import { describe, expect, it } from "vitest";
import { mount } from "@vue/test-utils";
import type { ContentDocument } from "@databro/types";
import ContentRenderer from "./ContentRenderer.vue";
import { SUPPORTED_BLOCK_TYPES } from "../index";
import { mediaResolverFor } from "./context";

function doc(blocks: ContentDocument["blocks"]): ContentDocument {
  return { version: 1, blocks };
}

// The renderer is the load-bearing half of the Article==Lesson bet (ADR-0007): the same component
// renders both. These tests pin the contract every block type must honour.
describe("ContentRenderer", () => {
  it("renders every supported block type", () => {
    // Guards against a block type being added to the registry without a rendering test.
    expect(SUPPORTED_BLOCK_TYPES).toHaveLength(11);
  });

  // Documents written before ADR-0009 carry plain `text` strings. There is no production content to
  // migrate, so the renderers accept both shapes; these assert the shim keeps working.
  it("still renders pre-ADR-0009 plain-text blocks", () => {
    const wrapper = mount(ContentRenderer, {
      props: {
        document: doc([
          { id: "p", type: "paragraph", data: { text: "legacy paragraph" } },
          { id: "q", type: "quote", data: { text: "legacy quote" } },
          { id: "c", type: "callout", data: { variant: "info", text: "legacy callout" } },
          { id: "l", type: "list", data: { ordered: false, items: ["legacy item"] } },
          { id: "t", type: "table", data: { headers: ["H"], rows: [["cell"]] } },
        ] as never),
      },
    });

    expect(wrapper.text()).toContain("legacy paragraph");
    expect(wrapper.get("blockquote").text()).toContain("legacy quote");
    expect(wrapper.get("aside").text()).toContain("legacy callout");
    expect(wrapper.get("li").text()).toContain("legacy item");
    expect(wrapper.get("td").text()).toContain("cell");
  });

  it("renders headings as h2-h4 with a deep-linkable anchor", () => {
    const wrapper = mount(ContentRenderer, {
      props: { document: doc([{ id: "h", type: "heading", data: { level: 3, text: "Vector Databases" } }]) },
    });
    const heading = wrapper.get("h3");
    expect(heading.text()).toBe("Vector Databases");
    expect(heading.attributes("id")).toBe("vector-databases");
  });

  it("clamps an out-of-range heading level instead of emitting invalid markup", () => {
    const wrapper = mount(ContentRenderer, {
      // Level 1 belongs to the article title; the contract only allows 2-4.
      props: { document: doc([{ id: "h", type: "heading", data: { level: 1 as 2, text: "Nope" } }]) },
    });
    expect(wrapper.find("h1").exists()).toBe(false);
    expect(wrapper.get("h2").text()).toBe("Nope");
  });

  it("escapes paragraph text rather than treating it as markup", () => {
    const hostile = '<img src=x onerror="alert(1)">';
    const wrapper = mount(ContentRenderer, {
      props: { document: doc([{ id: "p", type: "paragraph", data: { text: hostile } }]) },
    });
    expect(wrapper.find("img").exists()).toBe(false);
    expect(wrapper.get("p").text()).toBe(hostile);
  });

  it("emits the language-* convention on code blocks", () => {
    const wrapper = mount(ContentRenderer, {
      props: {
        document: doc([
          { id: "c", type: "code", data: { language: "Python", code: "print('hi')", filename: "main.py" } },
        ]),
      },
    });
    expect(wrapper.get("code").classes()).toContain("language-python");
    expect(wrapper.get("figcaption").text()).toBe("main.py");
  });

  it("rejects a code language that would break out of the class attribute", () => {
    const wrapper = mount(ContentRenderer, {
      props: { document: doc([{ id: "c", type: "code", data: { language: 'x" onload="y', code: "" } }]) },
    });
    expect(wrapper.get("code").classes()).toContain("language-plaintext");
  });

  it("renders lists, quotes, tables and dividers with semantic markup", () => {
    const wrapper = mount(ContentRenderer, {
      props: {
        document: doc([
          { id: "l", type: "list", data: { ordered: true, items: ["one", "two"] } },
          { id: "q", type: "quote", data: { text: "Ship it.", attribution: "Someone" } },
          { id: "t", type: "table", data: { headers: ["A"], rows: [["1"]] } },
          { id: "d", type: "divider", data: {} },
        ]),
      },
    });
    expect(wrapper.findAll("ol li")).toHaveLength(2);
    expect(wrapper.get("blockquote").text()).toBe("Ship it.");
    expect(wrapper.get("cite").text()).toBe("Someone");
    expect(wrapper.get("th").attributes("scope")).toBe("col");
    expect(wrapper.find("hr").exists()).toBe(true);
  });

  it("falls back to a placeholder when an id cannot be resolved", () => {
    // A deleted asset, or a host that supplied no resolver. Either way a reader must not see a
    // broken image, and the caption must survive.
    const wrapper = mount(ContentRenderer, {
      props: { document: doc([{ id: "i", type: "image", data: { mediaId: "abc", alt: "A diagram" } }]) },
    });
    expect(wrapper.find("img").exists()).toBe(false);
    expect(wrapper.get('[data-placeholder="media"]').attributes("aria-label")).toBe("A diagram");
  });

  it("renders a real image once a media resolver is supplied", () => {
    const wrapper = mount(ContentRenderer, {
      props: {
        document: doc([{ id: "i", type: "image", data: { mediaId: "abc", alt: "A diagram" } }]),
        resolveMedia: mediaResolverFor({
          abc: { url: "https://cdn.example/abc.png", altText: "stored alt", width: 1600, height: 900, variants: [] },
        }),
      },
    });
    const img = wrapper.get("img");
    expect(img.attributes("src")).toBe("https://cdn.example/abc.png");
    expect(img.attributes("loading")).toBe("lazy");
    // Intrinsic dimensions prevent the layout shift a lazy image otherwise causes.
    expect(img.attributes("width")).toBe("1600");
    expect(img.attributes("height")).toBe("900");
    // The block's alt wins over the asset's: the same image means different things in different
    // articles, and the block-level text is the one written for this context.
    expect(img.attributes("alt")).toBe("A diagram");
  });

  it("emits a srcset from the asset's variants, widest candidate last", () => {
    const wrapper = mount(ContentRenderer, {
      props: {
        document: doc([{ id: "i", type: "image", data: { mediaId: "abc", alt: "A diagram" } }]),
        resolveMedia: mediaResolverFor({
          abc: {
            url: "https://cdn.example/original.jpg",
            altText: "",
            width: 1600,
            height: 900,
            variants: [
              { name: "640", url: "https://cdn.example/640.jpg", width: 640, height: 360 },
              { name: "960", url: "https://cdn.example/960.jpg", width: 960, height: 540 },
            ],
          },
        }),
      },
    });

    const img = wrapper.get("img");
    expect(img.attributes("srcset")).toBe(
      "https://cdn.example/640.jpg 640w, https://cdn.example/960.jpg 960w, https://cdn.example/original.jpg 1600w",
    );
    expect(img.attributes("sizes")).toBeTruthy();
  });

  it("omits srcset entirely while an asset is still processing", () => {
    // Variants arrive from a background job (ADR-0011). A half-built srcset would be worse than
    // none: the browser would have no candidate wide enough and pick badly.
    const wrapper = mount(ContentRenderer, {
      props: {
        document: doc([{ id: "i", type: "image", data: { mediaId: "abc", alt: "A diagram" } }]),
        resolveMedia: mediaResolverFor({
          abc: { url: "https://cdn.example/original.jpg", altText: "", width: 1600, height: 900, variants: [] },
        }),
      },
    });

    const img = wrapper.get("img");
    expect(img.attributes("src")).toBe("https://cdn.example/original.jpg");
    expect(img.attributes("srcset")).toBeUndefined();
    expect(img.attributes("sizes")).toBeUndefined();
  });

  // Unknown blocks: content outlives renderers, so this must degrade, never throw.
  it("hides unknown block types from readers", () => {
    const wrapper = mount(ContentRenderer, {
      props: { document: doc([{ id: "x", type: "chart", data: {} } as never]) },
    });
    expect(wrapper.find("[data-unknown-block]").exists()).toBe(false);
    expect(wrapper.text()).toBe("");
  });

  it("shows unknown block types in CMS preview mode", () => {
    const wrapper = mount(ContentRenderer, {
      props: {
        document: doc([{ id: "x", type: "chart", data: {} } as never]),
        showUnknownBlocks: true,
      },
    });
    expect(wrapper.get("[data-unknown-block]").attributes("data-block-type")).toBe("chart");
  });

  it("renders blocks in document order", () => {
    const wrapper = mount(ContentRenderer, {
      props: {
        document: doc([
          { id: "1", type: "paragraph", data: { text: "first" } },
          { id: "2", type: "paragraph", data: { text: "second" } },
        ]),
      },
    });
    expect(wrapper.findAll("p").map((p) => p.text())).toEqual(["first", "second"]);
  });
});
