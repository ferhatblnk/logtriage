using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace LogTriage.Grouping;

/// <summary>
/// The identity of an error: service, exception type, HTTP method, route and status code.
/// Message and stack trace are left out on purpose, so a changed line number or a different
/// order id in the message does not start a new group.
/// </summary>
public static class Fingerprint
{
    private const int Length = 16;

    public static string Of(ErrorEvent error)
    {
        var identity = string.Join(
            '\n',
            error.Service,
            error.ExceptionType,
            error.Method,
            error.Route,
            error.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? "");

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(identity)))[..Length];
    }
}
