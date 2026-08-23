using Bugler.Alerting.DetectEpisodes;
using Bugler.Alerting.Settings;

namespace Bugler.Alerting.Tests;

/// <summary>
/// The recipe of ADR 0033, over stack traces as the runtimes actually write them. What is being
/// asserted is almost never a literal hash — it is that two occurrences of one bug meet and two
/// different bugs do not, and that a stack Bugler cannot read coarsens visibly instead of
/// producing a plausible fingerprint over nonsense.
/// </summary>
public class FingerprintTests
{
    private static FingerprintReading Read(
        string? stack = null,
        string? runtime = null,
        string? type = null,
        string? template = null,
        string? body = null,
        string? eventName = null,
        string? attribute = null,
        FingerprintRule rule = FingerprintRule.ThrowingCode) =>
        Fingerprint.Of(
            new FingerprintEvidence(template, eventName, body, type, stack, runtime, attribute),
            rule);

    // ---- The ladder ------------------------------------------------------------------------

    [Fact]
    public void A_named_attribute_outranks_every_rung_below_it()
    {
        var reading = Read(
            stack: DotNetStack("Acme.Payments.Charge", 42),
            runtime: "dotnet",
            type: "System.TimeoutException",
            attribute: "checkout.timeout");

        Assert.Equal(FingerprintRung.NamedAttribute, reading.Rung);
        Assert.Equal("checkout.timeout", reading.Title);
    }

    [Fact]
    public void The_throwing_code_is_the_identity_when_a_stack_can_be_read()
    {
        var reading = Read(
            stack: DotNetStack("Acme.Payments.Charge", 42),
            runtime: "dotnet",
            type: "System.TimeoutException",
            template: "Payment failed for order {OrderId}");

        Assert.Equal(FingerprintRung.Stack, reading.Rung);
        Assert.Equal("TimeoutException: Payment failed for order {OrderId}", reading.Title);
    }

    [Fact]
    public void A_match_with_no_stack_falls_to_the_kind_of_failure()
    {
        var reading = Read(type: "System.TimeoutException", template: "Warehouse timed out");

        Assert.Equal(FingerprintRung.Failure, reading.Rung);
        Assert.Equal("TimeoutException: Warehouse timed out", reading.Title);
    }

    [Fact]
    public void A_match_with_neither_stack_nor_type_falls_all_the_way_to_what_was_said()
    {
        var reading = Read(template: "Warehouse timed out");

        Assert.Equal(FingerprintRung.Message, reading.Rung);
        Assert.Equal("Warehouse timed out", reading.Title);
    }

    [Fact]
    public void A_runtime_with_no_recipe_coarsens_visibly_rather_than_hashing_nonsense()
    {
        var erlang = """
            ** exception error: no match of right hand side value {error,timeout}
                 in function  acme_pay:charge/2 (src/acme_pay.erl, line 42)
            """;

        var reading = Read(stack: erlang, runtime: "erlang", type: "badmatch", body: "charge failed");

        Assert.Equal(FingerprintRung.Failure, reading.Rung);
    }

    [Fact]
    public void The_rule_decides_where_the_ladder_starts()
    {
        var stack = DotNetStack("Acme.Payments.Charge", 42);

        var byCode = Read(stack, "dotnet", "System.TimeoutException", "Payment failed");
        var byFailure = Read(
            stack, "dotnet", "System.TimeoutException", "Payment failed",
            rule: FingerprintRule.KindOfFailure);
        var bySaying = Read(
            stack, "dotnet", "System.TimeoutException", "Payment failed",
            rule: FingerprintRule.WhatWasSaid);

        Assert.Equal(FingerprintRung.Stack, byCode.Rung);
        Assert.Equal(FingerprintRung.Failure, byFailure.Rung);
        Assert.Equal(FingerprintRung.Message, bySaying.Rung);
        Assert.Equal(3, new[] { byCode, byFailure, bySaying }.Select(r => r.Fingerprint).Distinct().Count());
    }

    // ---- The gap that started this ----------------------------------------------------------

    [Fact]
    public void Serilogs_message_template_is_read_alongside_the_dotnet_loggers_one()
    {
        var serilog = Read(
            template: "MongoDb transaction commit error", body: "MongoDb transaction commit error");
        var bare = Read(body: "MongoDb transaction commit error");

        Assert.Equal(FingerprintRung.Message, serilog.Rung);
        Assert.Equal(bare.Fingerprint, serilog.Fingerprint);
    }

    [Fact]
    public void One_generic_sentence_from_two_call_sites_is_two_kinds_of_trouble()
    {
        // The failure this whole decision exists to end: one Episode over "MongoDb transaction
        // commit error", fed by unrelated failures with nothing in common but the sentence.
        var template = "MongoDb transaction commit error";
        var checkout = Read(
            DotNetStack("Acme.Checkout.Commit", 42), "dotnet", "MongoDB.Driver.MongoException", template);
        var warehouse = Read(
            DotNetStack("Acme.Warehouse.Reserve", 91), "dotnet", "MongoDB.Driver.MongoException", template);

        Assert.NotEqual(checkout.Fingerprint, warehouse.Fingerprint);
    }

    // ---- What the frames must survive -------------------------------------------------------

    [Fact]
    public void A_dotnet_stack_carrying_a_hostname_in_its_message_still_yields_one_kind()
    {
        var first = Read(DotNetStackWithMessage("db-07", 1298), "dotnet", "System.TimeoutException");
        var second = Read(DotNetStackWithMessage("db-11", 4417), "dotnet", "System.TimeoutException");

        Assert.Equal(FingerprintRung.Stack, first.Rung);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
    }

    [Fact]
    public void A_deploy_that_shifted_every_line_does_not_split_one_trouble_in_two()
    {
        var before = Read(DotNetStack("Acme.Payments.Charge", 42), "dotnet", "System.Exception");
        var after = Read(DotNetStack("Acme.Payments.Charge", 57), "dotnet", "System.Exception");

        Assert.Equal(before.Fingerprint, after.Fingerprint);
    }

    [Fact]
    public void Recursion_of_two_depths_is_one_bug_rather_than_two()
    {
        string Recursive(int depth) =>
            "System.StackOverflowException: too deep\n"
            + string.Join("\n", Enumerable.Repeat("   at Acme.Tree.Walk(Node n)", depth))
            + "\n   at Acme.Tree.Start()";

        Assert.Equal(
            Read(Recursive(4), "dotnet", "System.StackOverflowException").Fingerprint,
            Read(Recursive(40), "dotnet", "System.StackOverflowException").Fingerprint);
    }

    [Fact]
    public void A_java_caused_by_chain_is_read_by_its_frames_and_not_by_its_messages()
    {
        string Java(string tenant) => $"""
            java.lang.IllegalStateException: cannot commit for {tenant}
            	at com.acme.pay.Checkout.commit(Checkout.java:88)
            	at com.acme.pay.Checkout.run(Checkout.java:41)
            	... 14 more
            Caused by: java.net.SocketTimeoutException: connect timed out to {tenant}-db:5432
            	at java.base/java.net.Socket.connect(Socket.java:633)
            	... 9 more
            """;

        var first = Read(Java("acme"), "java", "java.lang.IllegalStateException");
        var second = Read(Java("globex"), "java", "java.lang.IllegalStateException");

        Assert.Equal(FingerprintRung.Stack, first.Rung);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
    }

    [Fact]
    public void Dotnet_async_frames_survive_the_end_of_stack_trace_lines()
    {
        var async = """
            System.InvalidOperationException: no channel
               at Acme.Bus.Publisher.PublishAsync(Message m) in /src/Bus/Publisher.cs:line 61
            --- End of stack trace from previous location ---
               at Acme.Checkout.Handler.HandleAsync(Order o) in /src/Checkout/Handler.cs:line 22
            """;

        var reading = Read(async, "dotnet", "System.InvalidOperationException");

        Assert.Equal(FingerprintRung.Stack, reading.Rung);
    }

    [Fact]
    public void A_python_traceback_ignores_the_source_lines_echoed_under_its_frames()
    {
        string Python(string line) => $"""
            Traceback (most recent call last):
              File "/app/checkout.py", line 42, in commit
                {line}
              File "/app/db.py", line 91, in execute
                {line}
            psycopg.OperationalError: connection timed out
            """;

        var first = Read(Python("cursor.execute(sql)"), "python", "psycopg.OperationalError");
        var second = Read(Python("cursor.execute(  sql  )"), "python", "psycopg.OperationalError");

        Assert.Equal(FingerprintRung.Stack, first.Rung);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
    }

    [Fact]
    public void A_go_panic_is_read_by_its_functions_and_not_by_its_pointer_arguments()
    {
        string Go(string pointer) => $"""
            panic: runtime error: invalid memory address or nil pointer dereference
            [signal SIGSEGV: segmentation violation code=0x1 addr=0x0 pc=0x45f0a1]

            goroutine 17 [running]:
            github.com/acme/pay.(*Charger).Charge({pointer}, 0x2)
            	/app/pay/charge.go:42 +0x1a
            main.main()
            	/app/main.go:12 +0x25
            """;

        var first = Read(Go("0xc000012345"), "go", "runtime.Error");
        var second = Read(Go("0xc0000abcde"), "go", "runtime.Error");

        Assert.Equal(FingerprintRung.Stack, first.Rung);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
    }

    [Fact]
    public void A_php_trace_keeps_the_call_and_drops_the_path_and_the_arguments()
    {
        string Php(string order) => $$"""
            #0 /app/src/Pay.php(42): Acme\Pay->charge('{{order}}')
            #1 /app/src/Http.php(18): Acme\Controller->run(Array)
            #2 {main}
            """;

        var first = Read(Php("A-1298"), "php", "RuntimeException");
        var second = Read(Php("B-4417"), "php", "RuntimeException");

        Assert.Equal(FingerprintRung.Stack, first.Rung);
        Assert.Equal(first.Fingerprint, second.Fingerprint);
    }

    [Fact]
    public void A_ruby_frame_keeps_its_path_because_that_is_where_its_identity_lives()
    {
        // Two `block in call` frames are the same words in different files: dropping the path
        // would merge two unrelated troubles into one.
        var checkout = Read(
            "/app/checkout.rb:42:in 'block in call'\n/app/rack.rb:12:in 'call'", "ruby", "Timeout::Error");
        var warehouse = Read(
            "/app/warehouse.rb:42:in 'block in call'\n/app/rack.rb:12:in 'call'", "ruby", "Timeout::Error");

        Assert.Equal(FingerprintRung.Stack, checkout.Rung);
        Assert.NotEqual(checkout.Fingerprint, warehouse.Fingerprint);
    }

    // ---- Truncation --------------------------------------------------------------------------

    [Fact]
    public void A_truncated_stack_is_read_from_both_ends_and_says_that_it_was_cut()
    {
        var truncated =
            "System.Exception: boom\n"
            + "   at Acme.Top.Enter()\n"
            + "   at Acme.Middle.Sev" + StackFrames.TruncationMarker + "ered.Frame()\n"
            + "   at Acme.Bottom.Leave()";

        var reading = Read(truncated, "dotnet", "System.Exception");

        Assert.Equal(FingerprintRung.Stack, reading.Rung);
        Assert.True(reading.StackTruncated);
        // The seam's half-frame is gone; the ends the recipe could read are not.
        Assert.Equal(
            Read("   at Acme.Top.Enter()\n   at Acme.Bottom.Leave()", "dotnet", "System.Exception")
                .Fingerprint,
            reading.Fingerprint);
    }

    [Fact]
    public void A_stack_cut_down_to_nothing_readable_still_coarsens_rather_than_guessing()
    {
        var reading = Read(
            "boom " + StackFrames.TruncationMarker + " boom", "dotnet", "System.Exception", "boom");

        Assert.Equal(FingerprintRung.Failure, reading.Rung);
        Assert.True(reading.StackTruncated);
    }

    // ---- What a person reads -----------------------------------------------------------------

    [Fact]
    public void The_title_blanks_the_values_of_a_body_that_travelled_alone()
    {
        var first = Read(body: "Request 0198f3f2-4d1f-7aaa-bccc-0123456789ab failed after 1500 ms");
        var second = Read(body: "Request 0198f3f2-9999-7bbb-dddd-fedcba987654 failed after 800 ms");

        Assert.Equal(first.Fingerprint, second.Fingerprint);
        Assert.Equal("Request <id> failed after <n> ms", first.Title);
    }

    [Fact]
    public void An_event_name_stands_in_where_no_template_travelled()
    {
        Assert.Equal(
            "checkout.payment_declined",
            Read(eventName: "checkout.payment_declined", body: "Payment declined for order 1298").Title);
    }

    [Fact]
    public void A_match_that_said_nothing_at_all_still_has_a_kind_and_a_name()
    {
        var reading = Read();

        Assert.Equal(FingerprintRung.Message, reading.Rung);
        Assert.Equal("(no body)", reading.Title);
        Assert.NotEmpty(reading.Fingerprint);
    }

    [Fact]
    public void A_title_never_outgrows_its_column()
    {
        Assert.Equal(
            Fingerprint.MaxTitleLength, Read(template: new string('x', 500)).Title.Length);
    }

    [Fact]
    public void A_fingerprint_is_opaque_and_fits_the_column_whatever_it_was_distilled_from()
    {
        var reading = Read(DotNetStack("Acme.Payments.Charge", 42), "dotnet", "System.Exception");

        Assert.Equal(32, reading.Fingerprint.Length);
        Assert.True(reading.Fingerprint.Length <= Fingerprint.MaxLength);
        Assert.DoesNotContain("Acme", reading.Fingerprint, StringComparison.Ordinal);
    }

    private static string DotNetStack(string method, int line) =>
        $"""
        System.Exception: boom
           at {method}(Order order) in /src/Payments.cs:line {line}
           at Acme.Api.Endpoint.Post(Request request) in /src/Api/Endpoint.cs:line 18
        """;

    private static string DotNetStackWithMessage(string host, int transaction) =>
        $"""
        System.TimeoutException: connection to {host} timed out (transaction {transaction})
           at Acme.Payments.Charge(Order order) in /src/Payments.cs:line 42
           at Acme.Api.Endpoint.Post(Request request) in /src/Api/Endpoint.cs:line 18
        """;
}
