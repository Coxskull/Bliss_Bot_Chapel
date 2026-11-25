using System.Security.Cryptography;
using System.Text;
using System.Globalization;

namespace Bliss.Api.Security;

// A receipt-bound, short-lived proof of channel control. RequestKey is an
// existing random UUID kept out of all public/status/operator responses.
// The operator must independently observe the code on the submitted channel.
public static class CreatorChannelProof
{
    public static string Code(Guid receiptId, Guid requestKey, Guid creatorId, string channelUrl, DateTime utcNow)
    {
        var input = $"alpha-channel-control-v1\n{receiptId:D}\n{requestKey:D}\n{creatorId:D}\n{channelUrl}\n{utcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
        return "ALPHA-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)))[..32];
    }

    public static bool Matches(string? observed, Guid receiptId, Guid requestKey, Guid creatorId, string channelUrl, DateTime utcNow)
    {
        if (observed is null || observed.Length != 38) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(observed), Encoding.UTF8.GetBytes(Code(receiptId, requestKey, creatorId, channelUrl, utcNow)));
    }
}
