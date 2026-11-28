using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Bliss.Api.Operations;

// Fixed existing staging destination; no redirects, retries, artifact uploads or execution.
public static class CreatorFactorySubmission
{
    private static readonly Uri Endpoint=new("https://alpha-hf-factory-engine-staging.up.railway.app/internal/bliss/held-creative-jobs");
    private static readonly HttpClient Client=new(new HttpClientHandler{AllowAutoRedirect=false,UseCookies=false}){Timeout=TimeSpan.FromSeconds(12)};
    public static string Serialize(object manifest)=>JsonSerializer.Serialize(manifest,new JsonSerializerOptions(JsonSerializerDefaults.Web));
    public static async Task<JsonElement?> Submit(string body,Guid handoffId,CancellationToken ct,bool readOnly=false,bool renderStatus=false)
    {
        var secret=Environment.GetEnvironmentVariable("BLISS_FACTORY_CREATIVE_INTAKE_HMAC_KEY");
        if(!CreatorCompensationProposal.ValidDocumentHash(secret) || Encoding.UTF8.GetByteCount(body)>16384)return null;
        using var source=JsonDocument.Parse(body);
        var placementId=source.RootElement.GetProperty("placementId").GetGuid();
        if(source.RootElement.GetProperty("handoffId").GetGuid()!=handoffId)return null;
        var bytes=Encoding.UTF8.GetBytes(body);
        var digest=Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var timestamp=DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);
        var signed=Encoding.UTF8.GetBytes(((readOnly||renderStatus)?"alpha-bliss-held-creative-read-v1\n":"alpha-bliss-held-creative-v1\n")+timestamp+"\n"+body);
        var signature=Convert.ToHexString(HMACSHA256.HashData(Convert.FromHexString(secret!),signed)).ToLowerInvariant();
        using var req=new HttpRequestMessage(HttpMethod.Post,renderStatus?new Uri(Endpoint.AbsoluteUri+"/render-status"):readOnly?new Uri(Endpoint.AbsoluteUri+"/readback"):Endpoint);
        req.Content=new ByteArrayContent(bytes);
        req.Content.Headers.ContentType=new MediaTypeHeaderValue("application/json");
        req.Headers.Add("X-Alpha-Timestamp",timestamp);
        req.Headers.Add("X-Alpha-Signature",signature);
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(12));
        try{
            using var response=await Client.SendAsync(req,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
            if(!response.IsSuccessStatusCode || response.Content.Headers.ContentLength>16384)return null;
            await using var stream=await response.Content.ReadAsStreamAsync(deadline.Token);
            var buffer=new byte[16385];var offset=0;
            while(offset<buffer.Length){
                var n=await stream.ReadAsync(buffer.AsMemory(offset),deadline.Token);
                if(n==0)break;offset+=n;
            }
            if(offset>16384 || offset==0)return null;
            using var doc=JsonDocument.Parse(buffer.AsMemory(0,offset));
            var v=doc.RootElement;
            if(renderStatus){
                if(v.GetProperty("schema").GetString()!="alpha.henry-ford.held-render-status.v1" ||
                    v.GetProperty("placementId").GetGuid()!=placementId || v.GetProperty("handoffId").GetGuid()!=handoffId ||
                    v.GetProperty("manifestSha256").GetString()!=digest || v.GetProperty("release").GetString()!="HELD" ||
                    v.GetProperty("delivery").GetString()!="NOT_SENT" || v.GetProperty("dispatchAuthorized").GetBoolean() ||
                    v.GetProperty("visualQa").GetString()!="HUMAN_REVIEW_PENDING")return null;
                var rows=v.GetProperty("results");
                if(rows.ValueKind!=JsonValueKind.Array||rows.GetArrayLength()>20)return null;
                foreach(var row in rows.EnumerateArray()){
                    var status=row.GetProperty("status").GetString();
                    if(status is not ("SCHEDULE_RECORDED" or "CLAIMED_OUTCOME_UNCONFIRMED" or "PRIVATE_PREVIEW_READY" or "REVIEW_REQUIRED" or "FAILED_OR_INCOMPLETE")
                        || !CreatorCompensationProposal.ValidDocumentHash(row.GetProperty("specSha256").GetString()))return null;
                    foreach(var key in new[]{"compositionSha256","previewSha256","qaSha256","receiptSha256"}){
                        var hash=row.GetProperty(key);
                        if(status is "PRIVATE_PREVIEW_READY" or "REVIEW_REQUIRED"){
                            if(!CreatorCompensationProposal.ValidDocumentHash(hash.GetString()))return null;
                        }else if(hash.ValueKind!=JsonValueKind.Null)return null;
                    }
                }
                return v.Clone();
            }
            if(v.GetProperty("schema").GetString()!="alpha.henry-ford.held-job-admission.v1"
                || v.GetProperty("placementId").GetGuid()!=placementId || v.GetProperty("handoffId").GetGuid()!=handoffId
                || v.GetProperty("manifestSha256").GetString()!=digest || v.GetProperty("status").GetString()!="HELD_METADATA_ONLY"
                || v.GetProperty("release").GetString()!="HELD" || v.GetProperty("delivery").GetString()!="NOT_SENT"
                || v.GetProperty("renderAuthorized").GetBoolean() || v.GetProperty("dispatchAuthorized").GetBoolean()
                || v.GetProperty("paymentAuthorized").GetBoolean() || !v.GetProperty("admittedAt").TryGetDateTimeOffset(out _))return null;
            return v.Clone();
        }catch(Exception e) when(e is HttpRequestException or IOException or OperationCanceledException or JsonException or InvalidOperationException or FormatException or KeyNotFoundException){return null;}
    }
}
