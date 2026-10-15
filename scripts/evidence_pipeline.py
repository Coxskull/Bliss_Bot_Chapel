"""Local v2 evidence preparation/verification; Drive I/O belongs to authorized connectors.

No credential, network, payment, acceptance, or spreadsheet-write capability.
The separate submission index commits the manifest without circular self-hashing.
"""
import hashlib
import json
import re
import secrets
import uuid
import zipfile
from pathlib import Path, PurePosixPath

PROTOCOL = "AGM-AUTONOMOUS-EVIDENCE-PIPELINE-v2"
RESERVED = {"manifest.json", "submission-index.json", "owner-notification.txt"}
MAX_BYTES = 32 * 1024 * 1024


def canonical(value):
    return (json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False, allow_nan=False) + "\n").encode()


def load_json(path):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("Duplicate JSON key")
            result[key] = value
        return result
    return json.loads(Path(path).read_text(), object_pairs_hook=unique,
                      parse_constant=lambda value: (_ for _ in ()).throw(ValueError("Nonfinite JSON")))


def valid_id(value):
    if not isinstance(value, str) or not value.startswith("AGM-EV-"):
        raise ValueError("CSPRNG UUIDv4 evidence ID required")
    parsed = uuid.UUID(value[7:])
    if parsed.version != 4 or parsed.variant != uuid.RFC_4122 or value != "AGM-EV-" + str(parsed):
        raise ValueError("Canonical UUIDv4 evidence ID required")
    return value


def generate_evidence_id(existing_ids):
    known = set(existing_ids)
    for _ in range(32):
        value = "AGM-EV-" + str(uuid.UUID(bytes=secrets.token_bytes(16), version=4))
        if value not in known:
            return value
    raise ValueError("Evidence registry collision limit exceeded")


def artifact_path(root, relative):
    if not isinstance(relative, str) or not relative or "\\" in relative or ":" in relative or any(ord(c) < 32 for c in relative):
        raise ValueError("Unsafe artifact path")
    name = PurePosixPath(relative)
    if name.is_absolute() or str(name) != relative or any(p in (".", "..") for p in name.parts):
        raise ValueError("Unsafe artifact path")
    root = Path(root)
    if root.is_symlink():
        raise ValueError("Symlink artifact root refused")
    path = root / relative
    if any((root.joinpath(*name.parts[:n])).is_symlink() for n in range(1, len(name.parts) + 1)):
        raise ValueError("Symlink artifact refused")
    if not path.is_file() or not path.resolve().is_relative_to(root.resolve()) or path.stat().st_size > MAX_BYTES:
        raise ValueError("Missing, escaped or oversized artifact")
    return path


def calculate_sha256(path):
    with Path(path).open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256")
    return digest.hexdigest()


def collect_test_artifacts(root, relative_paths):
    if not relative_paths or len(relative_paths) > 100:
        raise ValueError("Bounded explicit artifact list required")
    artifacts, seen = [], set()
    for relative in relative_paths:
        if relative.casefold() in seen or relative in RESERVED:
            raise ValueError("Duplicate or circular/reserved artifact")
        seen.add(relative.casefold())
        path = artifact_path(root, relative)
        artifacts.append({"path": relative, "bytes": path.stat().st_size, "sha256": calculate_sha256(path)})
    if sum(a["bytes"] for a in artifacts) > MAX_BYTES:
        raise ValueError("Submission byte bound exceeded")
    return sorted(artifacts, key=lambda a: a["path"])


def construct_manifest(root, evidence_id, task_id, milestone_id, repository, verification,
                       paths, limitations, dependencies, supersedes=None):
    manifest = {"protocol": PROTOCOL, "evidence_id": valid_id(evidence_id), "task_id": task_id,
                "milestone_id": milestone_id, "submission_status": "PACKAGING", "repository": repository,
                "verification": verification, "artifacts": collect_test_artifacts(root, paths),
                "known_limitations": limitations, "blocking_dependencies": dependencies,
                "supersedes_evidence_id": supersedes, "executive_decision": "PENDING_REVIEW",
                "engineering_self_approval": False, "production_authorized": False}
    validate_manifest(root, manifest)
    return manifest


def validate_manifest(root, manifest):
    valid_id(manifest["evidence_id"])
    if manifest.get("protocol") != PROTOCOL or manifest.get("submission_status") != "PACKAGING":
        raise ValueError("Unknown protocol or manifest state")
    if manifest.get("executive_decision") != "PENDING_REVIEW" or manifest.get("engineering_self_approval") is not False or manifest.get("production_authorized") is not False:
        raise ValueError("Engineering cannot grant acceptance or production authority")
    if not re.fullmatch(r"AGM-(?:OPS|LAUNCH|A2A)-[0-9]{3}(?:-[A-Z0-9]+)*", manifest["task_id"]):
        raise ValueError("Canonical task ID required")
    repo = manifest["repository"]
    if not re.fullmatch(r"[0-9a-f]{40}", repo["head_sha"]) or not repo.get("name") or not repo.get("branch") or not manifest.get("milestone_id"):
        raise ValueError("Exact source references required")
    results = manifest["verification"]
    if not results.get("test_commands") or any(not isinstance(x, str) or not x.strip() for x in results["test_commands"]):
        raise ValueError("Actual test commands required")
    for name in ("passed", "failed", "skipped"):
        if type(results.get(name)) is not int or results[name] < 0:
            raise ValueError("Exact nonnegative test counts required")
    if not results.get("test_environment") or not results.get("execution_timestamp"):
        raise ValueError("Environment and timestamp required")
    if not isinstance(manifest.get("known_limitations"), list) or not isinstance(manifest.get("blocking_dependencies"), list):
        raise ValueError("Explicit limitation/dependency lists required")
    prior = manifest.get("supersedes_evidence_id")
    if prior is not None:
        valid_id(prior)
        if prior == manifest["evidence_id"]:
            raise ValueError("A submission cannot supersede itself")
    if manifest["artifacts"] != collect_test_artifacts(root, [a["path"] for a in manifest["artifacts"]]):
        raise ValueError("Artifact bytes or manifest inventory changed")
    return manifest


def package_evidence(root, manifest, destination):
    validate_manifest(root, manifest)
    destination = Path(destination)
    # Exclusive creation prevents silently replacing an existing submission.
    with destination.open("xb") as stream, zipfile.ZipFile(stream, "w", zipfile.ZIP_DEFLATED) as archive:
        archive.writestr("manifest.json", canonical(manifest))
        for artifact in manifest["artifacts"]:
            data = artifact_path(root, artifact["path"]).read_bytes()
            if hashlib.sha256(data).hexdigest() != artifact["sha256"]:
                raise ValueError("Artifact changed while packaging")
            archive.writestr(artifact["path"], data)
    return calculate_sha256(destination)


def verify_drive_upload(root, manifest, receipts, download_root, reviewer_access, expected_folder_id):
    """Rehash actual connector-downloaded bytes. Metadata alone is insufficient.

    ACL/account binding evidence is provided by the authorized connector caller;
    it is not independently authenticated by this local helper.
    """
    validate_manifest(root, manifest)
    wanted = {a["path"]: a for a in manifest["artifacts"]}
    raw_manifest = canonical(manifest)
    wanted["manifest.json"] = {"bytes": len(raw_manifest), "sha256": hashlib.sha256(raw_manifest).hexdigest()}
    paths = [r.get("path") for r in receipts]
    if len(paths) != len(set(paths)) or set(paths) != set(wanted) or len({r.get("file_id") for r in receipts}) != len(receipts):
        raise ValueError("Missing, duplicate or extra Drive receipts")
    verified = []
    for receipt in receipts:
        if not receipt.get("file_id") or receipt.get("parent_id") != expected_folder_id:
            raise ValueError("Drive identity or folder mismatch")
        path = artifact_path(download_root, receipt["path"])
        expected = wanted[receipt["path"]]
        if path.stat().st_size != expected["bytes"] or calculate_sha256(path) != expected["sha256"]:
            raise ValueError("Downloaded Drive content differs from submission")
        verified.append({"path": receipt["path"], "file_id": receipt["file_id"], "sha256": expected["sha256"]})
    access = reviewer_access.get("status") == "VERIFIED_EXISTING_ACL_ACCOUNT_BINDING" and bool(reviewer_access.get("evidence_reference"))
    return {"evidence_id": manifest["evidence_id"], "content_verified": True,
            "reviewer_access_verified": access, "reviewer_access": reviewer_access,
            "folder_id": expected_folder_id, "artifacts": verified}


def commit_submission(root, manifest, verification, registry_match_count, download_root):
    validate_manifest(root, manifest)
    if verification.get("evidence_id") != manifest["evidence_id"] or registry_match_count != 1:
        raise ValueError("Evidence ID must resolve to exactly one registered submission")
    receipts = [{"path": a["path"], "file_id": a["file_id"], "parent_id": verification["folder_id"]}
                for a in verification.get("artifacts", [])]
    repeated = verify_drive_upload(root, manifest, receipts, download_root, verification["reviewer_access"], verification["folder_id"])
    if repeated != verification:
        raise ValueError("Upload verification changed before commit")
    ready = verification.get("content_verified") is True and verification.get("reviewer_access_verified") is True and manifest["verification"]["failed"] == 0
    return {"protocol": PROTOCOL, "evidence_id": manifest["evidence_id"], "task_id": manifest["task_id"],
            "submission_status": "READY_FOR_EXECUTIVE_REVIEW" if ready else "SUBMISSION_INCOMPLETE",
            "manifest_sha256": hashlib.sha256(canonical(manifest)).hexdigest(), "upload_verification": verification,
            "executive_decision": "PENDING_REVIEW", "engineering_self_approval": False,
            "limitations": [] if ready else ["Content, reviewer access or zero-failure submission gate is unverified"]}


def generate_owner_notification(index, downloaded_index, index_receipt):
    if index.get("submission_status") != "READY_FOR_EXECUTIVE_REVIEW" or Path(downloaded_index).read_bytes() != canonical(index):
        raise ValueError("Owner-ready notification requires committed and read-back index")
    if not index_receipt.get("file_id") or index_receipt.get("parent_id") != index["upload_verification"]["folder_id"]:
        raise ValueError("Committed index Drive receipt is required")
    return ("EVIDENCE ID: " + valid_id(index["evidence_id"]) + "\nTASK: " + index["task_id"]
            + "\nSTATUS: READY_FOR_EXECUTIVE_REVIEW\nGOOGLE DRIVE: UPLOAD_VERIFIED\n")
