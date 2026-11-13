# ALPHA GLOBAL MEDIA — Task #3B, owner-only database identity classifier.
# Read-only: no PostgreSQL connection, DDL, migration, backup, deployment or auth change.
# Uses the existing authenticated Railway CLI SSH session on the existing bliss-bot app.
# Does not print, persist, export, or send the connection string or its password.
# Run from Windows PowerShell with railway.cmd already authenticated as the owner.
# Never add -Verbose, Start-Transcript, shell tracing, or send diagnostic secrets to GitHub.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$railway = Get-Command railway.cmd -ErrorAction SilentlyContinue
if ($null -eq $railway) { $railway = Get-Command railway -ErrorAction SilentlyContinue }
if ($null -eq $railway) { throw 'Task3: Railway CLI is not available. No changes made.' }

# A deliberately limited Perl probe runs INSIDE the existing application container.
# Perl is already installed by the app's existing Dockerfile. No deployment or package install.
# It recognizes Npgsql-style Host/Database key-value settings, with quoted semicolon handling.
# Unknown, missing and ambiguous target settings FAIL CLOSED. No raw values are printed.
$probe = @'
use strict;
use warnings;
print "TASK3_REMOTE_EXEC=PASS\n";
sub trim { my $v = shift; $v =~ s/^\s+|\s+$//g; return $v; }
sub split_fields {
  my ($s) = @_;
  my (@parts, $buffer, $quote) = ((), '', '');
  for (my $i=0; $i<length($s); $i++) {
    my $c=substr($s,$i,1);
    if ($quote ne '') {
      if ($c eq $quote) {
        if ($i+1 < length($s) && substr($s,$i+1,1) eq $quote) {
          $buffer .= $c.$c; $i++; next;
        }
        $quote = '';
      }
      $buffer .= $c; next;
    }
    if ($c eq '"' || $c eq "'") { $quote=$c; $buffer.=$c; next; }
    if ($c eq ';') { push @parts,$buffer; $buffer=''; next; }
    $buffer.=$c;
  }
  die 'invalid' if $quote ne '';
  push @parts,$buffer;
  return @parts;
}
sub classify {
  my $s = $ENV{'ConnectionStrings__DefaultConnection'} // '';
  die 'missing' if trim($s) eq '';
  die 'unsupported' if $s =~ /^\s*postgres(?:ql)?:\/\//i;
  my ($host, $database);
  for my $field (split_fields($s)) {
    next if trim($field) eq '';
    my ($key,$value)=split /=/, $field, 2;
    die 'invalid' unless defined $value;
    $key=lc(trim($key)); $key =~ s/\s+/ /g;
    $value=trim($value);
    if (length($value)>=2) {
      my $q=substr($value,0,1);
      if (($q eq '"' || $q eq "'") && substr($value,-1,1) eq $q) {
        $value=substr($value,1,length($value)-2);
        if ($q eq '"') { $value =~ s/""/"/g; }
        else { $value =~ s/''/'/g; }
      }
    }
    if ($key eq 'host' || $key eq 'server' || $key eq 'data source') {
      die 'ambiguous_host' if defined($host) && lc($host) ne lc($value);
      $host=$value;
    }
    if ($key eq 'database' || $key eq 'initial catalog') {
      die 'ambiguous_database' if defined($database) && lc($database) ne lc($value);
      $database=$value;
    }
  }
  die 'host_or_db_missing' unless defined($host) && defined($database) && $host ne '' && $database ne '';
  $host=lc(trim($host)); $host =~ s/\.$//;
  my $hclass='OTHER_OR_EXTERNAL';
  if ($host eq 'db.bkutbglzivfdnoerigyb.supabase.co') { $hclass='BLISS_SUPABASE_DIRECT'; }
  elsif ($host =~ /^[a-z0-9.-]+\.railway\.internal$/) { $hclass='RAILWAY_INTERNAL'; }
  elsif ($host =~ /^[a-z0-9.-]+\.(?:proxy\.rlwy\.net|rlwy\.net)$/) { $hclass='RAILWAY_PUBLIC_PROXY'; }
  my $dclass='OTHER';
  if (lc(trim($database)) eq 'bliss') { $dclass='BLISS'; }
  elsif (lc(trim($database)) eq 'postgres') { $dclass='POSTGRES'; }
  return ($hclass,$dclass);
}
my @result=eval { classify() };
if ($@ || @result!=2) {
  print "TASK3_DB_IDENTITY=INVALID_OR_UNSUPPORTED\n";
  print "TASK3_NO_DATABASE_WRITES=PASS\n";
  exit 2;
}
print "TASK3_DB_IDENTITY=CLASSIFIED\n";
print "TASK3_HOST_CLASS=$result[0]\n";
print "TASK3_DATABASE_CLASS=$result[1]\n";
print "TASK3_NO_DATABASE_WRITES=PASS\n";
print "TASK3_SCHEMA_AND_BACKUP_ACCEPTANCE=NOT_ESTABLISHED\n";
'@

Write-Output 'TASK3_OPERATION=READ_ONLY_REMOTE_ENV_CLASSIFIER'
Write-Output 'TASK3_TARGET_SERVICE=bliss-bot / production'
# Pipe the NONSECRET Perl source through stdin rather than passing a multiline
# command as a Windows SSH argument. Railway SSH detects piped input and avoids
# a PTY. A private SSH key passphrase remains human-only, never read by script.
# The only remotely read environment value stays in the Railway app process.
$scriptOutput = @($probe | & $railway.Source ssh -p 'fb33c1b7-03fb-42c8-89b1-081951a738fd' -s 'bliss-bot' -e 'production' -- perl -)
$remoteExitCode = $LASTEXITCODE
$lines = @($scriptOutput | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ -ne '' })

# A Railway SSH wrapper may return zero even if its remote command never ran.
# The previous version treated that as success. Require a remote marker AND a
# complete allowlisted classification, otherwise fail closed.
if (@($lines | Where-Object { $_ -ceq 'TASK3_REMOTE_EXEC=PASS' }).Count -ne 1) {
    Write-Output 'TASK3_REMOTE_EXEC=NOT_VERIFIED'
    Write-Output 'TASK3_DB_IDENTITY=NOT_VERIFIED'
    throw 'Task3: Railway SSH closed without proof the remote Perl command ran. Check Railway SSH access; no database changes were made.'
}
Write-Output 'TASK3_REMOTE_EXEC=PASS'
if ($remoteExitCode -ne 0) {
    Write-Output 'TASK3_DB_IDENTITY=INVALID_OR_UNSUPPORTED'
    throw 'Task3: remote classifier refused the connection setting or failed; no database changes were made.'
}
$hostLines = @($lines | Where-Object { $_ -cmatch '^TASK3_HOST_CLASS=(RAILWAY_INTERNAL|RAILWAY_PUBLIC_PROXY|BLISS_SUPABASE_DIRECT|OTHER_OR_EXTERNAL)$' })
$dbLines = @($lines | Where-Object { $_ -cmatch '^TASK3_DATABASE_CLASS=(BLISS|POSTGRES|OTHER)$' })
$identityLines = @($lines | Where-Object { $_ -ceq 'TASK3_DB_IDENTITY=CLASSIFIED' })
$noWriteLines = @($lines | Where-Object { $_ -ceq 'TASK3_NO_DATABASE_WRITES=PASS' })
$holdLines = @($lines | Where-Object { $_ -ceq 'TASK3_SCHEMA_AND_BACKUP_ACCEPTANCE=NOT_ESTABLISHED' })
if ($identityLines.Count -ne 1 -or $hostLines.Count -ne 1 -or
    $dbLines.Count -ne 1 -or $noWriteLines.Count -ne 1 -or $holdLines.Count -ne 1) {
    Write-Output 'TASK3_DB_IDENTITY=NOT_VERIFIED'
    throw 'Task3: remote classifier output incomplete. Never infer the database identity from an SSH exit code.'
}
Write-Output 'TASK3_DB_IDENTITY=CLASSIFIED'
Write-Output $hostLines[0]
Write-Output $dbLines[0]
Write-Output 'TASK3_NO_DATABASE_WRITES=PASS'
Write-Output 'TASK3_SCHEMA_AND_BACKUP_ACCEPTANCE=NOT_ESTABLISHED'
Write-Output 'TASK3_REGISTRATION_ACTIVATION=NOT_AUTHORIZED'
