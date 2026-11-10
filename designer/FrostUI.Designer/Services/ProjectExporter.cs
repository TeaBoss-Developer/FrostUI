using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using FrostUI.Designer.Models;

namespace FrostUI.Designer.Services;

public sealed class ExportOptions
{
    public string DestinationDirectory { get; set; } = string.Empty;
    public bool SelfContained { get; set; }
    public bool SingleFile { get; set; }
    public bool ReadyToRun { get; set; }
    public string PublishMode { get; set; } = "Dev";
    public bool OpenFolderAfterExport { get; set; } = true;

    public bool UsesEmbeddedTemplate =>
        PublishMode is "TemplateSingle" or "TemplateFolder";
}

/// <summary>
/// Publishes a design as a standalone FrostUI.Player folder.
/// The player shares the designer's runtime model and script engine, so the
/// exported EXE behaves exactly like the F5 preview.
/// </summary>
public static class ProjectExporter
{
    private const string PlayerProjectRelativePath =
        @"player\FrostUI.Player\FrostUI.Player.csproj";

    public static async Task<string> ExportAsync(
        DesignDocument document,
        ExportOptions options,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.DestinationDirectory))
        {
            throw new InvalidOperationException("请先选择导出文件夹。");
        }

        var destination = Path.GetFullPath(options.DestinationDirectory.Trim());
        Directory.CreateDirectory(destination);
        progress?.Report($"导出目录：{destination}");

        var repositoryRoot = FindRepositoryRoot();
        var playerProject = repositoryRoot is null
            ? null
            : Path.Combine(repositoryRoot, PlayerProjectRelativePath);
        var template = Path.Combine(AppContext.BaseDirectory, "player-template");

        if (playerProject is not null && File.Exists(playerProject))
        {
            progress?.Report(options.UsesEmbeddedTemplate
            ? "发布运行时：内置自包含模板（不调用 dotnet）"
            : $"发布运行时：{(options.SelfContained ? "自包含" : "框架依赖")}");
            if (options.UsesEmbeddedTemplate)
            {
                var variant = options.PublishMode == "TemplateSingle"
                    ? PlayerTemplateLocator.SingleFileTemplate
                    : PlayerTemplateLocator.FolderTemplate;
                var templateDirectory = PlayerTemplateLocator.Find(variant)
                    ?? throw new InvalidOperationException(
                        $"找不到内置 Player 模板：PlayerTemplates/{variant}。\n\n" +
                        "请重新安装 FrostUI Designer，或在开发者模式使用源码编译导出。");
                progress?.Report($"使用内置 Player 模板：{variant}");
                PlayerTemplateLocator.CopyTemplate(templateDirectory, destination);
            }
            else
            {
                var applicationIcon = PrepareApplicationIcon(document, destination);
                await PublishPlayerAsync(
                    repositoryRoot!,
                    playerProject,
                    destination,
                    options,
                    document,
                    applicationIcon,
                    progress,
                    cancellationToken);
            }
        }
        else if (Directory.Exists(template))
        {
            progress?.Report("使用随设计器附带的 player-template");
            CopyDirectory(template, destination);
        }
        else
        {
            throw new InvalidOperationException(
                "找不到 FrostUI.Player 项目，也没有 player-template。\n" +
                "请在 FrostUI 源码目录中运行设计器，或把 player-template 放到设计器目录。");
        }

        var playerExe = Path.Combine(destination, "FrostUI.Player.exe");
        if (!File.Exists(playerExe))
        {
            throw new InvalidOperationException(
                "发布完成，但没有找到 FrostUI.Player.exe。请检查导出日志。");
        }

        var designPath = Path.Combine(destination, "design.frostui.json");
        var exportedDocument = DesignSerializer.Clone(document);
        CopyImageAssets(exportedDocument, destination, progress);
        DesignSerializer.Save(exportedDocument, designPath);
        CopyIcon(document, destination);
        WriteMsixLogos(document, destination);
        WriteLauncher(destination);
        WriteInstaller(destination, document);
        WriteSigningNotes(destination);
        WriteReleaseExtras(destination, document);
        WriteReadme(destination, document, options);
        WriteChecksums(destination);
        WriteBuildReport(destination, document);
        CreateZip(destination);
        progress?.Report($"已写入设计：{Path.GetFileName(designPath)}");
        progress?.Report("导出完成。");

        if (options.OpenFolderAfterExport)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{destination}\"",
                    UseShellExecute = true,
                });
            }
            catch
            {
                // Opening Explorer is a convenience, not part of the export.
            }
        }

        return destination;
    }

    public static string? FindRepositoryRoot()
    {
        foreach (var start in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(start);
            for (var depth = 0; depth < 10 && directory is not null; depth++)
            {
                if (File.Exists(Path.Combine(directory.FullName, "FrostUI.sln")) &&
                    File.Exists(Path.Combine(directory.FullName, PlayerProjectRelativePath)))
                {
                    return directory.FullName;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }

    private static async Task PublishPlayerAsync(
        string repositoryRoot,
        string playerProject,
        string destination,
        ExportOptions options,
        DesignDocument document,
        string? applicationIcon,
        IProgress<string>? progress,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = FindDotnet(),
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("publish");
        startInfo.ArgumentList.Add(playerProject);
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("Release");
        startInfo.ArgumentList.Add("-r");
        startInfo.ArgumentList.Add("win-x64");
        startInfo.ArgumentList.Add("--self-contained");
        startInfo.ArgumentList.Add(options.SelfContained ? "true" : "false");
        if (options.SingleFile)
        {
            startInfo.ArgumentList.Add("-p:PublishSingleFile=true");
            startInfo.ArgumentList.Add("-p:IncludeNativeLibrariesForSelfExtract=true");
        }

        if (options.ReadyToRun)
        {
            startInfo.ArgumentList.Add("-p:PublishReadyToRun=true");
        }

        startInfo.ArgumentList.Add("-p:Version=" + document.Version);
        if (!string.IsNullOrWhiteSpace(document.Company))
        {
            startInfo.ArgumentList.Add("-p:Company=" + document.Company);
        }

        startInfo.ArgumentList.Add("-p:Product=" + document.Name);
        startInfo.ArgumentList.Add("-p:AssemblyTitle=" + document.Name);
        if (!string.IsNullOrWhiteSpace(applicationIcon) && File.Exists(applicationIcon))
        {
            startInfo.ArgumentList.Add("-p:ApplicationIcon=" + Path.GetFullPath(applicationIcon));
        }

        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(destination);
        startInfo.ArgumentList.Add("-p:DebugType=None");
        startInfo.ArgumentList.Add("-p:DebugSymbols=false");
        startInfo.ArgumentList.Add("--nologo");

        progress?.Report("正在运行 dotnet publish，这一步可能需要几十秒…");
        using var process = new Process { StartInfo = startInfo };
        if (!process.Start())
        {
            throw new InvalidOperationException("无法启动 dotnet publish。");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        if (process.ExitCode != 0)
        {
            var detail = string.Join(
                Environment.NewLine,
                new[] { stderr, stdout }.Where(x => !string.IsNullOrWhiteSpace(x)));
            throw new InvalidOperationException(
                $"dotnet publish 失败（退出码 {process.ExitCode}）。{Environment.NewLine}{Tail(detail, 4000)}");
        }

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            foreach (var line in stdout.Split(
                         ['\r', '\n'],
                         StringSplitOptions.RemoveEmptyEntries).TakeLast(12))
            {
                progress?.Report(line.Trim());
            }
        }
    }

    private static string FindDotnet()
    {
        var candidate = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            "dotnet",
            "dotnet.exe");
        return File.Exists(candidate) ? candidate : "dotnet";
    }

    private static void WriteLauncher(string destination)
    {
        File.WriteAllText(
            Path.Combine(destination, "启动.cmd"),
            """
            @echo off
            chcp 65001 >nul
            cd /d "%~dp0"
            start "" "FrostUI.Player.exe"
            """);
    }

    private static void CopyImageAssets(
        DesignDocument document,
        string destination,
        IProgress<string>? progress)
    {
        var imageControls = document.Controls
            .Where(control => control.Kind == ControlKind.Image &&
                              !string.IsNullOrWhiteSpace(control.ImagePath) &&
                              File.Exists(control.ImagePath))
            .ToList();
        if (imageControls.Count == 0)
        {
            return;
        }

        var assets = Path.Combine(destination, "assets");
        Directory.CreateDirectory(assets);
        for (var index = 0; index < imageControls.Count; index++)
        {
            var control = imageControls[index];
            var fileName = $"{index + 1}_{Path.GetFileName(control.ImagePath)}";
            File.Copy(control.ImagePath, Path.Combine(assets, fileName), overwrite: true);
            control.ImagePath = Path.Combine("assets", fileName);
        }

        progress?.Report($"已复制 {imageControls.Count} 个图片资源到 assets");
    }

    private static void CopyIcon(DesignDocument document, string destination)
    {
        if (string.IsNullOrWhiteSpace(document.IconPath) ||
            !File.Exists(document.IconPath))
        {
            return;
        }

        var extension = Path.GetExtension(document.IconPath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".png";
        }

        File.Copy(
            document.IconPath,
            Path.Combine(destination, "app-icon" + extension),
            overwrite: true);
    }

    private static void WriteInstaller(string destination, DesignDocument document)
    {
        var safeName = string.IsNullOrWhiteSpace(document.Name) ? "FrostUI App" : document.Name;
        var scriptName = safeName.Replace("'", "''");
        var installer =
            $$"""
            $ErrorActionPreference = "Stop"
            $appName = '{{scriptName}}'
            $installDir = Join-Path $env:LOCALAPPDATA "Programs\$appName"
            New-Item -ItemType Directory -Force -Path $installDir | Out-Null
            Copy-Item -Path (Join-Path $PSScriptRoot '*') -Destination $installDir -Recurse -Force
            $exe = Join-Path $installDir 'FrostUI.Player.exe'
            $shell = New-Object -ComObject WScript.Shell
            $desktop = [Environment]::GetFolderPath('Desktop')
            $shortcut = $shell.CreateShortcut((Join-Path $desktop "$appName.lnk"))
            $shortcut.TargetPath = $exe
            $shortcut.WorkingDirectory = $installDir
            $shortcut.IconLocation = $exe
            $shortcut.Save()
            Start-Process $exe
            Write-Host "已安装到 $installDir"
            """;
        File.WriteAllText(Path.Combine(destination, "安装.ps1"), installer);
        File.WriteAllText(
            Path.Combine(destination, "安装.cmd"),
            """
            @echo off
            chcp 65001 >nul
            powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0安装.ps1"
            pause
            """);
        File.WriteAllText(
            Path.Combine(destination, "build-info.txt"),
            $"应用：{document.Name}{Environment.NewLine}" +
            $"版本：{document.Version}{Environment.NewLine}" +
            $"公司：{document.Company}{Environment.NewLine}" +
            $"窗口：{document.WindowTitle} · {document.WindowWidth:0}×{document.WindowHeight:0}");

        var uninstaller =
            $$"""
            $ErrorActionPreference = "SilentlyContinue"
            $appName = '{{scriptName}}'
            $installDir = Join-Path $env:LOCALAPPDATA "Programs\$appName"
            $desktop = [Environment]::GetFolderPath('Desktop')
            Remove-Item (Join-Path $desktop "$appName.lnk") -Force
            Remove-Item (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\$appName.lnk") -Force
            if (Test-Path $installDir) {
                Remove-Item $installDir -Recurse -Force
            }
            Write-Host "已卸载 $appName"
            """;
        File.WriteAllText(Path.Combine(destination, "卸载.ps1"), uninstaller);
        File.WriteAllText(
            Path.Combine(destination, "卸载.cmd"),
            """
            @echo off
            chcp 65001 >nul
            powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0卸载.ps1"
            pause
            """);

        var innoName = document.Name.Replace("\"", "'");
        var innoCompany = (string.IsNullOrWhiteSpace(document.Company) ? document.Name : document.Company)
            .Replace("\"", "'");
        var inno =
            $$"""
            [Setup]
            AppName={{innoName}}
            AppVersion={{document.Version}}
            AppPublisher={{innoCompany}}
            DefaultDirName={autopf}\{{innoName}}
            DefaultGroupName={{innoName}}
            OutputBaseFilename={{innoName}}-Setup
            Compression=lzma2
            SolidCompression=yes
            WizardStyle=modern

            [Files]
            Source: "*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

            [Icons]
            Name: "{autoprograms}\{{innoName}}"; Filename: "{app}\FrostUI.Player.exe"
            Name: "{autodesktop}\{{innoName}}"; Filename: "{app}\FrostUI.Player.exe"

            [Run]
            Filename: "{app}\FrostUI.Player.exe"; Description: "启动 {{innoName}}"; Flags: nowait postinstall skipifsilent
            """;
        File.WriteAllText(Path.Combine(destination, "installer.iss"), inno);
    }

    private static string? PrepareApplicationIcon(DesignDocument document, string destination)
    {
        if (string.IsNullOrWhiteSpace(document.IconPath) || !File.Exists(document.IconPath))
        {
            return null;
        }

        var extension = Path.GetExtension(document.IconPath).ToLowerInvariant();
        if (extension == ".ico")
        {
            return Path.GetFullPath(document.IconPath);
        }

        if (extension != ".png")
        {
            return null;
        }

        var png = File.ReadAllBytes(document.IconPath);
        var icoPath = Path.Combine(destination, "app-icon.ico");
        using var stream = File.Create(icoPath);
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
        writer.Write((ushort)1);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((byte)0);
        writer.Write((ushort)1);
        writer.Write((ushort)32);
        writer.Write((uint)png.Length);
        writer.Write((uint)22);
        writer.Write(png);
        return icoPath;
    }

    private static void WriteMsixLogos(DesignDocument document, string destination)
    {
        if (string.IsNullOrWhiteSpace(document.IconPath) || !File.Exists(document.IconPath))
        {
            return;
        }

        try
        {
            var source = System.Windows.Media.Imaging.BitmapFrame.Create(
                new Uri(document.IconPath),
                System.Windows.Media.Imaging.BitmapCreateOptions.None,
                System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
            var assets = Path.Combine(destination, "assets");
            Directory.CreateDirectory(assets);
            foreach (var (name, size) in new[]
                     {
                         ("StoreLogo.png", 50),
                         ("Square44x44Logo.png", 44),
                         ("Square150x150Logo.png", 150),
                     })
            {
                var scaled = new System.Windows.Media.Imaging.TransformedBitmap(
                    source,
                    new System.Windows.Media.ScaleTransform(
                        (double)size / source.PixelWidth,
                        (double)size / source.PixelHeight));
                var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(scaled));
                using var stream = File.Create(Path.Combine(assets, name));
                encoder.Save(stream);
            }
        }
        catch
        {
            // A missing logo must not fail the whole export.
        }
    }

    private static void WriteChecksums(string destination)
    {
        var lines = Directory
            .EnumerateFiles(destination, "*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith("checksums.txt", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path =>
            {
                using var stream = File.OpenRead(path);
                var hash = Convert.ToHexString(SHA256.HashData(stream));
                return $"{hash}  {Path.GetRelativePath(destination, path)}";
            });
        File.WriteAllLines(Path.Combine(destination, "checksums.txt"), lines);
    }

    private static void WriteBuildReport(string destination, DesignDocument document)
    {
        var files = Directory.GetFiles(destination, "*", SearchOption.AllDirectories);
        var report = System.Text.Json.JsonSerializer.Serialize(
            new
            {
                app = document.Name,
                version = document.Version,
                controls = document.Controls.Count,
                files = files.Length,
                bytes = files.Sum(file => new FileInfo(file).Length),
                generatedAt = DateTimeOffset.Now.ToString("O"),
            },
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(destination, "build-report.json"), report);
    }

    private static void CreateZip(string destination)
    {
        var zipPath = destination.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + ".zip";
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        ZipFile.CreateFromDirectory(destination, zipPath, CompressionLevel.Optimal, includeBaseDirectory: false);
    }

    private static void WriteSigningNotes(string destination)
    {
        File.WriteAllText(
            Path.Combine(destination, "签名说明.txt"),
            """
            代码签名（需要你自己的 PFX 证书）：
            signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com /a "FrostUI.Player.exe"

            如果使用证书文件：
            signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com /f "cert.pfx" /p "密码" "FrostUI.Player.exe"

            签名后可以用：
            signtool verify /pa /v "FrostUI.Player.exe"

            FrostUI Designer 没有内置私钥，也不会伪造签名。
            """);
    }

    private static void WriteReleaseExtras(string destination, DesignDocument document)
    {
        File.WriteAllText(
            Path.Combine(destination, "app.manifest"),
            """
            <?xml version="1.0" encoding="utf-8"?>
            <assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
              <assemblyIdentity version="1.0.0.0" name="FrostUI.Player" />
              <trustInfo xmlns="urn:schemas-microsoft-com:asm.v3">
                <security><requestedPrivileges><requestedExecutionLevel level="asInvoker" uiAccess="false" /></requestedPrivileges></security>
              </trustInfo>
              <application xmlns="urn:schemas-microsoft-com:asm.v3">
                <windowsSettings>
                  <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
                  <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
                </windowsSettings>
              </application>
            </assembly>
            """);

        var info = System.Text.Json.JsonSerializer.Serialize(
            new
            {
                name = document.Name,
                version = document.Version,
                company = document.Company,
                windowTitle = document.WindowTitle,
                width = document.WindowWidth,
                height = document.WindowHeight,
                controls = document.Controls.Count,
                builtAt = DateTimeOffset.Now.ToString("O"),
            },
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(destination, "version-info.json"), info);

        File.WriteAllText(
            Path.Combine(destination, "LICENSE.txt"),
            $"""
            {document.Name}
            Copyright (c) {DateTime.Now:yyyy} {document.Company}

            这是由 FrostUI Designer 生成的应用发布目录。
            请把本文件替换为你自己的最终用户许可协议（EULA）。
            """);

        var versionParts = document.Version.Split('.');
        var msixVersion = string.Join('.', versionParts.Take(4));
        if (versionParts.Length < 4)
        {
            msixVersion += string.Concat(Enumerable.Repeat(".0", 4 - versionParts.Length));
        }

        var packageName = new string(document.Name
            .Select(c => char.IsLetterOrDigit(c) || c is '.' or '-'
                ? c
                : '_')
            .ToArray());
        File.WriteAllText(
            Path.Combine(destination, "Package.appxmanifest"),
            $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                     xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10">
              <Identity Name="{packageName}" Version="{msixVersion}" Publisher="CN=YourPublisher" />
              <Properties>
                <DisplayName>{document.Name}</DisplayName>
                <PublisherDisplayName>{document.Company}</PublisherDisplayName>
                <Logo>assets\StoreLogo.png</Logo>
              </Properties>
              <Applications>
                <Application Id="App" Executable="FrostUI.Player.exe" EntryPoint="Windows.FullTrustApplication">
                  <uap:VisualElements DisplayName="{document.Name}" Description="{document.Description}"
                                      BackgroundColor="transparent" Square150x150Logo="assets\Square150x150Logo.png"
                                      Square44x44Logo="assets\Square44x44Logo.png" />
                </Application>
              </Applications>
            </Package>
            """);

        File.WriteAllText(
            Path.Combine(destination, "快速开始.txt"),
            $"""
            1. 双击 FrostUI.Player.exe 直接运行。
            2. 需要安装到本机：双击 安装.cmd。
            3. 卸载：双击 卸载.cmd。
            4. 版本/公司/控件信息：version-info.json。
            5. 正式安装包可用 installer.iss（Inno Setup）或 Package.appxmanifest（MSIX）。
            """);

        File.WriteAllText(
            Path.Combine(destination, "签名.ps1"),
            """
            param([string]$Pfx = "", [string]$Password = "")
            $exe = Join-Path $PSScriptRoot "FrostUI.Player.exe"
            if ($Pfx) {
                signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com /f $Pfx /p $Password $exe
            } else {
                signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com /a $exe
            }
            """);
    }

    private static void WriteReadme(
        string destination,
        DesignDocument document,
        ExportOptions options)
    {
        var readme =
            $"""
             {document.Name}
             ========================================

             应用名称：{document.Name}
             版本：{document.Version}
             公司：{document.Company}
             窗口标题：{document.WindowTitle}
             窗口尺寸：{document.WindowWidth:0} × {document.WindowHeight:0}
             设计控件：{document.Controls.Count} 个
             运行方式：{(options.UsesEmbeddedTemplate ? "内置模板" : options.SelfContained ? "自包含，无需安装 .NET" : ".NET 9 Desktop Runtime（框架依赖）")}
             发布形式：{(options.UsesEmbeddedTemplate ? (options.PublishMode == "TemplateSingle" ? "内置模板 · 单文件" : "内置模板 · 文件夹") : options.SingleFile ? "单文件" : "文件夹")}

             启动：
               双击 FrostUI.Player.exe
               或双击 启动.cmd

             安装（当前用户，无需管理员）：
               双击 安装.cmd
               或运行 安装.ps1；会复制到 %LOCALAPPDATA%\Programs 并创建桌面快捷方式。

             文件：
               design.frostui.json   当前窗口、控件与脚本
               build-info.txt        应用名称、版本、公司
               assets/               图片控件引用的资源
               签名说明.txt          signtool 签名命令
               player.log            运行输出和脚本日志（首次运行后生成）
               player-error.log      启动失败时的详细错误（如有）

             这是由 FrostUI Designer 导出的 release 文件夹。
             修改界面或脚本后，在设计器中重新导出即可更新。
             """;
        File.WriteAllText(Path.Combine(destination, "README.txt"), readme);
    }

    private static string Tail(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[^maxLength..];

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(
                Path.Combine(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }
}
