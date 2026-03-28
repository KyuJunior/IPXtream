$content = Get-Content -Path "ViewModels/PlayerViewModel.cs" -Raw
$newContent = $content -replace 'App\.Current\.Dispatcher\.Invoke\(', 'App.Current.Dispatcher.BeginInvoke('
Set-Content -Path "ViewModels/PlayerViewModel.cs" -Value $newContent -Encoding UTF8
