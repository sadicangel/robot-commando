[CmdletBinding()]
param(
    [string]$BookDirectory = "D:\Development\robot-commando\_book",
    [string]$OutputDirectory = "D:\Development\robot-commando\.ocr-pages",
    [string]$StartImage = "i.014.jpg",
    [double]$Scale = 2.0,
    [int]$MaxImages = 0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$toolDirectory = "D:\Development\robot-commando\tools"
$ocrSource = Join-Path $toolDirectory "WinRtOcr.cs"
$ocrExe = Join-Path $toolDirectory "bin\WinRtOcr.exe"
$csc = "C:\Program Files\Microsoft Visual Studio\18\Insiders\MSBuild\Current\Bin\Roslyn\csc.exe"

if (-not (Test-Path -LiteralPath $ocrExe))
{
    if (-not (Test-Path -LiteralPath $csc))
    {
        throw "Roslyn compiler not found at '$csc'."
    }

    if (-not (Test-Path -LiteralPath $ocrSource))
    {
        throw "OCR source not found at '$ocrSource'."
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $ocrExe) | Out-Null

    & $csc `
        /nologo `
        /langversion:latest `
        /out:$ocrExe `
        /r:"C:\Program Files (x86)\Windows Kits\10\UnionMetadata\10.0.26100.0\Windows.winmd" `
        /r:"C:\Windows\Microsoft.NET\assembly\GAC_MSIL\System.Runtime.WindowsRuntime\v4.0_4.0.0.0__b77a5c561934e089\System.Runtime.WindowsRuntime.dll" `
        /r:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Drawing.dll" `
        /r:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\System.Runtime.Serialization.dll" `
        /r:"C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.8\Facades\System.Runtime.dll" `
        $ocrSource

    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $ocrExe))
    {
        throw "Failed to compile OCR tool from '$ocrSource'."
    }
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null

$images = Get-ChildItem -LiteralPath $BookDirectory -Filter "i.*.jpg" | Sort-Object Name
$images = $images | Where-Object { $_.Name -ge $StartImage }
if ($MaxImages -gt 0)
{
    $images = $images | Select-Object -First $MaxImages
}

Add-Type -AssemblyName System.Drawing

foreach ($image in $images)
{
    $bitmap = [System.Drawing.Bitmap]::FromFile($image.FullName)

    try
    {
        $halfWidth = [int][Math]::Floor($bitmap.Width / 2)
        $leftWidth = $halfWidth
        $rightWidth = $bitmap.Width - $halfWidth

        $targets = @(
            @{
                Side = "left"
                X = 0
                Width = $leftWidth
            },
            @{
                Side = "right"
                X = $halfWidth
                Width = $rightWidth
            }
        )

        foreach ($target in $targets)
        {
            $baseName = [System.IO.Path]::GetFileNameWithoutExtension($image.Name)
            $outputPath = Join-Path $OutputDirectory ($baseName + "." + $target.Side + ".json")

            & $ocrExe `
                --input $image.FullName `
                --output $outputPath `
                --x $target.X `
                --y 0 `
                --width $target.Width `
                --height $bitmap.Height `
                --scale $Scale

            if ($LASTEXITCODE -ne 0)
            {
                throw "OCR failed for '$($image.Name)' ($($target.Side))."
            }
        }
    }
    finally
    {
        $bitmap.Dispose()
    }
}
