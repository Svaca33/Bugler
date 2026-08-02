using Bugler.Ingestion.OtlpMapping;
using Bugler.SharedKernel;
using Google.Protobuf;
using OpenTelemetry.Proto.Collector.Logs.V1;
using OpenTelemetry.Proto.Collector.Trace.V1;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;
using OpenTelemetry.Proto.Resource.V1;
using OpenTelemetry.Proto.Trace.V1;

namespace Bugler.Ingestion.Tests;

/// <summary>
/// What the mappers hand the Release recorder: one sighting per Resource block, dated by the
/// oldest Signal under it (ADR 0016).
/// </summary>
public class OtlpMapperVersionTests
{
    private static readonly ServiceId Service = ServiceId.New();
    private static readonly DateTime Noon = new(2026, 8, 2, 12, 0, 0, DateTimeKind.Utc);

    private static ulong ToUnixNano(DateTime utc) => (ulong)(utc - DateTime.UnixEpoch).Ticks * 100;

    private static Resource Declaring(string? version)
    {
        var resource = new Resource
        {
            Attributes =
            {
                new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "checkout" } },
            },
        };

        if (version is not null)
        {
            resource.Attributes.Add(new KeyValue
            {
                Key = "service.version",
                Value = new AnyValue { StringValue = version },
            });
        }

        return resource;
    }

    private static ResourceLogs LogBlock(string? version, params DateTime[] stamps)
    {
        var scope = new ScopeLogs();
        foreach (var stamp in stamps)
        {
            scope.LogRecords.Add(new LogRecord
            {
                TimeUnixNano = ToUnixNano(stamp),
                SeverityNumber = SeverityNumber.Info,
                Body = new AnyValue { StringValue = "ok" },
            });
        }

        return new ResourceLogs { Resource = Declaring(version), ScopeLogs = { scope } };
    }

    private static ResourceSpans SpanBlock(string? version, params DateTime[] stamps)
    {
        var scope = new ScopeSpans();
        foreach (var stamp in stamps)
        {
            scope.Spans.Add(new Span
            {
                TraceId = ByteString.CopyFrom(new byte[16]),
                SpanId = ByteString.CopyFrom(new byte[8]),
                Name = "checkout",
                StartTimeUnixNano = ToUnixNano(stamp),
                EndTimeUnixNano = ToUnixNano(stamp.AddSeconds(1)),
            });
        }

        return new ResourceSpans { Resource = Declaring(version), ScopeSpans = { scope } };
    }

    [Fact]
    public void A_log_block_is_dated_by_its_oldest_record_whatever_order_they_arrive_in()
    {
        var request = new ExportLogsServiceRequest
        {
            ResourceLogs = { LogBlock("1.2.3", Noon.AddMinutes(5), Noon, Noon.AddMinutes(2)) },
        };

        var sighting = Assert.Single(OtlpLogMapper.Map(request, Service).Versions);

        Assert.Equal("1.2.3", sighting.Version);
        Assert.Equal(Noon, sighting.EarliestSignal);
    }

    [Fact]
    public void A_span_block_is_dated_by_its_earliest_start()
    {
        var request = new ExportTraceServiceRequest
        {
            ResourceSpans = { SpanBlock("1.2.3", Noon.AddMinutes(9), Noon.AddMinutes(1)) },
        };

        var sighting = Assert.Single(OtlpTraceMapper.Map(request, Service).Versions);

        Assert.Equal("1.2.3", sighting.Version);
        Assert.Equal(Noon.AddMinutes(1), sighting.EarliestSignal);
    }

    [Fact]
    public void A_block_declaring_no_version_yields_no_sighting()
    {
        var request = new ExportLogsServiceRequest { ResourceLogs = { LogBlock(null, Noon) } };

        Assert.Empty(OtlpLogMapper.Map(request, Service).Versions);
    }

    [Fact]
    public void A_block_carrying_no_signal_has_nothing_to_be_dated_by()
    {
        var request = new ExportLogsServiceRequest { ResourceLogs = { LogBlock("1.2.3") } };

        Assert.Empty(OtlpLogMapper.Map(request, Service).Versions);
    }

    [Fact]
    public void Each_resource_block_of_one_export_is_seen_on_its_own()
    {
        var request = new ExportLogsServiceRequest
        {
            ResourceLogs =
            {
                LogBlock("1.2.3", Noon),
                LogBlock("1.2.4", Noon.AddMinutes(1)),
            },
        };

        var sightings = OtlpLogMapper.Map(request, Service).Versions;

        Assert.Equal(["1.2.3", "1.2.4"], sightings.Select(s => s.Version));
    }
}
