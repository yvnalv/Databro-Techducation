import { describe, expect, it } from "vitest";
import { mount } from "@vue/test-utils";
import type { ContentDocument } from "@databro/types";
import ContentRenderer from "./ContentRenderer.vue";

function doc(blocks: ContentDocument["blocks"]): ContentDocument {
  return { version: 1, blocks };
}

const text = (value: string) => ({ type: "text", text: value }) as never;

// The three additions that landed with ADR-0009: math, code output, and blocks nested in list items.
describe("math", () => {
  it("renders a block equation in display mode", () => {
    const wrapper = mount(ContentRenderer, {
      props: { document: doc([{ id: "m", type: "math", data: { latex: "E = mc^2" } }] as never) },
    });

    const html = wrapper.get(".databro-math-block").html();
    expect(html).toContain("katex");
    // displayMode wraps the output in katex-display.
    expect(html).toContain("katex-display");
  });

  it("renders inline math inside a paragraph", () => {
    const wrapper = mount(ContentRenderer, {
      props: {
        document: doc([
          {
            id: "p",
            type: "paragraph",
            data: {
              content: [
                text("attention scales with "),
                { type: "mathInline", attrs: { latex: "O(n^2)" } },
              ],
            },
          },
        ] as never),
      },
    });

    expect(wrapper.text()).toContain("attention scales with");
    expect(wrapper.get(".databro-math-inline").html()).toContain("katex");
  });

  it("renders malformed LaTeX as an error instead of throwing", () => {
    // throwOnError: false — one bad formula must not fail the whole server render.
    expect(() =>
      mount(ContentRenderer, {
        props: { document: doc([{ id: "m", type: "math", data: { latex: "\\frac{" } }] as never) },
      }),
    ).not.toThrow();
  });

  it("does not emit script tags for LaTeX that tries to inject HTML", () => {
    // KaTeX runs with trust: false, so \href and \htmlClass cannot emit arbitrary markup.
    const wrapper = mount(ContentRenderer, {
      props: {
        document: doc([
          { id: "m", type: "math", data: { latex: "\\href{javascript:alert(1)}{x}" } },
        ] as never),
      },
    });

    const html = wrapper.html();
    expect(html).not.toContain("javascript:alert");
    expect(wrapper.find("script").exists()).toBe(false);
  });
});

describe("code output", () => {
  it("renders output as samp, distinct from the source", () => {
    const wrapper = mount(ContentRenderer, {
      props: {
        document: doc([
          {
            id: "c",
            type: "code",
            data: { language: "python", code: "print('hi')", output: "hi\n" },
          },
        ] as never),
      },
    });

    expect(wrapper.get("code").text()).toBe("print('hi')");
    // <samp> so it is never mistaken for source, and never syntax-highlighted.
    expect(wrapper.get("[data-code-output] samp").text()).toBe("hi");
  });

  it("omits the output region entirely when there is none", () => {
    const wrapper = mount(ContentRenderer, {
      props: {
        document: doc([{ id: "c", type: "code", data: { language: "py", code: "x = 1" } }] as never),
      },
    });

    expect(wrapper.find("[data-code-output]").exists()).toBe(false);
  });
});

describe("nested blocks in list items", () => {
  it("renders a code sample inside a tutorial step", () => {
    const wrapper = mount(ContentRenderer, {
      props: {
        document: doc([
          {
            id: "l",
            type: "list",
            data: {
              ordered: true,
              items: [
                {
                  content: [text("Install the client")],
                  blocks: [
                    { id: "n1", type: "code", data: { language: "bash", code: "pip install databro" } },
                  ],
                },
                { content: [text("Run it")] },
              ],
            },
          },
        ] as never),
      },
    });

    const items = wrapper.findAll("li");
    expect(items).toHaveLength(2);
    expect(items[0].text()).toContain("Install the client");
    expect(items[0].find("code").text()).toBe("pip install databro");
    expect(items[1].find("code").exists()).toBe(false);
  });

  it("stops recursing past the nesting cap", () => {
    // A document nested deeply enough to exhaust the stack must degrade, not take down the render.
    const deepest = {
      id: "d3",
      type: "list",
      data: { ordered: false, items: [{ content: [text("level-3")] }] },
    };
    const middle = {
      id: "d2",
      type: "list",
      data: { ordered: false, items: [{ content: [text("level-2")], blocks: [deepest] }] },
    };
    const outer = {
      id: "d1",
      type: "list",
      data: { ordered: false, items: [{ content: [text("level-1")], blocks: [middle] }] },
    };

    const wrapper = mount(ContentRenderer, { props: { document: doc([outer] as never) } });

    expect(wrapper.text()).toContain("level-1");
    expect(wrapper.text()).toContain("level-2");
    // Beyond MAX_NESTING_DEPTH the nested blocks are dropped rather than rendered.
    expect(wrapper.text()).not.toContain("level-3");
  });
});

describe("rich table cells", () => {
  it("renders inline code inside a cell", () => {
    const wrapper = mount(ContentRenderer, {
      props: {
        document: doc([
          {
            id: "t",
            type: "table",
            data: {
              headers: [[text("Option")]],
              rows: [[[{ type: "text", text: "--verbose", marks: [{ type: "code" }] }]]],
            },
          },
        ] as never),
      },
    });

    expect(wrapper.get("th").text()).toBe("Option");
    expect(wrapper.get("td code").text()).toBe("--verbose");
  });
});
