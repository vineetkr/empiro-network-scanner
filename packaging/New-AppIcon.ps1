# Format conversion only: the supplied JPEG artwork is preserved, not redrawn.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationCore,WindowsBase
$root = Split-Path $PSScriptRoot
$source = [Windows.Media.Imaging.BitmapImage]::new([Uri](Join-Path $root 'Empiro.App\Assets\cat-original.jpeg'))
$images = @()
foreach ($size in @(16,24,32,48,64,128,256)) {
    $visual = [Windows.Media.DrawingVisual]::new()
    $drawing = $visual.RenderOpen()
    $drawing.DrawRectangle([Windows.Media.Brushes]::White, $null, [Windows.Rect]::new(0,0,$size,$size))
    $height = $size * $source.PixelHeight / $source.PixelWidth
    $drawing.DrawImage($source, [Windows.Rect]::new(0,($size-$height)/2,$size,$height))
    $drawing.Close()
    $bitmap = [Windows.Media.Imaging.RenderTargetBitmap]::new($size,$size,96,96,[Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = [Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $memory = [IO.MemoryStream]::new(); $encoder.Save($memory)
    $images += ,@{Size=$size; Bytes=$memory.ToArray()}; $memory.Dispose()
}
$file = [IO.File]::Create((Join-Path $root 'Empiro.App\Assets\Empiro.ico'))
$writer = [IO.BinaryWriter]::new($file)
try {
    $writer.Write([UInt16]0); $writer.Write([UInt16]1); $writer.Write([UInt16]$images.Count)
    $offset = 6 + 16 * $images.Count
    foreach ($entry in $images) {
        $dimension = if ($entry.Size -eq 256) { 0 } else { $entry.Size }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([UInt16]1); $writer.Write([UInt16]32); $writer.Write([UInt32]$entry.Bytes.Length); $writer.Write([UInt32]$offset)
        $offset += $entry.Bytes.Length
    }
    foreach ($entry in $images) { $writer.Write([byte[]]$entry.Bytes) }
} finally { $writer.Dispose(); $file.Dispose() }
