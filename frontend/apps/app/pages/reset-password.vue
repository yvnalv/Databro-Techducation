<script setup lang="ts">
import { DbButton, DbInput } from "@databro/ui";
import { ApiClientError, createApiClient } from "@databro/api-client";

/**
 * Completes a password reset from the link in a recovery email.
 *
 * Public and layout-less for the same reason as `/verify-email`: the token in the URL is the proof,
 * and someone arriving here by definition cannot sign in.
 */
definePageMeta({ layout: false });

const { t } = useI18n();
const route = useRoute();
const config = useRuntimeConfig();

const password = ref("");
const confirm = ref("");
const submitting = ref(false);
const done = ref(false);
const formError = ref<string | null>(null);

const userId = computed(() => String(route.query.userId ?? ""));
const token = computed(() => String(route.query.token ?? ""));
const hasLink = computed(() => Boolean(userId.value && token.value));

const mismatch = computed(
  () => confirm.value.length > 0 && password.value !== confirm.value,
);

async function submit() {
  if (mismatch.value) return;

  formError.value = null;
  submitting.value = true;

  try {
    await createApiClient({ baseUrl: String(config.public.apiBaseUrl) }).resetPassword(
      userId.value,
      token.value,
      password.value,
    );
    done.value = true;
  } catch (error) {
    // The API distinguishes exactly one thing: a password that breaks the policy, which is
    // actionable and tells an attacker nothing. Everything else — expired, used, tampered with —
    // arrives as one message on purpose, and is passed through rather than reworded.
    formError.value =
      error instanceof ApiClientError ? error.message : t("reset.failed");
  } finally {
    submitting.value = false;
  }
}

useHead({ title: t("reset.title") });
</script>

<template>
  <div
    class="flex min-h-screen items-center justify-center bg-surface-sunken px-4 font-sans text-ink antialiased"
  >
    <div class="w-full max-w-sm">
      <div class="text-center">
        <span class="inline-flex text-accent-strong"><AppBrandMark /></span>
        <h1 class="mt-6 font-display text-2xl font-bold tracking-tight text-ink">
          {{ t("reset.title") }}
        </h1>
      </div>

      <div
        v-if="done"
        class="mt-8 rounded-card border border-line bg-surface-raised p-6 text-center shadow-card"
      >
        <p class="text-sm leading-relaxed text-ink-muted">{{ t("reset.doneBody") }}</p>
        <DbButton :as="'a'" href="/login" class="mt-5">{{ t("reset.signIn") }}</DbButton>
      </div>

      <!-- Reached without a link: say so plainly rather than showing a form that cannot work. -->
      <div
        v-else-if="!hasLink"
        class="mt-8 rounded-card border border-line bg-surface-raised p-6 text-center shadow-card"
      >
        <p class="text-sm leading-relaxed text-ink-muted">{{ t("reset.noLink") }}</p>
        <DbButton :as="'a'" href="/forgot-password" variant="outline" class="mt-5">
          {{ t("reset.requestNew") }}
        </DbButton>
      </div>

      <form
        v-else
        class="mt-8 space-y-4 rounded-card border border-line bg-surface-raised p-6 shadow-card"
        @submit.prevent="submit"
      >
        <p
          v-if="formError"
          role="alert"
          class="rounded-control border border-danger/30 bg-danger-subtle px-3 py-2 text-sm text-danger"
        >
          {{ formError }}
        </p>

        <DbInput
          v-model="password"
          :label="t('reset.newPassword')"
          type="password"
          required
          :disabled="submitting"
        />

        <DbInput
          v-model="confirm"
          :label="t('reset.confirmPassword')"
          type="password"
          required
          :disabled="submitting"
        />

        <!-- Checked here rather than server-side: the two fields exist to catch a typo, and a
             round trip to be told they differ is a worse way to learn it. -->
        <p v-if="mismatch" class="text-sm text-danger">{{ t("reset.mismatch") }}</p>

        <DbButton
          type="submit"
          block
          size="lg"
          :disabled="submitting || !password || mismatch"
        >
          {{ submitting ? t("reset.submitting") : t("reset.submit") }}
        </DbButton>
      </form>
    </div>
  </div>
</template>
