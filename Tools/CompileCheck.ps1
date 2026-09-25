# Compiles the project's C# outside Unity, using the compiler and libraries that ship with the editor,
# to catch errors while the Unity editor is closed or unfocused. It does not replace Unity's build or tests.
# Usage (from the project root):  powershell -ExecutionPolicy Bypass -File Tools\CompileCheck.ps1
param([string]$UnityVersion = "")

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
if (-not $UnityVersion) { $UnityVersion = ((Get-Content "$root\ProjectSettings\ProjectVersion.txt" -TotalCount 1) -split ' ')[1] }
$data = "C:\Program Files\Unity\Hub\Editor\$UnityVersion\Editor\Data"
$dotnet = "$data\DotNetSdk\dotnet.exe"
$csc = (Get-ChildItem "$data\DotNetSdk\sdk" -Recurse -Filter csc.dll | Where-Object { $_.FullName -match 'Roslyn\\bincore' } | Select-Object -First 1).FullName
$out = Join-Path $env:TEMP "squishy-compilecheck"
New-Item -ItemType Directory -Force $out | Out-Null

$netstd = "$data\NetStandard\ref\2.1.0\netstandard.dll"
$engine = Get-ChildItem "$data\Managed\UnityEngine" -Filter "UnityEngine*.dll" | ForEach-Object { $_.FullName }
$editor = Get-ChildItem "$data\Managed\UnityEngine" -Filter "UnityEditor*.dll" | ForEach-Object { $_.FullName }
$sa = "$root\Library\ScriptAssemblies"
$mscorlibShim = (Get-ChildItem "$data\NetStandard\compat" -Recurse -Filter mscorlib.dll | Select-Object -First 1).FullName
$nunit = (Get-ChildItem "$root\Library\PackageCache" -Recurse -Filter nunit.framework.dll | Select-Object -First 1).FullName

function Compile([string]$name, [string[]]$sources, [string[]]$refs, [string[]]$defines = @()) {
    $args = @("/nologo", "/noconfig", "/nostdlib+", "/t:library", "/langversion:9.0", "/nowarn:1701,1702,0649,0414", "/out:$out\$name.dll", "/r:$netstd")
    $args += $refs | ForEach-Object { "/r:$_" }
    if ($defines.Count) { $args += "/define:" + ($defines -join ';') }
    $args += $sources
    $result = & $dotnet $csc @args
    $errors = $result | Where-Object { $_ -match 'error' }
    if ($LASTEXITCODE -ne 0) { Write-Host "FAIL $name"; $errors | Select-Object -First 8 | ForEach-Object { Write-Host "  $_" }; return $false }
    Write-Host "ok   $name"; return $true
}

$scripts = "$root\Assets\_Project\Scripts"
$sim = Get-ChildItem "$scripts\Simulation" -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
$dataSrc = Get-ChildItem "$scripts\Data" -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
$runtime = Get-ChildItem "$scripts\Runtime" -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
$editorSrc = Get-ChildItem "$scripts\Editor" -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
$tests = Get-ChildItem "$root\Assets\_Project\Tests" -Recurse -Filter *.cs | ForEach-Object { $_.FullName }

$ok = Compile "Squishy.Simulation" $sim @()
$ok = $ok -and (Compile "Squishy.Data" $dataSrc (@("$out\Squishy.Simulation.dll") + $engine))
$ok = $ok -and (Compile "Squishy.Runtime" $runtime (@("$out\Squishy.Simulation.dll", "$out\Squishy.Data.dll", "$sa\Unity.InputSystem.dll", "$sa\UnityEngine.UI.dll") + $engine))
$urp = @("$sa\Unity.RenderPipelines.Core.Runtime.dll", "$sa\Unity.RenderPipelines.Universal.Runtime.dll")
$ok = $ok -and (Compile "Squishy.Editor" $editorSrc (@("$out\Squishy.Simulation.dll", "$out\Squishy.Data.dll", "$out\Squishy.Runtime.dll") + $urp + $engine + $editor) @("UNITY_EDITOR"))
$ok = $ok -and (Compile "Squishy.Tests.EditMode" $tests (@("$out\Squishy.Simulation.dll", "$out\Squishy.Data.dll", "$out\Squishy.Runtime.dll", $nunit, $mscorlibShim) + $engine + $editor) @("UNITY_EDITOR", "UNITY_INCLUDE_TESTS"))
if ($ok) { Write-Host "All assemblies compile." } else { exit 1 }
