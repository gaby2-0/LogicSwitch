# Builds LogicSwitch.dll with the C# compiler that ships with Windows (.NET Framework 4.8).
#   build.ps1            build into bin\
#   build.ps1 -Install   build and copy into the game's Mods folder
#   build.ps1 -Package   build and create dist\LogicSwitch-<version>.zip to share
param([switch]$Install, [switch]$Package)

$ErrorActionPreference = "Stop"

# Adjust to your StellarDrive install.
$game    = "A:\SteamLibrary\steamapps\common\StellarDrive Demo"

$managed = "$game\StellarDrive_Data\Managed"
$melon   = "$game\MelonLoader\net472"
$mods    = "$game\Mods"
$root    = $PSScriptRoot
$out     = "$root\bin\LogicSwitch.dll"
$csc     = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"

$refs = @(
    "$managed\mscorlib.dll", "$managed\netstandard.dll", "$managed\System.dll", "$managed\System.Core.dll",
    "$managed\UnityEngine.CoreModule.dll", "$managed\UnityEngine.PhysicsModule.dll", "$managed\UnityEngine.ParticleSystemModule.dll",
    "$managed\UnityEngine.UI.dll", "$managed\UnityEngine.UIModule.dll", "$managed\UnityEngine.UIElementsModule.dll",
    "$managed\UnityEngine.TextRenderingModule.dll", "$managed\Unity.TextMeshPro.dll",
    "$managed\Unity.Localization.dll", "$managed\Unity.ResourceManager.dll", "$managed\Unity.InputSystem.dll",
    "$managed\Ships.dll", "$managed\Ships.Interface.dll", "$managed\Core.dll", "$managed\Players.Interface.dll",
    "$managed\Items.Model.dll", "$managed\WorldTracking.Interface.dll", "$managed\Managers.Interface.dll",
    "$managed\FishNet.Runtime.dll", "$managed\UI.dll",
    "$melon\MelonLoader.dll", "$melon\0Harmony.dll",
    "$mods\SDModKit.dll"
)

New-Item -ItemType Directory -Force "$root\bin" | Out-Null
$cscArgs = @("/nologo", "/target:library", "/optimize+", "/nostdlib+", "/noconfig", "/codepage:65001",
             "/nowarn:1701,1702", "/out:$out") +
           ($refs | ForEach-Object { "/reference:$_" }) +
           (Get-ChildItem "$root\src\*.cs" | ForEach-Object { $_.FullName })
& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "Build failed" }
Write-Host "OK -> $out"

if ($Install) {
    Copy-Item $out "$mods\LogicSwitch.dll" -Force
    # Earlier builds were named ElectroLogic and SignalForge; loaded together they would register every part twice.
    foreach ($old in "ElectroLogic.dll", "SignalForge.dll") {
        if (Test-Path "$mods\$old") { Remove-Item "$mods\$old" }
    }
    Write-Host "Installed into $mods"
}

if ($Package) {
    $version = (Select-String -Path "$root\src\Plugin.cs" -Pattern 'Version = "([^"]+)"').Matches[0].Groups[1].Value
    $author  = (Select-String -Path "$root\src\Plugin.cs" -Pattern 'Author = "([^"]+)"').Matches[0].Groups[1].Value
    $stage   = "$root\dist\stage"
    Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Force "$stage\Mods" | Out-Null
    Copy-Item $out "$stage\Mods\LogicSwitch.dll"
    Copy-Item "$root\README.md", "$root\LICENSE" $stage
    $manifest = [ordered]@{
        id          = "logicswitch"
        name        = "Logic&Switch"
        author      = $author
        version     = $version
        description = "Signal lamps, logic gates (AND/OR/XOR and more), push buttons NO/NC, toggle switch, circuit breaker and signal door. Needs SDModKit."
    }
    $manifest | ConvertTo-Json | Set-Content "$stage\mod.json" -Encoding UTF8
    $zip = "$root\dist\LogicSwitch-$version.zip"
    Remove-Item $zip -Force -ErrorAction SilentlyContinue
    Compress-Archive -Path "$stage\*" -DestinationPath $zip
    Remove-Item $stage -Recurse -Force
    Write-Host "Package -> $zip"
}
