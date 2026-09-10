using System.Text;
using FluentAssertions;
using PulseStack.Abstractions.Persistence.AIAssets.Documents;
using PulseStack.Abstractions.Persistence.AIAssets.Schema;
using PulseStack.Abstractions.Persistence.AIAssets.Serialization;
using Xunit;

namespace PulseStack.Tests.Persistence.AIAssets;

public sealed class AIAssetDocumentCodecBoundaryTests
{
    private readonly AIAssetDocumentCodec codec = new();

    [Fact]
    public void StringAndByteSerialization_ShouldProjectTheSameCanonicalArtifact()
    {
        var document = Tool();

        var bytes = codec.Serialize(document);
        var text = codec.SerializeToString(document);

        new UTF8Encoding(false, true).GetBytes(text).Should().Equal(bytes);
        bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }).Should().BeFalse();
    }

    [Fact]
    public void Serialize_ShouldReturnCallerOwnedIndependentArrays()
    {
        var first = codec.Serialize(Tool());
        var expected = first.ToArray();
        var second = codec.Serialize(Tool());

        first.Should().NotBeSameAs(second);
        first[0] ^= 0x01;

        second.Should().Equal(expected);
        codec.Serialize(Tool()).Should().Equal(expected);
    }

    [Fact]
    public async Task SerializeAsync_ShouldWriteAtCurrentCursorWithoutFlushOrDispose()
    {
        var canonical = codec.Serialize(Tool());
        var stream = new TrackingMemoryStream(Encoding.UTF8.GetBytes("prefix-tail"));
        stream.Position = 7;

        await codec.SerializeAsync(Tool(), stream);

        stream.FlushCount.Should().Be(0);
        stream.DisposeCount.Should().Be(0);
        stream.Position.Should().Be(7 + canonical.Length);
        stream.ToArray().AsSpan(0, 7).ToArray().Should().Equal(Encoding.UTF8.GetBytes("prefix-"));
        stream.ToArray().AsSpan(7, canonical.Length).ToArray().Should().Equal(canonical);
    }

    [Fact]
    public async Task DeserializeAsync_ShouldReadFromCurrentCursorThroughEofAndLeaveStreamOpen()
    {
        var canonical = codec.Serialize(Tool());
        var payload = Encoding.UTF8.GetBytes("skip!").Concat(new byte[] { 0xEF, 0xBB, 0xBF }).Concat(canonical).ToArray();
        var stream = new TrackingMemoryStream(payload) { Position = 5 };

        var document = await codec.DeserializeAsync(stream);

        document.Should().BeEquivalentTo(Tool());
        stream.Position.Should().Be(stream.Length);
        stream.DisposeCount.Should().Be(0);
    }

    [Fact]
    public async Task DeserializeAsync_ShouldRequireEofAfterSingleArtifact()
    {
        var canonical = codec.Serialize(Tool());
        var payload = canonical.Concat(Encoding.UTF8.GetBytes("{}")).ToArray();
        using var stream = new MemoryStream(payload);

        var action = async () => await codec.DeserializeAsync(stream);

        var failure = await action.Should().ThrowAsync<AIAssetDocumentCodecException>();
        failure.Which.FailureReason.Should().Be(AIAssetDocumentCodecFailureReason.InvalidJson);
    }

    [Fact]
    public async Task StreamIoFailures_ShouldPropagateUnwrappedAndStreamsRemainCallerOwned()
    {
        var writeFailure = new IOException("write failure");
        var output = new ThrowingWriteStream(writeFailure);
        var write = async () => await codec.SerializeAsync(Tool(), output);
        (await write.Should().ThrowAsync<IOException>()).Which.Should().BeSameAs(writeFailure);
        output.DisposeCount.Should().Be(0);

        var readFailure = new IOException("read failure");
        var input = new ThrowingReadStream(readFailure);
        var read = async () => await codec.DeserializeAsync(input);
        (await read.Should().ThrowAsync<IOException>()).Which.Should().BeSameAs(readFailure);
        input.DisposeCount.Should().Be(0);
    }

    [Fact]
    public async Task SerializeAsync_FailureMayLeavePartialPrefixAndMustNotFlushOrDispose()
    {
        var canonical = codec.Serialize(Tool());
        var stream = new PartialThenThrowWriteStream(9);

        var action = async () => await codec.SerializeAsync(Tool(), stream);

        await action.Should().ThrowAsync<IOException>();
        stream.Written.Should().Equal(canonical.AsSpan(0, 9).ToArray());
        stream.FlushCount.Should().Be(0);
        stream.DisposeCount.Should().Be(0);
    }

    [Fact]
    public async Task CallerCancellation_ShouldPropagateWithoutCodecWrapping()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var write = async () => await codec.SerializeAsync(Tool(), new CancellationAwareWriteStream(), cancellation.Token);
        var writeFailure = await write.Should().ThrowAsync<OperationCanceledException>();
        writeFailure.Which.Should().NotBeOfType<AIAssetDocumentCodecException>();

        var read = async () => await codec.DeserializeAsync(new CancellationAwareReadStream(), cancellation.Token);
        var readFailure = await read.Should().ThrowAsync<OperationCanceledException>();
        readFailure.Which.Should().NotBeOfType<AIAssetDocumentCodecException>();
    }

    private static ToolAssetDocument Tool() => new(
        AIAssetSchemaVersion.V1,
        new AIAssetIdentityDocument { Id = "tool", Urn = "urn:tool", Version = "1" },
        new AIAssetMetadataDocument("Tool"),
        AIAssetLifecycleDocument.Published);

    private sealed class TrackingMemoryStream : MemoryStream
    {
        public TrackingMemoryStream(byte[] bytes) : base() => Write(bytes, 0, bytes.Length);
        public int FlushCount { get; private set; }
        public int DisposeCount { get; private set; }
        public override void Flush() { FlushCount++; base.Flush(); }
        public override Task FlushAsync(CancellationToken cancellationToken) { FlushCount++; return base.FlushAsync(cancellationToken); }
        protected override void Dispose(bool disposing) { DisposeCount++; base.Dispose(disposing); }
        public override ValueTask DisposeAsync() { DisposeCount++; return base.DisposeAsync(); }
    }

    private sealed class ThrowingWriteStream(IOException failure) : Stream
    {
        public int DisposeCount { get; private set; }
        public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw failure;
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromException(failure);
        protected override void Dispose(bool disposing) => DisposeCount++;
    }

    private sealed class ThrowingReadStream(IOException failure) : Stream
    {
        public int DisposeCount { get; private set; }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw failure;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromException<int>(failure);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing) => DisposeCount++;
    }

    private sealed class PartialThenThrowWriteStream(int prefixLength) : Stream
    {
        private readonly MemoryStream written = new();
        public byte[] Written => written.ToArray();
        public int FlushCount { get; private set; }
        public int DisposeCount { get; private set; }
        public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
        public override long Length => written.Length; public override long Position { get => written.Position; set => throw new NotSupportedException(); }
        public override void Flush() => FlushCount++;
        public override Task FlushAsync(CancellationToken cancellationToken) { FlushCount++; return Task.CompletedTask; }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var count = Math.Min(prefixLength, buffer.Length);
            written.Write(buffer.Span[..count]);
            return ValueTask.FromException(new IOException("partial write"));
        }
        protected override void Dispose(bool disposing) => DisposeCount++;
    }

    private sealed class CancellationAwareWriteStream : Stream
    {
        public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException(); public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromCanceled(cancellationToken);
    }

    private sealed class CancellationAwareReadStream : Stream
    {
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException(); public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => ValueTask.FromCanceled<int>(cancellationToken);
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
