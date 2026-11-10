# Rebuilds the demo and captures the four showcase pages plus the light overview.
# The demo opens its own demo wallpaper behind the glass window so that the real
# DWM acrylic material has something colorful to sample.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $root 'samples\FrostUI.Demo\FrostUI.Demo.csproj'
$demoDir = Join-Path $root 'samples\FrostUI.Demo\bin\Debug\net9.0-windows'
$dll = Join-Path $demoDir 'FrostUI.Demo.dll'
$shots = Join-Path $root 'screenshots'

dotnet build (Join-Path $root 'FrostUI.sln') -v:q -nologo
if ($LASTEXITCODE -ne 0) {
    throw 'FrostUI build failed.'
}

New-Item -ItemType Directory -Force $shots | Out-Null

$jobs = @(
    @{ Name = 'overview.png';        Page = 'overview';   Extra = @() },
    @{ Name = 'components.png';      Page = 'components'; Extra = @() },
    @{ Name = 'appearance.png';      Page = 'appearance'; Extra = @() },
    @{ Name = 'settings.png';        Page = 'settings';   Extra = @() },
    @{ Name = 'overview-light.png';  Page = 'overview';   Extra = @('--light') },
    @{ Name = 'appearance-light.png';Page = 'appearance'; Extra = @('--light') }
)

foreach ($job in $jobs) {
    $output = Join-Path $shots $job.Name
    Remove-Item $output -ErrorAction SilentlyContinue
    $arguments = @($dll, '--screenshot', $output, '--page', $job.Page) + $job.Extra
    $process = Start-Process -FilePath 'dotnet' -ArgumentList $arguments -WorkingDirectory $demoDir -WindowStyle Hidden -PassThru

    $deadline = (Get-Date).AddSeconds(15)
    while (-not (Test-Path $output) -and (Get-Date) -lt $deadline) {
        Start-Sleep -Milliseconds 200
    }

    if (Test-Path $output) {
        # The file appears as soon as the encoder starts; wait until its size settles.
        $previous = -1
        for ($i = 0; $i -lt 30; $i++) {
            $current = (Get-Item $output).Length
            if ($current -gt 0 -and $current -eq $previous) {
                break
            }

            $previous = $current
            Start-Sleep -Milliseconds 150
        }

        Write-Host "captured $($job.Name)"
    }
    else {
        if (-not $process.HasExited) {
            $process.Kill()
        }

        Write-Warning "timed out capturing $($job.Name)"
    }
}
