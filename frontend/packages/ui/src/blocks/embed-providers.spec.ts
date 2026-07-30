import { describe, expect, it } from "vitest";
import { mount } from "@vue/test-utils";
import { isSafeLink, resolveEmbed } from "./embed-providers";
import ContentRenderer from "./ContentRenderer.vue";

// An EmbedBlock URL is author-supplied and reaches the renderer straight out of JSONB. Framing it
// unchecked would let anyone with Content.Create embed arbitrary origins in a public page.
describe("embed allowlist", () => {
  it("normalizes YouTube watch, short and embed URLs to the no-cookie player", () => {
    for (const url of [
      "https://www.youtube.com/watch?v=dQw4w9WgXcQ",
      "https://youtu.be/dQw4w9WgXcQ",
      "https://www.youtube.com/embed/dQw4w9WgXcQ",
    ]) {
      expect(resolveEmbed(url)?.embedUrl).toBe("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ");
    }
  });

  it("normalizes Vimeo and CodePen URLs", () => {
    expect(resolveEmbed("https://vimeo.com/123456789")?.embedUrl).toBe("https://player.vimeo.com/video/123456789");
    expect(resolveEmbed("https://codepen.io/someone/pen/abcXYZ")?.embedUrl).toBe(
      "https://codepen.io/someone/embed/abcXYZ",
    );
  });

  it("refuses hosts that are not allowlisted", () => {
    expect(resolveEmbed("https://evil.example/embed/x")).toBeNull();
  });

  it("refuses a lookalike host", () => {
    // Substring matching on "youtube.com" would wrongly accept this.
    expect(resolveEmbed("https://youtube.com.evil.example/watch?v=dQw4w9WgXcQ")).toBeNull();
  });

  it("refuses non-https and dangerous schemes", () => {
    expect(resolveEmbed("http://www.youtube.com/watch?v=dQw4w9WgXcQ")).toBeNull();
    expect(resolveEmbed("javascript:alert(1)")).toBeNull();
    expect(resolveEmbed("data:text/html,<script>alert(1)</script>")).toBeNull();
  });

  it("refuses an allowlisted host with a malformed id", () => {
    expect(resolveEmbed("https://vimeo.com/not-an-id")).toBeNull();
  });

  it("treats only http(s) as a safe link fallback", () => {
    expect(isSafeLink("https://example.com")).toBe(true);
    expect(isSafeLink("javascript:alert(1)")).toBe(false);
  });
});

describe("EmbedBlock rendering", () => {
  const embed = (url: string) => ({ version: 1, blocks: [{ id: "e", type: "embed" as const, data: { provider: "auto", url } }] });

  it("frames an allowlisted embed with a restrictive sandbox", () => {
    const wrapper = mount(ContentRenderer, { props: { document: embed("https://youtu.be/dQw4w9WgXcQ") } });
    const iframe = wrapper.get("iframe");
    expect(iframe.attributes("src")).toBe("https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ");
    expect(iframe.attributes("sandbox")).toBeDefined();
    expect(iframe.attributes("title")).toBe("YouTube video");
  });

  it("degrades an unknown provider to a nofollow link instead of an iframe", () => {
    const wrapper = mount(ContentRenderer, { props: { document: embed("https://evil.example/x") } });
    expect(wrapper.find("iframe").exists()).toBe(false);
    expect(wrapper.get("a").attributes("rel")).toContain("noopener");
  });

  it("renders nothing at all for a dangerous scheme", () => {
    const wrapper = mount(ContentRenderer, { props: { document: embed("javascript:alert(1)") } });
    expect(wrapper.find("iframe").exists()).toBe(false);
    expect(wrapper.find("a").exists()).toBe(false);
  });
});
