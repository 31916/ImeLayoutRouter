# Generate share-card layouts using the existing icon; no runtime website dependency.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$repository = Split-Path -Parent $PSScriptRoot
$icon = [System.Drawing.Icon]::new((Join-Path $repository 'Assets/app.ico'), 256, 256)
$iconBitmap = $icon.ToBitmap()
$green = [System.Drawing.ColorTranslator]::FromHtml('#2d553e')
$paper = [System.Drawing.ColorTranslator]::FromHtml('#fbf8ef')
$muted = [System.Drawing.ColorTranslator]::FromHtml('#59614e')
$line = [System.Drawing.ColorTranslator]::FromHtml('#9ca58e')
$greenBrush = [System.Drawing.SolidBrush]::new($green)
$mutedBrush = [System.Drawing.SolidBrush]::new($muted)
$rule = [System.Drawing.Pen]::new($line, 2)
$titleFont = [System.Drawing.Font]::new('Georgia', 64, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$bodyFont = [System.Drawing.Font]::new('Meiryo', 28, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$smallFont = [System.Drawing.Font]::new('Meiryo', 24, [System.Drawing.FontStyle]::Regular, [System.Drawing.GraphicsUnit]::Pixel)
$format = [System.Drawing.StringFormat]::GenericTypographic.Clone()
$format.FormatFlags = [System.Drawing.StringFormatFlags]::NoWrap
$editions = @(
    @{ Language = 'ja'; Description = 'IMEの直接入力を、指定した配列で'; Editions = 'V1 日本語簡易版  /  V2 多機能版'; Download = '公式ダウンロード' },
    @{ Language = 'en'; Description = 'Your chosen layout for IME direct input'; Editions = 'V1 Japanese essentials  /  V2 Advanced'; Download = 'Official downloads' }
)
try {
    foreach ($edition in $editions) {
        $canvas = [System.Drawing.Bitmap]::new(1200, 630, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
        $graphics = [System.Drawing.Graphics]::FromImage($canvas)
        try {
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
            $graphics.Clear($paper)
            $graphics.FillRectangle($greenBrush, 0, 0, 1200, 14)
            $graphics.FillRectangle($greenBrush, 0, 616, 1200, 14)
            $graphics.DrawString('WINDOWS', $smallFont, $greenBrush, 72, 68, $format)
            $labelWidth = $graphics.MeasureString($edition.Download, $smallFont, 1200, $format).Width
            $graphics.DrawString($edition.Download, $smallFont, $mutedBrush, (1128 - $labelWidth), 68, $format)
            $graphics.DrawLine($rule, 72, 122, 1128, 122)
            $graphics.DrawImage($iconBitmap, 72, 210, 208, 208)
            $graphics.DrawString('IMELayoutRouter', $titleFont, $greenBrush, 324, 236, $format)
            $graphics.DrawString($edition.Description, $bodyFont, $greenBrush, 326, 328, $format)
            $graphics.DrawLine($rule, 72, 494, 1128, 494)
            $graphics.DrawString($edition.Editions, $smallFont, $mutedBrush, 72, 526, $format)
            $domainWidth = $graphics.MeasureString('31916.ch', $smallFont, 1200, $format).Width
            $graphics.DrawString('31916.ch', $smallFont, $greenBrush, (1128 - $domainWidth), 526, $format)
            $destination = Join-Path $repository ('site/social-' + $edition.Language + '-v2.png')
            $canvas.Save($destination, [System.Drawing.Imaging.ImageFormat]::Png)
            # Previously shared cards may still request the original image URL.
            Copy-Item -LiteralPath $destination -Destination (Join-Path $repository ('site/social-' + $edition.Language + '-v1.png'))
            Write-Output $destination
        } finally {
            $graphics.Dispose()
            $canvas.Dispose()
        }
    }
} finally {
    $format.Dispose()
    $titleFont.Dispose()
    $bodyFont.Dispose()
    $smallFont.Dispose()
    $greenBrush.Dispose()
    $mutedBrush.Dispose()
    $rule.Dispose()
    $iconBitmap.Dispose()
    $icon.Dispose()
}
