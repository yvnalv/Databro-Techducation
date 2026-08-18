<script setup lang="ts">
import { DbButton } from "@databro/ui";
import { createApiClient } from "@databro/api-client";

/**
 * The page a verification email links to.
 *
 * <b>No layout and no auth.</b> Someone arriving here has just clicked a link in their inbox and may
 * never have signed in on this device — the token in the URL is the proof, and demanding a session
 * first would mean signing in before being allowed to finish signing up. `auth.global` skips it for
 * the same reason it skips `/login`.
 */
definePageMeta({ layout: false });

const { t } = useI18n();
const route = useRoute();
const config = useRuntimeConfig();

const state = ref<"working" | "confirmed" | "failed">("working");

onMounted(async () => {
  const userId = String(route.query.userId ?? "");
  const token = String(route.query.token ?? "");

  if (!userId || !token) {
    state.value = "failed";
    return;
  }

  // A plain anonymous client: `useAuth`'s would attach whatever session happens to be in the
  // browser, and this must work identically for a signed-out visitor.
  const client = createApiClient({ baseUrl: String(config.public.apiBaseUrl) });

  try {
    await client.confirmEmail(userId, token);
    state.value = "confirmed";
  } catch {
    // Every failure reads the same: expired, already used, tampered with. Distinguishing them would
    // tell someone holding a stolen link which kind of stolen link they have.
    state.value = "failed";
  }
});
</script>

<template>
  <div
    class="flex min-h-screen items-center justify-center bg-surface-sunken px-4 font-sans text-ink antialiased"
  >
    <div class="w-full max-w-sm text-center">
      <span class="inline-flex text-accent"><AppBrandMark /></span>

      <div class="mt-8 rounded-card border border-line bg-surface p-6 shadow-card">
        <template v-if="state === 'working'">
          <h1 class="font-display text-xl font-bold tracking-tight text-ink">
            {{ t("verify.working") }}
          </h1>
        </template>

        <template v-else-if="state === 'confirmed'">
          <h1 class="font-display text-xl font-bold tracking-tight text-ink">
            {{ t("verify.confirmedTitle") }}
          </h1>
          <p class="mt-2 text-sm text-ink-muted">{{ t("verify.confirmedBody") }}</p>
          <DbButton :as="'a'" href="/login" class="mt-5">{{ t("verify.signIn") }}</DbButton>
        </template>

        <template v-else>
          <h1 class="font-display text-xl font-bold tracking-tight text-ink">
            {{ t("verify.failedTitle") }}
          </h1>
          <p class="mt-2 text-sm text-ink-muted">{{ t("verify.failedBody") }}</p>
          <DbButton :as="'a'" href="/login" variant="outline" class="mt-5">
            {{ t("verify.signIn") }}
          </DbButton>
        </template>
      </div>
    </div>
  </div>
</template>
