import re
import codecs

path = 'Views/PlayerWindow.xaml'

with codecs.open(path, 'r', encoding='utf-8', errors='ignore') as f:
    text = f.read()

# Buffering symbol
text = re.sub(r'Text="[^"]+" FontSize="22" Margin="0,0,10,0"', 'Text="⏳" FontSize="22" Margin="0,0,10,0"', text)

# Error
text = re.sub(r'Text="[^\s]+ Playback Error"', 'Text="⚠ Playback Error"', text)

# Back
text = re.sub(r'<TextBlock Text="[^"]+" FontSize="18"/>\s*</Button>\s*<!-- Channel name \+ icon -->', '<TextBlock Text="←" FontSize="18"/>\n                            </Button>\n\n                            <!-- Channel name + icon -->', text)

# Fullscreen
text = re.sub(r'ToolTip="Toggle fullscreen \(F\)">\s*<TextBlock Text="[^"]+" FontSize="18"/>', 'ToolTip="Toggle fullscreen (F)">\n                                <TextBlock Text="⛶" FontSize="18"/>', text)

# Play
text = re.sub(r'<Setter Property="Text" Value="[^"]+"/>\s*<Style\.Triggers>\s*<DataTrigger Binding="\{Binding IsPlaying\}"', '<Setter Property="Text" Value="▶"/>\n                                                <Style.Triggers>\n                                                    <DataTrigger Binding="{Binding IsPlaying}"', text)

# Pause
text = re.sub(r'Value="True">\s*<Setter Property="Text" Value="[^"]+"/>\s*</DataTrigger>', 'Value="True">\n                                                        <Setter Property="Text" Value="⏸"/>\n                                                    </DataTrigger>', text)

# Stop
text = re.sub(r'ToolTip="Stop">\s*<TextBlock Text="[^"]+" FontSize="18"/>', 'ToolTip="Stop">\n                                    <TextBlock Text="⏹" FontSize="18"/>', text)

# Mouse Click
text = text.replace('<Grid Background="Transparent" PreviewMouseMove="OnMouseMove">', '<Grid Background="Transparent" PreviewMouseMove="OnMouseMove" MouseLeftButtonDown="OnMouseLeftButtonDown">')

with codecs.open(path, 'w', encoding='utf-8') as f:
    f.write(text)
