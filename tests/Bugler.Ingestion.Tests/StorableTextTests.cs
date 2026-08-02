using System.Reflection;
using System.Text.Json;
using Bugler.Ingestion.OtlpMapping;
using Bugler.SharedKernel;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;

namespace Bugler.Ingestion.Tests;

/// <summary>
/// A NUL is put in every place a sender can put one, and none of it reaches a row. Proving that
/// PostgreSQL then accepts the row is the integration tests' half of the job — this half is that
/// the character is gone before the row exists.
/// </summary>
public class StorableTextTests
{
    private static readonly ServiceId Service = ServiceId.New();

    private static AnyValue Text(string value) => new() { StringValue = value };

    [Fact]
    public void A_string_with_nothing_to_tame_is_returned_untouched()
    {
        var value = "Order failed";

        Assert.Same(value, StorableText.Tame(value));
        Assert.Null(StorableText.Tame(null));
    }

    [Fact]
    public void Every_nul_becomes_its_own_replacement_character()
    {
        Assert.Equal("���", StorableText.Tame("\0\0\0"));
    }

    [Fact]
    public void Tabs_and_newlines_are_not_the_problem_and_are_left_alone()
    {
        var stackTrace = "at Checkout.Pay()\r\n\tat Checkout.Submit()";

        Assert.Same(stackTrace, StorableText.Tame(stackTrace));
    }

    [Fact]
    public void No_nul_a_log_export_can_carry_reaches_its_row()
    {
        var record = new LogRecord
        {
            SeverityText = "ERROR\0",
            Body = Text("Connection reset\0"),
            Attributes =
            {
                new KeyValue { Key = "payload\0key", Value = Text("payload\0value") },
                new KeyValue
                {
                    Key = "nested",
                    Value = new AnyValue
                    {
                        KvlistValue = new KeyValueList
                        {
                            Values = { new KeyValue { Key = "inner\0key", Value = Text("inner\0value") } },
                        },
                    },
                },
                new KeyValue
                {
                    Key = "list",
                    Value = new AnyValue { ArrayValue = new ArrayValue { Values = { Text("item\0") } } },
                },
            },
        };

        var scopeLogs = new ScopeLogs { Scope = new InstrumentationScope { Name = "Bugler\0.Tests" } };
        scopeLogs.LogRecords.Add(record);
        var resourceLogs = new ResourceLogs
        {
            Resource = new Resource
            {
                Attributes = { new KeyValue { Key = "host\0.name", Value = Text("web\0-01") } },
            },
        };
        resourceLogs.ScopeLogs.Add(scopeLogs);
        var request = new ExportLogsServiceRequest();
        request.ResourceLogs.Add(resourceLogs);

        var row = Assert.Single(OtlpLogMapper.Map(request, Service).Rows);

        AssertNothingUnstorable(row);
        Assert.Equal("Connection reset�", row.Body);
        Assert.Equal("ERROR�", row.SeverityText);
        Assert.Equal("Bugler�.Tests", row.ScopeName);

        using var resource = JsonDocument.Parse(row.ResourceAttributes);
        Assert.Equal("web�-01", resource.RootElement.GetProperty("host�.name").GetString());

        using var attributes = JsonDocument.Parse(row.Attributes);
        Assert.Equal("payload�value", attributes.RootElement.GetProperty("payload�key").GetString());
        Assert.Equal(
            "inner�value",
            attributes.RootElement.GetProperty("nested").GetProperty("inner�key").GetString());
        Assert.Equal("item�", attributes.RootElement.GetProperty("list")[0].GetString());
    }

    [Fact]
    public void No_nul_a_trace_export_can_carry_reaches_its_row()
    {
        var span = new Span
        {
            TraceId = Bytes(16),
            SpanId = Bytes(8),
            Name = "GET /orders\0",
            Status = new Status { Message = "upstream said\0" },
            Attributes = { new KeyValue { Key = "http\0.route", Value = Text("/orders/\0") } },
            Events =
            {
                new Span.Types.Event
                {
                    Name = "exception\0",
                    Attributes = { new KeyValue { Key = "type\0", Value = Text("IOException\0") } },
                },
            },
            Links =
            {
                new Span.Types.Link
                {
                    TraceId = Bytes(16),
                    SpanId = Bytes(8),
                    Attributes = { new KeyValue { Key = "link\0", Value = Text("upstream\0") } },
                },
            },
        };

        var scopeSpans = new ScopeSpans { Scope = new InstrumentationScope { Name = "Bugler\0.Tracing" } };
        scopeSpans.Spans.Add(span);
        var resourceSpans = new ResourceSpans
        {
            Resource = new Resource
            {
                Attributes = { new KeyValue { Key = "host\0.name", Value = Text("web\0-01") } },
            },
        };
        resourceSpans.ScopeSpans.Add(scopeSpans);
        var request = new ExportTraceServiceRequest();
        request.ResourceSpans.Add(resourceSpans);

        var row = Assert.Single(OtlpTraceMapper.Map(request, Service).Rows);

        AssertNothingUnstorable(row);
        Assert.Equal("GET /orders�", row.Name);
        Assert.Equal("upstream said�", row.StatusMessage);
        Assert.Equal("Bugler�.Tracing", row.ScopeName);

        using var events = JsonDocument.Parse(row.Events);
        var only = events.RootElement[0];
        Assert.Equal("exception�", only.GetProperty("name").GetString());
        Assert.Equal("IOException�", only.GetProperty("attributes").GetProperty("type�").GetString());

        using var links = JsonDocument.Parse(row.Links);
        Assert.Equal(
            "upstream�",
            links.RootElement[0].GetProperty("attributes").GetProperty("link�").GetString());
    }

    private static Google.Protobuf.ByteString Bytes(int length) =>
        Google.Protobuf.ByteString.CopyFrom(Enumerable.Range(1, length).Select(i => (byte)i).ToArray());

    /// <summary>
    /// Sweeps every string the row carries rather than the ones the assertions above name, so a
    /// field added to the row and forgotten in the mapper is caught here — provided the export
    /// built above learns to carry a NUL into it.
    /// </summary>
    private static void AssertNothingUnstorable(object row)
    {
        foreach (var property in row.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.PropertyType == typeof(string) && property.GetValue(row) is string value)
            {
                Assert.DoesNotContain('\0', value);
            }
        }
    }
}
