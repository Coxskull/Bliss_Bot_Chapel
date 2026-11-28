using System.Buffers.Binary;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bliss.Api.Operations;

// Operator-triggered private custody only. Separate secret and fixed staging destination.
public static class CreatorFactoryArtworkTransfer
{
    private static readonly Uri Endpoint=new("https://alpha-hf-factory-engine-staging.up.railway.app/internal/bliss/held-creative-artwork");
    private static readonly HttpClient Client=new(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}){Timeout=TimeSpan.FromSeconds(30)};
    public static async Task<JsonElement?> Send(string body,byte[] png,CancellationToken ct)
    {
        var secret=Environment.GetEnvironmentVariable("BLISS_FACTORY_PRIVATE_ARTWORK_HMAC_KEY");
        if(!CreatorCompensationProposal.ValidDocumentHash(secret) || secret==Environment.GetEnvironmentVariable("BLISS_FACTORY_CREATIVE_INTAKE_HMAC_KEY")
            || png.Length is <33 or >16000000)return null;
        var manifestBytes=Encoding.UTF8.GetBytes(body);
        if(manifestBytes.Length is <2 or >16384)return null;
        using var source=JsonDocument.Parse(body);
        var m=source.RootElement;
        var manifestHash=Convert.ToHexString(SHA256.HashData(manifestBytes)).ToLowerInvariant();
        var imageHash=Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant();
        if(imageHash!=m.GetProperty("imageSha256").GetString())return null;
        var payload=new byte[4+manifestBytes.Length+png.Length];
        BinaryPrimitives.WriteUInt32BigEndian(payload.AsSpan(0,4),(uint)manifestBytes.Length);
        manifestBytes.CopyTo(payload,4);png.CopyTo(payload,4+manifestBytes.Length);
        var timestamp=DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        using var mac=IncrementalHash.CreateHMAC(HashAlgorithmName.SHA256,Convert.FromHexString(secret!));
        mac.AppendData(Encoding.UTF8.GetBytes("alpha-bliss-held-artwork-v1\n"+timestamp+"\n"));
        mac.AppendData(payload);
        var signature=Convert.ToHexString(mac.GetHashAndReset()).ToLowerInvariant();
        using var req=new HttpRequestMessage(HttpMethod.Post,Endpoint);
        req.Content=new ByteArrayContent(payload);
        req.Content.Headers.ContentType=new MediaTypeHeaderValue("application/octet-stream");
        req.Headers.Add("X-Alpha-Timestamp",timestamp);req.Headers.Add("X-Alpha-Signature",signature);
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        try{
            using var response=await Client.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
            if(!response.IsSuccessStatusCode || response.Content.Headers.ContentLength>4096)return null;
            await using var stream=await response.Content.ReadAsStreamAsync(deadline.Token);
            var buffer=new byte[4097];var offset=0;
            while(offset<buffer.Length){var n=await stream.ReadAsync(buffer.AsMemory(offset),deadline.Token);if(n==0)break;offset+=n;}
            if(offset>4096||offset==0)return null;
            using var doc=JsonDocument.Parse(buffer.AsMemory(0,offset));
            var r=doc.RootElement;
            if(r.GetProperty("schema").GetString()!="alpha.henry-ford.held-artwork-custody.v1"
                || r.GetProperty("placementId").GetGuid()!=m.GetProperty("placementId").GetGuid()
                || r.GetProperty("handoffId").GetGuid()!=m.GetProperty("handoffId").GetGuid()
                || r.GetProperty("manifestSha256").GetString()!=manifestHash || r.GetProperty("imageSha256").GetString()!=imageHash
                || r.GetProperty("imageWidth").GetInt32()!=m.GetProperty("imageWidth").GetInt32()
                || r.GetProperty("imageHeight").GetInt32()!=m.GetProperty("imageHeight").GetInt32()
                || r.GetProperty("status").GetString()!="HELD_ARTWORK_STORED" || r.GetProperty("release").GetString()!="HELD"
                || r.GetProperty("delivery").GetString()!="NOT_SENT" || r.GetProperty("renderAuthorized").GetBoolean()
                || r.GetProperty("dispatchAuthorized").GetBoolean() || r.GetProperty("paymentAuthorized").GetBoolean()
                || !r.GetProperty("receivedAt").TryGetDateTimeOffset(out _))return null;
            return r.Clone();
        }catch(Exception e) when(e is HttpRequestException or IOException or OperationCanceledException or JsonException or InvalidOperationException or FormatException or KeyNotFoundException){return null;}
    }
}
