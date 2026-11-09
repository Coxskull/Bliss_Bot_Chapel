# Task20 — nonbillable model lookup and project-selection preflight

Source: scripts/task20-openai-project-model-preflight.mjs

The preflight only uses official GET /v1/models/gpt-6-luna with explicit OpenAI-Project and OpenAI-Organization headers. It does not create a Responses generation, send customer information, or spend generated model tokens. Reference: https://developers.openai.com/api/reference/resources/models/methods/retrieve

It is **NOT authorized for automatic execution in GitHub Actions**. The included GitHub workflow tests mocked responses only, with no service keys and no live API calls.

An authorized operator can run the preflight **on the existing trusted private application server**, using the server secret manager, with these variables set privately:
- OPENAI_API_KEY: existing service key, never displayed in console, ChatGPT or GitHub
- ALPHA_ASK_ALPHA_OPENAI_PROJECT_ID: verified Ask Alpha Production project identifier, in proj_... format
- ALPHA_ASK_ALPHA_OPENAI_ORGANIZATION_ID: verified ALPHA GLOBAL MEDIA LLC organization identifier, in org_... format
- TASK20_NONBILLABLE_ACCESS_CHECK: YES, explicit one-time manual authorization

Then run node scripts/task20-openai-project-model-preflight.mjs in that private runtime. The output contains only a boolean and generic status, never credentials or IDs. A PASS confirms that the configured headers and key obtained the selected model's metadata, and that the provider-returned organization header matched. It **does not prove** that this key can generate Responses, that actual billed usage matches the database, or that the OpenAI project $9 hard cap cannot lag in enforcement.

The owner has approved $10 credits and an enforced $9 project limit to keep $1 reserved. Do not change that arrangement. No paid inference or public AI enablement is authorized by this source-only preflight. Supabase hosted $9 migration, trusted ingress overwrite, Task #1 recovery, privacy approval and human support remain gated.
