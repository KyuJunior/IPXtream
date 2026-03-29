$content = Get-Content -Path "Views/PlayerWindow.xaml" -Raw -Encoding UTF8

$content = $content.Replace('Text="â† "', 'Text="←"')
$content = $content.Replace('Text="â ³"', 'Text="⏳"')
$content = $content.Replace('Text="âš  Playback Error"', 'Text="⚠ Playback Error"')
$content = $content.Replace('Text="â›¶"', 'Text="⛶"')
$content = $content.Replace('Value="â–¶"', 'Value="▶"')
$content = $content.Replace('Value="â ¸"', 'Value="⏸"')
$content = $content.Replace('Text="â ¹"', 'Text="⏹"')

$content = $content.Replace('<Grid Background="Transparent" PreviewMouseMove="OnMouseMove">', '<Grid Background="Transparent" PreviewMouseMove="OnMouseMove" MouseLeftButtonDown="OnMouseLeftButtonDown">')

Set-Content -Path "Views/PlayerWindow.xaml" -Value $content -Encoding UTF8
