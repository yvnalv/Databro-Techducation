import type { Config } from "tailwindcss";
import preset from "@databro/ui/tailwind-preset";

const config: Partial<Config> = {
  presets: [preset],
  // The Nuxt module already scans this app. The shared UI package must be listed explicitly: its
  // classes live outside the app root, so without this the primitives and the block renderer's
  // styles are purged from the production build and the editor preview renders unstyled.
  content: ["../../packages/ui/src/**/*.{vue,ts}"],
};

export default config;
