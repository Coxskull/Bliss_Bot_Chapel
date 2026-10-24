#!/usr/bin/env python3
"""Offline, names-only inventory. Never authenticates, connects, or grants access."""
import argparse
import json
import re

REQUIRED = (
    "Authentication__Enabled", "Authentication__SupabaseEnabled",
    "Authentication__Authority", "Authentication__ClientId", "Authentication__ClientSecret",
    "Authentication__SupabasePublishableKey", "ConnectionStrings__DefaultConnection",
    "Runtime__DataProtectionKeysPath", "Runtime__DataProtectionCertificatePath",
    "Runtime__DataProtectionCertificatePassword", "Runtime__Backup__Provider",
    "Runtime__Backup__Schedule", "Runtime__Backup__RetentionDays",
)


def inspect_source(source):
    """Compare configured source metadata only. Neither side proves deployed bytes."""
    if not isinstance(source, dict) or set(source) != {
        "main_repo", "main_sha", "configured_repo", "configured_sha"
    }:
        raise ValueError("Expected only the four non-secret Git source identity fields.")
    repo_pattern = r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+"
    for key in ("main_repo", "configured_repo"):
        if not isinstance(source[key], str) or not re.fullmatch(repo_pattern, source[key]):
            raise ValueError("Invalid Git repository identity.")
    for key in ("main_sha", "configured_sha"):
        if not isinstance(source[key], str) or not re.fullmatch(r"[0-9a-fA-F]{40}", source[key]):
            raise ValueError("Invalid Git commit SHA.")
    # A self-consistent but unrelated repository is NOT valid ALPHA provenance.\n    if source["main_repo"].casefold() != "coxskull/bliss_bot_chapel":\n        raise ValueError("Unexpected main repository identity.")\n    return ("CONFIG_MATCH" if
            source["main_repo"].casefold() == source["configured_repo"].casefold()
            and source["main_sha"].lower() == source["configured_sha"].lower()
            else "CONFIG_DRIFT")


def inspect(names, source=None):
    if not isinstance(names, list) or any(not isinstance(n, str) for n in names):
        raise ValueError("Expected an array of variable names, never values.")
    source_parity = "NOT_PROVIDED" if source is None else inspect_source(source)
    names = set(names)
    missing = [n for n in REQUIRED if n not in names]
    for prefix in ("Authentication__ApprovedTestSubjects", "Runtime__KnownProxies"):
        if not any(re.fullmatch(re.escape(prefix) + r"__(0|[1-9][0-9]*)", n) for n in names):
            missing.append(prefix + "__0")
    return {
        "inventory_status": "BLOCKED" if missing else "PRESENT_UNVERIFIED",
        "missing_required_variable_names": missing,
        "configuration_values_validated": False,
        "hosted_login_test": "NOT_TESTED",
        "role_isolation_test": "NOT_TESTED",
        "application_database_test": "NOT_TESTED",
        "frontend_api_test": "NOT_TESTED",
        "configured_source_parity": source_parity,
        "hosted_backup_schedule_verified": False,
        "hosted_restore_verified": False,
        "release_acceptance": "BLOCKED_PENDING_INDEPENDENT_EVIDENCE",
        "operational_acceptance": False,
        "production_changes": "NONE",
    }


def read_strict_json(stream):
    """Reject ambiguous key duplication and non-JSON numeric constants."""
    def unique_pairs(pairs):
        obj = {}
        for key, value in pairs:
            if key in obj:
                raise ValueError("Duplicate JSON field.")
            obj[key] = value
        return obj

    def reject_constant(_):
        raise ValueError("Invalid JSON numeric constant.")

    return json.load(stream, object_pairs_hook=unique_pairs, parse_constant=reject_constant)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("names_file", help="JSON array of variable names only")
    parser.add_argument("--source-state", help="Optional JSON file with four non-secret Git source identity fields")
    args = parser.parse_args()
    try:
        with open(args.names_file, encoding="utf-8") as source:
            names = read_strict_json(source)
        source_state = None
        if args.source_state:
            with open(args.source_state, encoding="utf-8") as source:
                source_state = read_strict_json(source)
        report = inspect(names, source_state)
    except (OSError, ValueError, TypeError):
        print(json.dumps({"inventory_status": "INVALID_INPUT", "operational_acceptance": False}))
        return 2
    print(json.dumps(report, indent=2))
    return 2 if report["inventory_status"] == "BLOCKED" or report["configured_source_parity"] == "CONFIG_DRIFT" else 0


if __name__ == "__main__":
    raise SystemExit(main())
