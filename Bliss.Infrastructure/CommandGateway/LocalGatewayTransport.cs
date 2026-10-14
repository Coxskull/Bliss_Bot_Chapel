using System.Security.Cryptography;
using Bliss.Domain.CommandGateway;
namespace Bliss.Infrastructure.CommandGateway;
// Unregistered in-process fixture adapter. Private key is injected by local fixture, never loaded from hosted config.
public sealed class LocalGatewayTransport
{
 private readonly LocalGatewayInbox inbox;private readonly GatewayRequestVerifier verifier;private readonly RSA signer;private readonly string keyId,audience;private readonly Guid tenant,gatewayAgent;private readonly TimeProvider clock;
 public LocalGatewayTransport(LocalGatewayInbox inbox,GatewayRequestVerifier verifier,RSA signer,string keyId,string receiptAudience,Guid tenant,Guid gatewayAgent,TimeProvider? clock=null)
 {
  GatewayTransportCodec.Audience(receiptAudience);if(signer.KeySize<2048||signer.KeySize>4096||string.IsNullOrWhiteSpace(keyId)||keyId.Length>80||keyId.Any(char.IsControl)||tenant==Guid.Empty||gatewayAgent==Guid.Empty)throw new ArgumentException("Explicit local gateway signing configuration required.");this.inbox=inbox;this.verifier=verifier;this.signer=signer;this.keyId=keyId;audience=receiptAudience;this.tenant=tenant;this.gatewayAgent=gatewayAgent;this.clock=clock??TimeProvider.System;
 }
 public async Task<GatewaySignedReceipt> Accept(GatewaySignedRequest request,CancellationToken ct=default)
 {
  var identity=verifier.Verify(request,clock.GetUtcNow());if(identity.Principal.TenantId!=tenant)throw new UnauthorizedAccessException("Gateway tenant mismatch.");var accepted=await inbox.Accept(request.Envelope,identity.Principal,identity.Policy,ct);var now=clock.GetUtcNow();var receipt=new GatewaySignedReceipt(new(accepted.MessageId,accepted.ContentHash,accepted.AcceptedAt,accepted.Replay,tenant,request.Envelope.SenderAgentId,request.Envelope.RecipientAgentId,gatewayAgent),keyId,now,now.AddMinutes(2),"");
  // Receipt signing follows durable acceptance. Signer/response failure may leave an accepted message;
  // exact authenticated retry retrieves the original acceptance, never executes twice.
  return receipt with{Signature=Convert.ToBase64String(signer.SignData(GatewayTransportCodec.ReceiptBytes(receipt,audience),HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))};
 }
}
