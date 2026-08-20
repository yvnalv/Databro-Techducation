<script setup lang="ts">
import { DbButton, DbInput } from "@databro/ui";
import { createApiClient } from "@databro/api-client";

/**
 * Start of password recovery.
 *
 * Public, and no layout — someone who cannot sign in must be able to reach this.
 */
definePageMeta({ layout: false });

const { t } = useI18n();
const config = useRuntimeConfig();

const email = ref("");
const submitting = ref(false);
const sent = ref(false);

async function submit() {
  submitting.value = true;

  try {
    await createApiClient({ baseUrl: String(config.public.apiBaseUrl) }).forgotPassword(email.value);
  } catch {
    // Deliberately ignored. The API answers identically for a known and an unknown address so it
    // cannot be used to test who has an account here; showing a network error would reintroduce
    // exactly that difference, since only a real send can fail in an interesting way.
  } finally {
    submitting.value = false;
    sent.value = true;
  }
}

useHead({ title: t("forgot.title") });
</script>

<template>
  <div
    class="flex min-h-screen items-center justify-center bg-surface-sunken px-4 font-sans text-ink antialiased"
  >
    <div class="w-full max-w-sm">
      <div class="text-center">
        <span class="inline-flex text-accent-strong"><AppBrandMark /></span>
        <h1 class="mt-6 font-display text-2xl font-bold tracking-tight text-ink">
          {{ t("forgot.title") }}
        </h1>
        <p class="mt-2 text-sm text-ink-muted">{{ t("forgot.subtitle") }}</p>
      </div>

      <!-- Says "if that address has an account", never "sent". The page genuinely does not know,
           and wording that implied otherwise would leak what the API withholds. -->
      <div
        v-if="sent"
        class="mt-8 rounded-card border border-line bg-surface p-6 text-center shadow-card"
      >
        <p class="text-sm leading-relaxed text-ink-muted">{{ t("forgot.maybeSent") }}</p>
        <DbButton :as="'a'" href="/login" variant="outline" class="mt-5">
          {{ t("forgot.backToSignIn") }}
        </DbButton>
      </div>

      <form
        v-else
        class="mt-8 space-y-4 rounded-card border border-line bg-surface p-6 shadow-card"
        @submit.prevent="submit"
      >
        <DbInput
          v-model="email"
          :label="t('login.email')"
          type="email"
          placeholder="you@databro.id"
          required
          :disabled="submitting"
        />

        <DbButton type="submit" block size="lg" :disabled="submitting || !email">
          {{ submitting ? t("forgot.submitting") : t("forgot.submit") }}
        </DbButton>

        <p class="text-center text-sm">
          <a href="/login" class="font-medium text-accent-strong hover:underline">
            {{ t("forgot.backToSignIn") }}
          </a>
        </p>
      </form>
    </div>
  </div>
</template>
