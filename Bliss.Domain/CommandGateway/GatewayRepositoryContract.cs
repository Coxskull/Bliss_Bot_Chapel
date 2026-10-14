namespace Bliss.Domain.CommandGateway;

public sealed record GatewayRepositoryPolicy(bool Enabled,Guid TenantId,Guid AgentId,string Repository,string BaseBranch,
 IReadOnlySet<string> AllowedPathPrefixes,IReadOnlySet<string> RequiredChecks,int MaxChangedFiles);
public sealed record GatewayChangeProposal(string Repository,string BaseBranch,string BaseSha,string HeadBranch,string HeadSha,IReadOnlyList<string> ChangedPaths);
public sealed record GatewayCiCheck(string Name,string Conclusion);
public sealed record GatewayCiEvidence(string Repository,string HeadSha,string ArtifactSha256,IReadOnlyList<GatewayCiCheck> Checks);
public sealed record GatewayCiReview(string Repository,string BaseSha,string HeadSha,IReadOnlyList<string> ChangedPaths,
 IReadOnlyList<string> PassedChecks,string ArtifactSha256,bool ReadyForHumanReview,bool ApprovalGranted=false,
 bool MergeAuthorized=false,bool DeploymentAuthorized=false,bool ToolExecutionAuthorized=false);

// Pure authorization/evidence contract. It cannot call GitHub, Codex, CI, a shell or deployment system.
public static class GatewayRepositoryContract
{
 public static GatewayCiReview Validate(GatewayPrincipal principal,GatewayRepositoryPolicy policy,GatewayChangeProposal proposal,GatewayCiEvidence evidence)
 {
  if(!policy.Enabled||!principal.Authenticated||principal.TenantId==Guid.Empty||principal.AgentId==Guid.Empty||principal.TenantId!=policy.TenantId||principal.AgentId!=policy.AgentId)throw new UnauthorizedAccessException("Trusted engineering repository grant required.");
  if(!Repository(policy.Repository)||proposal.Repository!=policy.Repository||evidence.Repository!=policy.Repository||!Branch(policy.BaseBranch)||proposal.BaseBranch!=policy.BaseBranch)throw new UnauthorizedAccessException("Exact granted repository and base branch required.");
  if(!Hash(proposal.BaseSha,40,false)||!Hash(proposal.HeadSha,40,false)||proposal.BaseSha==proposal.HeadSha||evidence.HeadSha!=proposal.HeadSha||!Branch(proposal.HeadBranch)||!proposal.HeadBranch.StartsWith("feature/",StringComparison.Ordinal))throw new ArgumentException("Exact distinct Git identities and feature branch required.");
  if(policy.MaxChangedFiles is<1 or>200||proposal.ChangedPaths.Count<1||proposal.ChangedPaths.Count>policy.MaxChangedFiles||proposal.ChangedPaths.Distinct(StringComparer.Ordinal).Count()!=proposal.ChangedPaths.Count)throw new ArgumentException("Bounded unique changed paths required.");
  var prefixes=policy.AllowedPathPrefixes.Select(Prefix).ToArray();if(prefixes.Length is 0 or>16||prefixes.Distinct(StringComparer.Ordinal).Count()!=prefixes.Length)throw new ArgumentException("Bounded allowed path prefixes required.");
  var paths=proposal.ChangedPaths.Select(Path).ToArray();if(paths.Any(p=>Forbidden(p)||!prefixes.Any(prefix=>p.StartsWith(prefix,StringComparison.Ordinal))))throw new UnauthorizedAccessException("Changed path is outside granted source scope.");
  var required=policy.RequiredChecks.Order(StringComparer.Ordinal).ToArray();if(required.Length is 0 or>16||required.Any(x=>!Name(x))||required.Distinct(StringComparer.Ordinal).Count()!=required.Length)throw new ArgumentException("Bounded unique required checks required.");
  if(evidence.Checks.Count!=required.Length||evidence.Checks.Any(x=>x is null||!Name(x.Name)||x.Conclusion!="SUCCESS")||evidence.Checks.Select(x=>x.Name).Distinct(StringComparer.Ordinal).Count()!=required.Length||!evidence.Checks.Select(x=>x.Name).Order(StringComparer.Ordinal).SequenceEqual(required))throw new InvalidOperationException("Exact successful required CI checks required.");
  if(!Hash(evidence.ArtifactSha256,64,true))throw new ArgumentException("Exact reported CI artifact SHA256 required.");
  return new(policy.Repository,proposal.BaseSha,proposal.HeadSha,paths,required,evidence.ArtifactSha256,true);
 }
 private static bool Repository(string s)=>!string.IsNullOrWhiteSpace(s)&&s.Length<=120&&s.Count(c=>c=='/')==1&&s.All(c=>char.IsLetterOrDigit(c)||c is '-' or '_' or '.' or '/');
 private static bool Branch(string s)=>!string.IsNullOrWhiteSpace(s)&&s.Length<=120&&!s.StartsWith('/')&&!s.EndsWith('/')&&!s.Contains("..",StringComparison.Ordinal)&&s.All(c=>char.IsLetterOrDigit(c)||c is '-' or '_' or '.' or '/');
 private static string Path(string s){if(string.IsNullOrWhiteSpace(s)||s.Length>240||s.StartsWith('/')||s.Contains('\\')||s.Split('/').Any(x=>x is "" or "." or "..")||s.Any(char.IsControl))throw new ArgumentException("Normalized relative path required.");return s;}
 private static string Prefix(string s){if(string.IsNullOrWhiteSpace(s)||!s.EndsWith('/',StringComparison.Ordinal))throw new ArgumentException("Directory prefix ending slash required.");return Path(s[..^1])+"/";}
 private static bool Forbidden(string p)=>p.StartsWith(".github/",StringComparison.Ordinal)||p.StartsWith("deploy/",StringComparison.Ordinal)||p.StartsWith("infra/",StringComparison.Ordinal)||p.StartsWith("k8s/",StringComparison.Ordinal)||p.Split('/').Any(x=>x.StartsWith(".env",StringComparison.OrdinalIgnoreCase)||x.EndsWith(".key",StringComparison.OrdinalIgnoreCase)||x.EndsWith(".pem",StringComparison.OrdinalIgnoreCase)||x.EndsWith(".pfx",StringComparison.OrdinalIgnoreCase));
 private static bool Name(string s)=>!string.IsNullOrWhiteSpace(s)&&s.Length<=80&&!s.Any(char.IsControl);
 private static bool Hash(string s,int n,bool upper)=>s?.Length==n&&s.All(c=>c is>='0' and<='9'||(upper?c is>='A' and<='F':c is>='a' and<='f'));
}
