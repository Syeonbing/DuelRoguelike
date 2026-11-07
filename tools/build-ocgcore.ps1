<#
.SYNOPSIS
ocgcore(External/ygopro-core)를 64비트 DLL로 빌드해 Assets/Plugins/x86_64/ocgcore.dll로 복사한다.

.DESCRIPTION
1. Visual Studio(C++ 도구 포함)와 MSBuild, C++ 도구 집합을 찾는다.
2. premake5가 없으면 premake 공식 GitHub 릴리스에서 받아 tools/.cache에 둔다. (해시 확인)
3. ocgcore가 공식 지원하는 vs2022 형식으로 솔루션을 만든다. (External/ygopro-core/build)
4. 설치된 Visual Studio의 MSBuild와 C++ 도구 집합으로 ocgcoreshared(DLL)만 빌드한다.
5. 결과 DLL을 유니티 플러그인 폴더로 복사한다.

유니티 에디터는 한 번 불러온 DLL을 에디터를 닫을 때까지 잠근다. 다시 빌드할 때는 유니티를 먼저 닫는다.

.EXAMPLE
powershell -ExecutionPolicy Bypass -File C:\Dev\DuelRoguelike\tools\build-ocgcore.ps1

.EXAMPLE
powershell -ExecutionPolicy Bypass -File C:\Dev\DuelRoguelike\tools\build-ocgcore.ps1 -Configuration Debug
#>
[CmdletBinding()]
param(
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release',

    # 비워 두면 설치된 Visual Studio의 가장 새 C++ 도구 집합을 쓴다. (VS 2026: v145, VS 2022: v143)
    [string]$Toolset
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$Root      = Split-Path -Parent $PSScriptRoot
$Core      = Join-Path $Root 'External\ygopro-core'
$PluginDir = Join-Path $Root 'Assets\Plugins\x86_64'
$CacheDir  = Join-Path $PSScriptRoot '.cache'

# ocgcore 저장소의 scripts/install-premake5.bat와 같은 버전을 쓴다.
$PremakeVersion = '5.0.0-beta2'
$PremakeUrl     = "https://github.com/premake/premake-core/releases/download/v$PremakeVersion/premake-$PremakeVersion-windows.zip"
# 2026-11-08에 받은 파일의 해시 (4,162,452바이트). 이 릴리스는 GitHub에 해시가 게시되어 있지 않다.
$PremakeSha256  = '87CFA10ED52FD1F4E835F738AC1033FF302035758671400FEC078B700C622C54'

function Write-Step([string]$Message) { Write-Host "==> $Message" -ForegroundColor Cyan }

# 0. 서브모듈 확인
if (-not (Test-Path (Join-Path $Core 'lua\src\lua.h'))) {
    throw 'External/ygopro-core 서브모듈이 비어 있습니다. 먼저 실행하세요: git submodule update --init --recursive'
}

# 1. Visual Studio, MSBuild, C++ 도구 집합 찾기
Write-Step 'Visual Studio C++ 도구 찾는 중'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path $vswhere)) { throw 'Visual Studio Installer(vswhere.exe)를 찾지 못했습니다.' }
$vsPath = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if (-not $vsPath) {
    throw 'C++ 빌드 도구가 설치된 Visual Studio가 없습니다. Visual Studio Installer에서 "C++를 사용한 데스크톱 개발" 워크로드를 설치하세요.'
}
$msbuild = Join-Path $vsPath 'MSBuild\Current\Bin\amd64\MSBuild.exe'
if (-not (Test-Path $msbuild)) { $msbuild = Join-Path $vsPath 'MSBuild\Current\Bin\MSBuild.exe' }
if (-not (Test-Path $msbuild)) { throw "MSBuild를 찾지 못했습니다: $vsPath" }
if (-not $Toolset) {
    $Toolset = Get-ChildItem (Join-Path $vsPath 'MSBuild\Microsoft\VC\*\Platforms\x64\PlatformToolsets\*') -Directory -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^v\d+$' } |
        Sort-Object { [int]$_.Name.Substring(1) } |
        Select-Object -Last 1 -ExpandProperty Name
    if (-not $Toolset) { throw 'C++ 도구 집합을 찾지 못했습니다. -Toolset v145 처럼 직접 지정하세요.' }
}
Write-Host "    Visual Studio: $vsPath"
Write-Host "    MSBuild:       $msbuild"
Write-Host "    도구 집합:     $Toolset"

# 2. premake5 준비 (없을 때만 받는다)
$premakeDir = Join-Path $CacheDir "premake-$PremakeVersion"
$premake    = Join-Path $premakeDir 'premake5.exe'
if (-not (Test-Path $premake)) {
    Write-Step "premake $PremakeVersion 받는 중: $PremakeUrl"
    New-Item -ItemType Directory -Force $premakeDir | Out-Null
    $zip = Join-Path $premakeDir 'premake.zip'
    Invoke-WebRequest -Uri $PremakeUrl -OutFile $zip -UseBasicParsing
    $hash = (Get-FileHash $zip -Algorithm SHA256).Hash
    if ($hash -ne $PremakeSha256) {
        Remove-Item $zip -Force
        throw "premake 압축 파일의 SHA256이 예상과 다릅니다. 받은 값: $hash"
    }
    Expand-Archive -Path $zip -DestinationPath $premakeDir -Force
    Remove-Item $zip -Force
}

# 3. Visual Studio 솔루션 생성
Write-Step 'premake5 vs2022 실행 중 (External/ygopro-core/build)'
Push-Location $Core
try {
    & $premake vs2022
    if ($LASTEXITCODE -ne 0) { throw "premake5 실패 (종료 코드 $LASTEXITCODE)" }
} finally {
    Pop-Location
}

# 4. DLL 빌드
Write-Step "ocgcoreshared 빌드 중 ($Configuration, x64, $Toolset)"
& $msbuild (Join-Path $Core 'build\ocgcore.sln') -nologo -m -verbosity:minimal `
    -t:ocgcoreshared "-p:Configuration=$Configuration" '-p:Platform=x64' "-p:PlatformToolset=$Toolset"
if ($LASTEXITCODE -ne 0) { throw "MSBuild 실패 (종료 코드 $LASTEXITCODE)" }

# 5. 유니티 플러그인 폴더로 복사
$dll = Join-Path $Core "bin\x64\$($Configuration.ToLowerInvariant())\ocgcore.dll"
if (-not (Test-Path $dll)) { throw "빌드 결과를 찾지 못했습니다: $dll" }
New-Item -ItemType Directory -Force $PluginDir | Out-Null
try {
    Copy-Item $dll $PluginDir -Force
} catch {
    throw "ocgcore.dll을 복사하지 못했습니다. 유니티 에디터가 DLL을 잡고 있으면 에디터를 닫고 다시 실행하세요. ($($_.Exception.Message))"
}
Write-Step "완료: $(Join-Path $PluginDir 'ocgcore.dll')"
