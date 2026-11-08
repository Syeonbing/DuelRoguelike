<#
화면 밖 시험(headless-duel.ps1, headless-run.ps1)이 함께 쓰는 준비 단계.
점 소싱으로 불러 쓴다: . (Join-Path $PSScriptRoot 'headless-common.ps1')
$Root, $OutDir, Write-Step, Initialize-Headless를 정의한다.

Initialize-Headless가 하는 일:
1. ProjectSettings/ProjectVersion.txt의 유니티 버전으로 설치된 에디터를 찾는다 (Unity Hub 기본 경로).
2. 에디터에 들어 있는 C# 컴파일러(Roslyn)로 어셈블리 정의(asmdef)와 같은 단위로 컴파일한다:
   DuelRoguelike.Engine → WindBot.Ignite → DuelRoguelike.AI → DuelRoguelike.Run → DuelRoguelike.UI → DuelRoguelike.Dev
   (.NET Standard 2.1, 유니티 엔진 DLL 참조, 에디터와 같이 DEBUG 정의). 결과는 tools/.cache/headless에 둔다.
3. ocgcore.dll, sqlite3.dll(Assets/Plugins/x86_64)을 같은 폴더에 복사하고 PowerShell에 어셈블리를 불러온다.
4. 카드 데이터(External 폴더)를 읽어 CardDataSet을 돌려준다.
#>

$Root   = Split-Path -Parent $PSScriptRoot
$OutDir = Join-Path $PSScriptRoot '.cache\headless'

function Write-Step([string]$Message) { Write-Host "==> $Message" -ForegroundColor Cyan }

function Initialize-Headless([string]$UnityEditor) {
    if (-not $UnityEditor) {
        $versionLine = Get-Content (Join-Path $Root 'ProjectSettings\ProjectVersion.txt') | Where-Object { $_ -like 'm_EditorVersion:*' }
        $version = ($versionLine -split ':', 2)[1].Trim()
        $UnityEditor = "C:\Program Files\Unity\Hub\Editor\$version\Editor"
    }
    $data        = Join-Path $UnityEditor 'Data'
    $dotnet      = Join-Path $data 'NetCoreRuntime\dotnet.exe'
    $csc         = Join-Path $data 'DotNetSdkRoslyn\csc.dll'
    $netstandard = Join-Path $data 'NetStandard\ref\2.1.0\netstandard.dll'
    $unityDlls   = Get-ChildItem (Join-Path $data 'Managed\UnityEngine') -Filter '*.dll' | ForEach-Object FullName
    foreach ($path in $dotnet, $csc, $netstandard) {
        if (-not (Test-Path $path)) { throw "유니티 에디터에서 찾지 못했습니다: $path (-UnityEditor로 에디터 폴더를 지정하세요)" }
    }

    $plugins = Join-Path $Root 'Assets\Plugins\x86_64'
    foreach ($native in 'ocgcore.dll', 'sqlite3.dll') {
        if (-not (Test-Path (Join-Path $plugins $native))) {
            throw "$native 이 없습니다. 먼저 tools/build-ocgcore.ps1, tools/fetch-sqlite.ps1을 실행하세요."
        }
    }

    New-Item -ItemType Directory -Force $OutDir | Out-Null

    # asmdef와 같은 단위: 이름, 소스 폴더, 참조할 앞 어셈블리, 유니티 엔진 참조 여부
    $assemblies = @(
        @{ Name = 'DuelRoguelike.Engine'; Dir = 'Assets\Scripts\Engine';     Refs = @();                                                         Unity = $true },
        @{ Name = 'WindBot.Ignite';       Dir = 'Assets\ThirdParty\WindBot'; Refs = @();                                                         Unity = $false },
        @{ Name = 'DuelRoguelike.AI';     Dir = 'Assets\Scripts\AI';         Refs = @('DuelRoguelike.Engine', 'WindBot.Ignite');                 Unity = $false },
        @{ Name = 'DuelRoguelike.Run';    Dir = 'Assets\Scripts\Run';        Refs = @('DuelRoguelike.Engine');                                   Unity = $false },
        @{ Name = 'DuelRoguelike.UI';     Dir = 'Assets\Scripts\UI';         Refs = @('DuelRoguelike.Engine', 'DuelRoguelike.AI');               Unity = $true },
        @{ Name = 'DuelRoguelike.Dev';    Dir = 'Assets\Scripts\Dev';        Refs = @('DuelRoguelike.Engine', 'DuelRoguelike.AI', 'DuelRoguelike.UI', 'DuelRoguelike.Run'); Unity = $true }
    )

    foreach ($asm in $assemblies) {
        Write-Step "컴파일: $($asm.Name)"
        $sources = Get-ChildItem (Join-Path $Root $asm.Dir) -Recurse -Filter '*.cs' -File | ForEach-Object FullName
        $out = Join-Path $OutDir "$($asm.Name).dll"
        $rsp = @(
            '-nologo', '-target:library', '-langversion:9', '-nostdlib', '-define:DEBUG;TRACE', '-debug:portable',
            "-out:`"$out`"", "-r:`"$netstandard`""
        )
        foreach ($ref in $asm.Refs) { $rsp += "-r:`"$(Join-Path $OutDir "$ref.dll")`"" }
        if ($asm.Unity) { foreach ($dll in $unityDlls) { $rsp += "-r:`"$dll`"" } }
        $rsp += $sources | ForEach-Object { "`"$_`"" }
        $rspFile = Join-Path $OutDir "$($asm.Name).rsp"
        Set-Content -Path $rspFile -Value $rsp -Encoding UTF8
        $output = & $dotnet $csc "@$rspFile" 2>&1
        $warnings = @($output | Where-Object { $_ -match ': warning ' })
        $errors   = @($output | Where-Object { $_ -match ': error ' })
        foreach ($line in $errors) { Write-Host $line -ForegroundColor Red }
        if ($LASTEXITCODE -ne 0) { throw "$($asm.Name) 컴파일 실패 (오류 $($errors.Count)건)" }
        if ($warnings.Count -gt 0) {
            Write-Host "    경고 $($warnings.Count)건" -ForegroundColor Yellow
            foreach ($line in $warnings) { Write-Host "    $line" -ForegroundColor DarkYellow }
        }
    }

    Copy-Item (Join-Path $plugins 'ocgcore.dll'), (Join-Path $plugins 'sqlite3.dll') $OutDir -Force
    $env:PATH = "$OutDir;$env:PATH"
    foreach ($asm in $assemblies) { Add-Type -Path (Join-Path $OutDir "$($asm.Name).dll") }

    Write-Step '카드 데이터 읽는 중'
    $cardData = [DuelRoguelike.Engine.Data.CardDataSet]::Load((Join-Path $Root 'External'))
    Write-Host "    카드 $($cardData.CardCount)장, 한글 $($cardData.LocalizedCount)장"
    return $cardData
}

# -File로 실행하면 쉼표 목록이 문자열 하나로 들어오므로 직접 나눈다. 이름은 Yugi, Kaiba, Joey, Pegasus, All.
function Resolve-Decks([string[]]$names, [string]$param) {
    $ids = foreach ($n in ($names | ForEach-Object { $_ -split ',' } | ForEach-Object { $_.Trim() } | Where-Object { $_ })) {
        if ($n -eq 'All') { [DuelRoguelike.Engine.Data.StarterDecks]::All | ForEach-Object Id; continue }
        $recipe = [DuelRoguelike.Engine.Data.StarterDecks]::Find($n)
        if (-not $recipe) { throw "-$param 에 모르는 덱: $n (Yugi, Kaiba, Joey, Pegasus, All)" }
        $recipe.Id
    }
    @($ids | Select-Object -Unique | ForEach-Object { [DuelRoguelike.Engine.Data.StarterDecks]::Find($_) })
}
