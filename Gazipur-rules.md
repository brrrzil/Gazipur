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

## Коммуникация с пользователем (паттерны, которые я заметил)

- Пользователь **тестирует в редакторе** после моих правок и присылает список “работает / не работает / откати это”.
- Часто просит **“верни как было”** когда фикс ломает что-то ещё — не спорить, откатывать, фиксить по-другому.
- Предпочитает **минимальные точечные правки**, а не широкие рефакторы.
- Готов править префабы в редакторе, если сказать как — но плохо понимает YAML-структуру Unity-ассетов.
- При обсуждении сумок и инвентаря упоминает “state” в смысле “GameMode / phase”, не “runtime state machine” — это важно.
- Часто путает два разных бага в один (“реплики” = и диалог, и голос). Уточнять, прежде чем откатывать всё.
- Когда говорит “если не уверен — спроси”, реально хочет, чтобы я спросил.