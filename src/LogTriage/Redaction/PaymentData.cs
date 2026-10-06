using System.Text.RegularExpressions;

namespace LogTriage.Redaction;

/// <summary>
/// Detects payment details: card numbers (13 to 19 digits with a card-like first digit and a valid
/// Luhn checksum) and compact IBANs (valid mod-97 checksum). Best effort, and biased towards
/// false positives: harmless numbers are sometimes flagged, which only hides request data.
/// </summary>
internal static partial class PaymentData
{
    private const int MaxCardDigits = 19;

    public static bool AppearsIn(string text) =>
        CardCandidates().Matches(text).Any(static match => IsCardNumber(match.Value))
        || IbanCandidates().Matches(text).Any(static match => IsIban(match.Value));

    private static bool IsCardNumber(string candidate)
    {
        Span<int> digits = stackalloc int[MaxCardDigits];
        var count = 0;
        foreach (var character in candidate)
        {
            if (char.IsAsciiDigit(character))
                digits[count++] = character - '0';
        }

        if (digits[0] is < 2 or > 6)
            return false;

        var sum = 0;
        for (var index = 0; index < count; index++)
        {
            var digit = digits[count - 1 - index];
            if (index % 2 == 1)
                digit = digit * 2 > 9 ? digit * 2 - 9 : digit * 2;

            sum += digit;
        }

        return sum % 10 == 0;
    }

    private static bool IsIban(string candidate)
    {
        var remainder = 0;
        foreach (var character in string.Concat(candidate.AsSpan(4), candidate.AsSpan(0, 4)))
        {
            var value = char.IsAsciiDigit(character) ? character - '0' : character - 'A' + 10;
            remainder = (value > 9 ? remainder * 100 + value : remainder * 10 + value) % 97;
        }

        return remainder == 1;
    }

    [GeneratedRegex(@"(?<!\d)(?:\d[ -]?){12,18}\d(?!\d)")]
    private static partial Regex CardCandidates();

    [GeneratedRegex(@"\b[A-Z]{2}\d{2}[A-Z0-9]{11,30}\b")]
    private static partial Regex IbanCandidates();
}
