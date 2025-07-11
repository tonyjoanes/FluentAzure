# Create a simple PNG icon for FluentAzure
# This script creates a basic 128x128 PNG icon

Add-Type -AssemblyName System.Drawing

# Create a new bitmap
$bitmap = New-Object System.Drawing.Bitmap(128, 128)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)

# Set high quality rendering
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit

# Fill background with Azure blue
$azureBlue = [System.Drawing.Color]::FromArgb(0, 120, 212)
$graphics.Clear($azureBlue)

# Create a white cloud-like shape
$cloudBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$cloudPath = New-Object System.Drawing.Drawing2D.GraphicsPath

# Simple cloud shape
$cloudPath.AddEllipse(20, 30, 40, 30)
$cloudPath.AddEllipse(40, 20, 35, 35)
$cloudPath.AddEllipse(60, 25, 40, 30)
$cloudPath.AddEllipse(80, 35, 30, 25)

$graphics.FillPath($cloudBrush, $cloudPath)

# Add "F" for Fluent
$font = New-Object System.Drawing.Font("Arial", 24, [System.Drawing.FontStyle]::Bold)
$textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$textSize = $graphics.MeasureString("F", $font)
$textX = (128 - $textSize.Width) / 2
$textY = 90
$graphics.DrawString("F", $font, $textBrush, $textX, $textY)

# Add some configuration dots
$dotBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
$graphics.FillEllipse($dotBrush, 30, 40, 6, 6)
$graphics.FillEllipse($dotBrush, 90, 40, 6, 6)
$graphics.FillEllipse($dotBrush, 60, 25, 6, 6)

# Save the icon
$iconPath = Join-Path $PSScriptRoot "..\icon.png"
$bitmap.Save($iconPath, [System.Drawing.Imaging.ImageFormat]::Png)

# Clean up
$graphics.Dispose()
$bitmap.Dispose()
$cloudBrush.Dispose()
$textBrush.Dispose()
$dotBrush.Dispose()
$font.Dispose()

Write-Host "Icon created at: $iconPath"
