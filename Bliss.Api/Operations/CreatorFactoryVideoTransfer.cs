using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
namespace Bliss.Api.Operations;
public static class CreatorFactoryVideoTransfer
{
    public static bool Enabled => Environment.GetEnvironmentVariable("BLISS_FACTORY_PRIVATE_VIDEO_READ")=="YES";
    private const int MaxBytes=16*1024*1024;
    private static readonly Uri Endpoint=new("https://alpha-hf-factory-engine-staging.up.railway.app/internal/bliss/held-creative-jobs/private-video");
    private static readonly HttpClient Client=new(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}){Timeout=TimeSpan.FromSeconds(45)};
    public static async Task<byte[]?> Read(string body,string specSha256,string previewSha256,CancellationToken ct)
    {
        var secret=Environment.GetEnvironmentVariable("BLISS_FACTORY_CREATIVE_INTAKE_HMAC_KEY");
        if(!Enabled||!CreatorCompensationProposal.ValidDocumentHash(secret)||!CreatorCompensationProposal.ValidDocumentHash(specSha256)
            ||!CreatorCompensationProposal.ValidDocumentHash(previewSha256)||Encoding.UTF8.GetByteCount(body)>16384)return null;
        var timestamp=DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var signed=Encoding.UTF8.GetBytes("alpha-bliss-private-video-read-v1\n"+timestamp+"\n"+specSha256+"\n"+previewSha256+"\n"+body);
        using var req=new HttpRequestMessage(HttpMethod.Post,Endpoint);
        req.Content=new ByteArrayContent(Encoding.UTF8.GetBytes(body));
        req.Content.Headers.ContentType=new MediaTypeHeaderValue("application/json");
        req.Headers.Add("X-Alpha-Timestamp",timestamp);
        req.Headers.Add("X-Alpha-Signature",Convert.ToHexString(HMACSHA256.HashData(Convert.FromHexString(secret!),signed)).ToLowerInvariant());
        req.Headers.Add("X-Alpha-Spec-Sha256",specSha256);
        req.Headers.Add("X-Alpha-Preview-Sha256",previewSha256);
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(45));
        try{
            using var response=await Client.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
            if(!response.IsSuccessStatusCode||response.Content.Headers.ContentType?.MediaType!="video/mp4"
                ||response.Content.Headers.ContentLength is not long len||len<12||len>MaxBytes
                ||!response.Headers.TryGetValues("X-Alpha-Preview-Sha256",out var values)||!values.SequenceEqual(new[]{previewSha256}))return null;
            await using var stream=await response.Content.ReadAsStreamAsync(deadline.Token);
            var bytes=new byte[(int)len];
            await stream.ReadExactlyAsync(bytes,deadline.Token);
            if(await stream.ReadAsync(new byte[1],deadline.Token)!=0
                ||Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()!=previewSha256)return null;
            return bytes;
        }catch(Exception e) when(e is HttpRequestException or IOException or OperationCanceledException or InvalidOperationException){return null;}
    }
}
