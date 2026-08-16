<script setup lang="ts">
import { DbButton, DbInput } from "@databro/ui";
import { ApiClientError } from "@databro/api-client";

/**
 * Sign-in for the authoring app.
 *
 * Uses the shared primitives rather than bespoke form markup, so the focus rings, error wiring and
 * disabled states are the ones already covered by tests in `@databro/ui`.
 */
definePageMeta({ layout: false });

const route = useRoute();
const { login, isAuthenticated } = useAuth();

const email = ref("");
const password = ref("");
const formError = ref<string | null>(null);
const submitting = ref(false);

// Only same-origin paths: an open redirect would let a crafted link bounce a signed-in editor to
// an attacker's page carrying their trust in this domain.
const redirectTo = computed(() => {
  const target = String(route.query.redirect ?? "/");
  return target.startsWith("/") && !target.startsWith("//") ? target : "/";
});

// Already signed in and hitting /login directly: go where they were headed.
if (isAuthenticated.value) await navigateTo(redirectTo.value, { replace: true });

async function submit() {
  formError.value = null;
  submitting.value = true;

  try {
    await login(email.value, password.value);
    await navigateTo(redirectTo.value, { replace: true });
  } catch (error) {
    // Deliberately identical for a wrong password and an unknown address: distinguishing them
    // turns the form into an account-enumeration oracle.
    formError.value =
      error instanceof ApiClientError && error.status === 401
        ? "Those credentials did not match an account."
        : "Sign-in failed. Please try again.";
  } finally {
    submitting.value = false;
  }
}

useHead({ title: "Sign in" });
</script>

<template>
  <div class="flex min-h-screen items-center justify-center bg-surface-sunken px-4 font-sans text-ink antialiased">
    <div class="w-full max-w-sm">
      <div class="text-center">
        <span class="inline-flex text-accent"><AppBrandMark /></span>
        <h1 class="mt-6 font-display text-2xl font-bold tracking-tight text-ink">Sign in</h1>
        <p class="mt-2 text-sm text-ink-muted">Manage articles and taxonomy.</p>
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
          label="Email"
          type="email"
          placeholder="you@databro.id"
          required
          :disabled="submitting"
        />

        <DbInput
          v-model="password"
          label="Password"
          type="password"
          required
          :disabled="submitting"
        />

        <DbButton type="submit" block size="lg" :disabled="submitting">
          {{ submitting ? "Signing in…" : "Sign in" }}
        </DbButton>
      </form>
    </div>
  </div>
</template>
