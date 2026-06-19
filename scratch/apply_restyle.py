import os
import re

target_xaml_path = r"IPXtream/Views/DashboardWindow.xaml"

with open(target_xaml_path, 'r', encoding='utf-8') as f:
    content = f.read()

# 1. Add PremiumSliderStyle and thin ScrollBar style to Window.Resources
# We can search for the end of Window.Resources </Window.Resources> and insert before it.

slider_and_scrollbar_styles = """
        <!-- ── Thin ScrollBar Style ── -->
        <Style TargetType="ScrollBar">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="BorderThickness" Value="0"/>
            <Setter Property="Width" Value="8"/>
            <Setter Property="Height" Value="8"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="ScrollBar">
                        <Grid x:Name="Bg" SnapsToDevicePixels="true" Background="Transparent">
                            <Track x:Name="PART_Track" IsDirectionReversed="true">
                                <Track.Thumb>
                                    <Thumb x:Name="Thumb" Cursor="Hand">
                                        <Thumb.Template>
                                            <ControlTemplate TargetType="Thumb">
                                                <Border x:Name="ThumbBd" CornerRadius="4" Background="#35FFFFFF" Margin="1"/>
                                                <ControlTemplate.Triggers>
                                                    <Trigger Property="IsMouseOver" Value="True">
                                                        <Setter TargetName="ThumbBd" Property="Background" Value="#65FFFFFF"/>
                                                    </Trigger>
                                                </ControlTemplate.Triggers>
                                            </ControlTemplate>
                                        </Thumb.Template>
                                    </Thumb>
                                </Track.Thumb>
                            </Track>
                        </Grid>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- ── Custom Premium Slider Style ── -->
        <Style x:Key="PremiumSliderStyle" TargetType="Slider">
            <Setter Property="Stylus.IsPressAndHoldEnabled" Value="false"/>
            <Setter Property="Background" Value="#25FFFFFF"/>
            <Setter Property="Foreground" Value="{DynamicResource AccentBlue}"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Slider">
                        <Grid x:Name="GridRoot">
                            <Grid.RowDefinitions>
                                <RowDefinition Height="Auto"/>
                                <RowDefinition Height="Auto" MinHeight="{TemplateBinding MinHeight}"/>
                                <RowDefinition Height="Auto"/>
                            </Grid.RowDefinitions>
                            <!-- Track Background -->
                            <Border x:Name="TrackBackground" Grid.Row="1" Height="4" CornerRadius="2" Background="{TemplateBinding Background}" VerticalAlignment="Center"/>
                            <Track x:Name="PART_Track" Grid.Row="1">
                                <Track.DecreaseRepeatButton>
                                    <RepeatButton Command="Slider.DecreaseLarge" Background="{TemplateBinding Foreground}">
                                        <RepeatButton.Template>
                                            <ControlTemplate TargetType="RepeatButton">
                                                <Border Height="4" CornerRadius="2,0,0,2" Background="{TemplateBinding Background}"/>
                                            </ControlTemplate>
                                        </RepeatButton.Template>
                                    </RepeatButton>
                                </Track.DecreaseRepeatButton>
                                <Track.IncreaseRepeatButton>
                                    <RepeatButton Command="Slider.IncreaseLarge" Background="Transparent">
                                        <RepeatButton.Template>
                                            <ControlTemplate TargetType="RepeatButton">
                                                <Border Height="4" Background="Transparent"/>
                                            </ControlTemplate>
                                        </RepeatButton.Template>
                                    </RepeatButton>
                                </Track.IncreaseRepeatButton>
                                <Track.Thumb>
                                    <Thumb x:Name="Thumb" Width="12" Height="12" Cursor="Hand">
                                        <Thumb.Template>
                                            <ControlTemplate TargetType="Thumb">
                                                <Border CornerRadius="6" Background="White" Width="12" Height="12" BorderBrush="{DynamicResource AccentBlue}" BorderThickness="1.5">
                                                    <Border.Effect>
                                                        <DropShadowEffect BlurRadius="4" Color="Black" Opacity="0.3" ShadowDepth="1"/>
                                                    </Border.Effect>
                                                </Border>
                                            </ControlTemplate>
                                        </Thumb.Template>
                                    </Thumb>
                                </Track.Thumb>
                            </Track>
                        </Grid>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="TrackBackground" Property="Height" Value="6"/>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
"""

if "PremiumSliderStyle" not in content:
    content = content.replace("    </Window.Resources>", slider_and_scrollbar_styles + "\n    </Window.Resources>")
    print("Added ScrollBar and PremiumSliderStyle to resources.")

# 2. Sidebar Logo Polish
old_logo = """                    <!-- Logo -->
                    <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="20,24,20,20">
                        <Border Width="36" Height="36" CornerRadius="10">
                            <Border.Background>
                                <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                                    <GradientStop Color="#4F8EF7" Offset="0"/>
                                    <GradientStop Color="#7C5CBF" Offset="1"/>
                                </LinearGradientBrush>
                            </Border.Background>
                            <Path Data="{StaticResource IconLogo}" Fill="White" Stretch="Uniform" Width="18" Height="18" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <TextBlock Text="IPXtream" FontSize="18" FontWeight="Bold"
                                   Foreground="{DynamicResource TextPrimary}"
                                   VerticalAlignment="Center" Margin="10,0,0,0"/>
                    </StackPanel>"""

new_logo = """                    <!-- Logo -->
                    <StackPanel Grid.Row="0" Orientation="Horizontal" Margin="20,24,20,20">
                        <Border Width="38" Height="38" CornerRadius="12">
                            <Border.Background>
                                <LinearGradientBrush StartPoint="0,0" EndPoint="1,1">
                                    <GradientStop Color="#5B9BFF" Offset="0"/>
                                    <GradientStop Color="#8B6BD4" Offset="1"/>
                                </LinearGradientBrush>
                            </Border.Background>
                            <Border.Effect>
                                <DropShadowEffect BlurRadius="15" Color="#5B9BFF" Opacity="0.4" ShadowDepth="0"/>
                            </Border.Effect>
                            <Path Data="{StaticResource IconLogo}" Fill="White" Stretch="Uniform" Width="20" Height="20" HorizontalAlignment="Center" VerticalAlignment="Center"/>
                        </Border>
                        <TextBlock Text="IPXtream" FontSize="20" FontWeight="Bold"
                                   Foreground="{DynamicResource TextPrimary}"
                                   VerticalAlignment="Center" Margin="12,0,0,0">
                            <TextBlock.Effect>
                                <DropShadowEffect BlurRadius="10" Color="#5B9BFF" Opacity="0.15" ShadowDepth="0"/>
                            </TextBlock.Effect>
                        </TextBlock>
                    </StackPanel>"""

if old_logo.replace('\r\n', '\n') in content.replace('\r\n', '\n'):
    content = content.replace(old_logo, new_logo)
    print("Polished Logo in Sidebar.")
else:
    print("Warning: Logo pattern not matched exactly.")

# 3. Sidebar Divider Gradient
old_divider = """                        <Border Height="1" Background="{DynamicResource DividerColor}" Margin="16,16"/>"""
new_divider = """                        <Border Height="1" Margin="16,16">
                            <Border.Background>
                                <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
                                    <GradientStop Color="Transparent" Offset="0"/>
                                    <GradientStop Color="{DynamicResource DividerColor}" Offset="0.5"/>
                                    <GradientStop Color="Transparent" Offset="1"/>
                                </LinearGradientBrush>
                            </Border.Background>
                        </Border>"""

if old_divider in content:
    content = content.replace(old_divider, new_divider)
    print("Updated sidebar divider to gradient.")
else:
    print("Warning: Sidebar divider not matched exactly.")

# 4. Logout Button Red Hover Tint
old_logout = """                        <Button Style="{StaticResource NavBtn}" Command="{Binding LogoutCommand}" Margin="0,4,0,0">
                            <StackPanel Orientation="Horizontal">
                                <Path Data="{StaticResource IconLogout}" Style="{StaticResource VectorIcon}" Width="16" Height="16" Margin="0,0,10,0"/>
                                <TextBlock Text="Logout" VerticalAlignment="Center"
                                           Foreground="{StaticResource ErrorRed}"/>
                            </StackPanel>
                        </Button>"""

new_logout = """                        <Button Command="{Binding LogoutCommand}" Margin="0,4,0,0">
                            <Button.Style>
                                <Style TargetType="Button" BasedOn="{StaticResource NavBtn}">
                                    <Setter Property="Foreground" Value="{StaticResource ErrorRed}"/>
                                    <Setter Property="Template">
                                        <Setter.Value>
                                            <ControlTemplate TargetType="Button">
                                                <Border x:Name="Bd" Background="Transparent" CornerRadius="10" Margin="6,2">
                                                    <ContentPresenter Margin="{TemplateBinding Padding}" VerticalAlignment="Center"/>
                                                </Border>
                                                <ControlTemplate.Triggers>
                                                    <Trigger Property="IsMouseOver" Value="True">
                                                        <Setter TargetName="Bd" Property="Background" Value="#20FF5370"/>
                                                        <Setter Property="Foreground" Value="#FF5370"/>
                                                    </Trigger>
                                                </ControlTemplate.Triggers>
                                            </ControlTemplate>
                                        </Setter.Value>
                                    </Setter>
                                </Style>
                            </Button.Style>
                            <StackPanel Orientation="Horizontal">
                                <Path Data="{StaticResource IconLogout}" Style="{StaticResource VectorIcon}" Width="16" Height="16" Margin="0,0,10,0"/>
                                <TextBlock Text="Logout" VerticalAlignment="Center"/>
                            </StackPanel>
                        </Button>"""

if old_logout.replace('\r\n', '\n') in content.replace('\r\n', '\n'):
    content = content.replace(old_logout, new_logout)
    print("Logout button styled with red hover tint.")
else:
    print("Warning: Logout pattern not matched exactly.")

# 5. User Footer Glass-Card Background
old_footer = """                    <!-- User footer -->
                    <Border Grid.Row="2" Background="{DynamicResource BgCard}"
                            CornerRadius="12" Margin="12,0,12,16" Padding="12,10">"""

new_footer = """                    <!-- User footer -->
                    <Border Grid.Row="2" Background="#141426"
                            BorderBrush="#15FFFFFF" BorderThickness="1"
                            CornerRadius="16" Margin="12,0,12,16" Padding="12,10">
                        <Border.Effect>
                            <DropShadowEffect BlurRadius="15" Color="#000000" Opacity="0.3" ShadowDepth="2" Direction="270"/>
                        </Border.Effect>"""

if old_footer.replace('\r\n', '\n') in content.replace('\r\n', '\n'):
    content = content.replace(old_footer, new_footer)
    print("User footer background upgraded to glass-card.")
else:
    print("Warning: User footer not matched exactly.")

# 6. Header Bar Separator
old_toolbar = """                <!-- Toolbar -->
                <Border Grid.Row="0" Padding="16,14">"""
new_toolbar = """                <!-- Toolbar -->
                <Border Grid.Row="0" Padding="16,14" BorderBrush="{DynamicResource DividerColor}" BorderThickness="0,0,0,1">"""

if old_toolbar.replace('\r\n', '\n') in content.replace('\r\n', '\n'):
    content = content.replace(old_toolbar, new_toolbar)
    print("Added header bar bottom separator.")
else:
    print("Warning: Toolbar not matched exactly.")

# 7. Category pills gradient active state
old_category_trigger = """                            <Trigger Property="IsSelected" Value="True">
                                <Setter Property="Foreground" Value="White"/>
                                <Setter TargetName="Bd" Property="Background" Value="{DynamicResource AccentBlue}"/>
                            </Trigger>"""

new_category_trigger = """                            <Trigger Property="IsSelected" Value="True">
                                <Setter Property="Foreground" Value="White"/>
                                <Setter TargetName="Bd" Property="Background">
                                    <Setter.Value>
                                        <LinearGradientBrush StartPoint="0,0" EndPoint="1,0">
                                            <GradientStop Color="#5B9BFF" Offset="0"/>
                                            <GradientStop Color="#8B6BD4" Offset="1"/>
                                        </LinearGradientBrush>
                                    </Setter.Value>
                                </Setter>
                                <Setter TargetName="Bd" Property="Effect">
                                    <Setter.Value>
                                        <DropShadowEffect BlurRadius="12" Color="#5B9BFF" Opacity="0.3" ShadowDepth="2" Direction="270"/>
                                    </Setter.Value>
                                </Setter>
                            </Trigger>"""

if old_category_trigger.replace('\r\n', '\n') in content.replace('\r\n', '\n'):
    content = content.replace(old_category_trigger, new_category_trigger)
    print("Category active pills updated with blue-to-purple gradient.")
else:
    print("Warning: Category trigger trigger not matched exactly.")

# 8. ToggleCheckBoxStyle animation
old_toggle_trigger = """                            <Trigger Property="IsChecked" Value="True">
                                <Setter TargetName="Track" Property="Background" Value="{DynamicResource AccentBlue}"/>
                                <Setter TargetName="Thumb" Property="Fill" Value="White"/>
                                <Setter TargetName="Thumb" Property="RenderTransform">
                                    <Setter.Value>
                                        <TranslateTransform X="20"/>
                                    </Setter.Value>
                                </Setter>
                            </Trigger>"""

new_toggle_trigger = """                            <Trigger Property="IsChecked" Value="True">
                                <Trigger.EnterActions>
                                    <BeginStoryboard>
                                        <Storyboard>
                                            <DoubleAnimation Storyboard.TargetName="Thumb" Storyboard.TargetProperty="(UIElement.RenderTransform).(TranslateTransform.X)" To="20" Duration="0:0:0.12"/>
                                        </Storyboard>
                                    </BeginStoryboard>
                                </Trigger.EnterActions>
                                <Trigger.ExitActions>
                                    <BeginStoryboard>
                                        <Storyboard>
                                            <DoubleAnimation Storyboard.TargetName="Thumb" Storyboard.TargetProperty="(UIElement.RenderTransform).(TranslateTransform.X)" To="0" Duration="0:0:0.12"/>
                                        </Storyboard>
                                    </BeginStoryboard>
                                </Trigger.ExitActions>
                                <Setter TargetName="Track" Property="Background" Value="{DynamicResource AccentBlue}"/>
                                <Setter TargetName="Thumb" Property="Fill" Value="White"/>
                            </Trigger>"""

if old_toggle_trigger.replace('\r\n', '\n') in content.replace('\r\n', '\n'):
    content = content.replace(old_toggle_trigger, new_toggle_trigger)
    print("Added transition animation to ToggleCheckBoxStyle.")
else:
    print("Warning: ToggleCheckBoxStyle trigger not matched exactly.")

with open(target_xaml_path, 'w', encoding='utf-8', newline='') as f:
    f.write(content)

print("Restyle part 1 applied successfully.")
