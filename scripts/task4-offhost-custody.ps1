# ALPHA Task4: validate only approved offsite recovery evidence providers.
# The Google Drive receipt records independent connected Drive verification
# plus the operator's explicit private-custody attestation, not a live API call.
function Test-Task4OffhostCustody($m) {
    if ($m.offhostSha256Verified -ne $true -or $m.offhostRemoteChecksum -cne $m.archiveSha256) { return $false }
    if ($m.offhostProvider -ceq 'DigitalOcean') { return ($m.offhostDropletId -ceq '571399692') }
    if ($m.offhostProvider -cne 'GoogleDrive') { return $false }
    return ($m.offhostDriveFolderId -ceq '1i3IQ1hFB8H6FazZXtgstZQq9OX-TgMyM' -and
        $m.offhostDriveArchiveId -cmatch '^[A-Za-z0-9_-]{10,200}$' -and
        $m.offhostDriveManifestId -cmatch '^[A-Za-z0-9_-]{10,200}$' -and
        $m.offhostDriveArchiveId -cne $m.offhostDriveManifestId -and
        $m.offhostDriveOwnerOnlyAttested -eq $true -and
        $m.offhostDriveIndependentReadbackAttested -eq $true -and
        $m.offhostDriveDownloadedCiphertextVerified -eq $true)
}
