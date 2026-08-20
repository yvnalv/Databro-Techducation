<script setup lang="ts">
/**
 * Social-login landing (ADR-0019).
 *
 * The OAuth callback redirected the browser here with a single-use `code` — never the tokens. This
 * page exchanges it for a session and then goes where the sign-in was headed. The exchange runs only
 * on the client (`onMounted`): the code is one-use, so letting SSR spend it would leave the browser
 * with nothing to exchange.
 */
definePageMeta({ layout: false });

const { t } = useI18n();
const route = useRoute();
const config = useRuntimeConfig();
const { completeExternalLogin } = useAuth();
const { homePath } = useRoles();

const failed = ref(false);

/**
 * A `returnTo` is validated server-side before it is ever placed in this URL, but re-validate on the
 * client too — a query parameter is untrusted input, and the rule (same-app path, or an absolute URL
 * on the public site) is cheap to reassert. Anything else falls back to the role's home.
 */
function safeReturn(target: string): string | null {
  if (!target) return null;
  if (target.startsWith("/") && !target.startsWith("//")) return target;

  try {
    const siteOrigin = new URL(String(config.public.siteUrl)).origin;
    return new URL(target).origin === siteOrigin ? target : null;
  } catch {
    return null;
  }
}

async function complete() {
  const code = String(route.query.code ?? "");
  if (!code) {
    failed.value = true;
    return;
  }

  try {
    await completeExternalLogin(code);
    const dest = safeReturn(String(route.query.returnTo ?? "")) ?? homePath.value;
    await navigateTo(dest, { replace: true, external: !dest.startsWith("/") });
  } catch {
    failed.value = true;
  }
}

onMounted(complete);

useHead({ title: t("oauthCallback.working") });
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-surface-sunken px-4 font-sans text-ink antialiased">
    <div class="w-full max-w-sm text-center">
      <span class="inline-flex text-accent"><AppBrandMark /></span>

      <template v-if="!failed">
        <p class="mt-6 text-sm text-ink-muted" role="status">{{ t("oauthCallback.working") }}</p>
      </template>

      <template v-else>
        <h1 class="mt-6 font-display text-xl font-bold tracking-tight text-ink">
          {{ t("oauthCallback.failedTitle") }}
        </h1>
        <p class="mt-2 text-sm text-ink-muted">{{ t("oauthCallback.failedBody") }}</p>
        <a href="/login" class="mt-4 inline-block font-medium text-accent hover:underline">
          {{ t("oauthCallback.signIn") }}
        </a>
      </template>
    </div>
  </div>
</template>
