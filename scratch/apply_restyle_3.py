import os

target_xaml_path = r"IPXtream/Views/DashboardWindow.xaml"

with open(target_xaml_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Normalize file content to LF
content = content.replace('\r\n', '\n')

def replace_normalized(target_str, replacement_str, label):
    global content
    target_norm = target_str.replace('\r\n', '\n')
    replacement_norm = replacement_str.replace('\r\n', '\n')
    if target_norm in content:
        content = content.replace(target_norm, replacement_norm)
        print(f"Successfully replaced: {label}")
    else:
        print(f"Error: Could not find target for: {label}")

# 1. Hero Carousel Action Buttons
old_hero_buttons = """                                <!-- Buttons -->
                                <StackPanel Grid.Row="3" Orientation="Horizontal">
                                    <Button Command="{Binding PlayCommand}" CommandParameter="{Binding FeaturedCarouselItem}"
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

                                    <!-- Download button (Only visible if downloadable) -->
                                    <Button Command="{Binding DownloadCommand}" CommandParameter="{Binding FeaturedCarouselItem}"
                                            Visibility="{Binding FeaturedCarouselItem.IsDownloadable, Converter={StaticResource BoolToVisibilityConverter}}"
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

new_hero_buttons = """                                <!-- Buttons -->
                                <StackPanel Grid.Row="3" Orientation="Horizontal">
                                    <!-- Play Button -->
                                    <Button Command="{Binding PlayCommand}" CommandParameter="{Binding FeaturedCarouselItem}"
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

                                    <!-- Download button (Only visible if downloadable) -->
                                    <Button Command="{Binding DownloadCommand}" CommandParameter="{Binding FeaturedCarouselItem}"
                                            Visibility="{Binding FeaturedCarouselItem.IsDownloadable, Converter={StaticResource BoolToVisibilityConverter}}"
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

replace_normalized(old_hero_buttons, new_hero_buttons, "Hero Banner Buttons")

# 2. Movie Details Poster shadow
old_details_poster = """                             <!-- Movie Poster -->
                             <Border Grid.Column="0" CornerRadius="8" ClipToBounds="True" BorderBrush="#33FFFFFF" BorderThickness="1" Height="300" VerticalAlignment="Top" Margin="0,0,24,0">"""

new_details_poster = """                             <!-- Movie Poster -->
                             <Border Grid.Column="0" CornerRadius="12" ClipToBounds="True" BorderBrush="#15FFFFFF" BorderThickness="1" Height="320" VerticalAlignment="Top" Margin="0,0,24,0">
                                 <Border.Effect>
                                     <DropShadowEffect BlurRadius="15" Color="#000000" Opacity="0.4" ShadowDepth="4" Direction="270"/>
                                 </Border.Effect>"""

replace_normalized(old_details_poster, new_details_poster, "Details Poster Border & Shadow")

# 3. Movie Details metadata wrap panel styling
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

replace_normalized(old_details_metadata, new_details_metadata, "Details Metadata Chips")

# 4. Movie Details Buttons
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

replace_normalized(old_details_buttons, new_details_buttons, "Details Buttons")

# 5. Volume Slider style
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

replace_normalized(old_volume_slider, new_volume_slider, "Volume Slider Style")

# Convert back to CRLF before writing
content = content.replace('\n', '\r\n')

with open(target_xaml_path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

print("Restyle part 3 applied successfully.")
