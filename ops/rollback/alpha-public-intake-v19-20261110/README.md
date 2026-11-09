# Alpha Public Intake — exact hosted v19 before Task20 $9 AI module release

Captured from Supabase project `bkutbglzivfdnoerigyb`, Edge function `alpha-public-intake`, ACTIVE version **19**, `verify_jwt=false` (public forms intentionally unauthenticated), immediately before optional release. These are original source files and must not be served directly. Existing public inquiry/CRM source in this snapshot is untouched.

For a rollback, an authorized operator should deploy exactly these six files with `index.ts` as entrypoint and preserve the existing JWT setting `false`, then verify public forms and keep `ALPHA_ASK_ALPHA_ENABLED=false`. Never overwrite secrets or private database state; rollback the Edge source only. **No customer data or API secret values were included.**

The latest Ask Alpha candidate source lives at `supabase/functions/alpha-public-intake/ask-alpha.mjs` on `codex/alpha-prelaunch-safe-integration-20261110`. This backup branch is intentionally separate and is NOT a production main branch merge.
