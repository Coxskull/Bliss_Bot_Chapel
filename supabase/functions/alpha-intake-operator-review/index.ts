import "jsr:@supabase/functions-js/edge-runtime.d.ts";
import { createClient } from "npm:@supabase/supabase-js@2.95.0";
import { createReviewHandler } from "./http-handler.mjs";

// #330 owner-authorized single-operator release candidate. Backup/restore,
// live RPC and exact identity enrollment are documented on the issue.
// Runtime switch remains mandatory; no public or multi-operator release.
const PRODUCTION_INTAKE_REVIEW_RELEASE_APPROVED = true;
const TRUSTED_ORIGIN = "https://alphacreativecontent.com";
const UUID = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
Deno.serve(createReviewHandler({
  getEnv: (name: string) => {
    if (name === "ALPHA_INTAKE_REVIEW_ENABLED") {
      const id = Deno.env.get("ALPHA_INTAKE_REVIEW_USER_IDS") || "";
      return PRODUCTION_INTAKE_REVIEW_RELEASE_APPROVED &&
        UUID.test(id) && Deno.env.get("ALPHA_INTAKE_REVIEW_ORIGINS") === TRUSTED_ORIGIN &&
        Deno.env.get(name) === "true" ? "true" : "false";
    }
    return Deno.env.get(name);
  },
  createClient
}));
