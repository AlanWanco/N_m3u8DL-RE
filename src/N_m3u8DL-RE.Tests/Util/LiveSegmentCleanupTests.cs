using Shouldly;
using N_m3u8DL_RE.Util;

namespace N_m3u8DL_RE.Tests.Util;

public class LiveSegmentCleanupTests
{
    [Fact]
    public void DeletesMergedSegmentsAndKeepsInit()
    {
        var root = Directory.CreateTempSubdirectory("live-segment-cleanup-").FullName;
        try
        {
            var init = Path.Combine(root, "_init.mp4");
            var first = Path.Combine(root, "0.m4s");
            var second = Path.Combine(root, "1.m4s");
            File.WriteAllText(init, "init");
            File.WriteAllText(first, "a");
            File.WriteAllText(second, "b");

            LiveSegmentCleanup.DeleteMergedSegments([init, first, second]);

            File.Exists(init).ShouldBeTrue();
            File.Exists(first).ShouldBeFalse();
            File.Exists(second).ShouldBeFalse();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void MissingSegmentIsNotAnError()
    {
        var root = Directory.CreateTempSubdirectory("live-segment-missing-").FullName;
        try
        {
            var missing = Path.Combine(root, "gone.m4s");
            LiveSegmentCleanup.DeleteMergedSegments([missing]);
            File.Exists(missing).ShouldBeFalse();
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void DeleteFailureKeepsFileAndDoesNotThrow()
    {
        if (OperatingSystem.IsWindows())
            return; // Windows 上目录只读不影响删除权限判定
        var root = Directory.CreateTempSubdirectory("live-segment-readonly-").FullName;
        var file = Path.Combine(root, "locked.m4s");
        try
        {
            File.WriteAllText(file, "x");
            File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserExecute);

            var deleted = LiveSegmentCleanup.TryDelete(file);

            deleted.ShouldBeFalse();
            File.Exists(file).ShouldBeTrue();
        }
        finally
        {
            File.SetUnixFileMode(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Directory.Delete(root, true);
        }
    }
}
