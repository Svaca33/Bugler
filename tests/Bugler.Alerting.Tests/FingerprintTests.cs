using Bugler.Alerting.DetectEpisodes;

namespace Bugler.Alerting.Tests;

public class FingerprintTests
{
    [Fact]
    public void The_senders_template_is_the_truth_when_it_travels_along()
    {
        var fingerprint = Fingerprint.Of(
            "Payment declined for order {OrderId}: {DeclineReason}",
            eventName: null,
            body: "Payment declined for order 1298: insufficient funds");

        Assert.Equal("Payment declined for order {OrderId}: {DeclineReason}", fingerprint);
    }

    [Fact]
    public void The_event_name_stands_in_when_there_is_no_template()
    {
        Assert.Equal("checkout.payment_declined", Fingerprint.Of(
            null, "checkout.payment_declined", "Payment declined for order 1298"));
    }

    [Fact]
    public void A_bare_body_is_normalized_so_its_variable_parts_do_not_split_kinds()
    {
        var first = Fingerprint.Of(null, null,
            "Request 0198f3f2-4d1f-7aaa-bccc-0123456789ab failed after 1500 ms (attempt 3)");
        var second = Fingerprint.Of(null, null,
            "Request 0198f3f2-9999-7bbb-dddd-fedcba987654 failed after 800 ms (attempt 12)");

        Assert.Equal(first, second);
        Assert.Equal("Request <id> failed after <n> ms (attempt <n>)", first);
    }

    [Fact]
    public void Hex_runs_are_blanked_like_numbers()
    {
        Assert.Equal("Span <hex> was dropped", Fingerprint.Of(null, null, "Span bc6ad7697424da4e was dropped"));
    }

    [Fact]
    public void A_missing_or_empty_body_still_yields_a_fingerprint()
    {
        Assert.Equal("(no body)", Fingerprint.Of(null, null, null));
        Assert.Equal("(no body)", Fingerprint.Of(null, null, "   "));
    }

    [Fact]
    public void A_fingerprint_never_outgrows_its_column()
    {
        var fingerprint = Fingerprint.Of(new string('x', 500), null, null);
        Assert.Equal(300, fingerprint.Length);
    }
}
