<script setup lang="ts">
import { DbButton, DbInput } from "@databro/ui";
import { ApiClientError, createApiClient } from "@databro/api-client";

/**
 * Sign-in for the authenticated app — both audiences (ADR-0015).
 *
 * Uses the shared primitives rather than bespoke form markup, so the focus rings, error wiring and
 * disabled states are the ones already covered by tests in `@databro/ui`.
 */
definePageMeta({ layout: false });

const { t } = useI18n();
const route = useRoute();
const { login, isAuthenticated, ensureUser } = useAuth();
const { homePath } = useRoles();
const config = useRuntimeConfig();

const email = ref("");
const password = ref("");
const formError = ref<string | null>(null);
const submitting = ref(false);

/**
 * True when sign-in was refused because the address is unconfirmed.
 *
 * Distinct from a generic failure, and safe to distinguish: the API only says this **after** the
 * password matched, so anyone seeing it has already proved the account is theirs. Without it the
 * learner gets a dead end — the one thing that would fix it is a link they cannot ask for.
 */
const unconfirmed = ref(false);
const resent = ref(false);

async function resend() {
  resent.value = true;

  try {
    await createApiClient({ baseUrl: String(config.public.apiBaseUrl) })
      .resendConfirmation(email.value);
  } catch {
    // Ignored, like forgot-password: the API is non-committal by design and a visible failure here
    // would leak the difference it withholds.
  }
}

/**
 * An explicit `?redirect=` the guard carried over, if it is safe to honour.
 *
 * Two shapes are allowed and nothing else:
 *
 *  * a same-origin **path** — what this app's own route guard sends;
 *  * an **absolute URL on the public site**, so a learner who clicked "sign in to track progress"
 *    on a lesson page is returned to that lesson rather than dumped on their dashboard.
 *
 * The second is an allowlist of exactly one origin, read from config — not "any absolute URL", which
 * would be an open redirect letting a crafted link bounce a signed-in editor to an attacker's page
 * carrying their trust in this domain. `new URL` does the parsing, because origin comparison by
 * string prefix is how `https://databro.id.evil.com` gets accepted.
 */
const requested = computed(() => {
  const target = String(route.query.redirect ?? "");
  if (!target) return null;

  if (target.startsWith("/") && !target.startsWith("//")) return target;

  try {
    const siteOrigin = new URL(String(config.public.siteUrl)).origin;
    return new URL(target).origin === siteOrigin ? target : null;
  } catch {
    return null;
  }
});

/**
 * Where to go after signing in: the page they were sent away from, or their role's home.
 *
 * Role-aware because one app now serves two audiences. Dropping an editor on the learner dashboard
 * every morning, or a learner in the Studio, would make the shared app feel like the wrong app for
 * whoever lost the coin toss.
 */
async function destination() {
  if (requested.value) return requested.value;

  await ensureUser();
  return homePath.value;
}

/**
 * Navigates to wherever {@link destination} resolved.
 *
 * `external` is required for the cross-origin case — Nuxt's router would otherwise try to resolve
 * `https://databro.id/courses/...` as one of *this* app's routes and land on a 404. The flag is set
 * from the shape of the target rather than passed in, so no caller can forget it.
 */
async function go() {
  const to = await destination();
  await navigateTo(to, { replace: true, external: !to.startsWith("/") });
}

// Already signed in and hitting /login directly: go where they were headed.
if (isAuthenticated.value) await go();

async function submit() {
  formError.value = null;
  unconfirmed.value = false;
  resent.value = false;
  submitting.value = true;

  try {
    await login(email.value, password.value);
    await go();
  } catch (error) {
    if (error instanceof ApiClientError && error.code === "email_not_confirmed") {
      unconfirmed.value = true;
      submitting.value = false;
      return;
    }

    // Everything else reads the same — a wrong password, an unknown address, a server error alike.
    // Distinguishing the first two would turn the form into an account-enumeration oracle.
    formError.value = t("login.failed");
  } finally {
    submitting.value = false;
  }
}

useHead({ title: t("login.title") });
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-surface-sunken px-4 font-sans text-ink antialiased">
    <div class="w-full max-w-sm">
      <div class="text-center">
        <span class="inline-flex text-accent"><AppBrandMark /></span>
        <h1 class="mt-6 font-display text-2xl font-bold tracking-tight text-ink">
          {{ t("login.title") }}
        </h1>
        <p class="mt-2 text-sm text-ink-muted">{{ t("login.subtitle") }}</p>
      </div>

      <form class="mt-8 space-y-4 rounded-card border border-line bg-surface p-6 shadow-card" @submit.prevent="submit">
        <!-- Actionable, unlike the generic failure: the account is known to be theirs by this
             point, so offering the fix is safe and the alternative is a dead end. -->
        <div
          v-if="unconfirmed"
          role="alert"
          class="rounded-md border border-warning/30 bg-warning-subtle px-3 py-3 text-sm text-warning"
        >
          <p>{{ t("login.unconfirmed") }}</p>
          <p v-if="resent" class="mt-2 font-medium">{{ t("login.resent") }}</p>
          <button
            v-else
            type="button"
            class="mt-2 font-medium underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent"
            @click="resend"
          >
            {{ t("login.resend") }}
          </button>
        </div>

        <!-- role=alert so the failure is announced, not just shown. -->
        <p
          v-if="formError"
          role="alert"
          class="rounded-md border border-danger/30 bg-danger-subtle px-3 py-2 text-sm text-danger"
        >
          {{ formError }}
        </p>

        <DbInput
          v-model="email"
          :label="t('login.email')"
          type="email"
          placeholder="you@databro.id"
          required
          :disabled="submitting"
        />

        <DbInput
          v-model="password"
          :label="t('login.password')"
          type="password"
          required
          :disabled="submitting"
        />

        <DbButton type="submit" block size="lg" :disabled="submitting">
          {{ submitting ? t("login.submitting") : t("login.submit") }}
        </DbButton>

        <!-- Without this the recovery pages exist and nobody can find them. -->
        <p class="text-center text-sm">
          <a href="/forgot-password" class="font-medium text-accent hover:underline">
            {{ t("forgot.title") }}
          </a>
        </p>
      </form>
    </div>
  </div>
</template>
