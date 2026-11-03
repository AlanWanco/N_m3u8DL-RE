using N_m3u8DL_RE.Common.Log;
using N_m3u8DL_RE.Common.Resource;

namespace N_m3u8DL_RE.Util;

/// <summary>
/// 实时合并模式下删除已经写入输出的源分片。
/// 分片提前消失或删除失败都只告警：清理动作不能中断整场录制。
/// </summary>
internal static class LiveSegmentCleanup
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// 删除一批已经成功合并进输出的分片。init 段不在此处处理，
    /// 由 <see cref="LiveRecordingCleanup"/> 在录制结束、混流成功后统一清理。
    /// </summary>
    public static void DeleteMergedSegments(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (Path.GetFileName(path).StartsWith("_init", StringComparison.Ordinal))
                continue;
            TryDelete(path);
        }
    }

    public static bool TryDelete(string path)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                if (!File.Exists(path))
                    return true;
                File.Delete(path);
                return true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (attempt < MaxAttempts)
                {
                    Thread.Sleep(RetryDelay);
                    continue;
                }
                Logger.Warn(string.Format(ResString.liveSegmentDeleteFailed, path, ex.Message));
            }
        }
        return false;
    }
}
