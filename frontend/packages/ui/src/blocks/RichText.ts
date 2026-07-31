import { h, type FunctionalComponent, type VNode } from "vue";
import type { InlineNode, RichText as RichTextContent } from "@databro/types";
import { markToElement } from "./rich-text";
import MathInline from "./MathInline.vue";

/**
 * Renders inline content (ADR-0009).
 *
 * A functional render component rather than a template, because marks nest arbitrarily — bold
 * inside a link inside italic — and expressing that as nested template conditionals would be
 * unreadable. Text is always passed as a *child*, never as HTML, so a mark can only ever produce
 * the element it maps to.
 */
function renderNode(node: InlineNode): VNode | string {
  if (node.type === "mathInline") {
    return h(MathInline, { latex: node.attrs.latex });
  }

  // Reduced innermost-first, so the last mark in the array ends up the outermost element.
  return (node.marks ?? []).reduce<VNode | string>((child, mark) => {
    const element = markToElement(mark);
    // An unknown mark, or a link with an unsafe href, drops the wrapper and keeps the text.
    return element ? h(element.tag, element.attrs, [child]) : child;
  }, node.text);
}

const RichText: FunctionalComponent<{ content: RichTextContent }> = (props) =>
  props.content.map(renderNode);

RichText.props = { content: { type: Array, required: true } };
RichText.displayName = "RichText";

export default RichText;
