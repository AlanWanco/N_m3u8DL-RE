using Shouldly;
using N_m3u8DL_RE.Common.Entity;
using N_m3u8DL_RE.Entity;
using N_m3u8DL_RE.Util;

namespace N_m3u8DL_RE.Tests.Util;

public class LiveTrackMergeTests
{
    [Fact]
    public async Task ConcatenatesInitThenSegmentsInNaturalOrder()
    {
        var root = Directory.CreateTempSubdirectory("live-track-merge-").FullName;
        try
        {
            var source = Path.Combine(root, "0____");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "_init.mp4"), "init");
            File.WriteAllText(Path.Combine(source, "0000000002_1.m4s"), "b");
            File.WriteAllText(Path.Combine(source, "0000000001_0.m4s"), "a");
            File.WriteAllText(Path.Combine(source, "0000000010_9.m4s"), "c");
            var target = Path.Combine(root, "out.mp4");

            var merged = await LiveTrackMerge.ConcatAsync(source, target, CancellationToken.None);

            merged.ShouldBeTrue();
            File.ReadAllText(target).ShouldBe("initabc");
            File.Exists(target + ".part").ShouldBeFalse();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task PrefersDecryptedSegmentsAndInit()
    {
        var root = Directory.CreateTempSubdirectory("live-track-decrypt-").FullName;
        try
        {
            var source = Path.Combine(root, "0____");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "_init.mp4"), "raw-init");
            File.WriteAllText(Path.Combine(source, "_init_dec.mp4"), "dec-init");
            File.WriteAllText(Path.Combine(source, "1_0.m4s"), "raw");
            File.WriteAllText(Path.Combine(source, "1_0_dec.m4s"), "dec");
            var target = Path.Combine(root, "out.mp4");

            (await LiveTrackMerge.ConcatAsync(source, target, CancellationToken.None)).ShouldBeTrue();

            File.ReadAllText(target).ShouldBe("dec-initdec");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task MissingInitStillConcatenatesSegments()
    {
        var root = Directory.CreateTempSubdirectory("live-track-ts-").FullName;
        try
        {
            var source = Path.Combine(root, "0____");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "1.ts"), "x");
            File.WriteAllText(Path.Combine(source, "2.ts"), "y");
            var target = Path.Combine(root, "out.ts");

            (await LiveTrackMerge.ConcatAsync(source, target, CancellationToken.None)).ShouldBeTrue();

            File.ReadAllText(target).ShouldBe("xy");
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task MultipleInitSectionsAreRejected()
    {
        var root = Directory.CreateTempSubdirectory("live-track-periods-").FullName;
        try
        {
            var source = Path.Combine(root, "0____");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "_init.mp4"), "init-0");
            File.WriteAllText(Path.Combine(source, "_init_1.mp4"), "init-1");
            File.WriteAllText(Path.Combine(source, "1_0.m4s"), "a");
            var target = Path.Combine(root, "out.mp4");

            var error = await Should.ThrowAsync<InvalidOperationException>(
                () => LiveTrackMerge.ConcatAsync(source, target, CancellationToken.None));

            error.Message.ShouldContain(source);
            File.Exists(target).ShouldBeFalse();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task EmptyDirectoryReportsNothingToMerge()
    {
        var root = Directory.CreateTempSubdirectory("live-track-empty-").FullName;
        try
        {
            var source = Path.Combine(root, "0____");
            Directory.CreateDirectory(source);
            File.WriteAllText(Path.Combine(source, "_init.mp4"), "init");
            var target = Path.Combine(root, "out.mp4");

            (await LiveTrackMerge.ConcatAsync(source, target, CancellationToken.None)).ShouldBeFalse();

            LiveTrackMerge.HasSegments(source).ShouldBeFalse();
            File.Exists(target).ShouldBeFalse();
        }
        finally { Directory.Delete(root, true); }
    }

    [Theory]
    [InlineData("m4s", null, ".mp4")]
    [InlineData("mp4", null, ".mp4")]
    [InlineData("ts", null, ".ts")]
    [InlineData("m4s", N_m3u8DL_RE.Common.Enum.MediaType.AUDIO, ".m4a")]
    [InlineData("mp4", N_m3u8DL_RE.Common.Enum.MediaType.AUDIO, ".m4a")]
    [InlineData("vtt", N_m3u8DL_RE.Common.Enum.MediaType.SUBTITLES, ".vtt")]
    public void OutputExtensionFollowsTrackKind(string extension, N_m3u8DL_RE.Common.Enum.MediaType? mediaType, string expected)
    {
        var spec = new StreamSpec { Extension = extension, MediaType = mediaType };
        LiveTrackMerge.OutputExtension(spec).ShouldBe(expected);
    }
}
