$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\upstream'))
$uiDir = Join-Path $root 'ProxiFyreUI'
$licenseDir = Join-Path $PSScriptRoot '..\src'
Copy-Item -LiteralPath (Join-Path $licenseDir 'BodexLicense.cs') -Destination (Join-Path $uiDir 'Infrastructure\BodexLicense.cs') -Force
Copy-Item -LiteralPath (Join-Path $licenseDir 'BodexActivationForm.cs') -Destination (Join-Path $uiDir 'Forms\BodexActivationForm.cs') -Force

$project = Join-Path $uiDir 'ProxiFyreUI.csproj'
$csproj = Get-Content -LiteralPath $project -Raw
if ($csproj -notmatch 'BodexLicense.cs') {
  $csproj = $csproj.Replace('<Compile Include="Program.cs" />', '<Compile Include="Program.cs" />' + [Environment]::NewLine + '    <Compile Include="Infrastructure\BodexLicense.cs" />' + [Environment]::NewLine + '    <Compile Include="Forms\BodexActivationForm.cs" />')
}
if ($csproj -notmatch 'System.Security') {
  $csproj = $csproj.Replace('<Reference Include="System.ServiceProcess" />', '<Reference Include="System.ServiceProcess" />' + [Environment]::NewLine + '    <Reference Include="System.Security" />')
}
Set-Content -LiteralPath $project -Value $csproj -Encoding UTF8

$main = Join-Path $uiDir 'Forms\MainForm.cs'
$source = Get-Content -LiteralPath $main -Raw
if ($source -notmatch 'BodexLicense.CheckAsync') {
  $source = $source.Replace('            await InitializeAsync();', @'
            var licenseState = await ProxiFyreUI.Infrastructure.BodexLicense.CheckAsync();
            if (licenseState != ProxiFyreUI.Infrastructure.BodexLicenseState.Active &&
                licenseState != ProxiFyreUI.Infrastructure.BodexLicenseState.OfflineGrace)
            {
                using (var activation = new BodexActivationForm())
                {
                    if (activation.ShowDialog(this) != DialogResult.OK)
                    {
                        Close();
                        return;
                    }
                }
            }
            await InitializeAsync();
'@)
}
Set-Content -LiteralPath $main -Value $source -Encoding UTF8
Write-Host 'BODEX licensing client and activation gate integrated into the ProxiFyre UI build.'
