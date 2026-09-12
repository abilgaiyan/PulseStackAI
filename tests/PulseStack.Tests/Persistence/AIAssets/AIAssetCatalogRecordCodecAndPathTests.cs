using System.Text;
using FluentAssertions;
using PulseStack.Abstractions.Assets;
using PulseStack.Abstractions.Persistence.AIAssets.Catalog;
using PulseStack.Core.Persistence.AIAssets.Catalog;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetCatalogRecordCodecAndPathTests
{
    private static readonly AssetId FixedId = new(Guid.Parse("00112233-4455-6677-8899-aabbccddeeff"));

    [Fact]
    public void Serialize_ShouldEmitExactCanonicalUtf8Bytes()
    {
        var record = CreateRecord("1.0", "urn:pulsestack:prompt:alpha");

        var bytes = AIAssetCatalogRecordCodec.Serialize(record);

        Encoding.UTF8.GetString(bytes).Should().Be(
            "{\"formatVersion\":\"1.0\",\"assetType\":\"prompt\",\"assetId\":\"00112233445566778899aabbccddeeff\",\"version\":\"1.0\",\"urn\":\"urn:pulsestack:prompt:alpha\"}");
        bytes.Should().NotStartWith(new byte[] { 0xEF, 0xBB, 0xBF });
        bytes[^1].Should().NotBe((byte)'\n');
    }

    [Fact]
    public void Serialize_ShouldPreserveNonAsciiAsUtf8InsteadOfUnicodeEscapes()
    {
        var record = CreateRecord("1.Ω", "urn:pulsestack:prompt:Δ");

        var json = Encoding.UTF8.GetString(AIAssetCatalogRecordCodec.Serialize(record));

        json.Should().Contain("1.Ω");
        json.Should().Contain("prompt:Δ");
        json.Should().NotContain("\\u03");
    }

    [Fact]
    public void SerializeAndDeserialize_ShouldRoundTripCanonicalRecord()
    {
        var expected = CreateRecord("2.5", "urn:pulsestack:prompt:roundtrip");
        var bytes = AIAssetCatalogRecordCodec.Serialize(expected);

        var actual = AIAssetCatalogRecordCodec.Deserialize(bytes);

        actual.DefinitionKey.Should().Be(expected.DefinitionKey);
        actual.Urn.Should().Be(expected.Urn);
        AIAssetCatalogRecordCodec.Serialize(actual).Should().Equal(bytes);
    }

    [Theory]
    [InlineData("{\"formatVersion\":\"1.0\",\"assetType\":\"prompt\",\"assetId\":\"00112233445566778899aabbccddeeff\",\"version\":\"1.0\",\"urn\":\"urn:x\",\"extra\":\"x\"}")]
    [InlineData("{\"formatVersion\":\"1.0\",\"formatVersion\":\"1.0\",\"assetType\":\"prompt\",\"assetId\":\"00112233445566778899aabbccddeeff\",\"version\":\"1.0\",\"urn\":\"urn:x\"}")]
    [InlineData("{\"assetType\":\"prompt\",\"assetId\":\"00112233445566778899aabbccddeeff\",\"version\":\"1.0\",\"urn\":\"urn:x\"}")]
    [InlineData("{\"formatVersion\":\"2.0\",\"assetType\":\"prompt\",\"assetId\":\"00112233445566778899aabbccddeeff\",\"version\":\"1.0\",\"urn\":\"urn:x\"}")]
    [InlineData("{\"formatVersion\":\"1.0\",\"assetType\":\"provider\",\"assetId\":\"00112233445566778899aabbccddeeff\",\"version\":\"1.0\",\"urn\":\"urn:x\"}")]
    [InlineData("{\"formatVersion\":\"1.0\",\"assetType\":\"future\",\"assetId\":\"00112233445566778899aabbccddeeff\",\"version\":\"1.0\",\"urn\":\"urn:x\"}")]
    public void Deserialize_ShouldRejectInvalidRecordShapeOrVocabulary(string json)
    {
        var act = () => AIAssetCatalogRecordCodec.Deserialize(Encoding.UTF8.GetBytes(json));

        act.Should().Throw<InvalidDataException>();
    }

    [Theory]
    [InlineData(" {\"formatVersion\":\"1.0\",\"assetType\":\"prompt\",\"assetId\":\"00112233445566778899aabbccddeeff\",\"version\":\"1.0\",\"urn\":\"urn:x\"}")]
    [InlineData("{\"assetType\":\"prompt\",\"formatVersion\":\"1.0\",\"assetId\":\"00112233445566778899aabbccddeeff\",\"version\":\"1.0\",\"urn\":\"urn:x\"}")]
    [InlineData("{\"formatVersion\": \"1.0\",\"assetType\":\"prompt\",\"assetId\":\"00112233445566778899aabbccddeeff\",\"version\":\"1.0\",\"urn\":\"urn:x\"}")]
    [InlineData("{\"formatVersion\":\"1.0\",\"assetType\":\"prompt\",\"assetId\":\"00112233445566778899aabbccddeeff\",\"version\":\"1.0\",\"urn\":\"urn:x\"}\n")]
    public void Deserialize_ShouldRejectValidButNonCanonicalJson(string json)
    {
        var act = () => AIAssetCatalogRecordCodec.Deserialize(Encoding.UTF8.GetBytes(json));

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Deserialize_ShouldRejectUtf8Bom()
    {
        var canonical = AIAssetCatalogRecordCodec.Serialize(CreateRecord());
        var bytes = new byte[canonical.Length + 3];
        bytes[0] = 0xEF;
        bytes[1] = 0xBB;
        bytes[2] = 0xBF;
        canonical.CopyTo(bytes, 3);

        var act = () => AIAssetCatalogRecordCodec.Deserialize(bytes);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Deserialize_ShouldRejectInvalidUtf8()
    {
        var bytes = new byte[] { (byte)'{', (byte)'"', 0xC3, 0x28, (byte)'"', (byte)'}' };

        var act = () => AIAssetCatalogRecordCodec.Deserialize(bytes);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Deserialize_ShouldRejectRecordAboveOneMiB()
    {
        var bytes = new byte[AIAssetCatalogRecordCodec.MaximumRecordBytes + 1];

        var act = () => AIAssetCatalogRecordCodec.Deserialize(bytes);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Serialize_ShouldRejectRecordAboveOneMiB()
    {
        var record = CreateRecord(urn: $"urn:pulsestack:prompt:{new string('x', AIAssetCatalogRecordCodec.MaximumRecordBytes)}");

        var act = () => AIAssetCatalogRecordCodec.Serialize(record);

        act.Should().Throw<InvalidDataException>();
    }

    [Fact]
    public void Serialize_ShouldRejectIllFormedUnicode()
    {
        var record = CreateRecord(urn: "urn:pulsestack:prompt:\uD800");

        var act = () => AIAssetCatalogRecordCodec.Serialize(record);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void GetRecordPath_ShouldUseFrozenDeterministicTopology()
    {
        var key = new AssetDefinitionKey(AssetType.Prompt, FixedId, new AssetVersion("1.0-beta"));

        var path = AIAssetCatalogPathModel.GetRecordPath("catalog-root", key);

        path.Should().Be(Path.Combine(
            "catalog-root",
            "records",
            "prompt",
            "00112233445566778899aabbccddeeff",
            "0031002E0030002D0062006500740061.catalog"));
    }

    [Fact]
    public void EncodeVersion_ShouldEncodeEveryUtf16CodeUnitAsFourUppercaseHexDigits()
    {
        var encoded = AIAssetCatalogPathModel.EncodeVersion(new AssetVersion("A😀"));

        encoded.Should().Be("0041D83DDE00");
    }

    [Fact]
    public void GetRecordPath_ShouldRejectInvalidRootAndProviderKey()
    {
        var promptKey = new AssetDefinitionKey(AssetType.Prompt, FixedId, AssetVersion.Initial);
        var providerKey = new AssetDefinitionKey(AssetType.Provider, FixedId, AssetVersion.Initial);

        var invalidRoot = () => AIAssetCatalogPathModel.GetRecordPath(" ", promptKey);
        var invalidProvider = () => AIAssetCatalogPathModel.GetRecordPath("catalog-root", providerKey);

        invalidRoot.Should().Throw<ArgumentException>();
        invalidProvider.Should().Throw<ArgumentException>();
    }

    private static CatalogRecord CreateRecord(string version = "1.0", string urn = "urn:pulsestack:prompt:test") =>
        new(
            new AssetDefinitionKey(AssetType.Prompt, FixedId, new AssetVersion(version)),
            new AssetUrn(urn));
}
