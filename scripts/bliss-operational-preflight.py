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


def inspect(names):
    if not isinstance(names, list) or any(not isinstance(n, str) for n in names):
        raise ValueError("Expected an array of variable names, never values.")
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
        "operational_acceptance": False,
        "production_changes": "NONE",
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("names_file", help="JSON array of variable names only")
    args = parser.parse_args()
    try:
        with open(args.names_file, encoding="utf-8") as source:
            report = inspect(json.load(source))
    except (OSError, ValueError, TypeError):
        print(json.dumps({"inventory_status": "INVALID_INPUT", "operational_acceptance": False}))
        return 2
    print(json.dumps(report, indent=2))
    return 2 if report["inventory_status"] == "BLOCKED" else 0


if __name__ == "__main__":
    raise SystemExit(main())
