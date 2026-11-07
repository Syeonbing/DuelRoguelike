# 외부 코드와 라이선스

이 프로젝트(AGPL-3.0)가 가져다 쓰는 외부 코드의 출처와 라이선스를 기록한다. 버전을 올리면 이 표도 함께 고친다.

## 게임에 들어가는 것

| 이름 | 위치 | 출처 | 버전 | 라이선스 |
| --- | --- | --- | --- | --- |
| ygopro-core (EDOPro 코어, ocgcore) | `External/ygopro-core` (Git 서브모듈), 빌드 결과 `Assets/Plugins/x86_64/ocgcore.dll` | https://github.com/edo9300/ygopro-core | master `c2e0b69` (2026-11-02, v11.0 태그 이후), C API 11.0 | AGPL-3.0-or-later |
| Lua | `External/ygopro-core/lua/src` (ygopro-core 안의 서브모듈) | https://github.com/lua/lua | 5.4.8 (`6e22fed`) | MIT |

- ocgcore.dll은 저장소에 넣지 않고 `tools/build-ocgcore.ps1`로 위 소스에서 빌드한다. Lua는 ocgcore.dll 안에 정적으로 링크된다.
- `Assets/Scripts/Engine/Ocg/`의 C# 선언은 ocgcore의 `ocgapi.h`, `ocgapi_types.h`, `ocgapi_constants.h`를 옮긴 것이다.

## 빌드할 때만 쓰는 것

| 이름 | 위치 | 출처 | 버전 | 라이선스 |
| --- | --- | --- | --- | --- |
| premake | `tools/.cache` (빌드할 때 받음, 저장소에 넣지 않음) | https://github.com/premake/premake-core | 5.0.0-beta2 | BSD-3-Clause |
