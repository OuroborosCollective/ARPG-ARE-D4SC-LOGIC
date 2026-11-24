using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using NUnit.Framework;

namespace AshenCrypt.ArtForge.Tests
{
    public class ArtForgeReaderBudgetTests
    {
        string root;

        [SetUp] public void SetUp()
        {
            root = Path.Combine(Path.GetTempPath(), "ashen-art-forge-budget-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
        }

        [TearDown] public void TearDown()
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }

        string FileWithRealBytes(int size = 64)
        {
            string path = Path.Combine(root, "real-file.bin");
            File.WriteAllBytes(path, Enumerable.Range(0, size).Select(i => (byte)(i + 32)).ToArray());
            return path;
        }

        [Test] public void ReceiptGrowthCannotIncreaseTheReservedAllocationOrReadBudget()
        {
            string path = FileWithRealBytes();
            long reserved = new FileInfo(path).Length;
            using (var input = new FileLengthRaceHarness(path, reserved * 2))
            {
                Exception rejection = InvokeReceipt(input, reserved, out _);
                Assert.IsTrue(input.LengthChanged, "The controlled file growth must actually occur.");
                Assert.AreEqual(reserved * 2, new FileInfo(path).Length);
                Assert.LessOrEqual(input.LargestReadRequest, reserved,
                    "Receipt allocation/read must use the checked length, not a later growing Length value.");
                Assert.LessOrEqual(input.BytesRead, reserved, "Receipt reads may not exceed the reserved bytes.");
                AssertHashDrift(rejection);
            }
        }

        [Test] public void ReceiptShrinkAfterLengthObservationIsRejected()
        {
            string path = FileWithRealBytes();
            long reserved = new FileInfo(path).Length;
            using (var input = new FileLengthRaceHarness(path, reserved / 2))
            {
                Exception rejection = InvokeReceipt(input, reserved, out _);
                Assert.IsTrue(input.LengthChanged, "The controlled file truncation must actually occur.");
                Assert.AreEqual(reserved / 2, new FileInfo(path).Length);
                AssertHashDrift(rejection);
            }
        }

        [Test] public void GrowingArtifactCannotConsumeBytesOutsideItsReservedBudget()
        {
            string path = FileWithRealBytes();
            byte[] original = File.ReadAllBytes(path);
            var node = new ArtGraphNode { artifactSha256 = ArtGraphContract.Fingerprint(original) };
            using (var input = new FileLengthRaceHarness(path, original.Length * 2))
            {
                Exception rejection = InvokeArtifact(node, input, original.Length);
                Assert.IsTrue(input.LengthChanged, "The real file must grow after the length observation.");
                Assert.AreEqual(original.Length * 2, new FileInfo(path).Length);
                AssertHashDrift(rejection);
                Assert.LessOrEqual(input.BytesRead, original.Length,
                    "Rejecting a hash after consuming extra bytes does not enforce the Reader's reserved budget.");
            }
        }

        [Test] public void StableRealFilePassesBothReaderPathsAndRemainsCallerOwned()
        {
            string path = FileWithRealBytes();
            byte[] original = File.ReadAllBytes(path);
            using (var input = File.OpenRead(path))
            {
                byte[] actual;
                Assert.IsNull(InvokeReceipt(input, original.Length, out actual));
                CollectionAssert.AreEqual(original, actual);
                Assert.IsTrue(input.CanRead);
            }
            var node = new ArtGraphNode { artifactSha256 = ArtGraphContract.Fingerprint(original) };
            using (var input = File.OpenRead(path))
            {
                Assert.IsNull(InvokeArtifact(node, input, original.Length));
                Assert.AreEqual(original.Length, input.Position);
                Assert.IsTrue(input.CanRead);
            }
        }

        [Test] public void CancelledReaderThrowsInsteadOfReturningADiagnosticSnapshot()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.Throws<OperationCanceledException>(() => ArtForgeWorldForgeReader.Read(root,
                    new string('a', 40), cancellation.Token));
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CancellationAfterOneRealChunkStopsFurtherReceiptOrArtifactReads(bool artifact)
        {
            string path = FileWithRealBytes(131072);
            byte[] original = File.ReadAllBytes(path);
            using (var cancellation = new CancellationTokenSource())
            using (var input = new FileLengthRaceHarness(path, original.Length, cancellation))
            {
                var node = new ArtGraphNode { artifactSha256 = ArtGraphContract.Fingerprint(original) };
                Exception rejection = artifact
                    ? InvokeArtifact(node, input, original.Length, cancellation.Token)
                    : InvokeReceipt(input, original.Length, out _, cancellation.Token);
                Assert.IsInstanceOf<OperationCanceledException>(rejection);
                Assert.AreEqual(65536, input.BytesRead,
                    "Cancellation after the first real chunk must prevent another underlying file read.");
                Assert.IsTrue(input.CanRead, "The Reader does not own the caller's stream.");
            }
            CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
        }

        static Exception InvokeReceipt(Stream input, long length, out byte[] bytes, CancellationToken? cancellation = null)
        {
            bytes = null;
            try
            {
                var method = cancellation.HasValue
                    ? Method("ReadReceiptBytes", typeof(Stream), typeof(long), typeof(CancellationToken))
                    : Method("ReadReceiptBytes", typeof(Stream), typeof(long));
                object[] arguments = cancellation.HasValue
                    ? new object[] { input, length, cancellation.Value } : new object[] { input, length };
                bytes = (byte[])method.Invoke(null, arguments);
                return null;
            }
            catch (TargetInvocationException error) { return error.InnerException; }
        }

        static Exception InvokeArtifact(ArtGraphNode node, Stream input, long length, CancellationToken? cancellation = null)
        {
            try
            {
                var method = cancellation.HasValue
                    ? Method("VerifyReservedArtifact", typeof(ArtGraphNode), typeof(Stream), typeof(long), typeof(CancellationToken))
                    : Method("VerifyReservedArtifact", typeof(ArtGraphNode), typeof(Stream), typeof(long));
                object[] arguments = cancellation.HasValue
                    ? new object[] { node, input, length, cancellation.Value } : new object[] { node, input, length };
                method.Invoke(null, arguments);
                return null;
            }
            catch (TargetInvocationException error) { return error.InnerException; }
        }

        static MethodInfo Method(string name, params Type[] parameters)
        {
            var method = typeof(ArtForgeWorldForgeReader).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic,
                null, parameters, null);
            Assert.IsNotNull(method, "The test must execute the Reader's actual stream path: " + name);
            return method;
        }

        static void AssertHashDrift(Exception rejection)
        {
            Assert.IsNotNull(rejection, "Changed file length must be rejected, not accepted as a different receipt.");
            var code = rejection.GetType().GetField("Code", BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(code, "Expected the Reader's explicit refusal, not an unrelated IO/test exception.");
            Assert.AreEqual("HASH_DRIFT", code.GetValue(rejection));
        }

        // Deterministic race-test harness, never a production or game-runtime source.
        // Length returns a value genuinely observed from a FileStream, then SetLength changes
        // that same real file before the caller continues. All reads and hashes use its bytes;
        // no length, byte content, hash or successful Reader response is fabricated.
        // Cancellation cases keep its actual length and cancel only after a real chunk read.
        sealed class FileLengthRaceHarness : Stream
        {
            readonly FileStream file;
            readonly long changedLength;
            readonly CancellationTokenSource cancelAfterRead;
            public bool LengthChanged { get; private set; }
            public long BytesRead { get; private set; }
            public int LargestReadRequest { get; private set; }

            public FileLengthRaceHarness(string path, long changedLength, CancellationTokenSource cancelAfterRead = null)
            {
                file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                this.changedLength = changedLength;
                this.cancelAfterRead = cancelAfterRead;
            }

            public override long Length
            {
                get
                {
                    long observed = file.Length;
                    if (!LengthChanged)
                    {
                        long position = file.Position;
                        file.SetLength(changedLength);
                        file.Flush();
                        file.Position = position;
                        LengthChanged = true;
                    }
                    return observed;
                }
            }
            public override bool CanRead { get { return file.CanRead; } }
            public override bool CanSeek { get { return file.CanSeek; } }
            public override bool CanWrite { get { return false; } }
            public override long Position { get { return file.Position; } set { file.Position = value; } }
            public override int Read(byte[] buffer, int offset, int count)
            {
                LargestReadRequest = Math.Max(LargestReadRequest, count);
                int read = file.Read(buffer, offset, count);
                BytesRead += read;
                if (read > 0 && cancelAfterRead != null) cancelAfterRead.Cancel();
                return read;
            }
            public override int ReadByte()
            {
                int value = file.ReadByte();
                if (value >= 0) BytesRead++;
                return value;
            }
            public override long Seek(long offset, SeekOrigin origin) { return file.Seek(offset, origin); }
            public override void Flush() { file.Flush(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }
            public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
            protected override void Dispose(bool disposing)
            {
                if (disposing) file.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}
