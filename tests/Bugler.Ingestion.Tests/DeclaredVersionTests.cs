using Bugler.Ingestion.ObserveReleases;
using Google.Protobuf.Collections;
using OpenTelemetry.Proto.Common.V1;

namespace Bugler.Ingestion.Tests;

public class DeclaredVersionTests
{
    private static RepeatedField<KeyValue> Attributes(params KeyValue[] attributes)
    {
        var field = new RepeatedField<KeyValue>();
        field.AddRange(attributes);
        return field;
    }

    private static KeyValue Version(AnyValue value) => new() { Key = "service.version", Value = value };

    [Fact]
    public void Reads_the_declared_version()
    {
        var attributes = Attributes(
            new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "checkout" } },
            Version(new AnyValue { StringValue = "1.2.3" }));

        Assert.Equal("1.2.3", DeclaredVersion.Read(attributes));
    }

    [Fact]
    public void A_git_hash_is_as_good_a_version_as_a_semver()
    {
        Assert.Equal("a01dbef8a", DeclaredVersion.Read(Attributes(
            Version(new AnyValue { StringValue = "a01dbef8a" }))));
    }

    [Fact]
    public void Absent_is_no_version()
    {
        Assert.Null(DeclaredVersion.Read(Attributes(
            new KeyValue { Key = "service.name", Value = new AnyValue { StringValue = "checkout" } })));
        Assert.Null(DeclaredVersion.Read(null));
    }

    [Fact]
    public void Blank_is_no_version()
    {
        Assert.Null(DeclaredVersion.Read(Attributes(Version(new AnyValue { StringValue = "   " }))));
    }

    [Fact]
    public void Surrounding_space_is_not_part_of_a_version()
    {
        Assert.Equal("1.2.3", DeclaredVersion.Read(Attributes(
            Version(new AnyValue { StringValue = " 1.2.3\n" }))));
    }

    [Fact]
    public void A_non_string_is_a_sender_getting_it_wrong_not_a_version()
    {
        Assert.Null(DeclaredVersion.Read(Attributes(Version(new AnyValue { IntValue = 3 }))));
    }

    [Fact]
    public void An_over_long_version_is_read_as_none_at_all()
    {
        Assert.Null(DeclaredVersion.Read(Attributes(
            Version(new AnyValue { StringValue = new string('v', 129) }))));
    }
}
