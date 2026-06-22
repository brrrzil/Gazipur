# Gazipur — Правила работы (для шеринга с minimax)

> Это файл с правилами экономии, которые я выписал себе в память при онбординге
> на Unity-проект **Gazipur**. Скидываю, чтобы в другом чате сэкономить время
> и не делать аудит заново. Файл живёт рядом с проектом: `/workspace/Gazipur-rules.md`.

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

- …