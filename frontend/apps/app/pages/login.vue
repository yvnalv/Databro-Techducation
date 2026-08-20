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
// Pre-filled when the OAuth callback bounced back here with `?error=oauth`: a social sign-in that
// could not be completed. Cleared the moment they try again. Safe to state plainly — it reveals
// nothing about any account, only that this attempt failed.
const formError = ref<string | null>(route.query.error === "oauth" ? t("login.oauthFailed") : null);
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
 * The provider challenge URL to send the browser to (ADR-0019).
 *
 * A plain link, not a fetch: OAuth is a top-level navigation the browser must follow so the provider
 * can set its own cookies and, on return, hit our callback. The `returnTo` is the same validated
 * destination the password flow would honour, carried so a learner who clicked "sign in" on a lesson
 * lands back there. It is re-validated server-side before it is trusted.
 */
function oauthUrl(provider: "google" | "github") {
  const base = `${String(config.public.apiBaseUrl).replace(/\/$/, "")}/api/v1/auth/oauth/${provider}`;
  return requested.value ? `${base}?returnTo=${encodeURIComponent(requested.value)}` : base;
}

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
        <span class="inline-flex text-accent-strong"><AppBrandMark /></span>
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
          class="rounded-control border border-warning/30 bg-warning-subtle px-3 py-3 text-sm text-warning"
        >
          <p>{{ t("login.unconfirmed") }}</p>
          <p v-if="resent" class="mt-2 font-medium">{{ t("login.resent") }}</p>
          <button
            v-else
            type="button"
            class="mt-2 font-medium underline focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-strong"
            @click="resend"
          >
            {{ t("login.resend") }}
          </button>
        </div>

        <!-- role=alert so the failure is announced, not just shown. -->
        <p
          v-if="formError"
          role="alert"
          class="rounded-control border border-danger/30 bg-danger-subtle px-3 py-2 text-sm text-danger"
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

        <!-- "or" divider between password and social sign-in. -->
        <div class="flex items-center gap-3 text-xs uppercase tracking-wide text-ink-subtle">
          <span class="h-px flex-1 bg-line" />
          {{ t("login.or") }}
          <span class="h-px flex-1 bg-line" />
        </div>

        <!-- Plain links, not buttons: OAuth is a top-level navigation the browser must follow. -->
        <a
          :href="oauthUrl('google')"
          class="flex w-full items-center justify-center gap-2 rounded-control border border-line bg-surface px-4 py-2.5 text-sm font-medium text-ink transition hover:bg-surface-sunken focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-strong"
        >
          <svg class="h-4 w-4" viewBox="0 0 24 24" aria-hidden="true">
            <path fill="#4285F4" d="M23.5 12.3c0-.8-.1-1.6-.2-2.3H12v4.5h6.5a5.6 5.6 0 0 1-2.4 3.6v3h3.9c2.3-2.1 3.5-5.2 3.5-8.8z" />
            <path fill="#34A853" d="M12 24c3.2 0 6-1.1 8-2.9l-3.9-3c-1 .7-2.4 1.2-4.1 1.2-3.1 0-5.8-2.1-6.7-5H1.3v3.1A12 12 0 0 0 12 24z" />
            <path fill="#FBBC05" d="M5.3 14.3a7.2 7.2 0 0 1 0-4.6V6.6H1.3a12 12 0 0 0 0 10.8l4-3.1z" />
            <path fill="#EA4335" d="M12 4.8c1.8 0 3.3.6 4.6 1.8l3.4-3.4A12 12 0 0 0 12 0 12 12 0 0 0 1.3 6.6l4 3.1c.9-2.9 3.6-5 6.7-5z" />
          </svg>
          {{ t("login.google") }}
        </a>

        <a
          :href="oauthUrl('github')"
          class="flex w-full items-center justify-center gap-2 rounded-control border border-line bg-surface px-4 py-2.5 text-sm font-medium text-ink transition hover:bg-surface-sunken focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent-strong"
        >
          <svg class="h-4 w-4" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
            <path d="M12 .5a12 12 0 0 0-3.8 23.4c.6.1.8-.3.8-.6v-2c-3.3.7-4-1.6-4-1.6-.6-1.4-1.3-1.8-1.3-1.8-1.1-.7 0-.7 0-.7 1.2.1 1.8 1.2 1.8 1.2 1.1 1.8 2.8 1.3 3.5 1 .1-.8.4-1.3.8-1.6-2.7-.3-5.5-1.3-5.5-5.9 0-1.3.5-2.4 1.2-3.2 0-.4-.5-1.6.2-3.2 0 0 1-.3 3.3 1.2a11.5 11.5 0 0 1 6 0c2.3-1.5 3.3-1.2 3.3-1.2.7 1.6.2 2.8.1 3.2.8.8 1.2 1.9 1.2 3.2 0 4.6-2.8 5.6-5.5 5.9.5.4.8 1 .8 2.2v3.3c0 .3.2.7.8.6A12 12 0 0 0 12 .5z" />
          </svg>
          {{ t("login.github") }}
        </a>

        <!-- Without this the recovery pages exist and nobody can find them. -->
        <p class="text-center text-sm">
          <a href="/forgot-password" class="font-medium text-accent-strong hover:underline">
            {{ t("forgot.title") }}
          </a>
        </p>
      </form>
    </div>
  </div>
</template>
