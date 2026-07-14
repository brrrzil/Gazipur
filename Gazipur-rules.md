# Gazipur — Правила работы (для шеринга с minimax)

> Это файл с правилами экономии, которые я выписал себе в память при онбординге
> на Unity-проект **Gazipur**. Скидываю, чтобы в другом чате сэкономить время
> и не делать аудит заново. Файл живёт рядом с проектом: `/workspace/Gazipur-rules.md`.

> **⏰ ВАЖНО для будущих итераторов:** этот файл — живой документ. После каждого
> существенного раунда правок **обновляй разделы, которые устарели**:
> - "Open problems" — закрытые проблемы удаляй, новые дописывай
> - "Бриф по проведённой работе" — добавляй новый раунд (round 6, round 7, ...)
> - "Какие файлы трогать НЕ нужно" — пополняй, если нашёл новое
> - "Куда смотреть в коде" — добавляй новые системы, если появились
>
> Если забыл — следующий итератор будет работать по устаревшим данным и наплодит
> уже починенных багов. **Не ленись обновлять!**

---

## Контекст проекта (зафиксировать сразу)

- **Жанр:** 3D first-person survival / adventure с эко-темой (свалка Gazipur).
- **Сюжет:** мать больна → собрать лекарство. Параллельно — построить фильтр воды из мусора.
- **Unity:** 6000.4.2f1 (Unity 6.4 beta), URP 17.4.0, новый Input System, Zenject DI.
- **Структура `Assets/Scripts/`:** `Craft/`, `Dialogs/`, `Environment/`, `General/`,
  `Inventory/`, `Items/`, `Market/`, `Player/`, `System/`, `UI/`, `WaterFilter/`.
- **DI-installers:** `System/ProjectInstaller.cs`, `GameInstaller.cs`, `MenuInstaller.cs`.
  `General/GameManager.cs` — DI-агрегатор.
- **GameMode (EnumData):** `outdors`, `trade`, `inventory`, `dialog`, `craft`, `storage`,
  `menu`, `die`, `otherPanels`. Управляет `General/GameModeManager.cs`.
- **Репы:**
  - рабочая: `https://github.com/brrrzil/Gazipur` (origin)
  - общий апстрим: `https://github.com/GameDevAlexandr/Gazipur` (upstream)
- **Размер репо:** ~1.3 GB. Не делать `git status` без необходимости — он медленный.

---

## Правила экономии токенов/кредитов (ОБЯЗАТЕЛЬНО)

Токены идут из подписки пользователя. Работаем экономно.

### НЕ читай без явной причины

- `Assets/Plugins/Zenject/` (~27 MB) — DI-фреймворк. Знай только API:
  `[Inject]`, `DiContainer.InstantiatePrefabForComponent`, installers.
  **Внутренности НЕ открывать.**
- `Assets/Plugins/Adobe/` (Substance) — только мета.
- `Library/`, `Logs/`, `Temp/`, `obj/` — не открывать.
- Vendor-папки: `BOXOPHOBIC/`, `Water Stylized Shader*/`, `QuickOutline/`,
  `NaughtyAttributes/`, `SimpleLocalization/`, `TextMesh Pro/`, `TutorialInfo/`,
  `Scalable Grid Prototype Materials/`, `Demigiant/`. Не лезть, пока нет
  конкретного бага в них.
- Тяжёлые текстуры/префабы (`.asset`, `.fbx`, `.png`) — только если задача требует.

### Прежде чем grep/read — спроси себя

1. Мне это точно нужно для текущей задачи или я просто любопытствую?
2. Можно ли ответить по названиям/сигнатурам, не открывая тело?
3. Если файл большой — `read` с `offset`/`limit`, не целиком.

### Дешёвые операции предпочтительнее

- `ls`, `git log`, `git ls-files`, `grep -l` (только список).
- `grep -c` для подсчёта, `grep` с лимитом — для подсчёта структуры.
- Чтение скриптов с конкретными `offset`/`limit`.

### Git

- Не делать `git status` без нужды — на 1.3 GB репо медленно.
- Если нужны изменения — `git diff --stat <path>` вместо полного `git status`.

### Коммиты/пуши

- Не коммитить и не пушить без явного запроса.
- Перед пушем — показать `git diff --stat` что именно уходит.

### Безопасность

- **GitHub PAT** пользователь передаёт явно под задачу.
  **НЕ сохранять токен в долговременной памяти.** Если нужен новый клон/операция —
  спросить у пользователя.

---

## Куда смотреть в коде (шпаргалка)

| Что хочу | Где лежит |
|---|---|
| Глобальное состояние | `General/GameManager.cs`, `System/DataManager.cs` |
| Режимы игры | `General/GameModeManager.cs`, `General/EnumData.cs` |
| Движение игрока | `Player/PlayerMovement.cs` |
| Состояние (голод/жажда/здоровье) | `Player/PlayerState.cs` |
| Инвентарь | `Inventory/Inventory.cs`, `InventoryCell.cs`, `FastCell.cs` |
| Предметы | `Items/ItemsManager.cs`, `ItemData.cs`, `ItemObject.cs`, `IUsebleItem.cs` |
| Диалоги | `Dialogs/DialogManager.cs`, `DialogStructure.cs`, `DialogAction.cs` |
| Торговля | `Market/MarketManager.cs`, `TraderObject.cs`, `BuyItemObject.cs` |
| Квесты | `General/QuestManager.cs` |
| Звук | `General/Sounds.cs`, `System/SoundControl.cs`, `System/GameSettings.cs` |
| UI | `UI/InfoPanel.cs`, `MainMenuScript.cs`, `WinDiePanel.cs`, `HoldProgressBar.cs` |
| Фильтр воды | `WaterFilter/FilterBlueprint.cs`, `Environment/WaterFilter.cs` |
| DI | `System/ProjectInstaller.cs`, `GameInstaller.cs`, `MenuInstaller.cs` |
| Сцены | `Assets/Scenes/` |
| Input | `Assets/InputSystem_Actions.inputactions` |

---

## Частые грабли проекта (фиксируются по мере обнаружения)

> Сюда дописывать конкретные баги/решения по мере работы, чтобы не наступать повторно.

- **Состояния GameMode** (`:8081` в любых системах) — это **state**, а не “позиция игрока”. Если что-то работает не так, первым делом проверять, в каком `GameMode` сейчас сцена, и как переходы `ChangeMode` запускаются/завершаются.
- **DI и `Inject`** — все `MonoBehaviour` подписываются на свои зависимости через атрибут `[Inject]`. Если что-то NRE'ит, проверь, что нужный `[Inject]` вызывается в `Start()`/`Awake()`. Сам `Inject` вручную не вызывается — его дёргает Zenject.
- **DI в инспекторе** — в скриптах `[SerializeField]` поля, привязанные в инспекторе (UI, AudioMixerGroup, Transform’ы и т.п.). Если в коде видишь `m_Foo: {fileID: 0}` — ссылка не привязана, будет NRE.
- **Голоса и AudioSource** — см. ниже “Аудио DOTween queueing”.

---

## Бриф по проведённой работе (round 1–5)

> Это сводка для будущего итератора: что было сделано, что ушло в откат, и где
> было взаимное недопонимание. Читай перед началом, чтоб не наступать повторно.

### Окружение

- **Unity 6000.4.2f1** (Unity 6.4 beta), URP 17.4.0, **новый Input System** (legacy Input отключён в Player Settings — `Input.GetKeyDown` бросает `InvalidOperationException`).
- **Zenject DI** — монобехи подписываются через `[Inject]`, не `new`.
- **DOTween** используется в UI (DialogManager, CharacterRemarks) и в плавных fade-эффектах.

### Что зафиксировано и работает (HEAD коммиты)

| Изменение | Где | Что починили |
|---|---|---|
| Удалена заставка | `Assets/Scenes/MainMenu.unity` (-305 строк), `Assets/VFX/*` (8 файлов) | Видео больше не показывается в начале |
| Skybox mipBias выровнен | `BOXOPHOBIC/.../Polyverse Skies - Night Sky.exr.meta` (`mipBias: 0 → -1`) | Убрало размытие на стыке двух кубемапов |
| SkyboxAtardecer пересобран | `Water Stylized Shader.../Textures/Skybox/Cubemap.cubemap` (новый, 6.3 МБ) | Кубик собран из `px/nx/py/ny/pz/nz.png` в RGBA32, GUID сохранён. В редакторе: `Generate Mipmaps` → Apply для получения полной мип-цепочки. |
| Audio mixer swap | `System/SoundControl.cs` (метод `ChangeMusicVolume` ↔ `ChangeSoundVolume` перепутаны были параметры `MusicVolume`/`SoundsVolume`) | Ползунки теперь рулят своими каналами |
| Slider swap в UI | `Prefabs/SettingsPanel.prefab` (`_musicVoloumeSlider: fileID 4984079079027660919`, `_soundVoloumeSlider: fileID 6968825805520622719`) | Подписи “Music/Sounds” совпадают с каналами |
| FPS lock 60 | `System/GameSettings.cs:Start()` (`QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60`) | Фреймрейт зафиксирован |
| FpsCounter | `General/FpsCounter.cs` (new) | F3 тоггл, IMGUI в углу, авто-бутстрап через `[RuntimeInitializeOnLoadMethod]` |
| Звук прыжка на приземлении | `Player/PlayerMovement.cs:HandleJump()` | `_jumpSource.Play()` в момент touchdown, не в момент отрыва |
| Шаги захардкожены | `Player/Footsteps.cs:HandleFootsteps()` | `walkingStepInterval = 0.55f`, `runningStepInterval = 0.28f` (был сломанный `InverseLerp(walkSpeed, runSpeed, ...)` с двойным умножением `currentSpeed`) |
| Голоса не отстают | `Player/CharacterRemarks.cs:PlayVoice()`, `Dialogs/DialogManager.cs:SetIteration()` | Заменена DOTween-очередь на “`Stop()` + `Play()` сразу” (см. ниже) |
| Торговец только вблизи | `Player/PlayerInteract.cs` | `SelectObject()` теперь каждый кадр перепроверяет дистанцию, `InteractObject()` требует `_isSelect` |
| Слайдер громкости не сбрасывается на 1 | `System/GameSettings.cs:Start()` | Убран `volume == 0 ? 1 : volume` — слайдер честно показывает сохранённое значение |
| Background в главном меню | `Scenes/MainMenu.unity` (fileID 1048896954) | `m_IsActive: 0 → 1` (был отключён scene-оверрайдом) |
| `LightFlicker` удалён | `Environment/LightFlicker.cs` + `.meta` | Не использовался нигде |

### Что было сделано, но ПОТОМ откачено (не повторять!)

| Идея | Что сделал | Почему отменил |
|---|---|---|
| Анкоры диалога в `Canvas.prefab` (QuestionText + 4 кнопки, `m_AnchoredPosition.x: 300 → 0`) | Считал, что весь диалог сдвинут на 300 пикселей вправо | Пользователь сказал: “возможно проблема в UI anchors в редакторе, откати и дай глянуть самому”. Проблема оказалась в лейауте префаба, не в коде. |
| `TextAnchor.MiddleCenter` в `DialogManager.Start()` | Runtime выставление alignment на `_questionText` и `GetComponentInChildren<Text>()` каждой кнопки | Тоже откатил вместе с анкорами. |
| Distance gate в `TraderObject.Update()` (остановка `_speaker.Stop()` если игрок дальше 12 м от трейдера) | Добавил `void Update()` с проверкой дистанции до `Camera.main` | Пользователь: “проблема была в state, а не в расстоянии”. Дистанция — не причина, трейдер не должен останавливать голос при удалении. Откатил. |
| `CharacterRemarks.ForceHide()` | Метод для принудительного скрытия `remark` UI | Откатил вместе с distance gate. |
| DOTween-queue fix (round 2): `_pendingVoiceSeq` + `CancelPendingVoice()` | Хранил ссылку на `DOTween.Sequence` и киллил её при `CancelPendingVoice()` | Откатил в round 3, но баг с задержкой голоса остался. Починил **по-другому** в round 5 — просто без очереди. |
| Сумки add-ёмкость (round 1): `Capacity += value` | Оригинал: `if (value < Capacity) return; Capacity = value;` | Получилось E+14 (каждый pickup одной и той же сумки вызывал `BagItem.Use()`, который `ChangeCargoValue`). Попробовал дедуп через `HashSet<ItemData> _usedToolItems` — тоже не помогло. Пользователь отменил все мои попытки и вернул оригинальный код. **Сумки — open problem, решать позже с пониманием модели** (видимо, нужна модель, где сумка потребляется при использовании, а не даёт бонус “вечно”). |
| `Inventory.CheckTool` дедупликация | Добавил `_usedToolItems.Add(item)` guard вокруг `use.Use(_manager)` | Откатил вместе с сумками. |

### Где были взаимные недопонимания

1. **Торговец-издалека vs голос-с-задержкой** — это **две разные проблемы**, я их путал.
   - **Проблема А:** “Нажимаю E на трейдера с 30 м, диалог открывается”. Это pre-existing баг в `PlayerInteract.cs` — `_isSelect` не сбрасывался при выходе из радиуса, и `InteractObject()` не проверял его. Починил в коммите `e5d80c5`.
   - **Проблема Б:** “После выхода из магазина через несколько секунд проигрывается голос ‘2 Рахул всегда поможет.mp3’”. Это DOTween-баг в `CharacterRemarks.PlayVoice()` и `DialogManager.SetIteration()` — последовательность `AppendInterval(remainingTime)` продолжала жить после `_speaker.Stop()`. Починил в коммите `686089f` заменой на `Stop() + Play()`.
   - **Ошибка ИИ:** в round 2–3 я откатил оба фикса как “одну проблему” (пользователь написал “откати всё, что связано с репликами”). Потом оказалось, что голосовой баг никуда не делся и пришлось чинить заново. **Lesson:** не откатывай фикс проблемы B, когда пользователь жалуется на проблему A.

2. **Скайбокс** — было три интерпретации:
   - “Скайбокс размытый” → пользователь тестировал `SkyboxAtardecer`, не `Night Sky` (BOXOPHOBIC). Я починил `Night Sky.exr.meta: mipBias`, но **не то**.
   - “Generate Mipmaps вызывает варнинг, кнопка Apply не появляется” → я предложил конвертировать в `Texture Shape = Cube` на `cubemap_layout.png` (modern way). Пользователь попробовал — получил авто-спрайты `px/nx/py/ny/pz/nz` в инспекторе и был сбит с толку.
   - Решение: собрал `Cubemap.cubemap` заново из 6 face-PNG в RGBA32 с правильным GUID. В редакторе: открыть Cubemap.cubemap → Generate Mipmaps → Apply (теперь работает, потому что источник корректный).

3. **Сумки** — модель не очевидна из кода. Текущее поведение: `Inventory.AddItem` для тулзов вызывает `IUsebleItem.Use(_manager)` через `CheckTool`. `BagItem.Use` прибавляет `_cargoValue` к `Capacity`. **Дыра:** вызывается при каждом pickup одного и того же предмета. Сумка не потребляется, не “съедается” — она лежит в инвентаре и продолжает давать бонус. **Открытый вопрос, требует дизайн-решения**, прежде чем чинить.

4. **Style/UX решений** — я несколько раз добавлял рантайм-фиксы (выравнивание текста, distance gate), которые в реальности были workaround'ами для неправильного лейаута в префабе. Пользователь предпочитает чинить префаб в редакторе, а не патчить в коде. **Lesson:** если проблема выглядит как лейаут, сначала спросить пользователя, готов ли он править префаб, а не накладывать рантайм-фикс.

### Какие файлы трогать НЕ нужно

- `Assets/Plugins/Zenject/` — внутренности.
- `Assets/Plugins/Adobe/` (Substance) — только мета.
- Vendor-папки в целом (`BOXOPHOBIC/`, `QuickOutline/`, `NaughtyAttributes/`, `SimpleLocalization/`, `TextMesh Pro/`, `TutorialInfo/`, `Scalable Grid Prototype Materials/`, `Demigiant/`).
- `Library/`, `Logs/`, `Temp/`, `obj/`.
- Тяжёлые `.asset/.fbx/.png` — только если задача требует.

### Какие файлы трогать МОЖНО (проектные)

- Любые `*.cs` в `Assets/Scripts/` — это всё проектный код.
- `Assets/Prefabs/`, `Assets/Scenes/`, `Assets/Settings/`, `Assets/Resources/` — через YAML-патчи, если уверен.
- `Assets/InputSystem_Actions.inputactions` — JSON-конфиг новой Input System.
- `Packages/manifest.json` — для добавления пакетов (но избегай без необходимости).

### Open problems (TODO, не решено)

- **Сумки:** нужна модель, где покупка сумки **потребляет** её (вычитается из инвентаря), а бонус `+cargoValue` остаётся. Текущее поведение: сумка лежит в инвентаре и даёт бонус навечно; повторный pickup той же сумки в текущей реализации **дублирует** бонус (см. проблему с E+14).
- **TMP-миграция** — `Assets/Scripts/` содержит 25 ссылок на `UnityEngine.UI.Text`. Плагин `SimpleLocalization` имеет `[RequireComponent(typeof(Text))]` в `LocalizedText.cs:10`. Миграция требует перебинд всех префабов + патча/замены плагина локализации. **Сделано не было**, рекомендую отдельный заход.
- **3D-аудио для NPC** — `Sounds.DialogSource` — единственный глобальный `AudioSource`, не spatial. Игрок не может “отойти” от голоса. Временное решение было — дистанционный гейт в `TraderObject.Update()`, но пользователь сказал, что проблема не в этом, и я откатил. Долгосрочно — сделать `AudioSource` на каждом NPC, спавнить голоса через `PlayClipAtPoint`.
- **Ползунок музыки в игре не двигается (регресс round 6)** — пользователь сообщил, что после round 6 in-game slider перестал реагировать на drag, хотя значение сохраняется в PlayerPrefs. K4 (Mathf.Clamp01) и начальное значение `= 0.75f` были подозрительными — откатил оба. **Если слайдер всё ещё не двигается — это отдельная проблема** (возможно, в `SettingsPanel.prefab` или в `GameSettings.cs:Start()` порядок инициализации, или `AudioMixerGroup mixer` поле потеряло бинд в инспекторе). Не расследовал дальше без подробностей от пользователя.
- **Двойная аудиосистема (menu + game)** — `MenuAudioManager` (главное меню) и `SoundControl` (игра) используют **разные** `AudioMixer` ассеты. После round 8 меню вернулось к **своему** ассету с параметрами `Music`/`Sound`, игра использует `Resources/AudioMixer.mixer` с `MusicVolume`/`SoundsVolume`/`MasterVolume`. Настройки в меню и в игре **независимы** (это by-design, так было до round 6). **Не пытаться их объединять** — два предыдущих раза (round 6 и round 7) это ломало.

---

## Бриф по проведённой работе (round 6 — аудит репозитория)

> Запустил сплошной grep-аудит по всему `Assets/Scripts/`. Нашёл 13 багов разной
> тяжести. 11 починил одним коммитом `8168b75`. 2 оставил как design-decision.

### Что починили (commit `8168b75`)

| ID | Файл | Что | Критичность |
|---|---|---|---|
| K1 | `UI/HoldProgressBar.cs` | `CompleteHold()` зацикливал удержание — при держании E у WaterFilter фильтр пересобирался бесконечно | 🔴 critical |
| K2 | `Player/Footsteps.cs` | Wet case использовал `stepDurationDirt`/`totalStepsDirt` (copy-paste) | 🔴 critical |
| K3 | `General/MenuAudioManager.cs` | Читал `MenuMusicVolume`, писал `MusicVolume` — настройки никогда не сохранялись | 🔴 critical |
| K4 | `System/SoundControl.cs` | `Mathf.Log10(0) = -Infinity` ломал `AudioMixer.SetFloat` со спамом warning'ов | 🔴 critical |
| K5 | `System/SoundControl.cs` | Громкость не персистилась в `PlayerPrefs` (см. Open problems) | 🔴 critical |
| M1 | `Dialogs/DialogManager.cs` | `SetIteration` обрезал голос ответа в `newChain` ветке | 🟡 medium |
| M2 | `Dialogs/DialogManager.cs` | `.ToArray()[0]` бросал `IndexOutOfRangeException` для неизвестного dialog type | 🟡 medium |
| M3 | `Inventory/InventoryCell.cs` | `OnDrop` в swap-ветке тихо терял overflow предметов | 🟡 medium |
| M4 | `Environment/DangerZone.cs` | `Tic()` не проверял наличие маски каждый тик | 🟡 medium |
| M5 | `Environment/DangerZone.cs` | `GetComponent<PlayerMovement>()` не находил PlayerMovement на дочерних коллайдерах | 🟡 medium |
| M6 | `Inventory/Inventory.cs` | `OnEnable` — ранний `return` делал fallback недостижимым | 🟡 medium |
| M7 | `WaterFilter/FilterBlueprint.cs` | `AddPart` бросал NRE если `part` не найден в `_parts` | 🟡 medium |
| m1 | `General/Control.cs` | `OnDisable` закомментирован — утечка подписок | 🟢 minor |
| m2 | `General/Control.cs` | `isHoldInProgress` guard закомментирован — tap и hold стреляли одновременно | 🟢 minor |
| m3 | `Inventory/InventoryCell.cs` | Лишний `transform.position = transform.position` в `OnBeginDrag` | 🟢 minor |
| m5 | `General/Control.cs` | Удалён неиспользуемый `OnMouseDownInObject` delegate | 🟢 minor |

### Что НЕ трогали (design decisions)

- **m4 `MedecineItem.Use` возвращает `false`.** По дизайну: медицина лечит **маму** при передаче (`MotherCollider.OnTriggerEnter` вызывает `_inventory.CheckMedeicine()` → `medCell.RemoveItem()` → `_quest.HealMother(true)`), а не игрока при использовании. Use-функция игрока остаётся no-op.
- **Двойная аудиосистема (menu + game)** — упомянуто в Open problems. В этом раунде только починили K3–K5 (persistence каждой системы по отдельности), но не объединяли микшеры.

### Архитектурные заметки

- **Два AudioMixer'а**: `MenuAudioManager.audioMixer` (отдельный ассет, параметры `Music`/`Sound`) и `SoundControl.mixer.audioMixer` (отдельный ассет, параметры `MusicVolume`/`SoundsVolume`/`MasterVolume`). Проверить в редакторе, если нужно объединить.
- **Багфиксы M1 и m2 могут менять UX:** tap-E больше не срабатывает во время hold-E, и голос ответа теперь полностью проигрывается перед следующим вопросом. Если окажется, что в диалоге нужна старая логика “быстрый skip”, скажи — верну как было.
- **HoldProgressBar больше не рестартит hold.** Если в каком-то месте проекта была многошаговая прогрессия (например, “зажать E 3 раза подряд”), нужно явно вызывать `StartHold()` повторно из обработчика. Сейчас в проекте только один потребитель — `WaterFilter`, и он одношаговый.

---

## Бриф по проведённой работе (round 8 — откат MenuAudioManager)

> Пользователь сообщил, что после round 6+7 ползунки музыки и звука в **главном меню** глушат друг друга. Откатил MenuAudioManager к pre-round-6 состоянию. Коммит `5f0396a`.

### Что откатил

- `Assets/Scripts/General/MenuAudioManager.cs`:
  - Ключи PlayerPrefs обратно к `MenuMusicVolume` / `MenuSoundsVolume` / `MenuMusicMuted` / `MenuSoundsMuted`.
  - Параметры миксера обратно к `Music` / `Sound` (да, это **те самые** несуществующие в `Resources/AudioMixer.mixer` параметры, на которые ругался Unity).

### Открытие (важное)

- Я в round 6+7 предполагал, что `MenuAudioManager.audioMixer` и `SoundControl.mixer.audioMixer` указывают на **один и тот же** `Resources/AudioMixer.mixer`. **Ошибся.** У меню свой AudioMixer ассет, в котором параметры называются `Music` / `Sound`. У игры — `Resources/AudioMixer.mixer` с параметрами `MusicVolume` / `SoundsVolume` / `MasterVolume`.
- Доказательство: до round 6 меню работало (ползунки не глушили друг друга), хотя код использовал `Music` / `Sound` — потому что **его миксер** содержит эти параметры.
- Раньше в этом же документе я писал “двойная аудиосистема” как open problem, но неправильно сформулировал. На самом деле:
  - **Меню**: `MenuAudioManager.audioMixer` → свой ассет с `Music`/`Sound`/`MusicMuted`/`SoundsMuted` (да, мьюты тоже есть).
  - **Игра**: `SoundControl.mixer.audioMixer` → `Resources/AudioMixer.mixer` с `MusicVolume`/`SoundsVolume`/`MasterVolume`.
  - **Они НИКОГДА не были синхронизированы** — разные ассеты, разные параметры. Настройки в меню **всегда** жили только в меню, в игре — только в игре. После моего round 6 (где я унифицировал PlayerPrefs ключи) значения начали шариться через PlayerPrefs, но **применялись в разных миксерах** — это и вызвало конфликт “слайдеры глушат друг друга” в меню: один слайдер ставил `MusicVolume` в один миксер, другой слайдер ставил `SoundsVolume` в тот же миксер (потому что в меню `MenuAudioManager.audioMixer` **оказался тем же** `Resources/AudioMixer.mixer`?? или я неправильно понимаю ситуацию).
- **Реальная природа бага не ясна.** Юзер потестил и сказал “ползунки глушат друг друга”. Я не могу воспроизвести. Возможные гипотезы:
  1. В editor `MenuAudioManager.audioMixer` указывает на `Resources/AudioMixer.mixer`, а не на свой ассет (как я думал).
  2. Или на какой-то третий миксер с единственным параметром, на котором сидят оба ползунка.
  3. Или у AudioMixer есть какой-то side-effect (например, snapshot-ы), который сбрасывает состояние.
- **Lesson:** **не предполагать** что два SerializeField AudioMixer указывают на один ассет, пока не проверишь в редакторе. В следующий раз спрашивать у юзера или grep'ать по `.prefab`/`.unity` файлам.

### Статус других изменений round 7 (не тронуто)

- K1 HoldProgressBar `loop` параметр — **ОСТАВЛЕН** (нужен для лута).
- K2 Footsteps Wet→Dirt — **ОСТАВЛЕН** (юзер сказал, Dirt-костыль звучал лучше).
- M1+M2 DialogManager — **ОСТАВЛЕН** (юзер сказал, было нормально).
- M4+M5 DangerZone — **ОСТАВЛЕН** (юзер сказал, было нормально).
- K4 SoundControl Clamp01 — **ОТКАЧЕН** в round 7 (юзер сказал, слайдер перестал двигаться; проверь после round 8 — ожил ли).
- K5 SoundControl PlayerPrefs — **ОСТАВЛЕН** (юзер подтвердил, сохраняется).

---

## Бриф по проведённой работе (round 7 — откат по фидбеку)

> После round 6 пользователь потестил в редакторе и прислал фидбек. 6 из 16
> фиксов пришлось частично или полностью откатить. Коммит `f3bea5c`.

### Что откатил

| ID | Что было | Что сделал в round 7 | Почему |
|---|---|---|---|
| K1 | `HoldProgressBar.CompleteHold()` рестартил hold | Добавил параметр `loop` в `StartHold(holdTime, loop=false)`. **GarbageObject** теперь вызывает `StartHold(time, loop:true)` — лут-бар продолжает работать как раньше. **WaterFilter** и **HoleInFance** остаются на `loop:false` — нет бесконечного цикла. | Пользователь: "Прогресс бар после первого лута пропадал. Функция была зациклена, чтобы лутать одной кнопкой несколько раз из одного префаба." |
| K2 | Wet case использовал `stepDurationWater` | Откатил к `stepDurationDirt`/`totalStepsDirt` | Пользователь: "Шаги по воде работали нормально, хоть и через костыли. Сейчас вроде бы стало хуже" |
| M1 | `DialogManager` проигрывал answer voice через корутину `PlayAnswerThenChain` | Убрал корутину и трекинг `_voiceSequence`. Восстановил исходную логику "Stop+Play ответ, сразу SetIteration следующего". | Пользователь: "M1 было нормально. Верни как было." |
| M2 | Добавил `matches.Length == 0` guard | Убрал guard (заодно с M1, один коммит) | Откатил вместе с M1 — пользователь сказал, что диалоги работают. |
| M4 + M5 | `DangerZone.Tic()` проверял маску каждый тик + `GetComponentInParent` | Откатил оба | Пользователь: "M4+M5 всё было нормально. Без маски был урон. Сейчас тоже есть. Что ты пофиксил в таком случае?" |
| K4 | `Mathf.Clamp01(value)` + эпсилон 0.0001 в `SoundControl.ChangeMusicVolume/ChangeSoundVolume` | Убрал clamp | Пользователь: "В игре перестал работать ползунок на музыку (не двигается)". Clamp был подозрительным. После отката проверить — если слайдер ожил, виноват был clamp. |

### Что оставил + доделал

| ID | Что было в round 6 | Что сделал в round 7 |
|---|---|---|
| K3 | Унифицировал ключи PlayerPrefs (`MenuMusicVolume`→`MusicVolume`, `MenuSoundsVolume`→`SoundsVolume`) | **Оставил** — это работало. Но обнаружил, что `MenuAudioManager.ApplyMusicVolume/ApplySoundsVolume` использовали **несуществующие** параметры миксера `Music`/`Sound`. Реальные параметры в `Assets/Resources/AudioMixer.mixer` — `MusicVolume`/`SoundsVolume`/`MasterVolume`. **Поправил** — теперь `MenuAudioManager` использует правильные имена. Ошибка `Exposed name does not exist` больше не будет. |
| K5 | `SoundControl` персистил в PlayerPrefs через `SaveSettings()` после каждого изменения + `OnApplicationPause/Quit` | **Оставил** — пользователь подтвердил: "Изменения сохраняются при выходе из игры". Убрал только `Mathf.Clamp01` обёртку, которая, возможно, и ломала слайдер. |

### Что НЕ затронуто (сохранено с round 6)

- M3 `InventoryCell.OnDrop` overflow — не было регрессии.
- M6 `Inventory.OnEnable` ранний return — не было регрессии.
- M7 `FilterBlueprint.AddPart` NRE — пользователь ещё не тестил.
- m1 `Control.OnDisable` — не было регрессии.
- m2 `Control.isHoldInProgress` — не было регрессии.
- m3 `InventoryCell.OnBeginDrag` redundant line — косметика.
- m5 `Control.OnMouseDownInObject` dead code — косметика.

### Открытый вопрос (новый)

- **Ползунок музыки в игре:** после round 7 (с убранным Clamp01) пользователь должен проверить, ожил ли он. Если **нет** — проблема в чём-то ещё: возможно, `[SerializeField] private AudioMixerGroup mixer` в `SoundControl` потерял бинд в инспекторе, или в `GameSettings.cs:Start()` есть конфликт. Без подробностей (значение слайдера при drag, точно ли меняется `_sounds.MusicVolume`, нет ли исключений в Console) копать дальше не имеет смысла.

---

## Бриф по проведённой работе (round 9 — MasterMuted не применяется)

> Юзер сообщил, что после round 8 в диалогах не звучат реплики гг. Проверил — `DialogManager.cs` идентичен pre-round-6, виноват не код. Реальная причина: залипшее `MasterMuted=1` в PlayerPrefs от round 6/7, которое на старте ставило `MasterVolume=-80` глобально и глушило ВСЁ (диалоги, музыку, шаги). Коммит `84942dc`.

### Что сделано

- `Assets/Scripts/System/SoundControl.cs`:
  - Убрал авто-применение `MasterMuted` в `Awake()` — громкость и звуки сохраняются между сессиями, мьют — нет (явный per-session выбор юзера).
  - **One-time cleanup**: при первом запуске после этого изменения удаляется устаревший ключ `MasterMuted` из PlayerPrefs (если есть). Это автоматический сброс залипшего состояния без ручной чистки реестра.
  - Убрал `MuteKey` из `SaveSettings()` — мёртвые данные больше не пишутся.
  - Сам метод `Mute(bool)` оставлен: мьют в рамках сессии работает, просто не персистится.

### BigTree (сделано юзером, коммит `0919285`)

Юзер сам пофиксил BigTree в редакторе. Видимо, заменил MeshCollider (без mesh'а, fileID 0) на что-то рабочее. Diff в GameScene.unity: -84/+41 строк. **Подробности не у меня — это сцена, я в неё не лезу.** Уважаю, что юзер предпочитает редактировать префабы/сцены руками.

### Lesson (финальная)

- **Не персистить `MasterMuted`** без явного запроса юзера. Любое значение != 0 в этом ключе потенциально глушит ВСЁ, и юзер может не сразу понять почему.
- **Делать one-time cleanup** устаревших PlayerPrefs ключей при изменении логики персистенса. Это спасает от ситуаций "раньше работало, после обновления звук пропал".
- **Подозревать prefab/scene setup** в первую очередь, когда юзер говорит "что-то не работает после твоих изменений". Если я не трогал `.cs` файл, упомянутый в проблеме — скорее всего, дело либо в персистенсе состояния, либо в сцене/префабе.

---

## Бриф по проведённой работе (round 10 — ревёрт + явный UI mode)

> Три задачи от юзера:
> 1. «Дело было не в аудиомикшере. Последние изменения неправильные.» — откатил round 9.
> 2. «Верни скайбокс таким, каким он был в самом начале нашего разговора» — восстановил оригинальный `Cubemap.cubemap` (50 МБ, 512×512, fileFormatVersion 2) из коммита `1264218` ("Финал", начало сессии).
> 3. «В UI-режимах отбирать управление, при возвращении в игру прятать курсор» — добавил явный `IsUIMode()` helper + новый режим `win`.
>
> Коммит `1f1fffe`.

### Что откатил

- `Assets/Scripts/System/SoundControl.cs`: вернул к round 7 (без round 9-овского MasterMuted cleanup). Юзер подтвердил, что дело было не в миксере — round 9 был ложной гипотезой.
- `Assets/Water Stylized Shader Orto & Perspective Camera/Textures/Skybox/Cubemap.cubemap` + `.meta`: вернул из коммита `1264218`. Оригинал был 50 МБ, 512×512×6 фейсов, `fileFormatVersion: 2`. Моя версия в `73f13a2` была 33 МБ, 1024×1024×6 (LANCZOS upscale), `fileFormatVersion: 3`.

### Что добавил (явный UI mode management)

1. **`EnumData.GameMode`**: добавил `win`. Раньше победа показывалась через `otherPanels` — семантически неверно, потому что `otherPanels` означает «открытая панель, не закрывающая геймплей» (блекплейн и т.п.).

2. **`GameModeManager`**:
   - Добавил `OnWin` UnityEvent и зарегистрировал в `_mods`.
   - Добавил `UIModes` HashSet и `static IsUIMode(GameMode)` — централизованный список UI-режимов. Это **замена** неявному `mode != outdors` в `PlayerMovement.SetMode`. Теперь добавление нового UI-режима = одна строчка в `UIModes`.

3. **`PlayerMovement.SetMode`**: использует `GameModeManager.IsUIMode(mode)` вместо `mode != outdors`. Поведение то же, но интент явный.

4. **`QuestManager.CompleteFilter`**: использует `GameMode.win` вместо `GameMode.otherPanels` — победа теперь явно UI-режим, управление отбирается сразу.

5. **`WinDiePanel.ContinueButton`**: помимо скрытия панели, вызывает `GameModeManager.OutDors()` — это **была реальная бага**:
   - До: нажатие Continue просто скрывало панель, режим оставался `die`/`otherPanels`, _isUIMode=true, **курсор виден, игрок не может двигаться**. Юзер застрял бы в мёртвом состоянии.
   - После: Continue скрывает панель + переключает режим на outdors + `SetMode` прячет курсор + возвращает управление.

### Урок

- **Сначала проверять, касается ли проблема моего кода.** Round 9 был построен на гипотезе «MasterMuted из PlayerPrefs залип и глушит всё» — юзер сказал, что дело было в другом. Гипотеза не подтвердилась → ревёрт.
- **Не делать предположений о сцене/префабах, если юзер не подтвердил.** В данном случае я не догадался спросить, что ещё он наблюдает (фоновая музыка? шаги?). Если бы спросил — увидел бы, что звук не везде пропал, и не полез бы в SoundControl.
- **Явный список лучше неявного.** `mode != outdors` работал, но `UIModes.Contains(mode)` явнее, и добавление нового режима = одна строчка, а не правка в двух местах.

### Lesson для round 11+

- При диагностике звуковых проблем спрашивать: **что конкретно не работает?** (голоса? музыка? шаги? все? ничего?) — это сильно сужает гипотезы.
- При добавлении UI-режима в `GameMode`: **всего одна строчка** в `UIModes` в `GameModeManager.cs`. Если забыть — будет бага типа round 6 (отсутствие мьюта в K3).
- **WinDiePanel.ContinueButton** — критичное место, легко забыть про переход в outdors. Если в будущем будут добавляться новые панели с ContinueButton — следовать тому же паттерну (Inject GameModeManager + вызвать OutDors()).

---

## Бриф по проведённой работе (round 11 — skybox + M1 вернуть)

> Юзер потестил round 10 и дал новый фидбек. Коммит `1cb63ca`.

### Что сделал

1. **Скайбокс обратно на 1024×1024.**
   - Юзер: «Верни исправленный скайбокс. Всё-таки он был нормальным.»
   - Восстановил `Cubemap.cubemap` (33 МБ, 1024×1024×6, 11 mip levels, `fileFormatVersion: 3`) из коммита `73f13a2`.
   - Юзер принял факт, что у Cubemap в Unity нет кнопки Apply — это нормально.

2. **M1 вернул, на этот раз окончательно.**
   - Юзер: «Голос персонажа так и не вернулся. Проблема не в аудиомикшере. Реплики npc воспроизводятся.»
   - Это та же бага, что я чинил в round 6 и которую юзер попросил откатить в round 7 («M1 было нормально. Верни как было.»). На самом деле бага **не ушла** — она просто была не так заметна, потому что:
     - Ответ игрока в диалоге начинал проигрываться, но мгновенно обрывался голосом следующего вопроса NPC.
     - Юзер слышал вопросы NPC, но не слышал свои ответы — и в round 7 счёл это нормальным.
     - В round 11 он пересмотрел и попросил починить.
   - **Фикс**: `PlayAnswerThenChain` корутина ждёт `answerClip.length` перед вызовом `SetIteration(nextChain)`. Теперь голос ответа проигрывается полностью.
   - **Заодно вернул M2 guard** (`matches.Length == 0` → return false) — защита от `IndexOutOfRangeException` для неизвестных dialog types. Без него NRE на любом dialog type, который не в массиве `_dialogs`.
   - **Почему юзер сначала сказал «M1 было нормально»** — вероятно, диалоги с короткими ответами (1–2 сек) казались приемлемыми. С длинными ответами обрыв стал очевиден. Или юзер просто не обратил внимание, потому что в диалоге основная информация — в вопросе NPC, а не в ответе игрока.

3. **Смержил твои scene-коммиты** (`0919285` и `83816c7`). Изменения в `GameScene.unity` — не моя зона, не лезу, уважаю.

### Lesson

- **Когда юзер говорит «X было нормально, верни как было» — уточнять, действительно ли нормально, или он просто не заметил.** M1-бага была видна с самого начала, но юзер её не идентифицировал как багу — думал, это нормальное поведение диалогов. **Если бы я в round 7 спросил «а ты слышишь голос ответа в диалоге? он не обрывается?»** — мы бы не катались туда-сюда три раунда.
- **Спрашивать про обе стороны диалога**, а не только про NPC. В диалоге два голоса — NPC (вопрос) и игрок (ответ). Если один работает, а другой нет — это и есть симптом конкретной баги.
- **Корректный фикс не становится неправильным от того, что юзер сначала не оценил.** M1 был правильным в round 6, остался правильным в round 11. Просто в round 7 юзер ещё не понял, что это бага.

---

## Бриф по проведённой работе (round 12 — Esc + DangerGarbage)

> Два фикса от юзера:
> 1. «Если выходить из режимов не на кнопку в UI, а через Esc, то курсор остаётся.»
> 2. «Если гг пытается взаимодействовать с лутом, для которого у него нет инструмента, не нужно включать прогресс-бар и пытаться его слутать, но реплика должна быть.»
>
> Коммит `56cf613`.

### Что сделал

1. **Esc → скрытие курсора (defensive).**
   - `GameModeManager.cs` `_control.OnEsc` handler:
     - После `OutDors()` явно `Cursor.lockState = Locked` и `Cursor.visible = false`.
     - Это страховка на случай, если какой-то `OnOutdors` обработчик в сцене снова показывает курсор, или если `PlayerMovement.onChangeMode` не отрабатывает.
     - Рефакторинг: заменил `if (cond1) { ...; return; } if (cond2) { ... }` на `if/else if` для ясности.
     - Добавил комментарий: `die` режим игнорирует Esc — игрок должен использовать die-панель для restart/quit.

2. **`DangerGarbageObject` — проверка инструмента до старта прогресс-бара.**
   - `Assets/Scripts/Items/DangerGarbageObject.cs`:
     - Переопределил `Intearct(bool isDown)`. Если `isDown` и `_inventory.HaveTools` не содержит `_needadTool` — играем ремарк (`noWrench`/`noHacksaw`/etc.) и выходим **до** запуска `StartHold()`.
     - Раньше прогресс-бар анимировался до конца, и только потом `PicItem` отказывал в луте. Тратилось время игрока + ощущалось как баг.
     - Проверка в `PicItem` оставлена как defense in depth (на случай, если инвентарь изменился между `Intearct` и завершением бара — например, через чит-консоль).

### Lesson

- **Defensive UI-логика** лучше, чем надежда на «правильное» срабатывание callbacks. Если что-то можно сделать в двух местах — лучше сделать в двух, чем отлаживать потом, почему одно не сработало.
- **UX баги важнее "правильного" flow.** Проверка инструмента **до** старта прогресс-бара — это +2 строки кода, но убирает раздражающее поведение у игрока (бесполезно стоит и ждёт, пока бар заполнится).
- **Не полагаться на OnEnable/OnDisable UI-панелей для cursor logic.** Сцены могут быть настроены криво, панели могут не подписываться на нужные события. Лучше явно скрывать курсор в централизованном обработчике Esc.

---

## Бриф по проведённой работе (round 13 — курсор каждый кадр)

> Юзер подтвердил, что фикс DangerGarbage работает. Курсор после Esc всё ещё не прятался — round 12 defensive hide был недостаточен. Коммит `422eff2`.

### Что сделал

`Assets/Scripts/Player/PlayerMovement.cs`:
- Добавил `EnforceCursorState()` метод, вызывается из `Update()` **каждый кадр**.
- Если `_isUIMode=true` — принудительно ставит `Cursor.lockState=None` и `Cursor.visible=true`.
- Если `_isUIMode=false` — принудительно ставит `Cursor.lockState=Locked` и `Cursor.visible=false`.
- Защита от лишних записей: сравнивает текущее состояние, перезаписывает только если реально нужно. Производительность не страдает.
- Раньше cursor ставился только в `SetMode()` (один раз при смене режима). Если что-то после этого включило курсор — UI-панель в `OnEnable`, какой-нибудь EventSystem quirk, или сцен-вайринг — он так и оставался включённым после Esc.
- Round 12 defensive hide в `OnEsc` обработчике **не помог** — какой-то код после handler'а успевал включить cursor обратно.

### Ответы на остальные вопросы юзера

3. **Cubemap >100 МБ и GitHub лимит.**
   - GitHub блокирует push файлов >100 МБ (строго). Юзер вручную меняет cubemap, видимо делает ещё больше.
   - Текущий cubemap в репе — 33 МБ (1024×1024, нормально).
   - **Варианты решения**:
     - **Git LFS** (рекомендую): `git lfs install` локально, `git lfs track \"*.cubemap\"`, `git add .gitattributes`, потом обычный `git add`/`commit`/`push`. GitHub выделит LFS-storage, файл будет в репе, но храниться отдельно. Бecплатно до 1 ГБ.
     - **Уменьшить размер**: 512×512 хватит для скайбокса, визуально разница минимальна. Будет ~8 МБ.
     - **Генерировать при build'е**: оставить только 6 face PNG (~5 МБ суммарно), cubemap собирать через `make_cubemap.py` (у нас уже есть). Но тогда юзеру придётся вручную перегенерировать после каждой правки PNG.
   - **Критично?** Да, если cubemap >100 МБ — push в origin провалится, юзер не сможет расшарить. LFS — стандартное решение для игровых ассетов.

4. **Убрать fork-зависимость от GameDevAlexandr/Gazipur.**
   - Локально: `git remote remove upstream` — отвязывает локальный git от чужого репо. **Я не буду это делать сам, это твоя локальная конфигурация.**
   - На GitHub: fork-relationship — это **метаданные репозитория**, не git-конфиг. Полностью «отвязаться» от upstream можно:
     - **Способ А** (рекомендую): создать новый пустой репо на GitHub, склонировать, скопировать все файлы (с `.git` или без), запушить. Старый `brrrzil/Gazipur` пометить archived или удалить.
     - **Способ Б**: на странице `brrrzil/Gazipur` → Settings → Danger Zone → «Fork behavior» / «Detach from upstream» — но GitHub обычно не даёт такой кнопки, нужно писать в support.
   - **Что делает upstream remote**: `git fetch upstream` подтягивает изменения из оригинала, `git merge upstream/main` мерджит их. Если ты не планируешь синхронизироваться с `GameDevAlexandr/Gazipur` — **просто удали upstream** (`git remote remove upstream`) и живи спокойно.
   - **Если upstream удалить** — **не забудь** убедиться, что `origin` указывает на `brrrzil/Gazipur` (твой форк), а не на чужой репо. Сейчас `origin` смотрит на твой — всё ок.

---

## Бриф по проведённой работе (round 14 — SoundControl default + upstream)

> Четыре пункта от юзера:
> 1. «Это не сильно нагружает игру? Если нет, то оставляем» — про `EnforceCursorState()`.
> 2. (skip, в задаче не было)
> 3. «В редакторе cubemap нормальный, но в билде он обрезан» — совет, не код.
> 4. «Сделай» — про удаление upstream remote.
> 5. «Ползунки звука и музыки стоят в нуле, хотя звуки работают» — фикс.
>
> Коммит `c9826f3`.

### Что сделал

1. **`EnforceCursorState()`** (round 13) — нагрузка **нулевая** (2 property reads + 2 сравнения/кадр; запись только при смене состояния). **Оставляем**.

3. **Cubemap в билде обрезан** — совет (не код):
   - В Inspector `Cubemap.cubemap`:
     - Снять галку **Streaming Mipmaps**
     - Поставить галку **Read/Write Enabled**
     - Убедиться что **Generate Mipmaps** включён
   - Нажать **Apply**, пересобрать билд.
   - Если не поможет — ПКМ → **Reimport** (полная переимпортизация).

4. **Upstream remote удалён.**
   - `git remote remove upstream` — отвязал `GameDevAlexandr/Gazipur`.
   - Теперь только `origin` → `brrrzil/Gazipur` (твой репо).
   - **Fork-relationship на GitHub остался** (это метаданные репо, не git-конфиг). Полная отвязка требует нового репо на GitHub. Если важно — см. round 13 brief.

5. **`SoundControl` — default 0.75 для слайдеров.**
   - **Root cause**: в round 7/9/12 я оставил `MusicVolume`/`SoundVolume` без initial value → C# default 0. Если в PlayerPrefs нет ключа (новый игрок), `MusicVolume=0`, слайдер показывает 0, **но AudioMixer param остаётся в Unity default 0 dB** (= полная громкость). Звук на максимуме, слайдер говорит «выключено». Визуально/аудио несоответствие.
   - **Фикс**:
     - `MusicVolume { get; private set; } = 0.75f` (initial value)
     - `SoundVolume { get; private set; } = 0.75f`
     - В `Awake()`: если нет ключа в PlayerPrefs — вызвать `ChangeMusicVolume(0.75f)` чтобы и mixer был на 0.75 (≈ -2.5 dB).
   - **Side effects**:
     - Существующие игроки с сохранённым MusicVolume в PlayerPrefs — **не затронуты** (их сохранённое значение грузится).
     - Новые игроки стартуют с 0.75 — нормальный уровень громкости.
     - Слайдер и микшер теперь всегда согласованы.

### Lesson

- **UI-display и runtime-state должны быть синхронизированы с самого начала.** Round 7 убрал round 6-овский `Mathf.Clamp01` initializer, чтобы не ломать слайдер (см. round 7 brief). Но это создало дыру: «значение по умолчанию» для UI-слайдера == 0, а «значение по умолчанию» для AudioMixer param == 0 dB. **Принцип**: если UI что-то показывает, runtime-значение, которое оно представляет, должно инициализироваться тем же дефолтом.
- **Когда в C# оставляешь auto-property без initial value** — не забывай, что `float` = 0, `bool` = false, `string` = null. Это часто источник «UI говорит одно, код делает другое» багов.
- **Производительность per-frame кода** — проверять сначала накладные расходы. `EnforceCursorState()` — два property reads + два сравнения. Меньше, чем `Time.deltaTime` (~30 ns). Не повод беспокоиться.

---

## Бриф по проведённой работе (round 15 — миграция скайбокса + git add)

> Три замечания от юзера:
> 1. «Ты всё это время путал Cubemap.cubemap и cubemap_layout.png» — признаю свою ошибку, это два **разных** файла.
> 2. «Но сейчас я всё сделал и это не помогло» — мой round 11 совет про Streaming Mipmaps/Read-Write не помог, потому что проблема в **другом**.
> 3. «Запушил свои последние изменения» + «git add Cubemap.cubemap не проходит» — файл в подпапке с пробелами в имени, нужен полный путь в кавычках.
>
> Коммит `e7e2806`.

### Что сделал

**SkyboxAtardecer.mat: `Cubemap.cubemap` (legacy) → `cubemap_layout.png` (modern Cube texture).**
- `Cubemap.cubemap` — это **Legacy Cubemap** (отдельный ассет, файл `.cubemap`). Unity 6 / URP 17 имеет известный баг: в билде legacy кубемап показывает только +X face.
- `cubemap_layout.png` — это **2D текстура** с `textureShape: 2` (Cube) в `.meta`. Современный подход, работает в редакторе и в билде одинаково.
- **Предупреждение в самом Inspector'е** `Cubemap.cubemap`: *«It's preferable to use Cubemap texture import type instead of Legacy Cubemap assets.»* — Unity сам говорит, что legacy не надо использовать, но я это проигнорировал в round 11. Признаю.
- Файл `Cubemap.cubemap` оставил в репе (это исходные данные, можно регенерировать из 6 face PNG). Если билд с новым материалом выглядит ок — можно удалить отдельным коммитом.

**`git add` — файл в подпапке с пробелами.**
- Команда `git add Cubemap.cubemap` не находит файл, потому что он в `Assets/Water Stylized Shader Orto & Perspective Camera/Textures/Skybox/`.
- Решение: `git add "Assets/Water Stylized Shader Orto & Perspective Camera/Textures/Skybox/Cubemap.cubemap"` (с кавычками).
- Или `git add -A` чтобы добавить всё.

### Файлы (чтоб не путаться)

- `Cubemap.cubemap` — **Legacy Cubemap** (`.cubemap` ассет, fileID 8900000, guid `3cd3fe...`). Глючит в билде Unity 6. **Больше не используется материалом.**
- `cubemap_layout.png` — **2D текстура с Texture Shape = Cube** (fileID 2800000, guid `85c7499f...`). Современный формат, работает в билде. **Теперь используется материалом.**
- `px/nx/py/ny/pz/nz.png` — 6 отдельных face-PNG, исходники для обоих форматов.
- `SkyboxAtardecer.mat` — материал скайбокса, теперь ссылается на `cubemap_layout.png`.

### Lesson

- **Не игнорировать предупреждения Unity Inspector.** В round 11 я восстановил `Cubemap.cubemap` несмотря на warning *"It's preferable to use Cubemap texture import type"*. Это было правильно по восстановлению (юзер просил), но я должен был сразу упомянуть warning и предложить миграцию на Cube texture, а не просто восстанавливать legacy формат.
- **Путаница Cubemap.cubemap vs cubemap_layout.png** — моя системная ошибка. Они действительно легко путаются: оба в одной папке Skybox, оба выглядят как «кубемап». **Файлы `.cubemap` и `.png`** — это совершенно разные ассеты с разной механикой. Сейчас задокументировано в round 15 brief, но мне самому стоит быть внимательнее.
- **«Build показывает только +X» — это сигнал legacy .cubemap в Unity 6.** Если юзер столкнётся с этим снова — сразу проверять, не использует ли материал legacy кубемап, и предлагать миграцию.

---

## Бриф по проведённой работе (round 16 — disable answer buttons)

> Три замечания от юзера:
> 1. «Посмотри мой последний коммит» — `fb31731 ReImport`, 81 файл, assetPath обновился. Skybox юзер настроил сам, мой round 15 migration в `cubemap_layout.png` откатил обратно на legacy `Cubemap.cubemap` — и это ОК, потому что у юзера получилось сделать legacy-кубемап рабочим в билде.
> 2. «Сделай в диалогах кнопку ответа неактивной на то время, пока звучит реплика гг» — фикс.
> 3. «Я всё ещё не собственник своего репозитория. Можешь убрать привязку (форк) к исходному репозиторию?» — **нельзя через CLI**, см. ниже.
>
> Коммит `11bc78d`.

### Что сделал

**Диалоги: блокировка кнопок пока звучит ответ гг.**
- `DialogManager.SetIteration()` теперь всегда выставляет `_ansverButtons[i].interactable = true` после создания/обновления кнопок. Это single source of truth для «кнопки готовы к вводу».
- Новый `DisableAnswerButtons()` helper ставит `interactable = false` на всех кнопках. Вызывается в click handler'е в ветке `newChain`.
- Корутина `PlayAnswerThenChain` вызывает `SetIteration` после проигрывания голоса → кнопки автоматически реактивируются.

**Раньше (баг):** игрок мог спам-кликать кнопки ответа, каждый клик рестартовал корутину, голос обрывался, мог перейти на неправильную ветку диалога.

### Про fork-binding (пункт 3)

**Через CLI это сделать нельзя.** GitHub не предоставляет «unfork» endpoint в API и нет кнопки «Detach from upstream» в web UI. Варианты:

**Вариант А: связаться с GitHub Support** (рекомендую)
- Перейти: https://support.github.com/contact
- Категория: «Repository management»
- Запрос: «Please detach my fork brrrzil/Gazipur from its upstream GameDevAlexandr/Gazipur. I want to keep all the code, history, and settings but make it a standalone repo.»
- Обычно делают за 1–3 рабочих дня.

**Вариант Б: миграция на новый репо** (если не хочешь ждать)
- Создать **новое** репо на GitHub (например, `brrrzil/GazipurGame` или другое имя, потому что `brrrzil/Gazipur` уже занято форком).
- Сменить `origin`:
  ```bash
  git remote set-url origin https://github.com/brrrzil/GazipurGame.git
  git push -u origin main
  ```
- В Settings → Danger Zone старого `brrrzil/Gazipur` → **Delete this repository**.
- ⚠️ **Это удалит все issues, PR, wiki, releases.** Если что-то важное — сначала экспортируй.

**Что я могу сделать прямо сейчас:**
- Сменить `origin` URL на любой другой, который ты скажешь (если выберешь вариант Б).
- **Не могу** создать новое репо на GitHub или обратиться в support за тебя.

### Lesson

- **UI-кнопки надо явно блокировать во время асинхронной работы**, иначе спам-клики ломают state. `interactable = false` — стандартный приём в Unity UI.
- **Single source of truth для состояния кнопок.** Если кнопки реактивируются в разных местах, легко забыть одно и получить «застрявшие» disabled кнопки. Я делаю SetIteration «владельцем» состояния кнопок: оно всегда выставляет `interactable = true`, click handler временно гасит.
- **GitHub fork — это метаданные, не git-конфиг.** `git remote remove upstream` отвязывает локальный git, но в GitHub UI репо по-прежнему «Forked from GameDevAlexandr/Gazipur». Полная отвязка — только через support или миграцию.

---

## Бриф по проведённой работе (round 17 — сумки в магазине по порядку)

> Юзер: «В продаже имеется три сумки. Сделай так, чтобы покупать их можно было только по порядку. Чтобы игрок не видел в продаже следующую, пока не купит текущую.»
>
> Коммит `0a6b692`.

### Что сделал

- `Assets/Scripts/Market/MarketManager.cs`:
  - Добавил поле `_bagsPurchased` (int) + персистенс через PlayerPrefs (`BagsPurchased`).
  - Список `_bagBuyObjects` (List<BuyItemObject>) хранит все bag-buy-обжекты **в порядке их добавления** (т.е. в порядке в inspector-е `_items`).
  - `RefreshBagVisibility()`: показывает только первые `_bagsPurchased+1` сумок (1, 2, 3...). Остальные скрыты.
  - Вызывается в `Start()` (после создания всех buy-обжектов) и в `HandleBagPurchased()` (после каждой покупки сумки).
  - `AddItem()`: если `item.ItemPrefab is BagItem`, подписывается на `OnBagPurchased` событие buy-обжекта.
- `Assets/Scripts/Market/BuyItemObject.cs`:
  - Новое публичное событие `OnBagPurchased` (System.Action).
  - В `Buy()` после успешной покупки: `if (_item.ItemPrefab is BagItem) OnBagPurchased?.Invoke();`
  - Не-bag предметы не затрагиваются.

### Что происходит в разных сценариях

- **Первый запуск:** `_bagsPurchased=0`, видна только сумка[0]. После покупки → `=1`, видна [0] и [1]. После ещё одной → `=2`, видны все три.
- **Сохранение между сессиями:** PlayerPrefs хранит счётчик. При следующем запуске `RefreshBagVisibility()` показывает нужное количество сумок.
- **Если у игрока уже были куплены сумки до round 17** (например, в старых сохранениях): PlayerPrefs.GetInt вернёт 0 (ключа нет), увидит только первую сумку. **Это не баг фикса, а особенность**: если хочется сохранить предыдущий прогресс, надо вручную выставить PlayerPrefs ключ. Или просто перепройти магазин.
- **Чтобы протестировать с нуля:** Edit → Clear All PlayerPrefs в Unity Editor (или удалить ключ `BagsPurchased` через реестр/файл).

### Lesson

- **Sequential unlocks** в магазине лучше делать через централизованный `Refresh*()` метод, а не разбрасывать `SetActive` по обработчикам. Иначе легко забыть обновить при изменении порядка элементов.
- **Персистенс через PlayerPrefs** с говорящим ключом (`BagsPurchased`) — простой и понятный. Не нужно городить save-систему ради одного счётчика.
- **Event-based subscription** (`OnBagPurchased += handler`) в MarketManager — BuyItemObject не знает о порядке сумок, MarketManager не знает о UI-кнопках. Clean separation.

---

## Бриф по проведённой работе (round 18 — урон от падения)

> Юзер: «Сделай так, чтобы при падении с большой высоты у игрока отнималось здоровье и воспроизводился соответствующий звук. Параметры урона и ссылку на звук вынеси в настройку в инспекторе. Скрипт выбери сам из существующих или добавь новый, если подходящих нет.»
>
> Коммит `68cab47`.

### Что сделал

`Assets/Scripts/Player/PlayerMovement.cs` — выбран как естественное место:
- Уже есть `_isGrounded` (raycast вниз каждый кадр в `CheckIfGrounded()`)
- Уже есть `_jumpSource` (AudioSource для звука прыжка) — переиспользую через `PlayOneShot`
- Уже есть CharacterController и вектор скорости — для расчёта дистанции падения хватит `transform.position.y`

**Новые поля в инспекторе** (`[Header("Fall Damage")]`):
- `_fallDamageThreshold` (float, default 3) — минимальная высота падения в метрах, выше которой начинается урон. Default подобран так, чтобы обычный прыжок (`_jumpHeight=2`) не наносил урона.
- `_fallDamagePerMeter` (float, default 10) — урон за каждый метр **сверх** порога. То есть падение с 5м: (5-3)*10 = 20 урона.
- `_fallSound` (AudioClip) — звук приземления после опасного падения.

**Новый метод `HandleFallDamage()`**:
- Вызывается в конце `Update()` после обновления `_isGrounded`.
- Отслеживает переходы grounded ↔ airborne:
  - `grounded → not grounded`: сохраняет `_fallStartY = transform.position.y`.
  - `not grounded → grounded`: `fallDistance = _fallStartY - currentY`.
  - Если `fallDistance > _fallDamageThreshold`: `_state.TakeDamage(...)` + `_jumpSource.PlayOneShot(_fallSound)`.

**Edge cases:**
- Обычный прыжок (~2м) ниже порога 3м → урон не наносится.
- Ходьба по наклону — игрок остаётся grounded, переход не регистрируется.
- Телепорт — `_fallStartY` остаётся на последней grounded позиции, фейкового урона нет.
- `_state == null` (DI ещё не инжектился) — null-check, просто пропускаем урон.
- `_fallSound == null` или `_jumpSource == null` — звук не играет, но урон наносится.

### Как включить в редакторе

1. Открыть Player GameObject в сцене.
2. В компоненте `PlayerMovement` появилась новая секция **Fall Damage**.
3. Перетащить `AudioClip` в поле **Fall Sound** (или оставить пустым для тишины).
4. По вкусу подкрутить **Threshold** (порог в метрах) и **Per Meter** (урон за метр сверх порога).

### Lesson

- **Переиспользовать существующее состояние**, а не плодить новые трекеры. `_isGrounded` уже есть — навесил на него логику fall damage, не пришлось добавлять новый raycast.
- **`PlayOneShot`** на существующем AudioSource — самый дешёвый способ добавить новый звук. Не нужно ни нового компонента, ни отдельного AudioSource, ни риска прервать текущий звук.
- **Inspector-friendly**: всё настраиваемое — в инспекторе. `[Tooltip]` объясняет каждый параметр в редакторе, не нужно лезть в код.

---

## Бриф по проведённой работе (round 19 — замедление после падения)

> Юзер: «Так же добавь замедление движения на одну секунду после такого удара о землю.»
>
> Коммит `978ca85`.

### Что сделал

Расширил round 18 fall damage двумя новыми полями в инспекторе (`[Header("Fall Damage")]`):
- `_fallSlowdownDuration` (float, default **1**) — секунды замедления после падения. 0 = замедление выключено.
- `_fallSlowdownFactor` (float, default **0.5**) — множитель скорости движения во время замедления. 0.5 = половина скорости. 0 = полная остановка.

**Логика:**
- `HandleFallDamage()` при опасном падении (fallDistance > threshold) выставляет `_slowdownEndTime = Time.time + _fallSlowdownDuration`.
- `HandleMovement()` проверяет `Time.time < _slowdownEndTime` и, если замедление активно, умножает `_currentSpeed` на `_fallSlowdownFactor`.

**По дизайну:**
- Только **опасные** падения (fallDistance > threshold) активируют замедление. Обычный прыжок (~2м) ничего не делает.
- Камеру не замедляет — игрок всё ещё может крутить головой свободно, что ощущается естественнее, чем полный стан.
- `_fallSlowdownFactor < 1` guard на случай, если юзер случайно поставит 1 (нет замедления) или >1 (ускорение, не запрашивалось, но безвредно).
- `_slowdownEndTime = 0` при старте = «нет активного замедления». `Time.time > 0` сразу, сравнение безопасно.

### Lesson

- **Замедление после получения урона — стандартный приём в играх** (Dark Souls, Hollow Knight, и т.д.). Делает урон «весомым», даёт тактильный фидбек. Не требует сложной анимации — достаточно снижения скорости.
- **Камеру не замедлять** — игрок теряет ориентацию в пространстве, если камера тоже станет вялой. Движение медленное, обзор свободный — естественно.
- **Multiplier вместо absolute speed** — `_currentSpeed *= factor` позволяет любую базовую скорость (walk/run/crouch) замедлить одинаково. Если бы я задал абсолютное значение (например, `_currentSpeed = 1.5`), то присел — получил бы ускорение.

---

## Бриф по проведённой работе (round 20 — сумки по цене + формат веса)

> Два фикса от юзера:
> 1. «Логика покупки сумок должна быть такая: сначала игрок видит самую дешёвую сумку. Он её покупает и она исчезает из магазина, появляется следующая. И так далее.»
> 2. «Иногда вес предметов в сумке отображается как дробное число с большим количеством знаков после запятой. На деле же вес любых предметов кратен 0,1.»
>
> Коммит `7b9e696`.

### Что сделал

**1. Сумки: сортировка по цене + показ по одной (заменяет round 17).**
- `Assets/Scripts/Market/MarketManager.cs`:
  - Pass 1: спавн не-bag предметов в порядке инспектора (без изменений).
  - Pass 2: собираю все bag items, сортирую по `Price` (от дешёвой к дорогой). Порядок в инспекторе **игнорируется** — прогрессия всегда cheap→expensive.
  - Pass 3: спавню сумки в отсортированном порядке, `isSingle=true` (каждая исчезает после покупки, это уже было в `BuyItemObject`).
  - `RefreshBagVisibility()`: показывает **только** сумку с индексом `_bagsPurchased`. После покупки `_bagsPurchased++`, появляется следующая.
  - **Round 17 fix был неправильным**: использовал `i <= _bagsPurchased`, что показывал N+1 сумок одновременно (можно было скипнуть дешёвую и купить дорогую). Теперь строго одна.
- `PlayerPrefs` ключ `BagsPurchased` сохранён — теперь используется как **строгий индекс**, не как порог. Старые сохранения (если были) продолжают работать.

**2. Вес: формат `F1` (1 знак после запятой).**
- `Inventory.cs`: total weight и capacity → `wgt.ToString("F1")` и `Capacity.ToString("F1")`.
- `Items/ItemInfoPanel.cs`: single item weight → `item.Weight.ToString("F1")`.
- Причина бага: float arithmetic — `0.1f + 0.1f + 0.1f = 0.30000000000000004`. По словам юзера, веса всех предметов **кратны 0.1**, так что `F1` достаточно и всегда точно.

### Lesson

- **Сортировать по бизнес-логике (цене), а не по порядку в инспекторе.** Иначе юзер должен помнить, в каком порядке он их расставил. Если сортируем по цене — прогрессия всегда «логичная» (от простого к сложному).
- **`isSingle=true` уже скрывает bag после покупки.** Не нужно дублировать `SetActive(false)` в `RefreshBagVisibility`. Достаточно показать следующую.
- **`ToString("F1")` — стандартный способ борьбы с float-noise при отображении сумм.** `0.30000000000000004` от `0.1+0.1+0.1` — классика. Если юзер говорит «числа кратны 0.1» — `F1` достаточно. Для более точных случаев есть `G9` или `R` format specifiers.

---

## Бриф по проведённой работе (round 21 — формат `0.#` + округление веса)

> Два фикса от юзера:
> 1. «Если число целое, то и после запятой не нужно ничего показывать.»
> 2. «Несмотря на то, что в сумке показано 2,9 из 3, игрок не может взять предмет весом 0,1 (хотя по отображению кажется, что может). Видимо проблема не только в отображении, но и в фактическом значении. Может его тоже округлять до одной десятой?»
>
> Коммит `b7440c6`.

### Что сделал

**1. Формат `0.#` вместо `F1`.**
- `Assets/Scripts/Inventory/Inventory.cs`: `wgt.ToString("0.#") + "/" + Capacity.ToString("0.#")`
- `Assets/Scripts/Items/ItemInfoPanel.cs`: `item.Weight.ToString("0.#")`
- Разница: `F1` всегда добавляет десятичный знак (`3.0`), `0.#` показывает дробную часть **только если она есть** (`3` → `3`, `2.9` → `2.9`, `0.3` → `0.3`).

**2. Округление фактического значения в `GetWeight()`.**
- **Root cause**: `0.1f * 29 = 2.9000000953674313` (float imprecision). Display с `F1` показывал `2.9`, но фактическое значение было 2.9000001. `AddItem` считал `cap = 3.0 - 2.9000001 = 0.0999999` и отвергал предмет весом 0.1 как «нет места».
- **Фикс**: `return Mathf.Round(res * 10f) / 10f;` в конце `GetWeight()`. Теперь фактический вес всегда чистое кратное 0.1, совпадает с display.
- Display и pickup-логика теперь согласованы — что видишь, то и проверяется.

### Lesson

- **`F1` vs `0.#`**: для чисел с фиксированной точностью (деньги, веса, проценты) `0.#` обычно лучше — не показывает лишние нули.
- **Display vs actual value** — это **две независимые вещи**. Если округляешь одно, надо округлять и другое. Round 20 фиксил только display; round 21 пофиксил actual — теперь они в sync.
- **`Mathf.Round(value * 10) / 10`** — стандартный способ округлить до 1 знака после запятой. Для 2 знаков — `* 100 / 100`. Это эффективнее, чем `Mathf.Round` с delta, потому что не зависит от масштаба.

---

## Бриф по проведённой работе (round 22 — crowbar → cutter)

> Юзер: «Предмет crowbar по факту является кусачками. Это неправильный перевод. Можешь переименовать его в clipper или cutter (как правильно называются кусачки для проволоки)?»
>
> Коммит `208ebec`.

### Что сделал

**Правильный термин — `cutter` (wire cutter).** В ассете уже было Russian display name `n_cutters` (кусачки) — оно правильное. Неправильным был **code-identifier / prefab / asset name** `crowbar` (другое значение — рычаг-гвоздодёр).

**Переименовано:**
- `Assets/Scripts/General/EnumData.cs`: `ToolsType.crowbar` → `ToolsType.cutter`
- `Assets/Scripts/Environment/MotherCollider.cs`: `ToolsType.crowbar` → `ToolsType.cutter`
- `Assets/Scripts/Market/TraderObject.cs`: `ToolsType.crowbar` → `ToolsType.cutter`
- `Assets/Prefabs/Items/Tools/Crowbar.prefab` → `Cutter.prefab` (git mv сохраняет GUID)
- `Assets/Resources/Items/Tools/Crowbar.asset` → `Cutter.asset`
- `m_Name: Crowbar` → `m_Name: Cutter` внутри обоих переименованных файлов

**Важно:**
- **Enum-позиция сохранена** (всё ещё индекс 4: `bag, wrench, hacksaw, mask, cutter, glowes, key`). Если где-то сохранены int-значения из enum (например, в PlayerPrefs/save-файлах), они остаются совместимыми.
- Russian display name `n_cutters` не менял — он был правильный.
- Scene-ссылок на Crowbar.prefab нет (проверил grep'ом), поэтому сцены править не пришлось.

### Lesson

- **Сверять code-identifier с тем, что он реально означает.** «Crowbar» и «кусачки для проволоки» — разные инструменты (один — рычаг-гвоздодёр, другой — режущие клещи). Имена в коде должны отражать фактический предмет, а не наивный перевод.
- **Display name (локализованный) ≠ identifier в коде.** Asset имеет два поля: `m_Name` (английское имя для инспектора/логов) и `<Name>k__BackingField` (локализованное, типа `n_cutters`). Они могут расходиться, и это ОК. Но code-side enum и prefab name — должны совпадать с **фактическим смыслом**.
- **`git mv` сохраняет GUID** Unity-ассета. Безопаснее, чем `mv` + ручное обновление meta.

---

## Бриф по проведённой работе (round 23 — звук сборки скиммера)

> Юзер: «Во время сборки скиммера должен звучать звук. Я не нашёл в коде этой функции. Если она есть, скажи, куда добавить. Если нет, добавь в WaterFilter.cs сериализуемое поле для звука и метод, который будет проигрывать этот звук во время зажатой кнопки 'использовать' во время сборки.»
>
> Коммит `72f7ddd`.

### Что сделал

**До этого в WaterFilter не было звука вообще.** Никаких AudioClip/AudioSource, никаких вызовов `_sounds.PlayerPlay`. Следовал паттерну из `HoleInFance.cs` и `GarbageObject.cs` (PlayerSound enum + `_sounds.PlayerPlay(sound, loop)`).

**Изменения:**
- `Assets/Scripts/General/EnumData.cs`: добавил `build` в `PlayerSound` enum.
- `Assets/Scripts/Environment/WaterFilter.cs`:
  - `[SerializeField] private EnumData.PlayerSound _buildSound` — поле, которое пользователь заполнит в инспекторе.
  - `[Inject] private Sounds _sounds` — DI аудио-сервиса (тот же, что в HoleInFance/GarbageObject).
  - `PlayBuildSound()` — приватный метод, вызывает `_sounds.PlayerPlay(_buildSound, true)` с loop=true (потому что `_makeTime` — несколько секунд).
  - `Intearct(isDown=true)`: вызывает `PlayBuildSound()` сразу после `StartHold` — звук стартует синхронно с прогресс-баром.
  - `Intearct(isDown=false)`: `_sounds.PlayerStop()` рядом с `CancelHold` — звук останавливается при отпускании кнопки.
  - `Finish()`: `_sounds.PlayerStop()` перед `CompleteFilter` — звук останавливается при успешной сборке.

**Что нужно сделать в Unity Editor (не код):**
1. Открыть проект, чтобы скрипт скомпилировался.
2. В `Sounds` prefab добавить запись в массив `_playerSounds`: `sound=build`, `clip=<звук сборки>`.
3. В сцене найти GameObject `WaterFilter`, в новом поле `_buildSound` выбрать `build` из дропдауна.

### Lesson

- **Перед добавлением новой фичи — grep существующие похожие фичи.** В этом проекте hold-progress + звук реализован в `HoleInFance.cs` (резка забора) и `GarbageObject.cs` (лут). Один и тот же паттерн, не изобретал велосипед.
- **PlayerSound enum + Sounds service** — это by-design архитектура проекта. Не добавлял отдельный AudioSource на GameObject, не добавлял AudioClip напрямую — использовал существующий роутинг через AudioMixer.
- **`isLoop=true` для hold-build** — логически правильно: звук должен крутиться, пока идёт сборка. Loop снимется при `PlayerStop` (cancel/finish).
- **Звук не стопается при deselect** — это **известное ограничение**, не фиксил в этом раунде. `WaterFilter` не переопределяет `Select(false)`. Если игрок отвернётся во время сборки, звук продолжит играть (как и сейчас, без звука, прогресс-бар тоже не отменится). Сказал юзеру — если захочет, отдельным раундом добавлю override `Select(bool)` в стиле `GarbageObject.Select`.

---

## Бриф по проведённой работе (round 24 — лекарство при подборе кусачек)

> Юзер: «Всё верно — если скипнуть диалог, то и лекарство не появится. Можно добавить этот action не на кнопку диалога, а раньше? Например на покупку кусачек.»
>
> Коммит `37f618b`.

### Что сделал

**Root cause:** `AddMedicine` action висел на кнопке ответа в итерации 1 цепочки `traderAfterBuy`. Если игрок скипал диалог или не доходил до итерации 1, экшн не срабатывал и лекарство не появлялось. **Геймплейное состояние не должно зависеть от прохождения диалога.**

**Изменения:**
- `Assets/Scripts/Market/TraderObject.cs`:
  - Добавлено `[SerializeField] private ItemData _medicine;` — в инспекторе привязать `Medecine.asset`.
  - Добавлены `[Inject] MarketManager _market` и `[Inject] QuestManager _quest`.
  - В колбэке `onTakeItem`, при `ToolType == cutter`:
    - **Сначала** добавляется лекарство в магазин + ставится `Quests.healMother = 1` (с проверкой на дубликат через `QuestsState[healMother] == 0`).
    - **Затем** запускается диалог `traderAfterBuy` (для нарратива, по-прежнему `isOneTime`).
- `Assets/Resources/Dialogs/Origin/TraderAfterCutters/TraderAfterCutters 1.asset`: убрана ссылка на action (`fileID: 0`).
- Удалён мёртвый код: `AddMedicine.cs` + `AddMedecine.prefab` (grep подтвердил: нигде больше не используются).

**В Unity Editor:** на `TraderObject` GameObject перетащить `Medecine.asset` в новое поле `_medicine`.

### Lesson

- **Геймплейное состояние ≠ нарратив.** Диалог — это сторителлинг. Если игрок скипает, его прогресс по квесту не должен ломаться. Триггеры квестов — на игровые события (подбор предмета, вход в зону, etc.), а не на клики по фразам NPC.
- **Idempotency через quest state** — простой способ защититься от дублей. Не нужно отдельное `bool` поле или `HashSet`; сам факт `QuestsState[healMother] != 0` уже означает, что экшн отработал.

### Workflow change (от юзера, round 24)

- **Не спрашивать разрешения на commit/push** — делать сразу, когда правка готова.
- **Перед каждым ответом** проверять `git log` — если были коммиты от юзера, подхватывать работу с того места, не заставляя его писать «запушь» / «коммить».
- Это правило зафиксировать в `AGENT_RULES.md` и в `gazipur-architecture` memory topic.

---

## Бриф по проведённой работе (round 25 — ошибки главного меню)

> Юзер: «При запуске игры через главное меню повылазили ошибки (на скрине). Это не все, а только те, что влезли на экран.»
>
> Скриншот с ошибками:
> - `Exposed name does not exist: Music / Sound` × несколько (AudioMixer.SetFloat)
> - `Adobe.Substance.SubstanceInputInt2 is being serialized by [SerializeReference]` (плагин)
> - `Can not play a disabled audio source` (EventSystem)
> - `The referenced script (Unknown) on this Behaviour is missing!`
> - `DontDestroyOnLoad only works for root GameObjects or components on root GameObjects`
> - `Not allowed to access vertices/normals/uv4 on mesh 'Trash' / 'Gate' / 'pCube1' (isReadable is false)`
>
> Коммит `1f656ca`.

### Что починил

**1. `MenuAudioManager.cs` — неправильные имена параметров AudioMixer.**
- `Resources/AudioMixer.mixer` экспортирует только `MasterVolume` / `SoundsVolume` / `MusicVolume`.
- `MenuAudioManager.cs` вызывал `audioMixer.SetFloat("Music"/"Sound", ...)`, и Unity спамил ошибки на каждом тике слайдера в главном меню.
- Это **регрессия из round 8**: round 7 (`f3bea5c`) фиксил именно mixer params, но round 8 (`5f0396a`) полностью откатил `MenuAudioManager`, утащив фикс обратно.
- **Фикс**: `"Music"` → `"MusicVolume"`, `"Sound"` → `"SoundsVolume"`. Совпадает с тем, что использует in-game `SoundControl.cs` (и с реально экспортированными параметрами микшера).
- **Поведение**: menu и game теперь делят живые параметры микшера, но **persisted state остаётся раздельный** (PlayerPrefs с префиксом `Menu*` — это by-design, как было до round 6).

**2. `Sounds.cs` — `DontDestroyOnLoad` на дочернем объекте.**
- `Sounds` компонент живёт на `SoundManager.prefab`, который **вложен** в `GameManager.prefab`. То есть `gameObject` (this) — **child**, не root.
- `DontDestroyOnLoad` работает только на root, поэтому спамило ошибку на каждой загрузке сцены.
- **Фикс**: `DontDestroyOnLoad(gameObject)` → `DontDestroyOnLoad(transform.root.gameObject)`. Реальный root — это `GameManager`, который в сцене root.

### Что НЕ чинил (предэкзистующее / низкий приоритет)

- **`Adobe.Substance.SubstanceInputInt2 ... [SerializeReference]`** — проблема Adobe Substance plugin, не блокирующая. Warning Unity.
- **`Can not play a disabled audio source`** — EventSystem пытается проиграть audio source, который выключен. Не блокирует.
- **`The referenced script (Unknown) on this Behaviour is missing!`** — есть старые ссылки на удалённые скрипты в сцене. Скрипты (например, какой-то `LocationChanger`) были удалены раньше, но scene всё ещё хранит m_Script GUID. Не блокирует.
- **`isReadable is false` для `Trash`/`Gate`/`pCube1`** — Unity требует `Read/Write Enabled` в import settings для мешей, к которым идёт runtime доступ (`Mesh.GetVertices/Normals/SetUVs`). Если это нужно — фиксить в .meta файлах мешей (`isReadable: 1`). Не блокирует.

### Lesson

- **`DontDestroyOnLoad` на root, всегда.** Если компонент лежит в nested prefab (как Sounds в GameManager), `gameObject` — child. Использовать `transform.root.gameObject`.
- **Имена параметров AudioMixer — single source of truth в самом mixer asset'е.** Если рефакторишь mixer (Music → MusicVolume), все `SetFloat` звонки надо обновлять синхронно. Иначе runtime-ошибки каждое касание слайдера.
- **Откат раунда 8** в `MenuAudioManager` (полный, не частичный) утащил обратно рабочий фикс mixer params. В будущем: если фикс разнородный (PlayerPrefs keys vs mixer params), разнести на 2 коммита чтобы можно было откатить только одно.

---

## Бриф по проведённой работе (round 26 — isReadable на всех .fbx)

> Юзер: «При запуске игры через главное меню повылазили ошибки (на скрине). Это не все, а только те, что влезли на экран.»
>
> Скриншот: спам `Not allowed to access vertices/normals/uv4 on mesh 'Trash' / 'rahul4S_04' / 'Gate' / 'pCube1' / 'pCube2' / 'pCylinder4' / 'pCvCylinder4' ...` от `UnityEngine.Mesh:GetVertices / GetNormals / SetUVs`.
>
> Коммит `733c43a`.

### Что сделал

**Root cause:** все 129 `.fbx.meta` файлов в `Assets/Models/` имели `isReadable: 0`. Unity 6 блокирует CPU-доступ к mesh-данным (вершины/нормали/UV) если Read/Write не включен в import settings. Любая runtime-система, которой нужен этот доступ, кидает ошибку:
- **NavMesh rebake при загрузке сцены** (`m_BuildHeightMesh: 1` в NavMeshSurface)
- **Mesh collider** при триангуляции сложных мешей
- **Процедурный mesh-код** (SetUVs, GetVertices для отладки и пр.)
- **Debug / Editor scripting** в сборке

**Фикс:** один `sed` проход — `s/^    isReadable: 0$/    isReadable: 1/g` по всем `.fbx.meta` в `Assets/Models/`. Тронута **только строка isReadable** в каждом .meta, остальное не тронуто. 127 файлов изменено.

**Trade-off:** Unity теперь держит CPU-копию mesh-данных для каждого FBX. Для 129 мелких мешей это ~единицы мегабайт. Если конкретные меши нужны не-ридбл для asset bundling / экономии памяти — откатить точечно через .meta.

### Что НЕ чинил (предэкзистующее)

- **Adobe.Substance warning** — плагин, не блокирующий.
- **EventSystem disabled audio source** — EventSystem пытается проиграть выключенный audio source.
- **Missing script (Unknown)** — старые ссылки на удалённые скрипты в сцене (типа старый `LocationChanger`).
- **«Exposed name does not exist»** — было в round 25, исправлено.

### Lesson

- **Unity 6 + isReadable по умолчанию** для импортированных mesh-ассетов = 0. Любой runtime-доступ к vertices/normals/UV = ошибка. Это глобальный настройка, которую легко забыть при импорте новых моделей.
- **Глобальный sed по .meta — это нормально**, когда поле стандартное и встречается много раз. Один проход sed быстрее и надёжнее, чем скрипт на Python.
- **Trade-off осознанный**: немного RAM за чистый error log — хороший обмен.

---

## Бриф по проведённой работе (round 27 — Zenject + bags)

> Юзер: «Опять пропадают сумки из продажи. Я каждый раз должен скидывать player prefs? Раньше такого не было.» Plus: ZenjectException при «Начать заново» после прохождения. Plus: попросил описание прелоадера.
>
> Коммит `d9cb40c`.

### Что починил (2 из 3)

**1. ZenjectException на «Начать заново»** — TryAgainButton в WinPanel и DiePanel был привязан к `SceneLoader.LoadScene(1)`. При клике Unity перезагружал game scene (1), пока у неё уже был активный SceneContext. `SceneContextRegistry.Add` → assertion failed.

Фикс: sed-ом `m_IntArgument: 1` → `0` в `Canvas.prefab` (затронуты оба TryAgainButton — Win и Die). Теперь «Начать заново» ведёт в главное меню (scene 0), оттуда Start запускает новую игру. Zenject-конфликт уходит потому что scene разные.

**Trade-off**: вместо мгновенного рестарта — экран главного меню. Это by-design в большинстве игр. Если хочешь настоящий same-scene restart — нужен `ZenjectSceneLoader` (handles context lifecycle). Зафиксировал как открытый TODO в коммите.

**2. Сумки сессионные** — убрал `PlayerPrefs.GetInt("BagsPurchased", 0)` / `SetInt / Save` в `MarketManager.cs`. Теперь `_bagsPurchased` стартует с 0 каждую сессию. Round 20-логика (одна сумка за раз внутри сессии) сохранена.

**Юзер больше не должен делать Edit → Clear All PlayerPrefs** между тестами.

### Lesson

- **Zenject SceneContext = one per scene.** Любой `LoadScene(sameSceneName)` (или `LoadScene(sameIndex)`) вызовет assertion в `SceneContextRegistry.Add`. Чтобы рестартить — нужно сначала уйти на другую сцену (например, главное меню), либо использовать `ZenjectSceneLoader`.
- **`PlayerPrefs.Save()` для одного-двух int'ов — overkill.** Если стейт нужен только в текущей сессии (bags), не персистить вообще. Если нужно кросс-сессионно — но тогда юзер должен иметь способ сбросить (debug menu / «новая игра»).

### Что НЕ сделал (проблема 2 — прелоадер)

Юзер попросил описание подхода, а не реализацию. Ниже мои рекомендации.

---

## Бриф по проведённой работе (round 28 — Preloader scene)

> Юзер выбрал Вариант A: отд. сцена `Preloader` с UI + async load.
>
> Коммит `0a790fe`.

### Что добавил

- **`Assets/Scripts/UI/Preloader.cs`** — скрипт, который в `Awake` строит Canvas + Slider + Status text программно (без YAML-канвы в .unity-файле). `LoadNextSceneRoutine` использует `allowSceneActivation = false`, ждёт `op.progress >= 0.9`, показывает 100% 0.25s, активирует сцену. Целевая сцена передаётся через PlayerPrefs-ключ `Preloader.NextScene`.
- **`Assets/Scenes/Preloader.unity`** — минимальная сцена: один GameObject «Preloader» со скриптом. Без камеры и без EventSystem (Canvas в ScreenSpaceOverlay, UI display-only).
- **`Assets/Scenes/Preloader.unity.meta`** — GUID сцены.
- **`Assets/Scripts/UI/Preloader.cs.meta`** — GUID скрипта.
- **`Assets/Scripts/UI/MainMenuScript.cs`** — `OnStartGame` теперь ставит `Preloader.NextScene = "GameScene"` и грузит `Preloader` по имени.
- **`ProjectSettings/EditorBuildSettings.asset`** — Preloader добавлен в m_Scenes, индекс 0. Build order: 0=Preloader, 1=MainMenu, 2=GameScene.

### Lesson

- **Строить UI в коде, а не в .unity-файле**, если сцена маленькая (preloader, splash). Сцена — это просто файл с GameObject и ссылкой на скрипт. Canvas/Button/Slider создаются в `Awake`. Преимущества: меньше YAML, проще поддерживать, не ломается на апгрейдах Unity, легко менять стиль.
- **`LoadScene("Name")` устойчивее чем `LoadScene(1)`** — при добавлении новой сцены в Build Settings не придётся пересчитывать индексы.
- **`op.progress` в Unity async load: 0..0.9 при загрузке, 0.9 при готовности к активации, 1.0 после активации.** Чтобы дождаться полной готовности — `allowSceneActivation = false`, ждать пока 0.9, активировать вручную.
- **Brief 100% пауза (0.25s) перед активацией** — иначе кадр 100% не видно: активация слишком быстрая. Юзер видит «что-то загрузилось», а не «дёрнулось».

### Что нужно сделать в Unity Editor (минимум)

1. Открыть проект — скрипт компилируется, сцена открывается.
2. Проверить что `Assets/Scenes/Preloader.unity` открывается (двойной клик).
3. Если Unity просит «Missing script» — перетащить `Preloader.cs` на GameObject «Preloader».
4. Play → MainMenu → нажать «Играть» → увидеть Preloader «Loading...» с прогресс-баром → автоматически загрузится GameScene.

### TODO (если захочешь дополнительно)

- Добавить Camera в Preloader (на случай если потом захочешь background image / 3D-эффект).
- Progress callback из GameScene (когда она полностью готова, не только десериализована). Полезно если GameScene делает долгий `Awake` (NavMesh, asset bundles). Сейчас активация происходит когда async говорит «готово», а не когда GameScene реально проинициализировалась.

---

## Бриф по проведённой работе (round 29 — Preloader fixup)

> Юзер поймал 2 бага в моём round 28:
> 1. CS0029 в Preloader.cs (тип `Slider` vs `SliderProxy`)
> 2. m_SceneGUID был `0000...0000`
> 3. Side effect: NaughtyAttributes «target object is null»
>
> Коммит `7cf45d7`.

### Что починил

**1. CS0029 в Preloader.cs:101** — мой косяк: поле `_progressBar` объявлено как `UnityEngine.UI.Slider`, но `CreateSliderProxy()` возвращает мой собственный `SliderProxy` (private nested class). Тип mismatch.

Фикс — одна строка: `private Slider _progressBar;` → `private SliderProxy _progressBar;`. Остальной код не сломался, т.к. `SliderProxy` имеет публичное свойство `value` (точно как у `Slider`).

**2. m_SceneGUID = `0000...0000`** — невалидный GUID. Юзер заменил на GUID скрипта (`9a6417f...`), но в репе на моей стороне всё ещё нули (видимо, его edit не persist'нулся, или сцена открывалась из кеша).

Фикс — `ff55dc98907c4f24a044c72b9590c37e` (новый уникальный GUID, не совпадает ни с GUID скрипта, ни с чем-то ещё).

**3. «Target object is null»** — side-effect от #1. NaughtyAttributes использует reflection для inspector'а, и при compile error у скрипта inspector не может найти поле. После фикса #1 и recompile warning уйдёт.

### Lesson

- **Приватный nested class vs `using` alias типа** — легко промахнуться. Если хочешь «квази-Slider», объяви поле того же типа что и возвращает helper. Помогло бы: `var _progressBar = ...;` (type inference), но Unity serialize этого не любит.
- **m_SceneGUID ≠ asset GUID** — scene имеет два GUID: 1) в .meta файле (для Build Settings и asset reference), 2) внутри .unity как `m_SceneGUID` (для cross-scene reference). Технически могут совпадать (Unity не проверяет), но чище держать их разными.
- **При создании Unity-сцен руками** через YAML — внимательно с `m_SceneGUID`. Поставь реальный GUID, а не нули или чужой asset GUID.

### Что нужно сделать в Editor

1. Recompile проекта (Unity сам пересоберёт после изменений).
2. Если сцена `Preloader.unity` не открывается — открой её, убедись что GameObject «Preloader» имеет прикреплённый `Preloader.cs`. Если script missing — перетащи.
3. Play → MainMenu → «Играть» → должен увидеть Preloader с progress bar → переход в GameScene.

---

## Бриф по проведённой работе (round 30 — Preloader NRE fix)

> Юзер поймал NRE в `Control.cs:77` при запуске через Preloader:
> ```
> NullReferenceException: Object reference not set to an instance of an object
> Control.GetInteractObjectUnderCursor () (at Assets/Scripts/General/Control.cs:77)
> Control.Update () (at Assets/Scripts/General/Control.cs:67)
> ```
>
> Коммит `4a7db2d`.

### Root cause

Я round 28 создал `Preloader.unity` **без Camera**. Но `Control` — это singleton-компонент из `GameManager` (видимо живёт между сценами), и его `Update()` продолжает работать пока активна Preloader-сцена. Внутри `GetInteractObjectUnderCursor`:
```csharp
Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
```
В Preloader сцене `Camera.main == null` (нет камеры с тегом `MainCamera`) и/или `Mouse.current == null` (touch-устройство или мышь не двигается) → NRE каждый кадр.

### Fix — две части

**1. Добавил Main Camera + AudioListener в `Preloader.unity`** (fileIDs 200000-200003):
- GameObject `Main Camera`, тег `MainCamera`
- `Camera` с clearFlags=SolidColor, чёрный фон
- `AudioListener` (без него Unity warning, и для будущих SFX нужно)

Сцена теперь «полноценная» — любой код, ожидающий `Camera.main`, работает.

**2. Null-guard в `Control.cs`** — early-return если `Camera.main` или `Mouse.current` null:
```csharp
if (Camera.main == null || Mouse.current == null)
    return null;
```
Возврат null корректен — на loading screen нечего подсвечивать, `OnSelectObject(null)` ничего не триггерит.

### Lesson

- **Каждая Unity-сцена должна иметь Main Camera**, иначе любой singleton в `Update()` с `Camera.main` упадёт.
- **Cross-scene singletons продолжают работать между сценами** — `Control` (и `Sounds`) живут за счёт DontDestroyOnLoad (или вложенности в GameManager) и их Update крутится даже когда активна сцена без нужных компонентов.
- **Defensive null-check на системные API** (`Camera.main`, `Mouse.current`, `Input.device`) — стандартная практика для кросс-устройств и кросс-сцен.
- **Не полагайся на то что другая сцена уже инициализировалась** — у тебя есть только 1-2 кадра между `LoadScene("Preloader")` и `LoadSceneAsync("GameScene")`, и за это время все твои Update() успеют натикать NRE.

### Что осталось проверить

- [ ] Двойной клик на `Preloader.unity` — камера должна появиться в иерархии
- [ ] Play → MainMenu → «Играть» — Preloader без NRE, переход в GameScene

---

## Бриф по проведённой работе (round 31 — Preloader не должен быть boot-сценой)

> Юзер: «Почему после загрузки прелоадера сразу начинается игра? Где главное меню?»
>
> Коммит `c1863b9`.

### Что было сломано

Я в round 28 допустил **две** связанные ошибки:

1. **`Preloader` стоял в `EditorBuildSettings.m_Scenes[0]`** — Unity при старте приложения сначала грузит сцену 0. То есть Preloader был boot-сценой → сразу же шёл в LoadSceneAsync → GameScene, минуя MainMenu.
2. **`Preloader._nextSceneName` дефолт = `"GameScene"`** — если PlayerPrefs пуст (первый запуск, или сцену открыли в Editor и нажали Play), fallback-цель была GameScene. Поэтому даже если бы Preloader НЕ был boot-сценой, при пустом PlayerPrefs всё равно сразу игра.

### Fix — оба сразу

**1. Build Settings переупорядочены**:
- 0 = `MainMenu` (теперь boot-сцена, юзер сразу видит меню)
- 1 = `Preloader` (промежуточная, грузится только по требованию из MainMenu)
- 2 = `GameScene` (без изменений)

**2. Дефолт `_nextSceneName = "MainMenu"`** (вместо `"GameScene"`) в двух местах для консистентности:
- `Preloader.cs:8` — значение по умолчанию в коде
- `Preloader.unity` — serialized value на MonoBehaviour

Логика теперь:
- При старте приложения → MainMenu (юзер видит меню)
- Юзер кликает «Играть» → MainMenu.OnStartGame: `PlayerPrefs["Preloader.NextScene"] = "GameScene"` → `LoadScene("Preloader")`
- Preloader читает `GameScene` из PlayerPrefs, async-грузит, показывает progress
- GameScene активна → игра

Fallback на MainMenu важен: если юзер в Editor открыл сцену Preloader и нажал Play (минуя MainMenu), он попадёт в MainMenu, а не в GameScene.

### Lesson

- **Boot-сцена (index 0) ≠ промежуточная сцена** — это разные роли. Boot видит юзер при старте приложения. Промежуточная — только при явном переходе.
- **Default value в скрипте и serialized value в сцене должны быть синхронны** — иначе при первом открытии сцены в Editor Unity подставит serialized value и default в коде не сработает. Поменял оба.
- **Промежуточные Preloader-сцены должны быть НЕ первыми в Build Settings**, иначе они становятся boot-сценой случайно.
- **Fallback в Preloader должен вести в «безопасное» место** (MainMenu), а не в самую тяжёлую сцену (GameScene). Если что-то пошло не так с PlayerPrefs — юзер попадёт в меню, а не сразу в игру.

---

## Коммуникация с пользователем (паттерны, которые я заметил)

- Пользователь **тестирует в редакторе** после моих правок и присылает список “работает / не работает / откати это”.
- Часто просит **“верни как было”** когда фикс ломает что-то ещё — не спорить, откатывать, фиксить по-другому.
- Предпочитает **минимальные точечные правки**, а не широкие рефакторы.
- Готов править префабы в редакторе, если сказать как — но плохо понимает YAML-структуру Unity-ассетов.
- При обсуждении сумок и инвентаря упоминает “state” в смысле “GameMode / phase”, не “runtime state machine” — это важно.
- Часто путает два разных бага в один (“реплики” = и диалог, и голос). Уточнять, прежде чем откатывать всё.
- Когда говорит “если не уверен — спроси”, реально хочет, чтобы я спросил.