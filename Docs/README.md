# 📖 Пошаговое руководство: Создание Sandbox с нуля

## Что это?

Это полное, пошаговое руководство по созданию точной копии игры **Sandbox** для движка **s&box** (Source 2).  
Руководство написано для тех, кто **никогда раньше не программировал**.

## Как пользоваться

1. **Читай по порядку** — каждый этап опирается на предыдущий
2. **Создавай файлы** — после каждого подэтапа ты можешь запустить игру и проверить
3. **Не пропускай** — если что-то непонятно, перечитай предыдущий этап
4. **Код копируй полностью** — каждый файл приведён целиком, строка в строку

## Структура руководства

### Фаза 0: Подготовка
- [00_01 — Установка s&box](00_01_Установка_sbox.md)
- [00_02 — Создание проекта](00_02_Создание_проекта.md)
- [00_03 — Настройка проекта (sbproj)](00_03_Настройка_sbproj.md)
- [00_04 — Настройки физики](00_04_Настройки_физики.md)
- [00_05 — Настройки столкновений](00_05_Настройки_столкновений.md)
- [00_06 — Настройки ввода](00_06_Настройки_ввода.md)
- [00_07 — Настройки звука](00_07_Настройки_звука.md)
- [00_08 — Настройки сети](00_08_Настройки_сети.md)

### Фаза 0.5: Основы движка s&box 🧠

> Теоретический блок по мотивам [официальной документации Facepunch](https://github.com/Facepunch/sbox-docs/). На этот фундамент опирается весь последующий код Sandbox. Если ты никогда не работал с s&box — **не пропускай**.

- [00_09 — GameObject и Transform](00_09_GameObject.md)
- [00_10 — Иерархия GameObject (родитель/дети)](00_10_GameObject_Hierarchy.md)
- [00_11 — Теги (Tags)](00_11_Tags.md)
- [00_12 — Component (компонент) и его жизненный цикл](00_12_Component.md)
- [00_13 — Атрибуты компонентов (`[Property]`, `[Sync]`, `[Button]`...)](00_13_Component_Attributes.md)
- [00_14 — Порядок выполнения (Execution Order)](00_14_Execution_Order.md)
- [00_15 — Scene и GameObjectSystem](00_15_Scene_GameObjectSystem.md)
- [00_16 — Prefabs (префабы)](00_16_Prefabs.md)
- [00_17 — Hotloading (живая перезагрузка кода)](00_17_Hotloading.md)
- [00_18 — ConVar и ConCmd (консоль)](00_18_ConVar_ConCmd.md)
- [00_19 — Input (ввод): основы](00_19_Input_Basics.md)
- [00_20 — Сеть: хост, клиент, соединение](00_20_Networking_Basics.md)
- [00_21 — Сетевые объекты и NetworkHelper](00_21_Networked_Objects.md)
- [00_22 — Ownership (владение сетевым объектом)](00_22_Ownership.md)
- [00_23 — RPC сообщения](00_23_Rpc_Messages.md)
- [00_24 — Sync Properties (синхронизация свойств)](00_24_Sync_Properties.md)
- [00_25 — События сети (INetworkListener и др.)](00_25_Network_Events.md)
- [00_26 — Razor Panels: основы UI](00_26_Razor_Basics.md)
- [00_27 — Стилизация Razor Panels (SCSS)](00_27_Razor_Styling.md)
- [00_28 — HudPainter (немедленный рендер HUD)](00_28_HudPainter.md)
- [00_29 — GameResource (кастомные ресурсы)](00_29_GameResource.md)
- [00_30 — Log, Assert и диагностика](00_30_Log_Assert.md)

### Фаза 1: Фундамент
- [01_01 — Assembly.cs (глобальные using)](01_01_Assembly.md)
- [01_02 — EngineAdditions.cs](01_02_EngineAdditions.md)
- [01_03 — Утилиты: Extensions.cs](01_03_Extensions.md)
- [01_04 — Утилиты: Effects.cs](01_04_Effects.md)
- [01_05 — Утилиты: HUD.cs](01_05_HUD.md)
- [01_06 — Утилиты: LocalData.cs](01_06_LocalData.md)
- [01_07 — Утилиты: CameraSetup.cs](01_07_CameraSetup.md)
- [01_08 — Утилиты: Tracer.cs](01_08_Tracer.md)
- [01_09 — Утилиты: CookieSource.cs](01_09_CookieSource.md)
- [01_10 — Утилиты: PackageSortMode.cs](01_10_PackageSortMode.md)
- [01_11 — Утилиты: DemoRecording.cs](01_11_DemoRecording.md)
- [01_12 — Утилиты: SelfCollisionSound.cs](01_12_SelfCollisionSound.md)
- [01_13 — Утилиты: EnvironmentShake.cs](01_13_EnvironmentShake.md)
- [01_14 — Камера: CameraNoiseSystem](01_14_CameraNoise.md)

### Фаза 2: Интерфейсы и события
- [02_01 — IKillSource.cs](02_01_IKillSource.md)
- [02_02 — IPlayerControllable.cs](02_02_IPlayerControllable.md)
- [02_03 — PlayerEvent.cs](02_03_PlayerEvent.md)
- [02_04 — DamageTags.cs](02_04_DamageTags.md)

### Фаза 3: Базовый игрок
- [03_01 — Player.cs (основной класс)](03_01_Player.md)
- [03_02 — Player.Camera.cs](03_02_Player_Camera.md)
- [03_03 — Player.Ammo.cs](03_03_Player_Ammo.md)
- [03_04 — Player.Undo.cs](03_04_Player_Undo.md)
- [03_05 — Player.ConsoleCommands.cs](03_05_Player_ConsoleCommands.md)
- [03_06 — PlayerData.cs](03_06_PlayerData.md)
- [03_07 — PlayerStats.cs](03_07_PlayerStats.md)
- [03_08 — PlayerInventory.cs](03_08_PlayerInventory.md)
- [03_09 — PlayerFlashlight.cs](03_09_PlayerFlashlight.md)
- [03_10 — PlayerFallDamage.cs](03_10_PlayerFallDamage.md)
- [03_11 — PlayerDamageIndicators.cs](03_11_PlayerDamageIndicators.md)
- [03_12 — PlayerGib.cs](03_12_PlayerGib.md)
- [03_13 — PlayerObserver.cs](03_13_PlayerObserver.md)
- [03_14 — DeathCameraTarget.cs](03_14_DeathCameraTarget.md)
- [03_15 — NoclipMoveMode.cs](03_15_NoclipMoveMode.md)
- [03_16 — SandboxVoice.cs](03_16_SandboxVoice.md)
- [03_17 — UndoSystem.cs](03_17_UndoSystem.md)

### Фаза 4: GameManager
- [04_01 — GameManager.cs](04_01_GameManager.md)
- [04_02 — GameManager.Achievements.cs](04_02_GameManager_Achievements.md)
- [04_03 — GameManager.Util.cs](04_03_GameManager_Util.md)
- [04_04 — GamePreferences.cs](04_04_GamePreferences.md)
- [04_05 — ServerSettings.cs](04_05_ServerSettings.md)

### Фаза 5: Базовый UI (HUD)
- [05_01 — Стили: Theme.scss и Hud.scss](05_01_Стили_Theme_Hud.md)
- [05_02 — Vitals (здоровье)](05_02_Vitals.md)
- [05_03 — Chat (чат)](05_03_Chat.md)
- [05_04 — Feed (килл-фид)](05_04_Feed.md)
- [05_05 — Scoreboard (таблица)](05_05_Scoreboard.md)
- [05_06 — Nameplate (имена)](05_06_Nameplate.md)
- [05_07 — Voices (голос)](05_07_Voices.md)
- [05_08 — Notices (уведомления)](05_08_Notices.md)
- [05_09 — Crosshair и PressableHud](05_09_PressableHud.md)

### Фаза 6: Система оружия
- [06_01 — BaseCarryable.cs](06_01_BaseCarryable.md)
- [06_02 — BaseCarryable.ViewModel/WorldModel](06_02_BaseCarryable_Models.md)
- [06_03 — WeaponModel, ViewModel, WorldModel](06_03_WeaponModel.md)
- [06_04 — BaseWeapon.cs](06_04_BaseWeapon.md)
- [06_05 — BaseWeapon.Ammo и Reloading](06_05_BaseWeapon_Ammo.md)
- [06_06 — BaseBulletWeapon.cs](06_06_BaseBulletWeapon.md)
- [06_07 — IronSightsWeapon.cs](06_07_IronSightsWeapon.md)
- [06_08 — MeleeWeapon.cs](06_08_MeleeWeapon.md)
- [06_09 — UI: Inventory (инвентарь)](06_09_Inventory_UI.md)

### Фаза 7: Конкретное оружие
- [07_01 — Colt1911](07_01_Colt1911.md)
- [07_02 — Glock](07_02_Glock.md)
- [07_03 — M4A1](07_03_M4A1.md)
- [07_04 — MP5](07_04_MP5.md)
- [07_05 — Shotgun](07_05_Shotgun.md)
- [07_06 — Sniper](07_06_Sniper.md)
- [07_07 — Crowbar](07_07_Crowbar.md)
- [07_08 — HandGrenade](07_08_HandGrenade.md)
- [07_09 — CameraWeapon и ScreenWeapon](07_09_CameraScreen.md)

### Фаза 8: PhysGun
- [08_01 — Physgun.cs (основа)](08_01_Physgun.md)
- [08_02 — Physgun.Effects.cs](08_02_Physgun_Effects.md)
- [08_03 — Physgun.Hud.cs](08_03_Physgun_Hud.md)
- [08_04 — PhygunViewmodel и Worldmodel](08_04_Physgun_Models.md)
- [08_05 — IPhysgunEvent.cs](08_05_IPhysgunEvent.md)

### Фаза 9: ToolGun — основа
- [09_01 — ToolMode.cs (базовый класс)](09_01_ToolMode.md)
- [09_02 — ToolMode.Cookies.cs](09_02_ToolMode_Cookies.md)
- [09_03 — ToolMode.Effects.cs](09_03_ToolMode_Effects.md)
- [09_04 — ToolMode.Helpers.cs](09_04_ToolMode_Helpers.md)
- [09_05 — ToolMode.SnapGrid.cs и SnapGrid.cs](09_05_SnapGrid.md)
- [09_06 — Toolgun.cs](09_06_Toolgun.md)
- [09_07 — Toolgun.Screen.cs и Effects.cs](09_07_Toolgun_Screen_Effects.md)
- [09_08 — IToolgunEvent.cs](09_08_IToolgunEvent.md)
- [09_09 — UI: ToolInfoPanel](09_09_ToolInfoPanel.md)

### Фаза 10: Инструменты — базовые
- [10_01 — BaseConstraintToolMode.cs](10_01_BaseConstraint.md)
- [10_02 — Rope (верёвка)](10_02_Rope.md)
- [10_03 — Elastic (резинка)](10_03_Elastic.md)
- [10_04 — Weld (сварка)](10_04_Weld.md)
- [10_05 — BallSocket (шарнир)](10_05_BallSocket.md)
- [10_06 — Slider (ползунок)](10_06_Slider.md)
- [10_07 — NoCollide](10_07_NoCollide.md)
- [10_08 — Remover (удалитель)](10_08_Remover.md)
- [10_09 — Resizer (масштаб)](10_09_Resizer.md)
- [10_10 — Mass (масса)](10_10_Mass.md)
- [10_11 — Unbreakable (неразрушимое)](10_11_Unbreakable.md)
- [10_12 — Trail (след)](10_12_Trail.md)
- [10_13 — Decal (декали)](10_13_Decal.md)

### Фаза 11: Инструменты — сущности
- [11_01 — Balloon (шарик)](11_01_Balloon.md)
- [11_02 — Hoverball (парящий шар)](11_02_Hoverball.md)
- [11_03 — Thruster (двигатель)](11_03_Thruster.md)
- [11_04 — Wheel (колесо)](11_04_Wheel.md)
- [11_05 — Hydraulic (гидравлика)](11_05_Hydraulic.md)
- [11_06 — Linker (связыватель)](11_06_Linker.md)
- [11_07 — Emitter (эмиттер)](11_07_Emitter.md)
- [11_08 — Duplicator (дупликатор)](11_08_Duplicator.md)

### Фаза 12: Компоненты
- [12_01 — Ownable.cs](12_01_Ownable.md)
- [12_02 — ConstraintCleanup.cs](12_02_ConstraintCleanup.md)
- [12_03 — ManualLink.cs](12_03_ManualLink.md)
- [12_04 — MassOverride.cs](12_04_MassOverride.md)
- [12_05 — MorphState.cs](12_05_MorphState.md)
- [12_06 — MountMetadata.cs](12_06_MountMetadata.md)
- [12_07 — SpawningProgress.cs](12_07_SpawningProgress.md)

### Фаза 13: Игровые сущности
- [13_01 — ScriptedEntity.cs](13_01_ScriptedEntity.md)
- [13_02 — ScriptedEmitter.cs](13_02_ScriptedEmitter.md)
- [13_03 — DynamiteEntity.cs](13_03_DynamiteEntity.md)
- [13_04 — PointLightEntity и SpotLightEntity](13_04_Lights.md)
- [13_05 — EmitterEntity.cs](13_05_EmitterEntity.md)
- [13_06 — EntitySpawnerEntity.cs](13_06_EntitySpawnerEntity.md)

### Фаза 14: Спавнер + меню спавна
- [14_01 — ISpawner.cs](14_01_ISpawner.md)
- [14_02 — PropSpawner.cs](14_02_PropSpawner.md)
- [14_03 — EntitySpawner.cs](14_03_EntitySpawner.md)
- [14_04 — MountSpawner.cs](14_04_MountSpawner.md)
- [14_05 — DuplicatorSpawner.cs](14_05_DuplicatorSpawner.md)
- [14_06 — SpawnerWeapon.cs](14_06_SpawnerWeapon.md)
- [14_07 — UI компоненты меню](14_07_UI_Components.md)
- [14_08 — SpawnMenu.razor](14_08_SpawnMenu.md)
- [14_09 — SpawnMenuHost.razor](14_09_SpawnMenuHost.md)
- [14_10 — Props (пропы)](14_10_Props.md)
- [14_11 — Ents (сущности)](14_11_Ents.md)
- [14_12 — Spawnlists (списки)](14_12_Spawnlists.md)
- [14_13 — Dupes (дупликаты)](14_13_Dupes.md)
- [14_14 — Mounts (маунты)](14_14_Mounts.md)
- [14_15 — ToolsTab и SpawnMenuModeBar](14_15_ToolsTab.md)
- [14_16 — SaveMenu, CleanupPage, Users, Utilities](14_16_SaveMenu.md)

### Фаза 15: Предметы и подбор
- [15_01 — DroppedWeapon.cs](15_01_DroppedWeapon.md)
- [15_02 — BasePickup, AmmoPickup, HealthPickup](15_02_Pickups.md)
- [15_03 — InventoryPickup.cs](15_03_InventoryPickup.md)

### Фаза 16: Объекты карты
- [16_01 — BaseToggle.cs](16_01_BaseToggle.md)
- [16_02 — Button.cs и Door.cs](16_02_Button_Door.md)
- [16_03 — FuncMover.cs](16_03_FuncMover.md)
- [16_04 — TriggerPush и TriggerTeleport](16_04_Triggers.md)
- [16_05 — MapPlayerSpawner.cs](16_05_MapPlayerSpawner.md)

### Фаза 17: Система управления
- [17_01 — ControlSystem.cs](17_01_ControlSystem.md)
- [17_02 — ClientInput.cs](17_02_ClientInput.md)

### Фаза 18: Пост-обработка
- [18_01 — PostProcessResource.cs](18_01_PostProcessResource.md)
- [18_02 — PostProcessManager.cs](18_02_PostProcessManager.md)
- [18_03 — UI: EffectsHost/List/Properties](18_03_Effects_UI.md)

### Фаза 19: Контекстное меню и инспектор
- [19_01 — ContextMenuHost.razor](19_01_ContextMenuHost.md)
- [19_02 — Inspector и GameObjectInspector](19_02_Inspector.md)
- [19_03 — InspectorEditor и атрибуты](19_03_InspectorEditor.md)
- [19_04 — ComponentHandle.cs](19_04_ComponentHandle.md)

### Фаза 20: UI — элементы управления
- [20_01 — Controls: ResourceSelect](20_01_ResourceSelect.md)
- [20_02 — Controls: GameResourceControl](20_02_GameResourceControl.md)
- [20_03 — Controls: ClientInputControl](20_03_ClientInputControl.md)
- [20_04 — Controls: StringQueryPopup](20_04_StringQueryPopup.md)
- [20_05 — DragDrop](20_05_DragDrop.md)
- [20_06 — OwnerLabel](20_06_OwnerLabel.md)

### Фаза 21: UI — специальные редакторы
- [21_01 — DresserEditor](21_01_DresserEditor.md)
- [21_02 — FacePoseEditor](21_02_FacePoseEditor.md)
- [21_03 — HotbarPresetsButton](21_03_HotbarPresets.md)

### Фаза 22: NPC система
- [22_01 — Npc.cs (основа)](22_01_Npc.md)
- [22_02 — Npc.Schedule и Npc.Layers](22_02_Npc_Schedule_Layers.md)
- [22_03 — Npc.Debug.cs](22_03_Npc_Debug.md)
- [22_04 — ScheduleBase и TaskBase](22_04_ScheduleBase_TaskBase.md)
- [22_05 — Layers: BaseNpcLayer и AnimationLayer](22_05_Layers.md)
- [22_06 — Layers: SensesLayer и SpeechLayer](22_06_Senses_Speech.md)
- [22_07 — Tasks: MoveTo, LookAt, Wait](22_07_Tasks_Basic.md)
- [22_08 — Tasks: Say, FireWeapon, PickUp/DropProp](22_08_Tasks_Advanced.md)
- [22_09 — CombatNpc и его расписания](22_09_CombatNpc.md)
- [22_10 — ScientistNpc и его расписания](22_10_ScientistNpc.md)
- [22_11 — RollermineNpc и его задачи](22_11_RollermineNpc.md)
- [22_12 — SubtitleExtension.cs](22_12_SubtitleExtension.md)

### Фаза 23: Сохранение и очистка
- [23_01 — ISaveEvents.cs](23_01_ISaveEvents.md)
- [23_02 — SaveSystem.cs](23_02_SaveSystem.md)
- [23_03 — CleanupSystem.cs](23_03_CleanupSystem.md)

### Фаза 24: Система банов и FreeCam
- [24_01 — BanSystem.cs](24_01_BanSystem.md)
- [24_02 — FreeCamGameObjectSystem.cs](24_02_FreeCam.md)
- [24_03 — UtilityPage.cs](24_03_UtilityPage.md)

### Фаза 25: Финал
- [25_01 — Editor/Assembly.cs](25_01_Editor_Assembly.md)
- [25_02 — Сцены и префабы](25_02_Scenes_Prefabs.md)
- [25_03 — Итоговая проверка](25_03_Final_Check.md)

---

## Ссылки

- 🔗 [Документация s&box](https://sbox.game/dev/)
- 🔗 [Исходный код движка](https://github.com/LiGyH/sbox-public)
- 🔗 [Первые шаги](https://sbox.game/dev/doc/about/getting-started/first-steps/)
- 🔗 [Форум s&box](https://sbox.game/f/)
