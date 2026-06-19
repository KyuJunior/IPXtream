import os

target_xaml_path = r"IPXtream/Views/DashboardWindow.xaml"

with open(target_xaml_path, 'r', encoding='utf-8') as f:
    content = f.read()

# 1. Hero Carousel Card & Background
old_hero_border = """                <!-- 16:9 Hero Banner (Auto-rotating What's New Carousel) -->
                <Border x:Name="HeroBanner" Grid.Row="3" Height="280" CornerRadius="10" Margin="16,0,16,10" ClipToBounds="True" Background="#121225">"""

new_hero_border = """                <!-- 16:9 Hero Banner (Auto-rotating What's New Carousel) -->
                <Border x:Name="HeroBanner" Grid.Row="3" Height="300" CornerRadius="16" Margin="16,0,16,14" ClipToBounds="True" Background="#121225">"""

if old_hero_border in content:
    content = content.replace(old_hero_border, new_hero_border)
    print("Upgraded Hero Banner card dimensions and corners.")
else:
    print("Warning: Hero banner border not matched exactly.")

old_hero_bg = """                        <!-- Overlay Dark Grid for Premium Contrast -->
                        <Border Background="#990F0F1D"/>"""

new_hero_bg = """                        <!-- Overlay Dark Grid for Premium Contrast -->
                        <Border>
                            <Border.Background>
                                <LinearGradientBrush StartPoint="0,0" EndPoint="0,1">
                                    <GradientStop Color="#250A0A14" Offset="0"/>
                                    <GradientStop Color="#AA0A0A14" Offset="0.6"/>
                                    <GradientStop Color="#F50A0A14" Offset="1"/>
                                </LinearGradientBrush>
                            </Border.Background>
                        </Border>"""

if old_hero_bg.replace('\r\n', '\n') in content.replace('\r\n', '\n'):
    content = content.replace(old_hero_bg, new_hero_bg)
    print("Upgraded Hero Banner background to cinematic gradient scrim.")
else:
    print("Warning: Hero background not matched exactly.")

# 2. Hero Carousel Poster Border
old_hero_poster = """                            <!-- Highlighted Poster -->
                            <Border Grid.Column="0" Width="150" CornerRadius="6" ClipToBounds="True" Background="#0A0A16"
                                    BorderBrush="#33FFFFFF" BorderThickness="1" Margin="0,0,24,0">"""

new_hero_poster = """                            <!-- Highlighted Poster -->
                            <Border Grid.Column="0" Width="150" CornerRadius="12" ClipToBounds="True" Background="#0A0A16"
                                    BorderBrush="#25FFFFFF" BorderThickness="1" Margin="0,0,24,0">
                                <Border.Effect>
                                    <DropShadowEffect BlurRadius="15" Color="#000000" Opacity="0.5" ShadowDepth="4" Direction="270"/>
                                </Border.Effect>"""

if old_hero_poster.replace('\r\n', '\n') in content.replace('\r\n', '\n'):
    content = content.replace(old_hero_poster, new_hero_poster)
    print("Added drop shadow to Hero Banner poster.")
else:
    print("Warning: Hero poster not matched exactly.")

# 3. Hero Carousel Title Text
old_hero_title = """                                <!-- Title -->
                                <TextBlock Grid.Row="1" Text="{Binding FeaturedCarouselItem.Name}" Foreground="White" FontSize="24" FontWeight="Bold" TextWrapping="Wrap" Margin="0,0,0,10"/>"""

new_hero_title = """                                <!-- Title -->
                                <TextBlock Grid.Row="1" Text="{Binding FeaturedCarouselItem.Name}" Foreground="White" FontSize="28" FontWeight="Bold" TextWrapping="Wrap" Margin="0,0,0,10">
                                    <TextBlock.Effect>
                                        <DropShadowEffect BlurRadius="8" Color="Black" Opacity="0.6" ShadowDepth="2" Direction="270"/>
                                    </TextBlock.Effect>
                                </TextBlock>"""

if old_hero_title.replace('\r\n', '\n') in content.replace('\r\n', '\n'):
    content = content.replace(old_hero_title, new_hero_title)
    print("Upgraded Hero Banner title typography and added text shadow.")
else:
    print("Warning: Hero title not matched exactly.")

# 4. Hero Carousel Tag and Chips
old_hero_tag = """                                <!-- Tag -->
                                <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,8">
                                    <Border Background="#FFD700" CornerRadius="4" Padding="6,2" Margin="0,0,8,0">
                                        <StackPanel Orientation="Horizontal">
                                            <Path Data="{StaticResource IconStar}" Fill="#0F0F1A" Width="9" Height="9" Stretch="Uniform" VerticalAlignment="Center" Margin="0,0,4,0"/>
                                            <TextBlock Text="WHAT'S NEW" Foreground="#0F0F1A" FontSize="10" FontWeight="Black" VerticalAlignment="Center"/>
                                        </StackPanel>
                                    </Border>
                                    <TextBlock Text="{Binding FeaturedCarouselItem.ReleaseDate}" Foreground="{DynamicResource TextMuted}" FontSize="12" VerticalAlignment="Center" Margin="0,0,12,0"/>
                                    <Path Data="{StaticResource IconStar}" Fill="#FFD700" Width="10" Height="10" Stretch="Uniform" VerticalAlignment="Center" Margin="0,0,6,0"/>
                                    <TextBlock Text="{Binding FeaturedCarouselItem.Rating}" Foreground="White" FontSize="12" FontWeight="SemiBold" VerticalAlignment="Center"/>
                                </StackPanel>"""

new_hero_tag = """                                <!-- Tag -->
                                <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="0,0,0,8">
                                    <Border CornerRadius="8" Padding="8,3" Margin="0,0,8,0">
                                        <Border.Background>
                                            <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
                                                <GradientStop Color="#5B9BFF" Offset="0"/>
                                                <GradientStop Color="#8B6BD4" Offset="1"/>
                                            </LinearGradientBrush>
                                        </Border.Background>
                                        <StackPanel Orientation="Horizontal">
                                            <Path Data="{StaticResource IconNew}" Fill="White" Width="10" Height="10" Stretch="Uniform" VerticalAlignment="Center" Margin="0,0,4,0"/>
                                            <TextBlock Text="WHAT'S NEW" Foreground="White" FontSize="10" FontWeight="Black" VerticalAlignment="Center"/>
                                        </StackPanel>
                                    </Border>
                                    <Border Background="#15FFFFFF" BorderBrush="#15FFFFFF" BorderThickness="1" CornerRadius="8" Padding="8,3" Margin="0,0,8,0">
                                        <TextBlock Text="{Binding FeaturedCarouselItem.ReleaseDate}" Foreground="White" FontSize="11" FontWeight="SemiBold" VerticalAlignment="Center"/>
                                    </Border>
                                    <Border Background="#15FFFFFF" BorderBrush="#15FFFFFF" BorderThickness="1" CornerRadius="8" Padding="8,3" Margin="0,0,8,0">
                                        <StackPanel Orientation="Horizontal">
                                            <Path Data="{StaticResource IconStar}" Fill="#FFD700" Width="10" Height="10" Stretch="Uniform" VerticalAlignment="Center" Margin="0,0,4,0"/>
                                            <TextBlock Text="{Binding FeaturedCarouselItem.Rating}" Foreground="White" FontSize="11" FontWeight="SemiBold" VerticalAlignment="Center"/>
                                        </StackPanel>
                                    </Border>
                                </StackPanel>"""

if old_hero_tag.replace('\r\n', '\n') in content.replace('\r\n', '\n'):
    content = content.replace(old_hero_tag, new_hero_tag)
    print("Upgraded Hero tag and metadata into glassmorphic pills.")
else:
    print("Warning: Hero tag not matched exactly.")

# 5. Hero Carousel Action Buttons
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
                                                        <Trigger Property="IsMouseOver" Value="True">
                                                            <Setter TargetName="DlBd" Property="Background" Value="#3D3D52"/>
                                                        </Trigger>
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

if old_hero_buttons.replace('\r\n', '\n') in content.replace('\r\n', '\n'):
    content = content.replace(old_hero_buttons, new_hero_buttons)
    print("Styled Hero Banner action buttons.")
else:
    print("Warning: Hero action buttons not matched exactly.")

# 6. Carousel indicator selected color
old_dot_trigger = """                                                    <DataTrigger Binding="{Binding IsSelectedCarousel}" Value="True">
                                                        <Setter TargetName="Dot" Property="Background" Value="#4F8EF7"/>
                                                        <Setter TargetName="Dot" Property="Width" Value="16"/>
                                                    </DataTrigger>"""

new_dot_trigger = """                                                    <DataTrigger Binding="{Binding IsSelectedCarousel}" Value="True">
                                                        <Setter TargetName="Dot" Property="Background" Value="#5B9BFF"/>
                                                        <Setter TargetName="Dot" Property="Width" Value="16"/>
                                                    </DataTrigger>"""

if old_dot_trigger.replace('\r\n', '\n') in content.replace('\r\n', '\n'):
    content = content.replace(old_dot_trigger, new_dot_trigger)
    print("Updated carousel dots active indicator color.")
else:
    print("Warning: Carousel dots not matched exactly.")

# 7. Movie Details Card dimensional polish and drop shadow
old_details_border = """                <!-- Details Card -->
                <Border Width="650" Height="420" CornerRadius="12" Background="#121225" BorderBrush="#33FFFFFF" BorderThickness="1"
                        HorizontalAlignment="Center" VerticalAlignment="Center" Padding="24" ClipToBounds="True">"""

new_details_border = """                <!-- Details Card -->
                <Border Width="700" Height="460" CornerRadius="16" Background="#10101F" BorderBrush="#15FFFFFF" BorderThickness="1"
                        HorizontalAlignment="Center" VerticalAlignment="Center" Padding="28" ClipToBounds="True">
                    <Border.Effect>
                        <DropShadowEffect BlurRadius="40" Color="#000000" Opacity="0.6" ShadowDepth="0"/>
                    </Border.Effect>"""

if old_details_border.replace('\r\n', '\n') in content.replace('\r\n', '\n'):
    content = content.replace(old_details_border, new_details_border)
    print("Upgraded details modal dimensions, corners and shadow.")
else:
    print("Warning: Details card border not matched exactly.")

# 8. Movie Details Poster shadow
old_details_poster = """                             <!-- Movie Poster -->
                             <Border Grid.Column="0" CornerRadius="8" ClipToBounds="True" BorderBrush="#33FFFFFF" BorderThickness="1" Height="300" VerticalAlignment="Top" Margin="0,0,24,0">"""

new_details_poster = """                             <!-- Movie Poster -->
                             <Border Grid.Column="0" CornerRadius="12" ClipToBounds="True" BorderBrush="#15FFFFFF" BorderThickness="1" Height="320" VerticalAlignment="Top" Margin="0,0,24,0">
                                 <Border.Effect>
                                     <DropShadowEffect BlurRadius="15" Color="#000000" Opacity="0.4" ShadowDepth="4" Direction="270"/>
                                 </Border.Effect>"""

if old_details_poster.replace('\r\n', '\n') in content.replace('\r\n', '\n'):
    content = content.replace(old_details_poster, new_details_poster)
    print("Upgraded details poster border and drop shadow.")
else:
    print("Warning: Details poster not matched exactly.")

# 9. Movie Details metadata wrap panel styling
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

if old_details_metadata.replace('\r\n', '\n') in content.replace('\r\n', '\n'):
    content = content.replace(old_details_metadata, new_details_metadata)
    print("Upgraded details modal metadata chips.")
else:
    print("Warning: Details metadata not matched exactly.")

# 10. Movie Details buttons
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

if old_details_buttons.replace('\r\n', '\n') in content.replace('\r\n', '\n'):
    content = content.replace(old_details_buttons, new_details_buttons)
    print("Styled details modal action buttons.")
else:
    print("Warning: Details buttons not matched exactly.")

# 11. Sliders Style (seek & volume)
old_seek_slider = """                                <Slider Grid.Column="1"
                                        Minimum="0" Maximum="1" Value="{Binding PlayerVm.Position, Mode=OneWay}"
                                        SmallChange="0.001" LargeChange="0.05"
                                        IsMoveToPointEnabled="True"
                                        Thumb.DragStarted="SeekSlider_DragStarted"
                                        Thumb.DragCompleted="SeekSlider_DragCompleted"
                                        VerticalAlignment="Center"
                                        Foreground="{DynamicResource AccentBlue}"/>"""

new_seek_slider = """                                <Slider Grid.Column="1" Style="{StaticResource PremiumSliderStyle}"
                                        Minimum="0" Maximum="1" Value="{Binding PlayerVm.Position, Mode=OneWay}"
                                        SmallChange="0.001" LargeChange="0.05"
                                        IsMoveToPointEnabled="True"
                                        Thumb.DragStarted="SeekSlider_DragStarted"
                                        Thumb.DragCompleted="SeekSlider_DragCompleted"
                                        VerticalAlignment="Center"
                                        Foreground="{DynamicResource AccentBlue}"/>"""

if old_seek_slider.replace('\r\n', '\n') in content.replace('\r\n', '\n'):
    content = content.replace(old_seek_slider, new_seek_slider)
    print("Applied PremiumSliderStyle to Seek slider.")
else:
    print("Warning: Seek slider not matched exactly.")

old_volume_slider = """                                    <Slider Minimum="0" Maximum="100"
                                            Value="{Binding PlayerVm.Volume}"
                                            Width="80" VerticalAlignment="Center"
                                            Foreground="{DynamicResource AccentBlue}"
                                            ToolTip="{Binding PlayerVm.Volume, StringFormat='{}{0}%'}"/>"""

new_volume_slider = """                                    <Slider Minimum="0" Maximum="100" Style="{StaticResource PremiumSliderStyle}"
                                            Value="{Binding PlayerVm.Volume}"
                                            Width="80" VerticalAlignment="Center"
                                            Foreground="{DynamicResource AccentBlue}"
                                            ToolTip="{Binding PlayerVm.Volume, StringFormat='{}{0}%'}"/>"""

if old_volume_slider.replace('\r\n', '\n') in content.replace('\r\n', '\n'):
    content = content.replace(old_volume_slider, new_volume_slider)
    print("Applied PremiumSliderStyle to Volume slider.")
else:
    print("Warning: Volume slider not matched exactly.")

# 12. Downloads progress bar gradient fill
old_progress_bar = """                                                <!-- Custom progress bar (Grid-based, avoids Win theme issues) -->
                                                <Grid Grid.Row="1" Grid.ColumnSpan="2"
                                                      x:Name="ProgressTrack"
                                                      Height="4" Margin="0,5,0,0">
                                                    <Border CornerRadius="2" Background="#1A1A2E"/>
                                                    <Border CornerRadius="2" Background="#4F8EF7"
                                                            HorizontalAlignment="Left">
                                                        <Border.Width>
                                                            <MultiBinding Converter="{StaticResource ProgressToWidthMultiConverter}">
                                                                <Binding Path="Progress"/>
                                                                <Binding ElementName="ProgressTrack" Path="ActualWidth"/>
                                                            </MultiBinding>
                                                        </Border.Width>
                                                    </Border>
                                                </Grid>"""

new_progress_bar = """                                                <!-- Custom progress bar (Grid-based, avoids Win theme issues) -->
                                                <Grid Grid.Row="1" Grid.ColumnSpan="2"
                                                      x:Name="ProgressTrack"
                                                      Height="5" Margin="0,6,0,0">
                                                    <Border CornerRadius="2.5" Background="#16162C"/>
                                                    <Border CornerRadius="2.5" HorizontalAlignment="Left">
                                                        <Border.Background>
                                                            <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
                                                                <GradientStop Color="#5B9BFF" Offset="0"/>
                                                                <GradientStop Color="#8B6BD4" Offset="1"/>
                                                            </LinearGradientBrush>
                                                        </Border.Background>
                                                        <Border.Width>
                                                            <MultiBinding Converter="{StaticResource ProgressToWidthMultiConverter}">
                                                                <Binding Path="Progress"/>
                                                                <Binding ElementName="ProgressTrack" Path="ActualWidth"/>
                                                            </MultiBinding>
                                                        </Border.Width>
                                                    </Border>
                                                </Grid>"""

if old_progress_bar.replace('\r\n', '\n') in content.replace('\r\n', '\n'):
    content = content.replace(old_progress_bar, new_progress_bar)
    print("Applied gradient brush to Downloads progress bar.")
else:
    print("Warning: Downloads progress bar not matched exactly.")

# 13. Settings modal card border brush
old_settings_card = """            <!-- Centered Glassy Card -->
            <Border Width="850" Height="600"
                    Background="{DynamicResource BgDeep}"
                    BorderBrush="{DynamicResource DividerColor}"
                    BorderThickness="1"
                    CornerRadius="16"
                    HorizontalAlignment="Center"
                    VerticalAlignment="Center">"""

new_settings_card = """            <!-- Centered Glassy Card -->
            <Border Width="850" Height="600"
                    Background="{DynamicResource BgDeep}"
                    BorderBrush="#15FFFFFF"
                    BorderThickness="1"
                    CornerRadius="16"
                    HorizontalAlignment="Center"
                    VerticalAlignment="Center">"""

if old_settings_card.replace('\r\n', '\n') in content.replace('\r\n', '\n'):
    content = content.replace(old_settings_card, new_settings_card)
    print("Polished Settings modal card border.")
else:
    print("Warning: Settings card border not matched exactly.")

# 14. Settings Save button gradient & drop shadow
old_save_settings = """                            <!-- Save / Apply Settings Button -->
                            <Button Content="Save Settings"
                                    Command="{Binding ApplyGeneralSettingsCommand}"
                                    Cursor="Hand" Height="44" FontSize="14" FontWeight="Bold" Foreground="White" Margin="0,16,0,0">
                                <Button.Style>
                                    <Style TargetType="Button">
                                        <Setter Property="Template">
                                            <Setter.Value>
                                                <ControlTemplate TargetType="Button">
                                                    <Border x:Name="Bd" CornerRadius="8">
                                                        <Border.Background>
                                                            <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
                                                                <GradientStop Color="#4F8EF7" Offset="0"/>
                                                                <GradientStop Color="#7C5CBF" Offset="1"/>
                                                            </LinearGradientBrush>
                                                        </Border.Background>
                                                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                                                    </Border>
                                                    <ControlTemplate.Triggers>
                                                        <Trigger Property="IsMouseOver" Value="True">
                                                            <Setter TargetName="Bd" Property="Opacity" Value="0.85"/>
                                                        </Trigger>
                                                    </ControlTemplate.Triggers>
                                                </ControlTemplate>
                                            </Setter.Value>
                                        </Setter>
                                    </Style>
                                </Button.Style>
                            </Button>"""

new_save_settings = """                            <!-- Save / Apply Settings Button -->
                            <Button Content="Save Settings"
                                    Command="{Binding ApplyGeneralSettingsCommand}"
                                    Cursor="Hand" Height="44" FontSize="14" FontWeight="Bold" Foreground="White" Margin="0,16,0,0">
                                <Button.Style>
                                    <Style TargetType="Button">
                                        <Setter Property="Template">
                                            <Setter.Value>
                                                <ControlTemplate TargetType="Button">
                                                    <Border x:Name="Bd" CornerRadius="10" RenderTransformOrigin="0.5,0.5">
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
                                                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                                                    </Border>
                                                    <ControlTemplate.Triggers>
                                                        <Trigger Property="IsMouseOver" Value="True">
                                                            <Setter TargetName="Bd" Property="Opacity" Value="0.95"/>
                                                            <Setter TargetName="Bd" Property="RenderTransform">
                                                                <Setter.Value>
                                                                    <ScaleTransform ScaleX="1.02" ScaleY="1.02"/>
                                                                </Setter.Value>
                                                            </Setter>
                                                        </Trigger>
                                                    </ControlTemplate.Triggers>
                                                </ControlTemplate>
                                            </Setter.Value>
                                        </Setter>
                                    </Style>
                                </Button.Style>
                            </Button>"""

if old_save_settings.replace('\r\n', '\n') in content.replace('\r\n', '\n'):
    content = content.replace(old_save_settings, new_save_settings)
    print("Upgraded Settings Save Settings button to gradient with drop shadow.")
else:
    print("Warning: Save settings button not matched exactly.")

with open(target_xaml_path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

print("Restyle part 2 applied successfully.")
