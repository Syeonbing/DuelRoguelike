# 외부 코드와 라이선스

이 프로젝트(AGPL-3.0)가 가져다 쓰는 외부 코드의 출처와 라이선스를 기록한다. 버전을 올리면 이 표도 함께 고친다.

## 게임에 들어가는 것

| 이름 | 위치 | 출처 | 버전 | 라이선스 |
| --- | --- | --- | --- | --- |
| ygopro-core (EDOPro 코어, ocgcore) | `External/ygopro-core` (Git 서브모듈), 빌드 결과 `Assets/Plugins/x86_64/ocgcore.dll` | https://github.com/edo9300/ygopro-core | master `c2e0b69` (2026-11-02, v11.0 태그 이후), C API 11.0 | AGPL-3.0-or-later |
| Lua | `External/ygopro-core/lua/src` (ygopro-core 안의 서브모듈) | https://github.com/lua/lua | 5.4.8 (`6e22fed`) | MIT |
| Project Ignis CardScripts (카드 효과 Lua 스크립트) | `External/CardScripts` (얕은 Git 서브모듈) | https://github.com/ProjectIgnis/CardScripts | master `6a59aedb` (2026-11-07) | AGPL-3.0-or-later |
| Project Ignis BabelCDB (카드 DB, SQLite .cdb) | `External/BabelCDB` (얕은 Git 서브모듈) | https://github.com/ProjectIgnis/BabelCDB | master `85c0539` (2026-11-07) | 명시 없음 (저장소에 LICENSE 파일과 라이선스 안내가 없다) |
| EDOPro 한글 패치 (한국어 카드 DB, strings.conf) | `External/edopro-korean` (얕은 Git 서브모듈) | https://github.com/Team-AllYGOPro/edopro-korean | main `2e11436` (2026-10-29) | AGPL-3.0 |
| SQLite | `Assets/Plugins/x86_64/sqlite3.dll` (`tools/fetch-sqlite.ps1`로 받음, 저장소에 넣지 않음) | https://www.sqlite.org | 3.54.1 (공식 64비트 DLL) | 퍼블릭 도메인 |
| WindBot Ignite (상대 AI) | `External/windbot` (얕은 Git 서브모듈), 그중 AI 본체를 `Assets/ThirdParty/WindBot/Upstream`에 복사 (`tools/import-windbot.ps1`) | https://github.com/ProjectIgnis/windbot | master `bffe6b6` (2026-08-27) | AGPL-3.0-or-later (원래 MIT였던 IceYGO WindBot 고지 포함, `Assets/ThirdParty/WindBot/LICENSE.txt`) |
| Pretendard (화면 글꼴) | `Assets/UI/Resources/Fonts` (`Pretendard-Regular.ttf`, `Pretendard-Bold.ttf`, `Pretendard-Black.ttf`) | https://github.com/orioncactus/pretendard (`packages/pretendard/dist/public/static/alternative`) | v1.3.9 | SIL Open Font License 1.1 (`Assets/UI/Resources/Fonts/Pretendard-LICENSE.txt`) |

- ocgcore.dll은 저장소에 넣지 않고 `tools/build-ocgcore.ps1`로 위 소스에서 빌드한다. Lua는 ocgcore.dll 안에 정적으로 링크된다.
- WindBot은 `ExecutorBase`(GameAI, Executor, DefaultExecutor, 필드 모델)만 고치지 않고 복사해 쓴다. 카드 DB 읽기(`NamedCardsManager.cs`, `CardsManager.cs`)와 대사(`Dialogs.cs`)는 `Assets/ThirdParty/WindBot/Replacements`의 대체 파일로 바꿨다. 네트워크 처리(`Game/GameBehavior.cs`)는 `Assets/Scripts/AI/WindBotPlayer.cs`로 옮겨 네트워크 대신 엔진 메시지를 직접 넣는다.
- `Assets/Scripts/Engine/Ocg/`의 C# 선언은 ocgcore의 `ocgapi.h`, `ocgapi_types.h`, `ocgapi_constants.h`를 옮긴 것이다.
- BabelCDB와 EDOPro 한글 패치의 카드 텍스트(카드명·효과문)는 원작 카드의 텍스트다. 이 저장소에는 복사하지 않고 서브모듈로만 가리킨다.
- 화면의 한글 카드명·효과문·선택지 문구는 EDOPro 한글 패치의 `cards.cdb`를 카드 ID별로 덮어써서 쓴다 (엔진 수치는 BabelCDB 그대로). 시스템 문구·승리 이유는 같은 패치의 `strings.conf`를 쓴다.
- Pretendard 글꼴 파일은 고치지 않고 그대로 넣었다. Pretendard에 없는 기호는 실행 중에 Windows 기본 글꼴(맑은 고딕)에서 찾는다 (글꼴 파일은 넣지 않는다).
- 카드 그림 칸의 픽토그램(`Assets/UI/Resources/Pictograms.txt`)과 카드 틀·뒷면은 이 프로젝트에서 직접 그린 것이다 (저장소 라이선스를 따른다).

## 소리

효과음은 `Assets/UI/Resources/Audio/Sfx`에, 배경음은 `Assets/UI/Resources/Audio/Music`에 있다. 파일은 고치지 않고 이름만 바꿔 넣었다. 효과음의 길이와 크기는 실행 중에 `Assets/Scripts/UI/Sound.cs`가 줄인다.

### 배경음

두 곡 모두 OpenGameArt에서 받았다. 지금 곡은 임시로 고른 것이다.

| 파일 | 쓰는 곳 | 원래 제목 (원래 파일) | 만든 사람 | 출처 | 라이선스 |
| --- | --- | --- | --- | --- | --- |
| `tower.ogg` | 처음 화면, 탑 | Magic Puzzle Title (`magic_puzzle_title_bpm100_0.ogg`) | MintoDog | https://opengameart.org/content/magic-puzzle-title | CC0 1.0 |
| `duel.mp3` | 듀얼 | Get Ready (`Komiku - Tale on the Late - 06 Get Ready_0.mp3`) | Komiku (Loyalty Freak Music) | https://opengameart.org/content/get-ready | CC0 1.0 |

### Freesound

Freesound 파일은 원본 대신 Freesound가 만든 고음질 미리듣기(.ogg)를 받았다. 위의 12개(`draw`~`dice`)는 EDOPro 기본 효과음과 같은 소리다. EDOPro 효과음 팩의 출처 목록(https://github.com/Lahrenheit/EDOPRO-Soundpack 의 CREDITS)에서 찾았다.

| 파일 | 원래 제목 | 만든 사람 | 출처 | 라이선스 |
| --- | --- | --- | --- | --- |
| `draw.ogg` | Sliding Envelope.wav | MTJohnson | https://freesound.org/people/MTJohnson/sounds/444431/ | CC0 1.0 |
| `set.ogg` | Paper slide | Kingofgamers_cz | https://freesound.org/people/Kingofgamers_cz/sounds/352914/ | CC0 1.0 |
| `flip.ogg` | flipCard.wav | Splashdust | https://freesound.org/people/Splashdust/sounds/84322/ | CC0 1.0 |
| `shuffle.ogg` | Riffle Card Shuffle | Kodack | https://freesound.org/people/Kodack/sounds/256508/ | CC0 1.0 |
| `summon.ogg` | Vocal_chord.wav | cellokratzer | https://freesound.org/people/cellokratzer/sounds/502947/ | CC0 1.0 |
| `attack.ogg` | Draw sword#1.wav | fielastro | https://freesound.org/people/fielastro/sounds/423935/ | CC0 1.0 |
| `damage.ogg` | Nasty Knife Stab 2.wav | Aris621 | https://freesound.org/people/Aris621/sounds/478145/ | **CC BY 4.0** |
| `recover.ogg` | jingle2 | soneproject | https://freesound.org/people/soneproject/sounds/255101/ | CC0 1.0 |
| `turn.ogg` | Sound5.mp3 | Kastenfrosch | https://freesound.org/people/Kastenfrosch/sounds/162471/ | CC0 1.0 |
| `phase.ogg` | Fast swipe.wav | ParadoxTheSock | https://freesound.org/people/ParadoxTheSock/sounds/412595/ | CC0 1.0 |
| `coin.ogg` | Coin Flip | bone666138 | https://freesound.org/people/bone666138/sounds/198877/ | **CC BY 4.0** |
| `dice.ogg` | Dices 3 | Aiwha | https://freesound.org/people/Aiwha/sounds/250714/ | **CC BY 4.0** |
| `destroy.ogg` | Glass Break | unfa | https://freesound.org/people/unfa/sounds/221528/ | CC0 1.0 |
| `banish.ogg` | Backwards Whoosh | SlavicMagic | https://freesound.org/people/SlavicMagic/sounds/446010/ | CC0 1.0 |
| `special-summon.ogg` | GLEAM-GLOW-SFX-CHIME.wav | newagesoup | https://freesound.org/people/newagesoup/sounds/351408/ | CC0 1.0 |
| `win.ogg` | Angelic Fanfare 2 | qubodup | https://freesound.org/people/qubodup/sounds/812468/ | CC0 1.0 |
| `gong.ogg` | Deep Gong Tolling.mp3 | Yin_Yang_Jake007 | https://freesound.org/people/Yin_Yang_Jake007/sounds/415200/ | CC0 1.0 |
| `arrive.ogg` | Pressure Plate | proolsen | https://freesound.org/people/proolsen/sounds/466272/ | CC0 1.0 |
| `heartbeat.ogg` | Single Hearbeat (Deep Bass Kick) | unfa | https://freesound.org/people/unfa/sounds/217456/ | CC0 1.0 |
| `hit-punch.ogg` | Punch (2) | johnfolker | https://freesound.org/people/johnfolker/sounds/269232/ | CC0 1.0 |
| `hit-boom.ogg` | FX Cinematic Impact.wav | Johnnie_Holiday | https://freesound.org/people/Johnnie_Holiday/sounds/671375/ | CC0 1.0 |

- CC BY 4.0 파일 3개는 위 표의 만든 사람·출처·라이선스 표기가 저작자 표시다 (https://creativecommons.org/licenses/by/4.0/). 게임 안에 크레딧 화면을 만들면 거기에도 같은 내용을 넣는다.

### Kenney

모두 Kenney(Kenney Vleugels, https://kenney.nl)가 만든 CC0 1.0 효과음 팩이다. OpenGameArt에 올라온 같은 팩을 받았다.

| 파일 | 팩의 원래 파일 | 팩 | 출처 |
| --- | --- | --- | --- |
| `activate.ogg` | `card-shove-2.ogg` | Casino Audio | https://kenney.nl/assets/casino-audio , https://opengameart.org/content/54-casino-sound-effects-cards-dice-chips |
| `grave-1.ogg`, `grave-2.ogg` | `card-slide-3.ogg`, `card-slide-4.ogg` | Casino Audio | 위와 같음 |
| `slide-1.ogg`, `slide-2.ogg` | `card-slide-6.ogg`, `card-slide-7.ogg` | Casino Audio | 위와 같음 |
| `ui-confirm.ogg` | `chip-lay-1.ogg` | Casino Audio | 위와 같음 |
| `target.ogg` | `metalClick.ogg` | RPG Audio | https://kenney.nl/assets/rpg-audio , https://opengameart.org/content/50-rpg-sound-effects |
| `equip.ogg` | `metalLatch.ogg` | RPG Audio | 위와 같음 |
| `step-1.ogg`~`step-4.ogg` | `footstep00.ogg`, `footstep02.ogg`, `footstep04.ogg`, `footstep06.ogg` | RPG Audio | 위와 같음 |
| `ui-open.ogg` | `bookFlip2.ogg` | RPG Audio | 위와 같음 |
| `ui-move.ogg` | `tick_002.ogg` | Interface Sounds | https://kenney.nl/assets/interface-sounds , https://opengameart.org/content/interface-sounds |
| `ui-cancel.ogg` | `back_001.ogg` | Interface Sounds | 위와 같음 |

## 빌드할 때만 쓰는 것

| 이름 | 위치 | 출처 | 버전 | 라이선스 |
| --- | --- | --- | --- | --- |
| premake | `tools/.cache` (빌드할 때 받음, 저장소에 넣지 않음) | https://github.com/premake/premake-core | 5.0.0-beta2 | BSD-3-Clause |
