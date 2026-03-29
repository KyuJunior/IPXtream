$inputFile = "C:\Myapps\ipxtream\IPXtream\Untitled design_20260328_004042_0000.png"
$outputFile = "C:\Myapps\ipxtream\IPXtream\appicon.ico"

# Read raw PNG bytes
$pngBytes = [System.IO.File]::ReadAllBytes($inputFile)
$pngSize = $pngBytes.Length

# Construct 22-byte Vista ICO header for a 256x256 (0) image
[byte[]]$header = @(
    0x00, 0x00,       # Reserved
    0x01, 0x00,       # Type: 1 = ICO
    0x01, 0x00,       # Image count: 1
    0x00,             # Width: 0 (256)
    0x00,             # Height: 0 (256)
    0x00,             # Color palette count
    0x00,             # Reserved
    0x01, 0x00,       # Color planes: 1
    0x20, 0x00        # Bits per pixel: 32
)

# Convert size and offset (22) to Little Endian bytes
$sizeBytes = [BitConverter]::GetBytes([int]$pngSize)
$offsetBytes = [BitConverter]::GetBytes([int]22)

# Write to .ico
$stream = [System.IO.File]::Create($outputFile)
$stream.Write($header, 0, $header.Length)
$stream.Write($sizeBytes, 0, 4)
$stream.Write($offsetBytes, 0, 4)
$stream.Write($pngBytes, 0, $pngSize)
$stream.Close()

Write-Host "Successfully generated appicon.ico!"
