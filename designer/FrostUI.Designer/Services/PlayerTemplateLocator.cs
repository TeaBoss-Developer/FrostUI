using System.IO;

namespace FrostUI.Designer.Services;

public static class PlayerTemplateLocator
{
    public const string SingleFileTemplate = "win-x64-single";
    public const string FolderTemplate = "win-x64-folder";

    public static string? Find(string variant)
    {
        var roots = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "PlayerTemplates", variant),
            Path.Combine(AppContext.BaseDirectory, "PlayerTemplates", "win-x64", variant),
        };
        return roots.FirstOrDefault(path =>
            File.Exists(Path.Combine(path, "FrostUI.Player.exe")));
    }

    public static void CopyTemplate(string templateDirectory, string destination)
    {
        Directory.CreateDirectory(destination);
        EnsureWritable(destination);
        foreach (var source in Directory.EnumerateFiles(
                     templateDirectory,
                     "*",
                     SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(templateDirectory, source);
            var fileName = Path.GetFileName(relative);
            if (fileName.Equals("design.frostui.json", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) ||
                fileName.EndsWith(".log", StringComparison.OrdinalIgnoreCase) ||
                fileName.Equals("player-error.log", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            CopyWithRetry(source, target);
        }
    }

    private static void EnsureWritable(string destination)
    {
        var probe = Path.Combine(destination, $".frostui-write-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
        }
        catch (Exception ex)
        {
            throw new IOException(
                $"目标目录不可写：{destination}\n\n" +
                "请检查权限、磁盘空间，或换一个空文件夹。\n" +
                "如果安装了火绒、360 等安全软件，请把 FrostUI.Designer.exe " +
                "加入信任/放行写入；管理员权限通常无法绕过这类 HIPS 规则。\n" +
                ex.Message,
                ex);
        }
    }

    private static void CopyWithRetry(string source, string target)
    {
        Exception? last = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (File.Exists(target))
                {
                    File.SetAttributes(target, FileAttributes.Normal);
                    File.Delete(target);
                }

                File.Copy(source, target, overwrite: true);
                return;
            }
            catch (UnauthorizedAccessException ex)
            {
                last = ex;
                if (IsPlayerExe(target))
                {
                    throw new IOException(
                        $"无法覆盖 {target}。\n\n" +
                        "该文件可能正在运行（FrostUI.Player.exe）或被安全软件锁定。\n" +
                        "请关闭正在运行的 Player，或换一个空导出文件夹后重试。",
                        ex);
                }
            }
            catch (IOException ex)
            {
                last = ex;
            }

            Thread.Sleep(250 * (attempt + 1));
        }

        throw new IOException(
            $"复制 Player 模板失败：{target}\n\n" +
            "可能是目标文件被占用、磁盘空间不足或安全软件拦截。\n" +
            last?.Message,
            last);
    }

    private static bool IsPlayerExe(string path) =>
        string.Equals(
            Path.GetFileName(path),
            "FrostUI.Player.exe",
            StringComparison.OrdinalIgnoreCase);
}
