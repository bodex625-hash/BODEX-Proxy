$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\upstream'))
$main = Join-Path $root 'ProxiFyreUI\Forms\MainForm.cs'
$source = Get-Content -LiteralPath $main -Raw
if ($source -notmatch 'ApplyBodexBranding\(\);') {
  $source = $source.Replace('            notifyIcon.Icon = _applicationIcon;', '            notifyIcon.Icon = _applicationIcon;' + [Environment]::NewLine + '            ApplyBodexBranding();')
  Set-Content -LiteralPath $main -Value $source -Encoding UTF8
}
$uiDir = Join-Path $root 'ProxiFyreUI'
$brandCode = @'
using System.Drawing;
using System.Windows.Forms;
namespace ProxiFyreUI.Forms
{
    public partial class MainForm
    {
        private void ApplyBodexBranding()
        {
            Text = "BODEX Proxy";
            BackColor = Color.FromArgb(11, 18, 32);
            ForeColor = Color.FromArgb(226, 238, 250);
            ApplyBodexThemeToControls(Controls);
        }
        private static void ApplyBodexThemeToControls(Control.ControlCollection controls)
        {
            foreach (Control control in controls)
            {
                if (control is TabControl || control is TabPage || control is Panel || control is GroupBox)
                {
                    control.BackColor = Color.FromArgb(15, 27, 45);
                    control.ForeColor = Color.FromArgb(226, 238, 250);
                }
                else if (control is Button)
                {
                    control.BackColor = Color.FromArgb(0, 174, 239);
                    control.ForeColor = Color.FromArgb(5, 16, 28);
                    control.FlatStyle = FlatStyle.Flat;
                }
                else if (control is DataGridView grid)
                {
                    grid.BackgroundColor = Color.FromArgb(11, 18, 32);
                    grid.GridColor = Color.FromArgb(36, 57, 79);
                    grid.EnableHeadersVisualStyles = false;
                    grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(0, 116, 166);
                    grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
                    grid.DefaultCellStyle.BackColor = Color.FromArgb(15, 27, 45);
                    grid.DefaultCellStyle.ForeColor = Color.FromArgb(226, 238, 250);
                    grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 116, 166);
                    grid.DefaultCellStyle.SelectionForeColor = Color.White;
                }
                else if (control is TextBoxBase || control is ComboBox)
                {
                    control.BackColor = Color.FromArgb(7, 14, 25);
                    control.ForeColor = Color.FromArgb(226, 238, 250);
                }
                else
                {
                    control.ForeColor = Color.FromArgb(226, 238, 250);
                }
                if (control.HasChildren) ApplyBodexThemeToControls(control.Controls);
            }
        }
    }
}
'@
Set-Content -LiteralPath (Join-Path $uiDir 'BodexBranding.cs') -Value $brandCode -Encoding UTF8
$project = Join-Path $uiDir 'ProxiFyreUI.csproj'
$csproj = Get-Content -LiteralPath $project -Raw
if ($csproj -notmatch 'BodexBranding.cs') {
  $csproj = $csproj.Replace('<Compile Include="Program.cs" />', '<Compile Include="Program.cs" />' + [Environment]::NewLine + '    <Compile Include="BodexBranding.cs" />')
  Set-Content -LiteralPath $project -Value $csproj -Encoding UTF8
}
$assembly = Join-Path $uiDir 'Properties\AssemblyInfo.cs'
$a = Get-Content -LiteralPath $assembly -Raw
$a = $a.Replace('[assembly: AssemblyTitle("ProxiFyreUI")]', '[assembly: AssemblyTitle("BODEX Proxy")]')
$a = $a.Replace('[assembly: AssemblyDescription("Native Windows management interface for the ProxiFyre service")]', '[assembly: AssemblyDescription("BODEX Proxy Windows management interface")]')
$a = $a.Replace('[assembly: AssemblyProduct("ProxiFyre")]', '[assembly: AssemblyProduct("BODEX Proxy")]')
Set-Content -LiteralPath $assembly -Value $a -Encoding UTF8
$package = Join-Path $root 'ProxiFyre.Installer\Package.wxs'
$w = Get-Content -LiteralPath $package -Raw
$w = $w.Replace('Name="ProxiFyre"', 'Name="BODEX Proxy"')
$w = $w.Replace('Description="ProxiFyre per-machine installer"', 'Description="BODEX Proxy per-machine installer"')
$w = $w.Replace('Comments="Installs the architecture-specific ProxiFyre engine and management UI."', 'Comments="Installs the BODEX Proxy engine and management UI."')
$w = $w.Replace('Title="ProxiFyre"', 'Title="BODEX Proxy"')
$w = $w.Replace('Description="ProxiFyre engine, service, management UI, and Start menu shortcut"', 'Description="BODEX Proxy engine, service, management UI, and Start menu shortcut"')
Set-Content -LiteralPath $package -Value $w -Encoding UTF8
$bundle = Join-Path $root 'ProxiFyre.Bundle\Bundle.wxs'
if (Test-Path $bundle) {
  $b = Get-Content -LiteralPath $bundle -Raw
  $b = $b.Replace('Name="ProxiFyre"', 'Name="BODEX Proxy"')
  $b = $b.Replace('Title="ProxiFyre"', 'Title="BODEX Proxy"')
  Set-Content -LiteralPath $bundle -Value $b -Encoding UTF8
}
Write-Host 'BODEX branding patch applied. Engine, service names, routing and configuration identifiers remain unchanged.'
