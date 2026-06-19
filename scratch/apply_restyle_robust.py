import os

target_xaml_path = r"IPXtream/Views/DashboardWindow.xaml"

with open(target_xaml_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Normalize file content to LF
content = content.replace('\r\n', '\n')

def replace_by_normalized_lines(target_block, replacement_block, label):
    global content
    
    # Clean and split target block into trimmed lines (ignoring empty lines)
    target_lines = [line.strip() for line in target_block.replace('\r\n', '\n').split('\n') if line.strip()]
    if not target_lines:
        print(f"Warning: Empty target block for {label}")
        return
        
    # Clean and split file content into lines
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
        # We found the block!
        # Let's get the indentation of the first line in the file block
        first_line = file_lines[found_idx]
        indent_len = len(first_line) - len(first_line.lstrip())
        indent_str = ' ' * indent_len
        
        # Let's format the replacement block to have the same base indentation
        rep_lines_raw = replacement_block.replace('\r\n', '\n').split('\n')
        # Find the minimum indentation of non-empty lines in replacement block
        non_empty_indents = []
        for rline in rep_lines_raw:
            if rline.strip():
                non_empty_indents.append(len(rline) - len(rline.lstrip()))
        min_rep_indent = min(non_empty_indents) if non_empty_indents else 0
        
        rep_lines_formatted = []
        for rline in rep_lines_raw:
            if not rline.strip():
                rep_lines_formatted.append("")
            else:
                # Relative indent
                rel_indent = len(rline) - len(rline.lstrip()) - min_rep_indent
                rep_lines_formatted.append(indent_str + (' ' * rel_indent) + rline.strip())
                
        # Replace the slice of lines in file_lines
        # We need to know how many lines we are replacing.
        # Since target_lines contains trimmed non-empty lines, we should replace from found_idx
        # to the line corresponding to the last matched line in the file.
        # Let's calculate the exact slice of lines to replace in file_lines.
        end_idx = found_idx
        matched_count = 0
        while matched_count < len(target_lines) and end_idx < len(file_lines):
            if file_lines[end_idx].strip() == target_lines[matched_count]:
                matched_count += 1
            end_idx += 1
            
        file_lines[found_idx : end_idx] = rep_lines_formatted
        content = '\n'.join(file_lines)
        print(f"Successfully replaced: {label}")
    else:
        print(f"Error: Could not find block for: {label}")

# 1. Movie Details Poster shadow
old_details_poster = """                             <!-- Movie Poster -->
                             <Border Grid.Column="0" CornerRadius="8" ClipToBounds="True" BorderBrush="#33FFFFFF" BorderThickness="1" Height="300" VerticalAlignment="Top" Margin="0,0,24,0">"""

new_details_poster = """                             <!-- Movie Poster -->
                             <Border Grid.Column="0" CornerRadius="12" ClipToBounds="True" BorderBrush="#15FFFFFF" BorderThickness="1" Height="320" VerticalAlignment="Top" Margin="0,0,24,0">
                                 <Border.Effect>
                                     <DropShadowEffect BlurRadius="15" Color="#000000" Opacity="0.4" ShadowDepth="4" Direction="270"/>
                                 </Border.Effect>"""

replace_by_normalized_lines(old_details_poster, new_details_poster, "Details Poster Border & Shadow")

# 2. Movie Details metadata wrap panel styling
old_details_metadata = """                                 <!-- Metadata info -->
                                 <WrapPanel Grid.Row="1" Margin="0,0,0,14">
                                     <Border Background="#FFD700" CornerRadius="4" Padding="5,2" Margin="0,0,10,0">
                                         <StackPanel Orientation="Horizontal">
                                             <Path Data="{StaticResource IconStar}" Fill="#0F0F1A" Width="9" Height="9" Stretch="Uniform" VerticalAlignment="Center" Margin="0,0,4,0"/>
                                             <TextBlock Text="{Binding SelectedMovieForInfo.Rating}" Foreground="#0F0F1A" FontSize="10" FontWeight="Bold" VerticalAlignment="Center"/>
                                         </StackPanel>
                                     </Border>
                                     <TextBlock Text="{Binding SelectedMovieForInfo.ReleaseDate}" Foreground="{DynamicResource TextMuted}" FontSize="12" Margin="0,0,16,0" VerticalAlignment="Center"/>
                                     <TextBlock Text="{Binding SelectedMovieForInfo.Genre}" Foreground="{DynamicResource TextMuted}" FontSize="12" VerticalAlignment="Center"/>
                                 </WrapPanel>"""

new_details_metadata = """                                 <!-- Metadata info -->
                                 <WrapPanel Grid.Row="1" Margin="0,0,0,14">
                                     <Border CornerRadius="8" Padding="8,3" Margin="0,0,8,0">
                                         <Border.Background>
                                             <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
                                                 <GradientStop Color="#FFD700" Offset="0"/>
                                                 <GradientStop Color="#FFA500" Offset="1"/>
                                             </LinearGradientBrush>
                                         </Border.Background>
                                         <StackPanel Orientation="Horizontal">
                                             <Path Data="{StaticResource IconStar}" Fill="#0F0F1A" Width="9" Height="9" Stretch="Uniform" VerticalAlignment="Center" Margin="0,0,4,0"/>
                                             <TextBlock Text="{Binding SelectedMovieForInfo.Rating}" Foreground="#0F0F1A" FontSize="11" FontWeight="Bold" VerticalAlignment="Center"/>
                                         </StackPanel>
                                     </Border>
                                     <Border Background="#15FFFFFF" BorderBrush="#15FFFFFF" BorderThickness="1" CornerRadius="8" Padding="8,3" Margin="0,0,8,0">
                                         <TextBlock Text="{Binding SelectedMovieForInfo.ReleaseDate}" Foreground="White" FontSize="11" FontWeight="SemiBold" VerticalAlignment="Center"/>
                                     </Border>
                                     <Border Background="#15FFFFFF" BorderBrush="#15FFFFFF" BorderThickness="1" CornerRadius="8" Padding="8,3" Margin="0,0,8,0">
                                         <TextBlock Text="{Binding SelectedMovieForInfo.Genre}" Foreground="White" FontSize="11" FontWeight="SemiBold" VerticalAlignment="Center"/>
                                     </Border>
                                 </WrapPanel>"""

replace_by_normalized_lines(old_details_metadata, new_details_metadata, "Details Metadata Chips")

# 3. Movie Details Buttons
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

replace_by_normalized_lines(old_details_buttons, new_details_buttons, "Details Buttons")

# 4. Volume Slider style
old_volume_slider = """                                     <Slider Minimum="0" Maximum="100"
                                             Value="{Binding PlayerVm.Volume}"
                                             Width="80" VerticalAlignment="Center"
                                             Foreground="{DynamicResource AccentBlue}"
                                             IsMoveToPointEnabled="True"
                                             ToolTip="Volume"/>"""

new_volume_slider = """                                     <Slider Minimum="0" Maximum="100" Style="{StaticResource PremiumSliderStyle}"
                                             Value="{Binding PlayerVm.Volume}"
                                             Width="80" VerticalAlignment="Center"
                                             Foreground="{DynamicResource AccentBlue}"
                                             IsMoveToPointEnabled="True"
                                             ToolTip="Volume"/>"""

replace_by_normalized_lines(old_volume_slider, new_volume_slider, "Volume Slider Style")

# Convert back to CRLF before writing
content = content.replace('\n', '\r\n')

with open(target_xaml_path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

print("Restyle part robust finished.")
