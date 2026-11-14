$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\upstream'))
$uiDir = Join-Path $root 'ProxiFyreUI'
$licenseDir = Join-Path $PSScriptRoot '..\src'
Copy-Item -LiteralPath (Join-Path $licenseDir 'BodexLicense.cs') -Destination (Join-Path $uiDir 'Infrastructure\BodexLicense.cs') -Force
Copy-Item -LiteralPath (Join-Path $licenseDir 'BodexActivationForm.cs') -Destination (Join-Path $uiDir 'Forms\BodexActivationForm.cs') -Force

$project = Join-Path $uiDir 'ProxiFyreUI.csproj'
$csproj = Get-Content -LiteralPath $project -Raw
if ($csproj -notmatch 'BodexLicense.cs') {
  $before = $csproj
  $csproj = $csproj.Replace('<Compile Include="Program.cs" />', '<Compile Include="Program.cs" />' + [Environment]::NewLine + '    <Compile Include="Infrastructure\BodexLicense.cs" />' + [Environment]::NewLine + '    <Compile Include="Forms\BodexActivationForm.cs" />')
  if ($csproj -eq $before) { throw 'Could not add BODEX source files to ProxiFyreUI.csproj.' }
}
if ($csproj -notmatch 'System.Security') {
  $before = $csproj
  $csproj = $csproj.Replace('<Reference Include="System.ServiceProcess" />', '<Reference Include="System.ServiceProcess" />' + [Environment]::NewLine + '    <Reference Include="System.Security" />')
  if ($csproj -eq $before) { throw 'Could not add System.Security reference for DPAPI.' }
}
Set-Content -LiteralPath $project -Value $csproj -Encoding UTF8

# Enforce the license gate before the single-instance coordinator. This ensures that
# cancelling activation terminates this launch and a later launch cannot reopen a
# tray-resident, unlicensed MainForm.
$program = Join-Path $uiDir 'Program.cs'
$source = Get-Content -LiteralPath $program -Raw
$old = @'
            using (startupPayloadLease)
            using (var singleInstance = SingleInstanceCoordinator.CreateForCurrentUser())
            {
                if (!singleInstance.IsPrimary)
                {
                    singleInstance.RequestActivation();
                    return;
                }

                RunVerifiedApplication(singleInstance);
            }
'@
$new = @'
            using (startupPayloadLease)
            {
                var licenseState = BodexLicense.CheckAsync().GetAwaiter().GetResult();
                if (licenseState != BodexLicenseState.Active &&
                    licenseState != BodexLicenseState.OfflineGrace)
                {
                    using (var activation = new Forms.BodexActivationForm())
                    {
                        if (activation.ShowDialog() != DialogResult.OK)
                            return;
                    }
                }

                using (var singleInstance = SingleInstanceCoordinator.CreateForCurrentUser())
                {
                    if (!singleInstance.IsPrimary)
                    {
                        singleInstance.RequestActivation();
                        return;
                    }

                    RunVerifiedApplication(singleInstance);
                }
            }
'@
if ($source -notmatch 'BodexLicense.CheckAsync') {
  if (-not $source.Contains($old)) { throw 'Could not locate the exact Program.Run startup block; refusing to build without an enforced license gate.' }
  $source = $source.Replace($old, $new)
}
if ($source -notmatch 'BodexLicense.CheckAsync' -or $source -notmatch 'activation.ShowDialog\(\) != DialogResult.OK') {
  throw 'BODEX startup license gate verification failed.'
}
Set-Content -LiteralPath $program -Value $source -Encoding UTF8
Write-Host 'BODEX licensing gate enforced before single-instance startup.'
