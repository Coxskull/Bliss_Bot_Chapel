import "jsr:@supabase/functions-js/edge-runtime.d.ts";
import { createClient } from "npm:@supabase/supabase-js@2.95.0";
import { createReviewHandler } from "./http-handler.mjs";

// ALPHA #330: NEVER deploy/enable this operator endpoint without the separate
// signed-session RPC/security review and live Auth/backup acceptance.
// All access, origin, AAL2, live-session and source-query gates are exercised
// by the same HTTP handler used in offline negative security CI tests.
Deno.serve(createReviewHandler({
  getEnv: (name: string) => Deno.env.get(name),
  createClient
}));
