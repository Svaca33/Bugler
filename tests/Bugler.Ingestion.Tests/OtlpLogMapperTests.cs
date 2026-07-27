using Bugler.Ingestion.OtlpMapping;
using Bugler.SharedKernel;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;

namespace Bugler.Ingestion.Tests;

public class OtlpLogMapperTests
{
    private static readonly InstanceId Instance = InstanceId.New();
    private static readonly DateTime KnownTime = new(2026, 7, 27, 10, 0, 0, DateTimeKind.Utc);

    private static ulong ToUnixNano(DateTime utc) => (ulong)(utc - DateTime.UnixEpoch).Ticks * 100;

    private static ExportLogsServiceRequest Request(params LogRecord[] records)
    {
        var request = new ExportLogsServiceRequest();
        var resourceLogs = new ResourceLogs
        {
            Resource = new Resource
            {
                Attributes =
                {
                    new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "checkout" } },
                    new KeyValue { Key = "tenant.id", Value = new AnyValue { StringValue = "acme" } },
                },
            },
        };
        var scopeLogs = new ScopeLogs { Scope = new InstrumentationScope { Name = "Bugler.Tests" } };
        scopeLogs.LogRecords.AddRange(records);
        resourceLogs.ScopeLogs.Add(scopeLogs);
        request.ResourceLogs.Add(resourceLogs);
        return request;
    }

    [Fact]
    public void Flattens_resource_scope_and_record_into_one_row()
    {
        var record = new LogRecord
        {
            TimeUnixNano = ToUnixNano(KnownTime),
            SeverityNumber = SeverityNumber.Warn,
            SeverityText = "Warning",
            Body = new AnyValue { StringValue = "Order failed" },
            TraceId = ByteString.CopyFrom(Enumerable.Range(1, 16).Select(i => (byte)i).ToArray()),
            SpanId = ByteString.CopyFrom(Enumerable.Range(1, 8).Select(i => (byte)i).ToArray()),
            Attributes = { new KeyValue { Key = "order.id", Value = new AnyValue { IntValue = 42 } } },
        };

        var row = Assert.Single(OtlpLogMapper.Map(Request(record), Instance));

        Assert.Equal(Instance.Value, row.InstanceId);
        Assert.Equal(KnownTime, row.Timestamp);
        Assert.Equal((short)SeverityNumber.Warn, row.SeverityNumber);
        Assert.Equal("Warning", row.SeverityText);
        Assert.Equal("Order failed", row.Body);
        Assert.Equal("0102030405060708090a0b0c0d0e0f10", row.TraceId);
        Assert.Equal("0102030405060708", row.SpanId);
        Assert.Equal("checkout", row.ServiceName);
        Assert.Equal("Bugler.Tests", row.ScopeName);
        Assert.Contains("\"tenant.id\":\"acme\"", row.ResourceAttributes);
        Assert.Equal("""{"order.id":42}""", row.Attributes);
    }

    [Fact]
    public void Falls_back_to_observed_time_when_time_is_missing()
    {
        var record = new LogRecord { ObservedTimeUnixNano = ToUnixNano(KnownTime) };

        var row = Assert.Single(OtlpLogMapper.Map(Request(record), Instance));

        Assert.Equal(KnownTime, row.Timestamp);
        Assert.Equal(KnownTime, row.ObservedTimestamp);
    }

    [Fact]
    public void Serializes_structured_body_as_json()
    {
        var record = new LogRecord
        {
            Body = new AnyValue
            {
                KvlistValue = new KeyValueList
                {
                    Values =
                    {
                        new KeyValue { Key = "code", Value = new AnyValue { IntValue = 7 } },
                        new KeyValue { Key = "fatal", Value = new AnyValue { BoolValue = true } },
                    },
                },
            },
        };

        var row = Assert.Single(OtlpLogMapper.Map(Request(record), Instance));

        Assert.Equal("""{"code":7,"fatal":true}""", row.Body);
    }

    [Fact]
    public void Drops_malformed_trace_and_span_ids_to_null()
    {
        var record = new LogRecord
        {
            TraceId = ByteString.CopyFrom(1, 2, 3),
            SpanId = ByteString.Empty,
        };

        var row = Assert.Single(OtlpLogMapper.Map(Request(record), Instance));

        Assert.Null(row.TraceId);
        Assert.Null(row.SpanId);
    }
}
