<script setup lang="ts">
import { Editor, EditorContent } from "@tiptap/vue-3";
import StarterKit from "@tiptap/starter-kit";
import Link from "@tiptap/extension-link";
import type { RichText } from "@databro/types";

/**
 * Inline rich-text editing for a block's `content` (ADR-0009).
 *
 * The conversion here is deliberately trivial, and that is the whole point of the ADR: DataBro
 * stores inline content in **ProseMirror's own shape**, which is what Tiptap edits natively. So
 * "converting" is wrapping the node array in a document and unwrapping it again — no mark
 * translation, no offset remapping, no lossy round-trip. Had we stored offset-range marks, this
 * component would be the translation layer that decision was made to avoid.
 */
const props = defineProps<{ modelValue: RichText; placeholder?: string }>();
const emit = defineEmits<{ "update:modelValue": [value: RichText] }>();

const editor = shallowRef<Editor>();

/** Our stored nodes -> a single-paragraph ProseMirror doc. */
const toDoc = (content: RichText) => ({
  type: "doc",
  content: [{ type: "paragraph", ...(content.length ? { content } : {}) }],
});

/** The paragraph's inline children -> our stored nodes. */
function fromDoc(json: Record<string, unknown>): RichText {
  const paragraph = (json.content as Array<Record<string, unknown>> | undefined)?.[0];
  return ((paragraph?.content as RichText | undefined) ?? []).filter(Boolean);
}

onMounted(() => {
  editor.value = new Editor({
    content: toDoc(props.modelValue ?? []),
    extensions: [
      StarterKit.configure({
        // One paragraph only: block structure is DataBro's content model, not the editor's. Letting
        // Tiptap create headings or lists inside a paragraph block would produce a document the
        // block renderer cannot represent.
        heading: false,
        bulletList: false,
        orderedList: false,
        listItem: false,
        blockquote: false,
        codeBlock: false,
        horizontalRule: false,
      }),
      Link.configure({
        openOnClick: false,
        autolink: false,
        // Mirrors the renderer's allowlist: an unsafe scheme must not survive a round trip.
        protocols: ["http", "https"],
        HTMLAttributes: { rel: "nofollow noopener noreferrer" },
      }),
    ],
    editorProps: {
      attributes: {
        class:
          "min-h-[5rem] w-full rounded-md border border-line-strong bg-surface px-3 py-2 text-sm text-ink focus:outline-none focus:ring-2 focus:ring-accent/25",
      },
    },
    onUpdate: ({ editor: instance }) => {
      emit("update:modelValue", fromDoc(instance.getJSON() as Record<string, unknown>));
    },
  });
});

onBeforeUnmount(() => editor.value?.destroy());

const isActive = (name: string) => editor.value?.isActive(name) ?? false;

function toggleLink() {
  if (!editor.value) return;

  if (isActive("link")) {
    editor.value.chain().focus().unsetLink().run();
    return;
  }

  const href = window.prompt("Link URL (http or https)");
  if (!href) return;

  editor.value.chain().focus().setLink({ href }).run();
}

const TOOLS = [
  { name: "bold", label: "B", title: "Bold", cls: "font-bold" },
  { name: "italic", label: "I", title: "Italic", cls: "italic" },
  { name: "code", label: "<>", title: "Inline code", cls: "font-mono text-xs" },
  { name: "strike", label: "S", title: "Strikethrough", cls: "line-through" },
] as const;
</script>

<template>
  <div>
    <div class="mb-1.5 flex flex-wrap gap-1" role="toolbar" aria-label="Text formatting">
      <button
        v-for="tool in TOOLS"
        :key="tool.name"
        type="button"
        :title="tool.title"
        :aria-label="tool.title"
        :aria-pressed="isActive(tool.name)"
        class="h-7 w-8 rounded border text-xs transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
        :class="[
          tool.cls,
          isActive(tool.name)
            ? 'border-accent bg-accent-subtle text-accent'
            : 'border-line text-ink-muted hover:bg-surface-sunken',
        ]"
        @click="editor?.chain().focus().toggleMark(tool.name).run()"
      >
        {{ tool.label }}
      </button>

      <button
        type="button"
        title="Link"
        aria-label="Link"
        :aria-pressed="isActive('link')"
        class="h-7 rounded border px-2 text-xs transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
        :class="
          isActive('link')
            ? 'border-accent bg-accent-subtle text-accent'
            : 'border-line text-ink-muted hover:bg-surface-sunken'
        "
        @click="toggleLink"
      >
        Link
      </button>
    </div>

    <EditorContent :editor="editor" />
  </div>
</template>
