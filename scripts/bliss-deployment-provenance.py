#!/usr/bin/env python3
"""Offline metadata-only release gate. No network or production access."""
import json
import re
import sys

EXPECTED = {
    "main_repository", "main_sha", "configured_repository", "configured_branch",
    "configured_sha", "deployment_metadata_sha", "deployment_status"
}
PINNED_REPOSITORY = "Coxskull/Bliss_Bot_Chapel"
SHA = re.compile(r"[0-9a-fA-F]{40}$")
REPO = re.compile(r"[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$")


def reject_duplicate_keys(pairs):
    """Prevent silent last-value-wins parsing of supplied evidence."""
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError("Duplicate metadata field")
        result[key] = value
    return result


def reject_json_constant(value):
    """Disallow non-JSON NaN and Infinity values accepted by json.load."""
    raise ValueError("Invalid JSON constant")


def inspect(data):
    if not isinstance(data, dict) or set(data) != EXPECTED:
        raise ValueError("Invalid metadata keys")
    for name in ("main_repository", "configured_repository"):
        if not isinstance(data[name], str) or not REPO.fullmatch(data[name]):
            raise ValueError("Invalid repository identity")
    if data["main_repository"].casefold() != PINNED_REPOSITORY.casefold():
        raise ValueError("Unexpected main repository identity")
    for name in ("main_sha", "configured_sha", "deployment_metadata_sha"):
        val = data[name]
        if val is not None and (
            not isinstance(val, str) or SHA.fullmatch(val) is None
        ):
            raise ValueError("Invalid commit")
    if data["main_sha"] is None or data["configured_branch"] not in ("main", None):
        raise ValueError("Missing main commit or invalid branch")
    if data["deployment_status"] not in ("SUCCESS", None):
        raise ValueError("Unsupported deployment state")
    if bool(data["deployment_metadata_sha"]) != (data["deployment_status"] == "SUCCESS"):
        raise ValueError("Deployment status does not match metadata presence")

    def eq(a, b):
        return "UNKNOWN" if a is None or b is None else (
            "PASS" if a.lower() == b.lower() else "FAIL"
        )

    checks = {
        "repository": eq(data["main_repository"], data["configured_repository"]),
        "branch": "PASS" if data["configured_branch"] == "main" else "UNKNOWN",
        "configured_vs_main": eq(data["configured_sha"], data["main_sha"]),
        "deployment_vs_main": eq(data["deployment_metadata_sha"], data["main_sha"]),
        "configured_vs_deployment": eq(data["configured_sha"], data["deployment_metadata_sha"]),
    }
    status = ("FAIL_DRIFT" if "FAIL" in checks.values() else
              "BLOCKED_INCOMPLETE" if "UNKNOWN" in checks.values() else
              "MATCH_UNATTESTED")
    return {
        "status": status,
        "checks": checks,
        "running_image_verified": False,
        "merged_fixes_verified": False,
        "ingress_verified": False,
        "backup_verified": False,
        "release_authorized": False,
    }


def main():
    if len(sys.argv) != 2:
        print('{"status":"INVALID_INPUT","release_authorized":false}')
        return 2
    try:
        with open(sys.argv[1], encoding="utf-8") as fh:
            result = inspect(json.load(
                fh, object_pairs_hook=reject_duplicate_keys,
                parse_constant=reject_json_constant))
    except (OSError, ValueError, TypeError):
        print('{"status":"INVALID_INPUT","release_authorized":false}')
        return 2
    print(json.dumps(result, sort_keys=True, indent=2))
    return 0 if result["status"] == "MATCH_UNATTESTED" else 2


if __name__ == "__main__":
    raise SystemExit(main())
