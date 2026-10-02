param([switch]$SelfContained, [string]$PackagesPath = (Join-Path $PSScriptRoot '.nuget\packages'))
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$env:DOTNET_CLI_HOME = Join-Path $taskRoot '.dotnet-home'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
$config = Join-Path $taskRoot 'NuGet.Config'
$packages = [System.IO.Path]::GetFullPath($PackagesPath)
$project = Join-Path $taskRoot 'src\TimeOnScreen\TimeOnScreen.csproj'
dotnet restore $project --configfile $config --packages $packages
if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
dotnet build $project -c Release --no-restore --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$tests = Join-Path $taskRoot 'tests\TimeOnScreen.Tests\TimeOnScreen.Tests.csproj'
dotnet restore $tests --configfile $config --packages $packages
if ($LASTEXITCODE -ne 0) { throw 'Test restore failed' }
dotnet run --project $tests -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Tests failed' }
$taskOutput = Join-Path $taskRoot $(if ($SelfContained) { 'artifacts\standalone' } else { 'artifacts\app' })
if ($SelfContained) {
    dotnet publish $project -c Release -r win-x64 --self-contained true -o $taskOutput -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:RestoreConfigFile=$config -p:RestorePackagesPath=$packages --nologo
} else {
    dotnet publish $project -c Release --no-restore --self-contained false -o $taskOutput -p:DebugType=None --nologo
}
if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
foreach ($taskGuide in @('README.md', 'README.zh-CN.md', 'LICENSE', 'licenses')) {
    Copy-Item -LiteralPath (Join-Path $taskRoot $taskGuide) -Destination $taskOutput -Recurse -Force
}
if ($SelfContained) {
    $taskAssets = Get-Content -LiteralPath (Join-Path (Split-Path -Parent $project) 'obj\project.assets.json') -Raw | ConvertFrom-Json
    $taskRuntimePackages = @($taskAssets.project.frameworks.PSObject.Properties.Value | ForEach-Object { $_.downloadDependencies } | Where-Object { $_.name -in 'Microsoft.NETCore.App.Runtime.win-x64', 'Microsoft.WindowsDesktop.App.Runtime.win-x64' })
    if ($taskRuntimePackages.Count -ne 2) { throw 'Could not identify both bundled runtime license sources' }
    foreach ($taskRuntimePackage in $taskRuntimePackages) {
        $taskRuntimeVersion = ($taskRuntimePackage.version.Trim('[', ']') -split ',')[0].Trim()
        $taskRuntimeDirectory = Join-Path (Join-Path $packages $taskRuntimePackage.name.ToLowerInvariant()) $taskRuntimeVersion
        $taskVendorFiles = @(Get-ChildItem -LiteralPath $taskRuntimeDirectory -File | Where-Object { $_.Name -match '^(LICENSE(\..*)?|THIRD-PARTY-NOTICES\.TXT)$' })
        if (-not ($taskVendorFiles | Where-Object { $_.Name -match '^LICENSE' })) { throw 'Bundled runtime license file was not found' }
        $taskVendorOutput = Join-Path (Join-Path $taskOutput 'licenses') $taskRuntimePackage.name
        [void](New-Item -ItemType Directory -Path $taskVendorOutput -Force)
        foreach ($taskVendorFile in $taskVendorFiles) { Copy-Item -LiteralPath $taskVendorFile.FullName -Destination $taskVendorOutput -Force }
    }
}
foreach ($taskLegacyName in @('使用说明.md', '开发日志.md', 'CHANGELOG.md', 'TimeOnScreen.pdb', 'TimeOnScreen.Core.pdb')) {
    $taskLegacyGuide = Join-Path $taskOutput $taskLegacyName
    if (Test-Path -LiteralPath $taskLegacyGuide) { Remove-Item -LiteralPath $taskLegacyGuide }
}
$taskLegacyDocs = [System.IO.Path]::GetFullPath((Join-Path $taskOutput 'docs'))
$taskOutputRoot = [System.IO.Path]::GetFullPath($taskOutput).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if (-not $taskLegacyDocs.StartsWith($taskOutputRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unexpected package documentation path' }
if (Test-Path -LiteralPath $taskLegacyDocs) {
    if ((Get-Item -LiteralPath $taskLegacyDocs).Attributes -band [System.IO.FileAttributes]::ReparsePoint) { throw 'Package documentation must not be a linked directory' }
    Remove-Item -LiteralPath $taskLegacyDocs -Recurse -Force
}
