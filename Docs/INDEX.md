# 📘 Sandbox Game — Полная документация

## Как пользоваться этой документацией

Документы пронумерованы **ФФ_НН** (фаза_номер) и расположены в порядке, в котором **рекомендуется читать и реализовывать**. Каждый файл начинает с «Что мы делаем?» и объясняет код на русском языке с примерами.

> **Принцип**: Внедряй по очереди, проверяй после каждого шага. Каждый файл — самодостаточный этап.

> **Фаза 0.5** — теория движка, собранная по [официальной документации Facepunch/sbox-docs](https://github.com/Facepunch/sbox-docs/). Прочитай её перед Фазой 1: дальше каждая фаза опирается на эти термины (`GameObject`, `Component`, `[Sync]`, `IsProxy`, Razor и т.д.).

---

## Фазы 0 и 0.5: Подготовка и основы 🏗️

| Файл | Тема |
|------|------|
| [00.01](00_01_Установка_sbox.md) | Установка s&box |
| [00.02](00_02_Создание_проекта.md) | Создание проекта |
| [00.03](00_03_Настройка_sbproj.md) | Настройка проекта (sandbox.sbproj) |
| [00.04](00_04_Настройки_физики.md) | Настройки физики |
| [00.05](00_05_Настройки_столкновений.md) | Настройки столкновений (Collision) |
| [00.06](00_06_Настройки_ввода.md) | Настройки ввода (Input) |
| [00.07](00_07_Настройки_звука.md) | Настройки звука (Mixer) |
| [00.08](00_08_Настройки_сети.md) | Настройки сети (Networking) |
| [00.09](00_09_GameObject.md) | Основы движка: GameObject и Transform |
| [00.10](00_10_GameObject_Hierarchy.md) | Иерархия GameObject: родитель и дети |
| [00.11](00_11_Tags.md) | Теги (Tags) |
| [00.12](00_12_Component.md) | Component (компонент) |
| [00.13](00_13_Component_Attributes.md) | Атрибуты компонентов |
| [00.14](00_14_Execution_Order.md) | Порядок выполнения (Execution Order) |
| [00.15](00_15_Scene_GameObjectSystem.md) | Scene и GameObjectSystem |
| [00.16](00_16_Prefabs.md) | Prefabs (префабы) |
| [00.17](00_17_Hotloading.md) | Hotloading (живая перезагрузка кода) |
| [00.18](00_18_ConVar_ConCmd.md) | ConVar и ConCmd (консоль) |
| [00.19](00_19_Input_Basics.md) | Input (ввод): основы |
| [00.20](00_20_Networking_Basics.md) | Сеть: хост, клиент, соединение |
| [00.21](00_21_Networked_Objects.md) | Сетевые объекты и NetworkHelper |
| [00.22](00_22_Ownership.md) | Ownership (владение сетевым объектом) |
| [00.23](00_23_Rpc_Messages.md) | RPC сообщения |
| [00.24](00_24_Sync_Properties.md) | Sync Properties (синхронизация свойств) |
| [00.25](00_25_Network_Events.md) | События сети (INetworkListener и др.) |
| [00.26](00_26_Razor_Basics.md) | Razor Panels: основы UI |
| [00.27](00_27_Razor_Styling.md) | Стилизация Razor Panels (SCSS) |
| [00.28](00_28_HudPainter.md) | HudPainter (немедленный рендер HUD) |
| [00.29](00_29_GameResource.md) | GameResource (кастомные ресурсы) |
| [00.30](00_30_Log_Assert.md) | Log, Assert и диагностика |

## Фаза 1: Ядро и расширения движка ⚙️

| Файл | Тема |
|------|------|
| [01.01](01_01_Assembly.md) | Глобальные пространства имён (Assembly) |
| [01.02](01_02_EngineAdditions.md) | Дополнения к движку (EngineAdditions) |
| [01.03](01_03_Extensions.md) | Расширения векторов (Extensions) |
| [01.04](01_04_Effects.md) | Система эффектов (Effects) |
| [01.05](01_05_HUD.md) | Масштабирование интерфейса (HUD) |
| [01.06](01_06_LocalData.md) | Локальное хранилище данных (LocalData) |
| [01.07](01_07_CameraSetup.md) | Настройка камеры (CameraSetup) |
| [01.08](01_08_Tracer.md) | Трассер пуль (Tracer) |
| [01.09](01_09_CookieSource.md) | Система куки (CookieSource) |
| [01.10](01_10_PackageSortMode.md) | Сортировка пакетов (PackageSortMode) |
| [01.11](01_11_DemoRecording.md) | Запись демо (DemoRecording) |
| [01.12](01_12_SelfCollisionSound.md) | Звук столкновений (SelfCollisionSound) |
| [01.13](01_13_EnvironmentShake.md) | Тряска окружения (EnvironmentShake) |
| [01.14](01_14_CameraNoise.md) | Шум камеры (CameraNoise) |

## Фаза 2: Урон и лента фрагов 💥

| Файл | Тема |
|------|------|
| [02.01](02_01_IKillSource.md) | Источник убийства (IKillSource) 🎯 |
| [02.02](02_02_IPlayerControllable.md) | Управляемые объекты (IPlayerControllable) 🚗 |
| [02.03](02_03_PlayerEvent.md) | События игрока (PlayerEvent) 📡 |
| [02.04](02_04_DamageTags.md) | Теги урона (DamageTags) 🏷️ |

## Фаза 3: Игрок (Player) 🧑

| Файл | Тема |
|------|------|
| [03.01](03_01_Player.md) | Главный компонент игрока (Player) 🧑 |
| [03.02](03_02_Player_Camera.md) | Камера игрока (Player.Camera) 🎥 |
| [03.03](03_03_Player_Ammo.md) | Патроны игрока (Player.Ammo) 🔫 |
| [03.04](03_04_Player_Undo.md) | Доступ к отмене (Player.Undo) ↩️ |
| [03.05](03_05_Player_ConsoleCommands.md) | Консольные команды игрока (Player.ConsoleCommands) ⌨️ |
| [03.06](03_06_PlayerData.md) | Данные игрока (PlayerData) 📊 |
| [03.07](03_07_PlayerStats.md) | Статистика игрока (PlayerStats) 📈 |
| [03.08](03_08_PlayerInventory.md) | Инвентарь игрока (PlayerInventory) 🎒 |
| [03.09](03_09_PlayerFlashlight.md) | Фонарик игрока (PlayerFlashlight) 🔦 |
| [03.10](03_10_PlayerFallDamage.md) | Урон от падения (PlayerFallDamage) 🦴 |
| [03.11](03_11_PlayerDamageIndicators.md) | Индикаторы урона (PlayerDamageIndicators) 🎯 |
| [03.12](03_12_PlayerGib.md) | Гибы игрока (PlayerGib) 💥 |
| [03.13](03_13_PlayerObserver.md) | Наблюдатель после смерти (PlayerObserver) 👁️ |
| [03.14](03_14_DeathCameraTarget.md) | Камера смерти (DeathCameraTarget) 💀 |
| [03.15](03_15_NoclipMoveMode.md) | Режим Noclip (NoclipMoveMode) 👻 |
| [03.16](03_16_SandboxVoice.md) | Голосовой чат (SandboxVoice) 🎤 |
| [03.17](03_17_UndoSystem.md) | Система отмены (UndoSystem) ↩️ |

## Фаза 4: Менеджер игры (GameManager) 🎮

| Файл | Тема |
|------|------|
| [04.01](04_01_GameManager.md) | Игровой менеджер (GameManager) 🎮 |
| [04.02](04_02_GameManager_Achievements.md) | Ачивки (GameManager.Achievements) 🏆 |
| [04.03](04_03_GameManager_Util.md) | Утилиты GameManager: кик игроков (GameManager.Util) 🥾 |
| [04.04](04_04_GamePreferences.md) | Игровые настройки (GamePreferences) ⚙️ |
| [04.05](04_05_ServerSettings.md) | Серверные настройки (ServerSettings) 🖥️ |

## Фаза 5: HUD и внутриигровой UI 🖼️

| Файл | Тема |
|------|------|
| [05.01](05_01_Стили_Theme_Hud.md) | Этап 05_01 — Стили Theme и Hud |
| [05.02](05_02_Vitals.md) | Этап 05_02 — Vitals (Здоровье, броня и патроны) |
| [05.03](05_03_Chat.md) | Этап 05_03 — Chat (Чат) |
| [05.04](05_04_Feed.md) | Этап 05_04 — Feed (Лента убийств) |
| [05.05](05_05_Scoreboard.md) | Этап 05_05 — Scoreboard (Таблица счёта) |
| [05.06](05_06_Nameplate.md) | Этап 05_06 — Nameplate (Табличка с именем) |
| [05.07](05_07_Voices.md) | Этап 05_07 — Voices (Индикатор голосового чата) |
| [05.08](05_08_Notices.md) | Этап 05_08 — Notices (Уведомления и подсказки) |
| [05.09](05_09_PressableHud.md) | Этап 05_09 — PressableHud (Интерактивные объекты и подсказки) |

## Фаза 6: Базовое оружие 🔫

| Файл | Тема |
|------|------|
| [06.01](06_01_BaseCarryable.md) | Базовый переносимый предмет (BaseCarryable) 🎒 |
| [06.02](06_02_BaseCarryable_Models.md) | ViewModel переносимого предмета (BaseCarryable.ViewModel) 👁️ |
| [06.03](06_03_WeaponModel.md) | Модель оружия (WeaponModel) 🔧 |
| [06.04](06_04_BaseWeapon.md) | Базовое оружие (BaseWeapon) ⚔️ |
| [06.05](06_05_BaseWeapon_Ammo.md) | Система боеприпасов (BaseWeapon.Ammo) 🔫 |
| [06.06](06_06_BaseBulletWeapon.md) | Стрелковое оружие (BaseBulletWeapon) 🔫 |
| [06.07](06_07_IronSightsWeapon.md) | Оружие с механическим прицелом (IronSightsWeapon) 🎯 |
| [06.08](06_08_MeleeWeapon.md) | Ближнее оружие (MeleeWeapon) 🗡️ |
| [06.09](06_09_Inventory_UI.md) | Этап 06_09 — Inventory UI (Панель инвентаря / хотбар) |
| [06.10](06_10_AmmoResource_Inventory.md) | Общий пул патронов (AmmoResource + AmmoInventory) 🎯 |
| [06.11](06_11_Projectile.md) | Базовый снаряд (Projectile) 🚀 |

## Фаза 7: Конкретные виды оружия 🎯

| Файл | Тема |
|------|------|
| [07.01](07_01_Colt1911.md) | Пистолет Colt 1911 (Colt1911Weapon) 🔫 |
| [07.02](07_02_Glock.md) | Пистолет Glock (GlockWeapon) 🔫 |
| [07.03](07_03_M4A1.md) | Штурмовая винтовка M4A1 (M4a1Weapon) 🔫 |
| [07.04](07_04_MP5.md) | Пистолет-пулемёт MP5 (Mp5Weapon) 🔫 |
| [07.05](07_05_Shotgun.md) | Дробовик (ShotgunWeapon) 🔫 |
| [07.06](07_06_Sniper.md) | Снайперская винтовка (SniperWeapon) 🎯 |
| [07.07](07_07_Crowbar.md) | Монтировка (CrowbarWeapon) 🔧 |
| [07.08](07_08_HandGrenade.md) | Ручная граната (HandGrenadeWeapon) 💣 |
| [07.09](07_09_CameraScreen.md) | Фотокамера (CameraWeapon) 📷 |
| [07.10](07_10_Rpg.md) | РПГ — ракетомёт (RpgWeapon + RpgProjectile) 🚀 |

## Фаза 8: Physgun 🔷

| Файл | Тема |
|------|------|
| [08.01](08_01_Physgun.md) | 🔫 Physgun — Основная логика физической пушки |
| [08.02](08_02_Physgun_Effects.md) | ✨ Physgun.Effects — Визуальные эффекты и экран физической пушки |
| [08.03](08_03_Physgun_Hud.md) | 🎯 Physgun.Hud — HUD-прицел физической пушки |
| [08.04](08_04_Physgun_Models.md) | 🖐️ PhygunViewmodel — Визуальные эффекты viewmodel физической пушки |
| [08.05](08_05_IPhysgunEvent.md) | Компонент: Событие физ-пушки (IPhysgunEvent) 🔫 |

## Фаза 9: Toolgun 🧰

| Файл | Тема |
|------|------|
| [09.01](09_01_ToolMode.md) | 🛠️ ToolMode.cs — Базовый абстрактный класс режима инструмента |
| [09.02](09_02_ToolMode_Cookies.md) | 🍪 ToolMode.Cookies.cs — Система сохранения настроек инструмента |
| [09.03](09_03_ToolMode_Effects.md) | ✨ ToolMode.Effects.cs — Эффекты стрельбы инструмента |
| [09.04](09_04_ToolMode_Helpers.md) | 🎯 ToolMode.Helpers.cs — SelectionPoint и вспомогательные методы |
| [09.05](09_05_SnapGrid.md) | 📐 ToolMode.SnapGrid.cs и SnapGrid.cs — Система привязки к сетке |
| [09.06](09_06_Toolgun.md) | 🔫 Toolgun.cs — Основной класс оружия-тулгана |
| [09.07](09_07_Toolgun_Screen_Effects.md) | 🖥️ Toolgun.Screen.cs и Toolgun.Effects.cs — Экран и эффекты тулгана |
| [09.08](09_08_IToolgunEvent.md) | Компонент: Событие тул-гана (IToolgunEvent) 🔧 |
| [09.09](09_09_ToolInfoPanel.md) | Этап 09_09 — ToolInfoPanel (Панель информации об инструменте) |

## Фаза 10: Тулы и констрейнты 🔗

| Файл | Тема |
|------|------|
| [10.01](10_01_BaseConstraint.md) | 🔗 BaseConstraintToolMode.cs — Базовый класс инструментов-соединений |
| [10.02](10_02_Rope.md) | 🐍 Rope — Инструмент «Верёвка» |
| [10.03](10_03_Elastic.md) | 🌀 Elastic — Инструмент «Резинка» (пружина) |
| [10.04](10_04_Weld.md) | 🥽 Weld — Инструмент «Сварка» |
| [10.05](10_05_BallSocket.md) | 🎱 BallSocket — Инструмент «Шарнир» |
| [10.06](10_06_Slider.md) | ➖ Slider — Инструмент «Ползунок» |
| [10.07](10_07_NoCollide.md) | ⛔ NoCollide — Инструмент «Без столкновений» |
| [10.08](10_08_Remover.md) | 🧨 Remover — Инструмент «Удалитель» |
| [10.09](10_09_Resizer.md) | 🍄 Resizer — Инструмент «Масштабирование» |
| [10.10](10_10_Mass.md) | 🍔 Mass — Инструмент «Масса» |
| [10.11](10_11_Unbreakable.md) | 🛡️ Unbreakable — Инструмент «Неразрушимость» |
| [10.12](10_12_Trail.md) | ✨ Trail — Инструмент «След» + LineDefinition |
| [10.13](10_13_Decal.md) | 🖌️ Decal — Инструмент «Декали» |
| [10.14](10_14_KeepUpright.md) | 👆🏻 KeepUpright — Инструмент «Upright» (стабилизатор поворота) |

## Фаза 11: Физические объекты и сборка 🧱

| Файл | Тема |
|------|------|
| [11.01](11_01_Balloon.md) | Воздушный шар (Balloon) 🎈 |
| [11.02](11_02_Hoverball.md) | 🎱 Инструмент Hoverball (HoverballTool) |
| [11.03](11_03_Thruster.md) | 🚀 Инструмент Thruster (ThrusterTool) |
| [11.04](11_04_Wheel.md) | 🛞 Инструмент Wheel (WheelTool) |
| [11.05](11_05_Hydraulic.md) | ⚙️ Инструмент Hydraulic (HydraulicTool) |
| [11.06](11_06_Linker.md) | Этап 11_06 — Linker (Линкер — связывание объектов) |
| [11.07](11_07_Emitter.md) | Этап 11_07 — Emitter (Эмиттер — источник частиц) |
| [11.08](11_08_Duplicator.md) | Этап 11_08 — Duplicator (Дупликатор — копирование конструкций) |

## Фаза 12: Метаданные объектов 🏷️

| Файл | Тема |
|------|------|
| [12.01](12_01_Ownable.md) | Компонент: Владение объектом (Ownable) 🛡️ |
| [12.02](12_02_ConstraintCleanup.md) | Компонент: Очистка констрейнтов (ConstraintCleanup) 🔗 |
| [12.03](12_03_ManualLink.md) | Компонент: Ручная связь (ManualLink) 🔗 |
| [12.04](12_04_MassOverride.md) | Компонент: Переопределение массы (MassOverride) ⚖️ |
| [12.05](12_05_MorphState.md) | Компонент: Состояние морфов (MorphState) 🎭 |
| [12.06](12_06_MountMetadata.md) | Компонент: Метаданные монтирования (MountMetadata) 🧩 |
| [12.07](12_07_SpawningProgress.md) | Компонент: Прогресс спавна (SpawningProgress) ✨ |

## Фаза 13: Скриптуемые энтити 📜

| Файл | Тема |
|------|------|
| [13.01](13_01_ScriptedEntity.md) | Этап 13_01 — ScriptedEntity (Пользовательская сущность) |
| [13.02](13_02_ScriptedEmitter.md) | Этап 13_02 — ScriptedEmitter (Ресурс эмиттера частиц) |
| [13.03](13_03_DynamiteEntity.md) | Этап 13_03 — DynamiteEntity (Динамит — взрывная сущность) |
| [13.04](13_04_Lights.md) | Этап 13_04 — Lights (Управляемые источники света) |
| [13.05](13_05_EmitterEntity.md) | Этап 13_05 — EmitterEntity (Компонент управления эмиттером) |
| [13.06](13_06_EntitySpawnerEntity.md) | Этап 13_06 — EntitySpawnerEntity (Спаунер сущностей) |

## Фаза 14: Меню спавна и инвентарь 🗂️

| Файл | Тема |
|------|------|
| [14.01](14_01_ISpawner.md) | Этап 14_01 — ISpawner |
| [14.02](14_02_PropSpawner.md) | Этап 14_02 — PropSpawner |
| [14.03](14_03_EntitySpawner.md) | Этап 14_03 — EntitySpawner |
| [14.04](14_04_MountSpawner.md) | Этап 14_04 — MountSpawner |
| [14.05](14_05_DuplicatorSpawner.md) | Этап 14_05 — DuplicatorSpawner |
| [14.06](14_06_SpawnerWeapon.md) | Этап 14_06 — SpawnerWeapon |
| [14.07](14_07_UI_Components.md) | Этап 14_07 — UI-компоненты меню спавна |
| [14.08](14_08_SpawnMenu.md) | Этап 14_08 — SpawnMenu |
| [14.09](14_09_SpawnMenuHost.md) | Этап 14_09 — SpawnMenuHost |
| [14.10](14_10_Props.md) | Этап 14_10 — Props |
| [14.11](14_11_Ents.md) | Этап 14_11 — Ents |
| [14.12](14_12_Spawnlists.md) | Этап 14_12 — Spawnlists |
| [14.13](14_13_Dupes.md) | Этап 14_13 — Dupes (Дубликаты) |
| [14.14](14_14_Mounts.md) | Этап 14_14 — Mounts (Подключённые игры) |
| [14.15](14_15_ToolsTab.md) | Этап 14_15 — ToolsTab и SpawnMenuModeBar |
| [14.16](14_16_SaveMenu.md) | Этап 14_16 — SaveMenu, CleanupPage, UsersPage, UtilitiesPage |
| [14.17](14_17_LocalProps.md) | Этап 14_17 — LocalProps и SpawnPageLocal (раздел «Local» в меню спавна) 📦 |

## Фаза 15: Выброшенное и подборы 📦

| Файл | Тема |
|------|------|
| [15.01](15_01_DroppedWeapon.md) | Выброшенное оружие (DroppedWeapon) 🔫 |
| [15.02](15_02_Pickups.md) | Базовый подбираемый предмет (BasePickup) 📦 |
| [15.03](15_03_InventoryPickup.md) | Подбор инвентарных предметов (InventoryPickup) 🎒 |

## Фаза 16: Элементы карты 🗺️

| Файл | Тема |
|------|------|
| [16.01](16_01_BaseToggle.md) | Базовый переключатель (BaseToggle) 🔀 |
| [16.02](16_02_Button_Door.md) | Кнопка (Button) 🔘 |
| [16.02b](16_02b_Door.md) | Дверь (Door) 🚪 |
| [16.03](16_03_FuncMover.md) | Движущийся объект (FuncMover) 🚚 |
| [16.04](16_04_Triggers.md) | Триггер толчка (TriggerPush) 🌠 |
| [16.05](16_05_MapPlayerSpawner.md) | Спавнер игроков на карте (MapPlayerSpawner) 🗺️ |

## Фаза 17: Ввод и управление 🎛️

| Файл | Тема |
|------|------|
| [17.01](17_01_ControlSystem.md) | Система управления транспортом (ControlSystem) 🚗 |
| [17.02](17_02_ClientInput.md) | ClientInput: обёртка пользовательского ввода |

## Фаза 18: Постпроцессинг и эффекты ✨

| Файл | Тема |
|------|------|
| [18.01](18_01_PostProcessResource.md) | Пост-обработка (PostProcessing) 🎨 |
| [18.02](18_02_PostProcessManager.md) | PostProcessManager: менеджер пост-обработки |
| [18.03](18_03_Effects_UI.md) | Effects UI: интерфейс управления эффектами пост-обработки |
| [18.03a](18_03a_EffectsHost.md) | Effects UI: EffectsHost (корневой контейнер) |
| [18.03b](18_03b_EffectsList.md) | Effects UI: EffectsList (список эффектов) |
| [18.03c](18_03c_EffectsProperties.md) | Effects UI: EffectsProperties (панель свойств) |

## Фаза 19: Инспектор и контекстное меню 🔍

| Файл | Тема |
|------|------|
| [19.01](19_01_ContextMenuHost.md) | Context Menu Host (хост контекстного меню) |
| [19.02](19_02_Inspector.md) | Inspector и GameObjectInspector |
| [19.03](19_03_InspectorEditor.md) | InspectorEditor и InspectorEditorAttribute |
| [19.04](19_04_ComponentHandle.md) | ComponentHandle (ручка компонента) |

## Фаза 20: UI-контролы 🎨

| Файл | Тема |
|------|------|
| [20.01](20_01_ResourceSelect.md) | Элементы управления: Выбор ресурса (ResourceSelect) 🎯 |
| [20.02](20_02_GameResourceControl.md) | Элемент управления: GameResource-контрол (GameResourceControl) 🗂️ |
| [20.03](20_03_ClientInputControl.md) | Элемент управления: Привязка ввода (ClientInputControl) 🎮 |
| [20.04](20_04_StringQueryPopup.md) | Элемент управления: Диалог ввода строки (StringQueryPopup) 💬 |
| [20.05](20_05_DragDrop.md) | Система перетаскивания (Drag & Drop) 🖱️ |
| [20.06](20_06_OwnerLabel.md) | HUD-компонент: Метка владельца (OwnerLabel) 👤 |

## Фаза 21: Редакторы (одежда, эмоции, хотбар) 👗

| Файл | Тема |
|------|------|
| [21.01](21_01_DresserEditor.md) | Редактор одежды (DresserEditor) |
| [21.02](21_02_FacePoseEditor.md) | Редактор выражения лица (FacePoseEditor) |
| [21.03](21_03_HotbarPresets.md) | Пресеты хотбара (HotbarPresetsButton) |

## Фаза 22: NPC и ИИ 🤖

| Файл | Тема |
|------|------|
| [22.01](22_01_Npc.md) | NPC: Основной класс (Npc) 🤖 |
| [22.02](22_02_Npc_Schedule_Layers.md) | NPC: Планировщик расписаний (Npc.Schedule) 📋 |
| [22.03](22_03_Npc_Debug.md) | NPC: Отладочный оверлей (Npc.Debug) 🐛 |
| [22.04](22_04_ScheduleBase_TaskBase.md) | NPC: Базовое расписание (ScheduleBase) 📅 |
| [22.05](22_05_Layers.md) | NPC: Базовый слой (BaseNpcLayer) 🧱 |
| [22.06](22_06_Senses_Speech.md) | NPC: Слой сенсоров (SensesLayer) 👁️ |
| [22.07](22_07_Tasks_Basic.md) | Базовые задачи NPC (Wait, LookAt, MoveTo) |
| [22.08](22_08_Tasks_Advanced.md) | Продвинутые задачи NPC (Say, FireWeapon, PickUpProp, DropProp) |
| [22.09](22_09_CombatNpc.md) | NPC: Боевой NPC (CombatNpc) ⚔️ |
| [22.10](22_10_ScientistNpc.md) | NPC: Учёный (ScientistNpc) 🔬 |
| [22.11](22_11_RollermineNpc.md) | NPC: Роллермайн (RollermineNpc) 🔵 |
| [22.12](22_12_SubtitleExtension.md) | Расширение субтитров (SubtitleExtension) |

## Фаза 23: Сохранение и очистка 💾

| Файл | Тема |
|------|------|
| [23.01](23_01_ISaveEvents.md) | Интерфейс событий сохранения (ISaveEvents) |
| [23.02](23_02_SaveSystem.md) | Система сохранения (SaveSystem) 💾 |
| [23.03](23_03_CleanupSystem.md) | Система очистки (CleanupSystem) 🧹 |

## Фаза 24: Служебное: баны, свободная камера, утилиты 🛠️

| Файл | Тема |
|------|------|
| [24.01](24_01_BanSystem.md) | Система банов (BanSystem) 🔨 |
| [24.02](24_02_FreeCam.md) | Свободная камера (FreeCam) 🎥 |
| [24.03](24_03_UtilityPage.md) | Базовый класс страницы утилит (UtilityPage) 🧰 |

## Фаза 25: Финал: сборка и проверка 🏁

| Файл | Тема |
|------|------|
| [25.01](25_01_Editor_Assembly.md) | Editor/Assembly.cs (глобальные using для редактора) |
| [25.02](25_02_Scenes_Prefabs.md) | Сцены и префабы |
| [25.03](25_03_Final_Check.md) | Итоговая проверка |

## Фаза 26: Углублённые основы движка s&box (продолжение Фазы 0.5) 📚

> Дополнение к Фазе 0.5: темы из [официальной документации Facepunch](https://github.com/Facepunch/sbox-docs/), не раскрытые или раскрытые лишь поверхностно. Можно читать в любом порядке как справочник; каждый файл самодостаточен.

| Файл | Тема |
|------|------|
| [26.01](26_01_Physics_Bodies.md) | Физика: PhysicsBody, тела и формы 🪨 |
| [26.02](26_02_Tracing_Ray.md) | Трассировка: Scene.Trace и луч (Ray) 🎯 |
| [26.03](26_03_Tracing_Shapes.md) | Трассировка: Sphere/Box/Sweep и фильтры |
| [26.04](26_04_Physics_Events.md) | События физики: OnCollisionStart/Update/Stop 💥 |
| [26.05](26_05_Triggers.md) | Триггеры: OnTriggerEnter/Exit 🚪 |
| [26.06](26_06_Sound.md) | Звук: SoundEvent и Sound.Play 🔊 |
| [26.07](26_07_Math_Types.md) | Математические типы: Vector3, Rotation, Angles ➕ |
| [26.08](26_08_Curve_Easing.md) | Curve и интерполяция (Easing, Lerp) 📈 |
| [26.09](26_09_Color.md) | Color и цветовые пространства 🎨 |
| [26.10](26_10_Async_Components.md) | Async/Task в компонентах ⏱️ |
| [26.11](26_11_Scene_Events.md) | Сценовые события: ISceneStartup, IGameObjectNetworkEvents 🚦 |
| [26.12](26_12_Http_Requests.md) | HTTP-запросы (Http.RequestAsync) 🌐 |
| [26.13](26_13_WebSockets.md) | WebSockets 🔌 |
| [26.14](26_14_Network_Visibility.md) | Сетевая видимость (Network Visibility) 👁️ |
| [26.15](26_15_Localization.md) | Локализация UI (`#token`, словари) 💬 |
| [26.16](26_16_VirtualGrid.md) | VirtualGrid: большие списки в Razor 🧮 |
| [26.17](26_17_NavMesh.md) | NavMesh: основы и генерация 🗺️ |
| [26.18](26_18_NavMesh_Agent.md) | NavMeshAgent: движение по навмешу 🛞 |
| [26.19](26_19_Terrain.md) | Terrain: рельеф 🌄 |
| [26.20](26_20_ActionGraph.md) | ActionGraph: введение 🍝 |
| [26.21](26_21_Custom_Assets.md) | Custom-ассеты и GameResource-расширения 📦 |
| [26.22](26_22_Editor_Extensions.md) | Editor-расширения: основы 🛠️ |
| [26.23](26_23_Property_Attributes.md) | Property Attributes: полный справочник 🏷️ |
| [26.24](26_24_Services.md) | Сервисы: Achievements, Leaderboards, Stats 🏆 |

---

## Полезные ссылки

- [Facepunch/sbox-docs](https://github.com/Facepunch/sbox-docs/) — официальная документация s&box
- [sbox.game](https://sbox.game/) — загрузчик, мастерская, документация API
- [README.md](README.md) — краткий обзор репозитория
