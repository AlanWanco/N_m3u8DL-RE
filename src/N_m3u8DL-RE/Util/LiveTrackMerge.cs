using N_m3u8DL_RE.Common.Entity;
using N_m3u8DL_RE.Common.Resource;
using N_m3u8DL_RE.Entity;
using System.Text.RegularExpressions;

namespace N_m3u8DL_RE.Util;

// 直播录制目录的字节级合并：init 在前、分片按序号顺序，优先使用解密产物。
// 与实时合并写出的字节流一致，因此不需要 ffmpeg，也不依赖轨道信息探测。
internal static class LiveTrackMerge
{
    private static readonly Regex SegmentNameRegex = new(
        @"^(?:\d+(?:_\d+)?|\d{10,}_.+)(?:_dec)?\.(?:m4s|mp4|ts|aac|m4a)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static bool HasSegments(string directory) =>
        Directory.Exists(directory) && Directory.EnumerateFiles(directory)
            .Any(path => SegmentNameRegex.IsMatch(Path.GetFileName(path)));

    internal static async Task<bool> ConcatAsync(string directory, string target, CancellationToken token)
    {
        var names = Directory.GetFiles(directory).Select(Path.GetFileName).OfType<string>().ToArray();
        var hasMultiplePeriods = names
            .Where(name => name.StartsWith("_init", StringComparison.OrdinalIgnoreCase))
            .Select(name => name.Replace("_dec.", ".", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Skip(1)
            .Any();
        if (hasMultiplePeriods)
            throw new InvalidOperationException(string.Format(ResString.liveMergeMultiInit, directory));
        var segments = names
            .Where(name => SegmentNameRegex.IsMatch(name))
            .GroupBy(name => name.Replace("_dec.", ".", StringComparison.OrdinalIgnoreCase), StringComparer.OrdinalIgnoreCase)
            .Select(group => group.FirstOrDefault(name => name.Contains("_dec.", StringComparison.OrdinalIgnoreCase)) ?? group.First())
            .OrderBy(name => name, Comparer<string>.Create(FileToolUtil.CompareNatural))
            .ToArray();
        if (segments.Length == 0)
            return false;
        var decrypted = segments.All(name => name.Contains("_dec.", StringComparison.OrdinalIgnoreCase));
        var initName = decrypted ? "_init_dec.mp4" : "_init.mp4";
        var init = names.Contains(initName, StringComparer.OrdinalIgnoreCase) ? Path.Combine(directory, initName) : null;
        var partial = target + ".part";
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        try
        {
            await using (var output = new FileStream(partial, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, useAsync: true))
            {
                if (init != null)
                {
                    await using var initStream = new FileStream(init, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
                    await initStream.CopyToAsync(output, token);
                }
                foreach (var name in segments)
                {
                    token.ThrowIfCancellationRequested();
                    await using var input = new FileStream(Path.Combine(directory, name), FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, useAsync: true);
                    await input.CopyToAsync(output, token);
                }
                await output.FlushAsync(token);
            }
            File.Move(partial, target, overwrite: true);
            return true;
        }
        finally
        {
            if (File.Exists(partial))
                File.Delete(partial);
        }
    }

    // 分片目录合并后的容器后缀：fMP4 片段输出 mp4/音频 m4a，TS 保持 ts。
    internal static string OutputExtension(StreamSpec spec)
    {
        if (spec.MediaType == Common.Enum.MediaType.AUDIO && spec.Extension is "m4s" or "mp4")
            return ".m4a";
        return spec.Extension switch
        {
            "m4s" or "mp4" => ".mp4",
            "ts" or null => ".ts",
            var ext => "." + ext,
        };
    }
}
