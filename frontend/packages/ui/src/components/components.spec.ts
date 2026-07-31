import { describe, expect, it } from "vitest";
import { mount } from "@vue/test-utils";
import DbButton from "./DbButton.vue";
import DbChip from "./DbChip.vue";
import DbCard from "./DbCard.vue";
import DbInput from "./DbInput.vue";
import DbAccordion from "./DbAccordion.vue";

// These assert the accessibility contracts of the primitives (docs/DESIGN_SYSTEM.md §6), not their
// appearance. Visual styling is expected to change; these behaviours are not.

describe("DbButton", () => {
  it("renders a real button by default and keeps a focus ring", () => {
    const wrapper = mount(DbButton, { slots: { default: "Save" } });

    expect(wrapper.element.tagName).toBe("BUTTON");
    expect(wrapper.classes().join(" ")).toContain("focus-visible:ring-2");
  });

  it("renders as another element when asked, without a bogus disabled attribute", () => {
    // `disabled` is not valid on an anchor, so it has to become aria-disabled instead.
    const wrapper = mount(DbButton, { props: { as: "a", disabled: true } });

    expect(wrapper.element.tagName).toBe("A");
    expect(wrapper.attributes("disabled")).toBeUndefined();
    expect(wrapper.attributes("aria-disabled")).toBe("true");
  });

  it("uses the native disabled attribute on a real button", () => {
    const wrapper = mount(DbButton, { props: { disabled: true } });

    expect(wrapper.attributes("disabled")).toBeDefined();
    expect(wrapper.attributes("aria-disabled")).toBeUndefined();
  });
});

describe("DbChip", () => {
  it("renders its label, so colour is never the only signal", () => {
    const wrapper = mount(DbChip, { props: { tone: "danger" }, slots: { default: "Failed" } });
    expect(wrapper.text()).toBe("Failed");
  });
});

describe("DbCard", () => {
  it("is not itself a link", () => {
    // A card-wide anchor takes the card's entire text as its accessible name.
    const wrapper = mount(DbCard, { slots: { default: "<h3>Title</h3>" } });
    expect(wrapper.element.tagName).not.toBe("A");
  });
});

describe("DbInput", () => {
  it("associates its label with the input", () => {
    const wrapper = mount(DbInput, { props: { label: "Email" } });

    const id = wrapper.get("input").attributes("id");
    expect(wrapper.get("label").attributes("for")).toBe(id);
  });

  it("wires an error through aria-invalid and aria-describedby, not just colour", () => {
    const wrapper = mount(DbInput, { props: { label: "Email", error: "Required" } });
    const input = wrapper.get("input");

    expect(input.attributes("aria-invalid")).toBe("true");

    const describedBy = input.attributes("aria-describedby");
    expect(describedBy).toBeTruthy();
    expect(wrapper.get(`#${describedBy}`).text()).toBe("Required");
  });

  it("hides the hint once an error takes over, so only one message is announced", () => {
    const wrapper = mount(DbInput, { props: { hint: "We never share it", error: "Required" } });

    expect(wrapper.text()).toContain("Required");
    expect(wrapper.text()).not.toContain("We never share it");
  });

  it("keeps a hidden label announced", () => {
    const wrapper = mount(DbInput, { props: { label: "Search", labelHidden: true } });
    expect(wrapper.get("label").classes()).toContain("sr-only");
  });

  it("emits on input for v-model", async () => {
    const wrapper = mount(DbInput);
    await wrapper.get("input").setValue("hello");

    expect(wrapper.emitted("update:modelValue")?.[0]).toEqual(["hello"]);
  });
});

describe("DbAccordion", () => {
  const items = [
    { id: "a", question: "First?", answer: "Yes." },
    { id: "b", question: "Second?", answer: "Also yes." },
  ];

  it("uses real buttons with aria-expanded and aria-controls", () => {
    const wrapper = mount(DbAccordion, { props: { items } });
    const first = wrapper.findAll("button")[0];

    expect(first.attributes("aria-expanded")).toBe("false");
    const controls = first.attributes("aria-controls");
    expect(wrapper.find(`#${controls}`).exists()).toBe(true);
  });

  it("opens on click and closes on a second click", async () => {
    const wrapper = mount(DbAccordion, { props: { items } });
    const first = wrapper.findAll("button")[0];

    await first.trigger("click");
    expect(first.attributes("aria-expanded")).toBe("true");

    await first.trigger("click");
    expect(first.attributes("aria-expanded")).toBe("false");
  });

  it("closes the other panel in single-open mode", async () => {
    const wrapper = mount(DbAccordion, { props: { items, defaultOpen: "a" } });
    const buttons = wrapper.findAll("button");

    await buttons[1].trigger("click");

    expect(buttons[0].attributes("aria-expanded")).toBe("false");
    expect(buttons[1].attributes("aria-expanded")).toBe("true");
  });

  it("keeps both open when multiple is allowed", async () => {
    const wrapper = mount(DbAccordion, { props: { items, multiple: true, defaultOpen: "a" } });
    const buttons = wrapper.findAll("button");

    await buttons[1].trigger("click");

    expect(buttons[0].attributes("aria-expanded")).toBe("true");
    expect(buttons[1].attributes("aria-expanded")).toBe("true");
  });
});
