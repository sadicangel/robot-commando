[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,

    [int]$X = 0,
    [int]$Y = 0,
    [int]$Width = 0,
    [int]$Height = 0,
    [double]$Scale = 1.0,
    [int]$Threshold = -1
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Runtime.WindowsRuntime

$StorageFileType = [Windows.Storage.StorageFile, Windows, ContentType = WindowsRuntime]
$AsyncActionType = [Windows.Foundation.IAsyncAction, Windows, ContentType = WindowsRuntime]
$FileAccessModeType = [Windows.Storage.FileAccessMode, Windows, ContentType = WindowsRuntime]
$BitmapDecoderType = [Windows.Graphics.Imaging.BitmapDecoder, Windows, ContentType = WindowsRuntime]
$BitmapPixelFormatType = [Windows.Graphics.Imaging.BitmapPixelFormat, Windows, ContentType = WindowsRuntime]
$BitmapAlphaModeType = [Windows.Graphics.Imaging.BitmapAlphaMode, Windows, ContentType = WindowsRuntime]
$OcrEngineType = [Windows.Media.Ocr.OcrEngine, Windows, ContentType = WindowsRuntime]

function Await([object]$Operation)
{
    for ($attempt = 0; $attempt -lt 1000; $attempt++)
    {
        try
        {
            if ($null -ne $Operation.PSObject.Methods['GetResults'])
            {
                return $Operation.GetResults()
            }

            return $null
        }
        catch
        {
            if ($_.Exception.Message -match 'not completed' -or $_.Exception.Message -match 'currently in progress')
            {
                Start-Sleep -Milliseconds 10
                continue
            }

            throw
        }
    }

    throw 'The async operation did not complete in the expected time.'
}

function Invoke-Ocr([string]$ImagePath)
{
    $storageFile = Await ($StorageFileType::GetFileFromPathAsync($ImagePath))
    $stream = Await ($storageFile.OpenAsync($FileAccessModeType::Read))
    $decoder = Await ($BitmapDecoderType::CreateAsync($stream))
    $bitmap = Await ($decoder.GetSoftwareBitmapAsync(
        $BitmapPixelFormatType::Bgra8,
        $BitmapAlphaModeType::Premultiplied))

    $engine = $OcrEngineType::TryCreateFromUserProfileLanguages()
    if ($null -eq $engine)
    {
        throw "Windows OCR engine could not be created from the current user profile languages."
    }

    return Await ($engine.RecognizeAsync($bitmap))
}

function Get-CroppedBitmap([System.Drawing.Bitmap]$Bitmap)
{
    $cropWidth = if ($Width -gt 0) { $Width } else { $Bitmap.Width - $X }
    $cropHeight = if ($Height -gt 0) { $Height } else { $Bitmap.Height - $Y }
    $cropWidth = [Math]::Min($cropWidth, $Bitmap.Width - $X)
    $cropHeight = [Math]::Min($cropHeight, $Bitmap.Height - $Y)

    $sourceRectangle = [System.Drawing.Rectangle]::new($X, $Y, $cropWidth, $cropHeight)
    $targetWidth = [Math]::Max(1, [int][Math]::Round($cropWidth * $Scale))
    $targetHeight = [Math]::Max(1, [int][Math]::Round($cropHeight * $Scale))

    $target = [System.Drawing.Bitmap]::new($targetWidth, $targetHeight, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
    $graphics = [System.Drawing.Graphics]::FromImage($target)

    try
    {
        $graphics.Clear([System.Drawing.Color]::White)
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.DrawImage(
            $Bitmap,
            [System.Drawing.Rectangle]::new(0, 0, $targetWidth, $targetHeight),
            $sourceRectangle,
            [System.Drawing.GraphicsUnit]::Pixel)
    }
    finally
    {
        $graphics.Dispose()
    }

    if ($Threshold -ge 0)
    {
        for ($xIndex = 0; $xIndex -lt $target.Width; $xIndex++)
        {
            for ($yIndex = 0; $yIndex -lt $target.Height; $yIndex++)
            {
                $pixel = $target.GetPixel($xIndex, $yIndex)
                $brightness = [int](($pixel.R + $pixel.G + $pixel.B) / 3)
                $color = if ($brightness -ge $Threshold) { [System.Drawing.Color]::White } else { [System.Drawing.Color]::Black }
                $target.SetPixel($xIndex, $yIndex, $color)
            }
        }
    }

    return $target
}

$fullPath = (Resolve-Path -LiteralPath $InputPath).Path
$bitmap = [System.Drawing.Bitmap]::FromFile($fullPath)

try
{
    $prepared = Get-CroppedBitmap -Bitmap $bitmap
    $tempPath = Join-Path ([System.IO.Path]::GetTempPath()) ("ocr-" + [Guid]::NewGuid().ToString("N") + ".png")

    try
    {
        $prepared.Save($tempPath, [System.Drawing.Imaging.ImageFormat]::Png)
        $ocr = Invoke-Ocr -ImagePath $tempPath

        [pscustomobject]@{
            InputPath = $fullPath
            Crop = [pscustomobject]@{
                X = $X
                Y = $Y
                Width = $prepared.Width
                Height = $prepared.Height
                Scale = $Scale
                Threshold = $Threshold
            }
            Text = $ocr.Text
            Lines = @(
                foreach ($line in $ocr.Lines)
                {
                    [pscustomobject]@{
                        Text = $line.Text
                        Left = $line.Words[0].BoundingRect.X
                        Top = $line.Words[0].BoundingRect.Y
                        Words = @(
                            foreach ($word in $line.Words)
                            {
                                [pscustomobject]@{
                                    Text = $word.Text
                                    Left = $word.BoundingRect.X
                                    Top = $word.BoundingRect.Y
                                    Width = $word.BoundingRect.Width
                                    Height = $word.BoundingRect.Height
                                }
                            }
                        )
                    }
                }
            )
        } | ConvertTo-Json -Depth 6
    }
    finally
    {
        if (Test-Path -LiteralPath $tempPath)
        {
            Remove-Item -LiteralPath $tempPath -Force
        }
    }
}
finally
{
    $bitmap.Dispose()
    if ($null -ne $prepared)
    {
        $prepared.Dispose()
    }
}
