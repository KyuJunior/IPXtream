$content = Get-Content -Path "Views/PlayerWindow.xaml" -Raw
$pattern = '(?s)<!-- ── Root: video \+ bars ───────────────────────────────────── -->.*?</Grid>\s*</Window>'

$replacement = @"
    <!-- ── Root: video + bars ───────────────────────────────────── -->
    <Grid x:Name="RootGrid" Background="Black">

        <!-- VLC Video Surface (spans the entire window) -->
        <vlc:VideoView x:Name="VideoView"
                       MediaPlayer="{Binding MediaPlayer}"
                       HorizontalAlignment="Stretch"
                       VerticalAlignment="Stretch"/>

        <!-- The Popup holds all WPF controls securely over the Win32 airspace -->
        <Popup x:Name="ControlsPopup"
               IsOpen="True"
               AllowsTransparency="True"
               PlacementTarget="{Binding ElementName=RootGrid}"
               Placement="Center"
               Width="{Binding ActualWidth, ElementName=RootGrid}"
               Height="{Binding ActualHeight, ElementName=RootGrid}">
               
            <!-- Background="Transparent" ensures we catch mouse moves perfectly over the video -->
            <Grid Background="Transparent" PreviewMouseMove="OnMouseMove">
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>

                <!-- ── Buffering spinner ──────────────────────────────────────────── -->
                <Border Grid.Row="1" HorizontalAlignment="Center" VerticalAlignment="Center"
                        Background="#99000000" CornerRadius="12"
                        Padding="24,16"
                        Visibility="{Binding IsBuffering,
                                     Converter={StaticResource BoolToVisibilityConverter}}">
                    <StackPanel Orientation="Horizontal">
                        <TextBlock Text="⏳" FontSize="22" Margin="0,0,10,0"
                                   VerticalAlignment="Center"/>
                        <TextBlock Text="{Binding StatusText}"
                                   Foreground="White" FontSize="15"
                                   VerticalAlignment="Center"/>
                    </StackPanel>
                </Border>

                <!-- ── Error message ──────────────────────────────────────────────── -->
                <Border Grid.Row="1" HorizontalAlignment="Center" VerticalAlignment="Center"
                        Background="#99200000" CornerRadius="12"
                        Padding="24,16"
                        Visibility="{Binding ErrorText,
                                     Converter={StaticResource StringNotEmptyToVisibilityConverter}}">
                    <StackPanel>
                        <TextBlock Text="⚠ Playback Error" FontSize="16" FontWeight="Bold"
                                   Foreground="{StaticResource ErrorRed}"
                                   HorizontalAlignment="Center" Margin="0,0,0,6"/>
                        <TextBlock Text="{Binding ErrorText}" Foreground="#FFDDDD"
                                   FontSize="13" TextWrapping="Wrap" MaxWidth="380"
                                   HorizontalAlignment="Center"/>
                    </StackPanel>
                </Border>

                <!-- Top bar: back button + stream name + icon -->
                <Border x:Name="TopBar" Grid.Row="0"
                        Opacity="0" IsHitTestVisible="False"
                        MouseEnter="ControlsOverlay_MouseEnter"
                        MouseLeave="ControlsOverlay_MouseLeave"
                        Background="#0F0F1A"
                        BorderBrush="#2E2E50" BorderThickness="0,0,0,1"
                        Padding="12,10">
                        <Grid>
                            <Grid.ColumnDefinitions>
                                <ColumnDefinition Width="Auto"/>
                                <ColumnDefinition Width="*"/>
                                <ColumnDefinition Width="Auto"/>
                            </Grid.ColumnDefinitions>

                            <!-- Back button -->
                            <Button Grid.Column="0"
                                    Style="{StaticResource CtrlBtn}"
                                    Command="{Binding BackCommand}"
                                    ToolTip="Back to Dashboard">
                                <TextBlock Text="←" FontSize="18"/>
                            </Button>

                            <!-- Channel name + icon -->
                            <StackPanel Grid.Column="1" Orientation="Horizontal"
                                        VerticalAlignment="Center" Margin="12,0">
                                <Image Source="{Binding StreamIconUrl}"
                                       Width="28" Height="28"
                                       RenderOptions.BitmapScalingMode="LowQuality"
                                       Margin="0,0,8,0">
                                    <Image.Style>
                                        <Style TargetType="Image">
                                            <Style.Triggers>
                                                <Trigger Property="Source" Value="{x:Null}">
                                                    <Setter Property="Visibility" Value="Collapsed"/>
                                                </Trigger>
                                            </Style.Triggers>
                                        </Style>
                                    </Image.Style>
                                </Image>
                                <TextBlock Text="{Binding StreamTitle}"
                                           Foreground="White" FontSize="16"
                                           FontWeight="SemiBold"
                                           VerticalAlignment="Center"
                                           TextTrimming="CharacterEllipsis"/>
                            </StackPanel>

                            <!-- Fullscreen toggle -->
                            <Button Grid.Column="2"
                                    Style="{StaticResource CtrlBtn}"
                                    Command="{Binding ToggleFullscreenCommand}"
                                    ToolTip="Toggle fullscreen (F)">
                                <TextBlock Text="⛶" FontSize="18"/>
                            </Button>
                        </Grid>
                </Border>

                <!-- Bottom bar: playback controls + volume -->
                <Border x:Name="BottomBar" Grid.Row="2"
                        Opacity="0" IsHitTestVisible="False"
                        MouseEnter="ControlsOverlay_MouseEnter"
                        MouseLeave="ControlsOverlay_MouseLeave"
                        Background="#0F0F1A"
                        BorderBrush="#2E2E50" BorderThickness="0,1,0,0"
                        Padding="16,12">
                        <Grid>
                            <Grid.RowDefinitions>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto"/>
                            </Grid.RowDefinitions>

                            <!-- ── SEEKBAR (Row 0) ─────────────────────────────────── -->
                            <Grid Grid.Row="0" Margin="0,0,0,12" 
                                  Visibility="{Binding IsSeekable, Converter={StaticResource BoolToVisibilityConverter}}">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="Auto"/>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="Auto"/>
                                </Grid.ColumnDefinitions>

                                <TextBlock Grid.Column="0" Text="{Binding PositionText}" 
                                           Foreground="White" FontSize="12" Margin="0,0,12,0" VerticalAlignment="Center"/>
                                
                                <Slider Grid.Column="1" Minimum="0" Maximum="1" Value="{Binding Position}"
                                        SmallChange="0.01" LargeChange="0.1"
                                        Thumb.DragStarted="SeekSlider_DragStarted"
                                        Thumb.DragCompleted="SeekSlider_DragCompleted"
                                        VerticalAlignment="Center" Foreground="{StaticResource AccentBlue}"/>
                                
                                <TextBlock Grid.Column="2" Text="{Binding LengthText}" 
                                           Foreground="{StaticResource TextMuted}" FontSize="12" Margin="12,0,0,0" VerticalAlignment="Center"/>
                            </Grid>

                            <!-- ── CONTROLS (Row 1) ────────────────────────────────── -->
                            <Grid Grid.Row="1">
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*"/>
                                    <ColumnDefinition Width="Auto"/>
                                </Grid.ColumnDefinitions>

                            <!-- Left: Play/Pause · Stop -->
                            <StackPanel Grid.Column="0" Orientation="Horizontal"
                                        VerticalAlignment="Center">
                                <!-- Play / Pause -->
                                <Button Style="{StaticResource CtrlBtn}"
                                        Command="{Binding TogglePlayCommand}"
                                        Margin="0,0,8,0"
                                        ToolTip="Play / Pause (Space)">
                                    <TextBlock FontSize="18">
                                        <TextBlock.Style>
                                            <Style TargetType="TextBlock">
                                                <Setter Property="Text" Value="▶"/>
                                                <Style.Triggers>
                                                    <DataTrigger Binding="{Binding IsPlaying}"
                                                                 Value="True">
                                                        <Setter Property="Text" Value="⏸"/>
                                                    </DataTrigger>
                                                </Style.Triggers>
                                            </Style>
                                        </TextBlock.Style>
                                    </TextBlock>
                                </Button>

                                <!-- Stop -->
                                <Button Style="{StaticResource CtrlBtn}"
                                        Command="{Binding StopCommand}"
                                        Margin="0,0,16,0"
                                        ToolTip="Stop">
                                    <TextBlock Text="⏹" FontSize="18"/>
                                </Button>

                                <!-- Status label (e.g. "Paused") -->
                                <TextBlock Text="{Binding StatusText}"
                                           Foreground="{StaticResource TextMuted}"
                                           FontSize="13"
                                           VerticalAlignment="Center"
                                           Visibility="{Binding StatusText,
                                                        Converter={StaticResource StringNotEmptyToVisibilityConverter}}"/>
                            </StackPanel>

                            <!-- Right: Mute · Volume slider -->
                            <StackPanel Grid.Column="1" Orientation="Horizontal"
                                        VerticalAlignment="Center">
                                <Button Style="{StaticResource CtrlBtn}"
                                        Command="{Binding ToggleMuteCommand}"
                                        Margin="0,0,8,0"
                                        ToolTip="Toggle mute (M)">
                                    <TextBlock Text="🔊" FontSize="16"/>
                                </Button>
                                <Slider Style="{StaticResource VolumeSlider}"
                                        Minimum="0" Maximum="100"
                                        Value="{Binding Volume}"
                                        ToolTip="Volume"/>
                                <TextBlock Text="{Binding Volume, StringFormat='{}{0}%'}"
                                           Foreground="{StaticResource TextMuted}"
                                           Margin="6,0,16,0"/>
                                
                                <!-- Tracks & Subtitles -->
                                <Border Background="#1A1A2E" CornerRadius="6" Padding="6,2">
                                    <StackPanel Orientation="Horizontal">
                                        <TextBlock Text="DUB:" Foreground="{StaticResource TextMuted}" FontSize="10" VerticalAlignment="Center" Margin="0,0,4,0"/>
                                        <ComboBox Width="100" Margin="0,0,12,0" Foreground="Black"
                                                  ItemsSource="{Binding AudioTracks}" DisplayMemberPath="Name" 
                                                  SelectedItem="{Binding SelectedAudioTrack}">
                                            <ComboBox.ItemContainerStyle>
                                                <Style TargetType="ComboBoxItem">
                                                    <Setter Property="Foreground" Value="Black"/>
                                                </Style>
                                            </ComboBox.ItemContainerStyle>
                                        </ComboBox>

                                        <TextBlock Text="SUB:" Foreground="{StaticResource TextMuted}" FontSize="10" VerticalAlignment="Center" Margin="0,0,4,0"/>
                                        <ComboBox Width="100" Foreground="Black"
                                                  ItemsSource="{Binding SubtitleTracks}" DisplayMemberPath="Name" 
                                                  SelectedItem="{Binding SelectedSubtitleTrack}">
                                            <ComboBox.ItemContainerStyle>
                                                <Style TargetType="ComboBoxItem">
                                                    <Setter Property="Foreground" Value="Black"/>
                                                </Style>
                                            </ComboBox.ItemContainerStyle>
                                        </ComboBox>
                                    </StackPanel>
                                </Border>
                            </StackPanel>
                        </Grid>
                    </Grid>
                </Border>
            </Grid>
        </Popup>
    </Grid>
</Window>
"@

$newContent = [regex]::Replace($content, $pattern, $replacement)
Set-Content -Path "Views/PlayerWindow.xaml" -Value $newContent -Encoding UTF8
