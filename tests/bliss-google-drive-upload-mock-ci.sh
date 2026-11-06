#!/usr/bin/env bash
# Mock-only Google Drive uploader checks. No network or provider credentials.
set -Eeuo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
script="$root/scripts/bliss-google-drive-upload-operator.sh"
base="$(mktemp -d "$HOME/bliss-drive-test-XXXXXXXX")"
trap 'rm -rf "$base"' EXIT
mkdir -m 700 "$base/local" "$base/remote" "$base/bin"
chmod g-s "$base" "$base/local" "$base/remote" "$base/bin"
chmod 700 "$base" "$base/local" "$base/remote" "$base/bin"
cat > "$base/bin/rclone" <<'MOCK'
#!/usr/bin/env bash
set -Eeuo pipefail
[[ "$1" == --config ]] || exit 88; shift 2
[[ "$1" == --drive-root-folder-id && "$2" == 1qwj-2nGmxTQJl2LSKvNkBtfs8rcXtOSo ]] || exit 89
shift 2
cmd="$1"; shift
case "$cmd" in
  lsf) [[ "$1" == drive: ]] || exit 90 ;;
  copyto)
     [[ "$1" == --immutable && "$2" == -- ]] || exit 91; shift 2
     src="$1"; dst="$2"
     [[ "$dst" == drive:* ]] || exit 92
     final="$MOCK_DRIVE_ROOT/${dst#drive:}"
     [[ ! -f "$final" ]] || cmp -s "$src" "$final" || exit 93
     cp -- "$src" "$final"
     ;;
  cat)
     [[ "$1" == drive:* ]] || exit 94
     if [[ "${MOCK_TAMPER_REMOTE:-}" == 1 ]]; then printf 'spoofed'; else cat -- "$MOCK_DRIVE_ROOT/${1#drive:}"; fi
     ;;
  *) exit 95 ;;
esac
MOCK
chmod 700 "$base/bin/rclone"
cat > "$base/config.conf" <<'CONF'
[drive]
type = drive
token = synthetic-test-not-real
CONF
chmod 600 "$base/config.conf"
export PATH="$base/bin:$PATH" MOCK_DRIVE_ROOT="$base/remote" RCLONE_CONFIG="$base/config.conf" BLISS_DRIVE_RCLONE_REMOTE=drive BLISS_DRIVE_UPLOAD_APPROVED=YES BLISS_DRIVE_FOLDER_ID=1qwj-2nGmxTQJl2LSKvNkBtfs8rcXtOSo
localname="bliss-backup-20261105T130000Z-ABCDEFGH.age"
localfile="$base/local/$localname"
printf 'age-encryption.org/v1\nsynthetic encrypted bytes no customer data\n' > "$localfile"
chmod 600 "$localfile"
( cd "$base/local" && sha256sum "$localname" > "$localname.sha256" )
chmod 600 "$localfile.sha256"
reject() {
  local label="$1" status=0 out
  shift
  out="$("$@" 2>&1)" || status=$?
  [[ "$status" -ne 0 && "$out" == *'BLISS DRIVE UPLOAD REFUSED'* ]] || {
    echo "FAIL negative: $label (rc $status output $out)" >&2; exit 1;
  }
  echo "PASS negative: $label"
}
reject no-approval env BLISS_DRIVE_UPLOAD_APPROVED='' bash "$script" "$localfile"
reject wrong-folder env BLISS_DRIVE_FOLDER_ID=wrong bash "$script" "$localfile"
reject CI env CI=true bash "$script" "$localfile"
reject bad-config env RCLONE_CONFIG=/not-existing bash "$script" "$localfile"
reject remote-nondrive env BLISS_DRIVE_RCLONE_REMOTE=other bash "$script" "$localfile"
reject absent-manifest bash "$script" "$base/local/no-such.age"
reject corrupted-manifest sh -c 'echo BAD > "$1"; bash "$2" "$3"' sh "$localfile.sha256" "$script" "$localfile"
( cd "$base/local" && sha256sum "$localname" > "$localname.sha256" )
[[ -z "$(find "$base/remote" -type f -print -quit)" ]] || exit 1

# Explicitly simulate an operator environment for the local, mock-only rclone;
# no real token, socket, service, or Google Drive request is available.
env CI=false GITHUB_ACTIONS=false bash "$script" "$localfile"
cmp "$localfile" "$base/remote/$localname"
cmp "$localfile.sha256" "$base/remote/$localname.sha256"
reject readback-tamper env CI=false GITHUB_ACTIONS=false MOCK_TAMPER_REMOTE=1 bash "$script" "$localfile"
echo 'PASS: pinned Drive folder, encrypted-only output, archive/manifest byte readback, and negative guards (mock only)'
