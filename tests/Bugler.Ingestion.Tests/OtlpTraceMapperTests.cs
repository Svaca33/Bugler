using Bugler.Ingestion.OtlpMapping;
using Bugler.SharedKernel;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;

namespace Bugler.Ingestion.Tests;

public class OtlpTraceMapperTests
{
    private static readonly ServiceId Service = ServiceId.New();
    private static readonly DateTime Start = new(2026, 7, 27, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = Start.AddMilliseconds(250);

    private static ulong ToUnixNano(DateTime utc) => (ulong)(utc - DateTime.UnixEpoch).Ticks * 100;

    private static byte[] Bytes(int length) => Enumerable.Range(1, length).Select(i => (byte)i).ToArray();

    private static ExportTraceServiceRequest Request(params Span[] spans)
    {
        var request = new ExportTraceServiceRequest();
        var resourceSpans = new ResourceSpans
        {
            Resource = new Resource
            {
                Attributes =
                {
                    new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "checkout" } },
                },
            },
        };
        var scopeSpans = new ScopeSpans { Scope = new InstrumentationScope { Name = "Bugler.Tests" } };
        scopeSpans.Spans.AddRange(spans);
        resourceSpans.ScopeSpans.Add(scopeSpans);
        request.ResourceSpans.Add(resourceSpans);
        return request;
    }

    [Fact]
    public void Maps_span_with_status_events_and_links()
    {
        var span = new Span
        {
            TraceId = ByteString.CopyFrom(Bytes(16)),
            SpanId = ByteString.CopyFrom(Bytes(8)),
            ParentSpanId = ByteString.CopyFrom(Bytes(8)),
            Name = "GET /orders",
            Kind = Span.Types.SpanKind.Server,
            StartTimeUnixNano = ToUnixNano(Start),
            EndTimeUnixNano = ToUnixNano(End),
            Status = new Status { Code = Status.Types.StatusCode.Error, Message = "boom" },
            Attributes = { new KeyValue { Key = "http.status_code", Value = new AnyValue { IntValue = 500 } } },
            Events =
            {
                new Span.Types.Event
                {
                    Name = "exception",
                    TimeUnixNano = ToUnixNano(Start.AddMilliseconds(100)),
                    Attributes = { new KeyValue { Key = "exception.type", Value = new AnyValue { StringValue = "IOException" } } },
                },
            },
            Links =
            {
                new Span.Types.Link
                {
                    TraceId = ByteString.CopyFrom(Bytes(16)),
                    SpanId = ByteString.CopyFrom(Bytes(8)),
                },
            },
        };

        var (rows, dropped, _) = OtlpTraceMapper.Map(Request(span), Service);

        Assert.Equal(0, dropped);
        var row = Assert.Single(rows);
        Assert.Equal(Service.Value, row.ServiceId);
        Assert.Equal("0102030405060708090a0b0c0d0e0f10", row.TraceId);
        Assert.Equal("0102030405060708", row.SpanId);
        Assert.Equal("0102030405060708", row.ParentSpanId);
        Assert.Equal("GET /orders", row.Name);
        Assert.Equal((short)Span.Types.SpanKind.Server, row.Kind);
        Assert.Equal(Start, row.StartTime);
        Assert.Equal(End, row.EndTime);
        Assert.Equal((short)Status.Types.StatusCode.Error, row.StatusCode);
        Assert.Equal("boom", row.StatusMessage);
        Assert.Equal("Bugler.Tests", row.ScopeName);

        // The Declared Identity is kept as a plain resource attribute, never lifted into the row.
        Assert.Contains("\"service.name\":\"checkout\"", row.ResourceAttributes);
        Assert.Equal("""{"http.status_code":500}""", row.Attributes);
        Assert.Contains("\"name\":\"exception\"", row.Events);
        Assert.Contains("\"exception.type\":\"IOException\"", row.Events);
        Assert.Contains("\"traceId\":\"0102030405060708090a0b0c0d0e0f10\"", row.Links);
    }

    [Fact]
    public void Drops_spans_with_malformed_ids_and_counts_them()
    {
        var valid = new Span
        {
            TraceId = ByteString.CopyFrom(Bytes(16)),
            SpanId = ByteString.CopyFrom(Bytes(8)),
            Name = "ok",
            StartTimeUnixNano = ToUnixNano(Start),
            EndTimeUnixNano = ToUnixNano(End),
        };
        var malformed = new Span
        {
            TraceId = ByteString.CopyFrom(1, 2, 3),
            SpanId = ByteString.CopyFrom(Bytes(8)),
            Name = "broken",
        };

        var (rows, dropped, _) = OtlpTraceMapper.Map(Request(valid, malformed), Service);

        Assert.Equal(1, dropped);
        Assert.Equal("ok", Assert.Single(rows).Name);
    }

    [Fact]
    public void Missing_parent_and_empty_collections_map_to_null_and_empty_json()
    {
        var span = new Span
        {
            TraceId = ByteString.CopyFrom(Bytes(16)),
            SpanId = ByteString.CopyFrom(Bytes(8)),
            Name = "leaf",
            StartTimeUnixNano = ToUnixNano(Start),
            EndTimeUnixNano = ToUnixNano(End),
        };

        var (rows, _, _) = OtlpTraceMapper.Map(Request(span), Service);

        var row = Assert.Single(rows);
        Assert.Null(row.ParentSpanId);
        Assert.Equal((short)Status.Types.StatusCode.Unset, row.StatusCode);
        Assert.Equal("[]", row.Events);
        Assert.Equal("[]", row.Links);
        Assert.Equal("{}", row.Attributes);
    }
}
