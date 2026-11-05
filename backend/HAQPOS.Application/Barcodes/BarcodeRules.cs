using System.Globalization;
using System.Security.Cryptography;

namespace HAQPOS.Application.Barcodes;

public static class BarcodeRules
{
    public static string Generate() =>
        RandomNumberGenerator.GetInt32(100_000_000, 1_000_000_000).ToString(CultureInfo.InvariantCulture);
}
