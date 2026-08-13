using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PayCalc24.Integration.Services;

internal static class CanonicalHash
{
    public static string Create(params object?[] values)
    {
        var canonical=string.Join("|",values.Select(Format));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }
    private static string Format(object? value)=>value switch
    {
        null=>string.Empty,
        decimal number=>number.ToString("0.00000000",CultureInfo.InvariantCulture),
        DateOnly date=>date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),
        Enum enumeration=>enumeration.ToString().ToUpperInvariant(),
        _=>Convert.ToString(value,CultureInfo.InvariantCulture)??string.Empty
    };
}
