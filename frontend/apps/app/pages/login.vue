<script setup lang="ts">
import { DbButton, DbInput } from "@databro/ui";

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
  submitting.value = true;

  try {
    await login(email.value, password.value);
    await go();
  } catch {
    // Deliberately identical for every failure — a wrong password, an unknown address, a server
    // error alike. Distinguishing the first two turns the form into an account-enumeration oracle,
    // which is why the status code is not inspected here at all.
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
      </form>
    </div>
  </div>
</template>
