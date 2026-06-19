import os

target_xaml_path = r"IPXtream/Views/DashboardWindow.xaml"

with open(target_xaml_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Normalize file content to LF
content = content.replace('\r\n', '\n')

def replace_by_normalized_lines(target_block, replacement_block, label):
    global content
    
    # Split target block into lines and strip them (keep empty lines as empty strings)
    target_lines = [line.strip() for line in target_block.replace('\r\n', '\n').split('\n')]
    
    # Split file content into lines
    file_lines = content.split('\n')
    
    # Search for a consecutive sequence of lines in file_lines that match target_lines (when stripped)
    found_idx = -1
    for i in range(len(file_lines) - len(target_lines) + 1):
        match = True
        for j in range(len(target_lines)):
            if file_lines[i + j].strip() != target_lines[j]:
                match = False
                break
        if match:
            found_idx = i
            break
            
    if found_idx != -1:
        # We found the block! Get the indentation of the first line in the file block
        first_line = file_lines[found_idx]
        indent_len = len(first_line) - len(first_line.lstrip())
        indent_str = ' ' * indent_len
        
        # Format the replacement block to have the same base indentation
        rep_lines_raw = replacement_block.replace('\r\n', '\n').split('\n')
        non_empty_indents = [len(rline) - len(rline.lstrip()) for rline in rep_lines_raw if rline.strip()]
        min_rep_indent = min(non_empty_indents) if non_empty_indents else 0
        
        rep_lines_formatted = []
        for rline in rep_lines_raw:
            if not rline.strip():
                rep_lines_formatted.append("")
            else:
                rel_indent = len(rline) - len(rline.lstrip()) - min_rep_indent
                rep_lines_formatted.append(indent_str + (' ' * rel_indent) + rline.strip())
                
        # Replace the slice of lines in file_lines
        file_lines[found_idx : found_idx + len(target_lines)] = rep_lines_formatted
        content = '\n'.join(file_lines)
        print(f"Successfully replaced: {label}")
    else:
        print(f"Error: Could not find block for: {label}")

# 1. Movie Details Buttons
old_details_buttons = """                                 <!-- Action Buttons -->
                                 <StackPanel Grid.Row="4" Orientation="Horizontal">
                                     <!-- Play Button -->
                                     <Button Command="{Binding PlayCommand}" CommandParameter="{Binding SelectedMovieForInfo}"
                                             Background="#4F8EF7" Foreground="White" FontSize="14" FontWeight="Bold" BorderThickness="0" Cursor="Hand" Padding="20,8" Margin="0,0,12,0">
                                         <Button.Template>
                                             <ControlTemplate TargetType="Button">
                                                 <Border x:Name="PlayBd" CornerRadius="6" Background="{TemplateBinding Background}">
                                                     <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Center" Margin="{TemplateBinding Padding}">
                                                         <Path Data="{StaticResource IconPlay}" Style="{StaticResource VectorIconFilled}" Width="12" Height="12" Margin="0,0,8,0"/>
                                                         <TextBlock Text="Play" FontWeight="Bold" VerticalAlignment="Center"/>
                                                     </StackPanel>
                                                 </Border>
                                                 <ControlTemplate.Triggers>
                                                     <Trigger Property="IsMouseOver" Value="True">
                                                         <Setter TargetName="PlayBd" Property="Background" Value="#3B7AE3"/>
                                                     </Trigger>
                                                 </ControlTemplate.Triggers>
                                             </ControlTemplate>
                                         </Button.Template>
                                     </Button>

                                     <!-- Download Button -->
                                     <Button Command="{Binding DownloadCommand}" CommandParameter="{Binding SelectedMovieForInfo}"
                                             Visibility="{Binding SelectedMovieForInfo.IsDownloadable, Converter={StaticResource BoolToVisibilityConverter}}"
                                             Background="#2C2C3E" Foreground="White" FontSize="14" FontWeight="Bold" BorderThickness="0" Cursor="Hand" Padding="20,8">
                                         <Button.Template>
                                             <ControlTemplate TargetType="Button">
                                                 <Border x:Name="DlBd" CornerRadius="6" Background="{TemplateBinding Background}">
                                                     <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Center" Margin="{TemplateBinding Padding}">
                                                         <Path Data="{StaticResource IconDownload}" Style="{StaticResource VectorIconFilled}" Width="12" Height="12" Margin="0,0,8,0"/>
                                                         <TextBlock Text="Download" FontWeight="Bold" VerticalAlignment="Center"/>
                                                     </StackPanel>
                                                 </Border>
                                                 <ControlTemplate.Triggers>
                                                     <Trigger Property="IsMouseOver" Value="True">
                                                         <Setter TargetName="DlBd" Property="Background" Value="#3D3D52"/>
                                                     </Trigger>
                                                 </ControlTemplate.Triggers>
                                             </ControlTemplate>
                                         </Button.Template>
                                     </Button>
                                 </StackPanel>"""

new_details_buttons = """                                 <!-- Action Buttons -->
                                 <StackPanel Grid.Row="4" Orientation="Horizontal">
                                     <!-- Play Button -->
                                     <Button Command="{Binding PlayCommand}" CommandParameter="{Binding SelectedMovieForInfo}"
                                             Foreground="White" FontSize="14" FontWeight="Bold" BorderThickness="0" Cursor="Hand" Padding="24,10" Margin="0,0,12,0">
                                         <Button.Template>
                                             <ControlTemplate TargetType="Button">
                                                 <Border x:Name="PlayBd" CornerRadius="10" RenderTransformOrigin="0.5,0.5">
                                                     <Border.Background>
                                                         <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
                                                             <GradientStop Color="#5B9BFF" Offset="0"/>
                                                             <GradientStop Color="#8B6BD4" Offset="1"/>
                                                         </LinearGradientBrush>
                                                     </Border.Background>
                                                     <Border.Effect>
                                                         <DropShadowEffect BlurRadius="15" Color="#5B9BFF" Opacity="0.3" ShadowDepth="2" Direction="270"/>
                                                     </Border.Effect>
                                                     <Border.RenderTransform>
                                                         <ScaleTransform ScaleX="1" ScaleY="1"/>
                                                     </Border.RenderTransform>
                                                     <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Center" Margin="{TemplateBinding Padding}">
                                                         <Path Data="{StaticResource IconPlay}" Style="{StaticResource VectorIconFilled}" Width="12" Height="12" Margin="0,0,8,0"/>
                                                         <TextBlock Text="Watch Now" FontWeight="Bold" VerticalAlignment="Center"/>
                                                     </StackPanel>
                                                 </Border>
                                                 <ControlTemplate.Triggers>
                                                     <Trigger Property="IsMouseOver" Value="True">
                                                         <Setter TargetName="PlayBd" Property="Opacity" Value="0.95"/>
                                                         <Setter TargetName="PlayBd" Property="RenderTransform">
                                                             <Setter.Value>
                                                                 <ScaleTransform ScaleX="1.03" ScaleY="1.03"/>
                                                             </Setter.Value>
                                                         </Setter>
                                                     </Trigger>
                                                 </ControlTemplate.Triggers>
                                             </ControlTemplate>
                                         </Button.Template>
                                     </Button>

                                     <!-- Download Button -->
                                     <Button Command="{Binding DownloadCommand}" CommandParameter="{Binding SelectedMovieForInfo}"
                                             Visibility="{Binding SelectedMovieForInfo.IsDownloadable, Converter={StaticResource BoolToVisibilityConverter}}"
                                             Foreground="White" FontSize="14" FontWeight="Bold" BorderThickness="0" Cursor="Hand" Padding="22,10">
                                         <Button.Template>
                                             <ControlTemplate TargetType="Button">
                                                 <Border x:Name="DlBd" CornerRadius="10" Background="#20FFFFFF" BorderBrush="#20FFFFFF" BorderThickness="1">
                                                     <StackPanel Orientation="Horizontal" HorizontalAlignment="Center" VerticalAlignment="Center" Margin="{TemplateBinding Padding}">
                                                         <Path Data="{StaticResource IconDownload}" Style="{StaticResource VectorIconFilled}" Width="12" Height="12" Margin="0,0,8,0"/>
                                                         <TextBlock Text="Download" FontWeight="Bold" VerticalAlignment="Center"/>
                                                     </StackPanel>
                                                 </Border>
                                                 <ControlTemplate.Triggers>
                                                     <Trigger Property="IsMouseOver" Value="True">
                                                         <Setter TargetName="DlBd" Property="Background" Value="#35FFFFFF"/>
                                                         <Setter TargetName="DlBd" Property="BorderBrush" Value="#40FFFFFF"/>
                                                     </Trigger>
                                                 </ControlTemplate.Triggers>
                                             </ControlTemplate>
                                         </Button.Template>
                                     </Button>
                                 </StackPanel>"""

replace_by_normalized_lines(old_details_buttons, new_details_buttons, "Details Modal Action Buttons")

# Convert back to CRLF before writing
content = content.replace('\n', '\r\n')

with open(target_xaml_path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

print("Restyle part final finished.")
