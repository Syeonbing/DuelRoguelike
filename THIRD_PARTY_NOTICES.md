# 외부 코드와 라이선스

이 프로젝트(AGPL-3.0)가 가져다 쓰는 외부 코드의 출처와 라이선스를 기록한다. 버전을 올리면 이 표도 함께 고친다.

## 게임에 들어가는 것

| 이름 | 위치 | 출처 | 버전 | 라이선스 |
| --- | --- | --- | --- | --- |
| ygopro-core (EDOPro 코어, ocgcore) | `External/ygopro-core` (Git 서브모듈), 빌드 결과 `Assets/Plugins/x86_64/ocgcore.dll` | https://github.com/edo9300/ygopro-core | master `c2e0b69` (2026-11-02, v11.0 태그 이후), C API 11.0 | AGPL-3.0-or-later |
| Lua | `External/ygopro-core/lua/src` (ygopro-core 안의 서브모듈) | https://github.com/lua/lua | 5.4.8 (`6e22fed`) | MIT |
| Project Ignis CardScripts (카드 효과 Lua 스크립트) | `External/CardScripts` (얕은 Git 서브모듈) | https://github.com/ProjectIgnis/CardScripts | master `6a59aedb` (2026-11-07) | AGPL-3.0-or-later |
| Project Ignis BabelCDB (카드 DB, SQLite .cdb) | `External/BabelCDB` (얕은 Git 서브모듈) | https://github.com/ProjectIgnis/BabelCDB | master `85c0539` (2026-11-07) | 명시 없음 (저장소에 LICENSE 파일과 라이선스 안내가 없다) |
| SQLite | `Assets/Plugins/x86_64/sqlite3.dll` (`tools/fetch-sqlite.ps1`로 받음, 저장소에 넣지 않음) | https://www.sqlite.org | 3.54.1 (공식 64비트 DLL) | 퍼블릭 도메인 |

- ocgcore.dll은 저장소에 넣지 않고 `tools/build-ocgcore.ps1`로 위 소스에서 빌드한다. Lua는 ocgcore.dll 안에 정적으로 링크된다.
- `Assets/Scripts/Engine/Ocg/`의 C# 선언은 ocgcore의 `ocgapi.h`, `ocgapi_types.h`, `ocgapi_constants.h`를 옮긴 것이다.
- BabelCDB의 카드 텍스트(영문 카드명·효과문)는 원작 카드의 텍스트다. 화면에 쓰는 한글 카드명·효과문은 카드 ID별로 따로 입힌다.

## 빌드할 때만 쓰는 것

| 이름 | 위치 | 출처 | 버전 | 라이선스 |
| --- | --- | --- | --- | --- |
| premake | `tools/.cache` (빌드할 때 받음, 저장소에 넣지 않음) | https://github.com/premake/premake-core | 5.0.0-beta2 | BSD-3-Clause |
