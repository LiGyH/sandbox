# 25.03 — Итоговая проверка

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.26 — Razor UI](00_26_Razor_Basics.md)

## Что мы делаем?

Проводим **финальную проверку** всего проекта — убеждаемся, что все файлы на месте, все системы работают и игра запускается.

## Контрольный список файлов

### Фаза 0: Подготовка
- [ ] `sandbox.sbproj` — настроен правильно
- [ ] `ProjectSettings/Physics.config` — настройки физики
- [ ] `ProjectSettings/Collision.config` — настройки столкновений
- [ ] `ProjectSettings/Input.config` — настройки ввода
- [ ] `ProjectSettings/Mixer.config` — настройки звука
- [ ] `ProjectSettings/Networking.config` — настройки сети

### Фаза 1: Фундамент
- [ ] `Code/Assembly.cs` — глобальные using
- [ ] `Code/EngineAdditions.cs` — дополнения к движку
- [ ] `Code/Utility/Extensions.cs` — расширения
- [ ] `Code/Utility/Effects.cs` — эффекты
- [ ] `Code/Utility/HUD.cs` — HUD утилиты
- [ ] `Code/Utility/LocalData.cs` — локальные данные
- [ ] `Code/Utility/CameraSetup.cs` — настройка камеры
- [ ] `Code/Utility/Tracer.cs` — трейсеры
- [ ] `Code/Utility/CookieSource.cs` — источник cookies
- [ ] `Code/Utility/PackageSortMode.cs` — сортировка пакетов
- [ ] `Code/Utility/DemoRecording.cs` — запись демо
- [ ] `Code/Utility/SelfCollisionSound.cs` — звуки столкновений
- [ ] `Code/Utility/EnvironmentShake.cs` — тряска окружения
- [ ] `Code/Utility/CameraNoise/CameraNoiseSystem.cs` — шум камеры
- [ ] `Code/Utility/CameraNoise/Punch.cs`
- [ ] `Code/Utility/CameraNoise/Recoil.cs`
- [ ] `Code/Utility/CameraNoise/Shake.cs`

### Фаза 2: Интерфейсы и события
- [ ] `Code/Game/IKillSource.cs`
- [ ] `Code/Game/ControlSystem/IPlayerControllable.cs`
- [ ] `Code/Player/PlayerEvent.cs`
- [ ] `Code/Player/DamageTags.cs`

### Фаза 3: Игрок
- [ ] `Code/Player/Player.cs`
- [ ] `Code/Player/Player.Camera.cs`
- [ ] `Code/Player/Player.Ammo.cs`
- [ ] `Code/Player/Player.Undo.cs`
- [ ] `Code/Player/Player.ConsoleCommands.cs`
- [ ] `Code/Player/PlayerData.cs`
- [ ] `Code/Player/PlayerStats.cs`
- [ ] `Code/Player/PlayerInventory.cs`
- [ ] `Code/Player/PlayerFlashlight.cs`
- [ ] `Code/Player/PlayerFallDamage.cs`
- [ ] `Code/Player/PlayerDamageIndicators.cs`
- [ ] `Code/Player/PlayerGib.cs`
- [ ] `Code/Player/PlayerObserver.cs`
- [ ] `Code/Player/DeathCameraTarget.cs`
- [ ] `Code/Player/NoclipMoveMode.cs`
- [ ] `Code/Player/SandboxVoice.cs`
- [ ] `Code/Player/UndoSystem/UndoSystem.cs`

### Фаза 4: GameManager
- [ ] `Code/GameLoop/GameManager.cs`
- [ ] `Code/GameLoop/GameManager.Achievements.cs`
- [ ] `Code/GameLoop/GameManager.Util.cs`
- [ ] `Code/GameLoop/GamePreferences.cs`
- [ ] `Code/GameLoop/ServerSettings.cs`

### Фаза 5: UI (HUD)
- [ ] `Code/UI/Theme.scss`
- [ ] `Code/UI/Hud.scss`
- [ ] `Code/UI/Vitals/Vitals.razor` + `.scss`
- [ ] `Code/UI/Chat.razor` + `.scss`
- [ ] `Code/UI/Feed.razor` + `.razor.cs` + `.scss`
- [ ] `Code/UI/Scoreboard.razor` + `.scss`
- [ ] `Code/UI/ScoreboardRow.razor`
- [ ] `Code/UI/Nameplate.razor` + `.scss`
- [ ] `Code/UI/Voices.razor` + `.scss`
- [ ] `Code/UI/Notices/Notices.cs` + `.scss`
- [ ] `Code/UI/Notices/NoticePanel.cs` + `.scss`
- [ ] `Code/UI/Notices/Hints.cs`
- [ ] `Code/UI/Pressable/PressableHud.razor` + `.scss`
- [ ] `Code/UI/Pressable/PressableTooltip.razor` + `.scss`

### Фаза 6: Система оружия
- [ ] `Code/Game/Weapon/BaseCarryable/BaseCarryable.cs`
- [ ] `Code/Game/Weapon/BaseCarryable/BaseCarryable.ViewModel.cs`
- [ ] `Code/Game/Weapon/BaseCarryable/BaseCarryable.WorldModel.cs`
- [ ] `Code/Game/Weapon/WeaponModel/WeaponModel.cs`
- [ ] `Code/Game/Weapon/WeaponModel/ViewModel.cs`
- [ ] `Code/Game/Weapon/WeaponModel/ViewModel.Throwables.cs`
- [ ] `Code/Game/Weapon/WeaponModel/WorldModel.cs`
- [ ] `Code/Game/Weapon/BaseWeapon/BaseWeapon.cs`
- [ ] `Code/Game/Weapon/BaseWeapon/BaseWeapon.Ammo.cs`
- [ ] `Code/Game/Weapon/BaseWeapon/BaseWeapon.Reloading.cs`
- [ ] `Code/Game/Weapon/BaseBulletWeapon/BaseBulletWeapon.cs`
- [ ] `Code/Game/Weapon/IronSightsWeapon.cs`
- [ ] `Code/Game/Weapon/MeleeWeapon/MeleeWeapon.cs`
- [ ] `Code/UI/Inventory/Inventory.razor` + `.scss`
- [ ] `Code/UI/Inventory/InventorySlot.razor` + `.scss`

### Фаза 7: Конкретное оружие
- [ ] `Code/Weapons/Colt1911/Colt1911Weapon.cs`
- [ ] `Code/Weapons/GlockWeapon.cs`
- [ ] `Code/Weapons/M4a1/M4a1Weapon.cs`
- [ ] `Code/Weapons/Mp5/Mp5Weapon.cs`
- [ ] `Code/Weapons/Shotgun/ShotgunWeapon.cs`
- [ ] `Code/Weapons/Sniper/SniperWeapon.cs` + `SniperViewModel.cs` + `SniperScopeEffect.cs`
- [ ] `Code/Weapons/CrowbarWeapon.cs`
- [ ] `Code/Weapons/HandGrenade/HandGrenadeWeapon.cs` + `TimedExplosive.cs`
- [ ] `Code/Weapons/CameraWeapon.cs`
- [ ] `Code/Weapons/ScreenWeapon.cs`

### Фаза 8: PhysGun
- [ ] `Code/Weapons/PhysGun/Physgun.cs`
- [ ] `Code/Weapons/PhysGun/Physgun.Effects.cs`
- [ ] `Code/Weapons/PhysGun/Physgun.Hud.cs`
- [ ] `Code/Weapons/PhysGun/PhygunViewmodel.cs`
- [ ] `Code/Weapons/PhysGun/PhygunWorldmodel.cs`
- [ ] `Code/Components/IPhysgunEvent.cs`

### Фаза 9: ToolGun
- [ ] `Code/Weapons/ToolGun/ToolMode.cs`
- [ ] `Code/Weapons/ToolGun/ToolMode.Cookies.cs`
- [ ] `Code/Weapons/ToolGun/ToolMode.Effects.cs`
- [ ] `Code/Weapons/ToolGun/ToolMode.Helpers.cs`
- [ ] `Code/Weapons/ToolGun/ToolMode.SnapGrid.cs`
- [ ] `Code/Weapons/ToolGun/SnapGrid.cs`
- [ ] `Code/Weapons/ToolGun/Toolgun.cs`
- [ ] `Code/Weapons/ToolGun/Toolgun.Screen.cs`
- [ ] `Code/Weapons/ToolGun/Toolgun.Effects.cs`
- [ ] `Code/Components/IToolgunEvent.cs`
- [ ] `Code/UI/ToolInfo/IToolInfo.cs`
- [ ] `Code/UI/ToolInfo/ToolInfoPanel.razor` + `.scss`

### Фаза 10: Базовые инструменты
- [ ] `Code/Weapons/ToolGun/Modes/BaseConstraintToolMode.cs`
- [ ] `Code/Weapons/ToolGun/Modes/Rope.cs`
- [ ] `Code/Weapons/ToolGun/Modes/Elastic.cs`
- [ ] `Code/Weapons/ToolGun/Modes/Weld.cs`
- [ ] `Code/Weapons/ToolGun/Modes/BallSocket.cs`
- [ ] `Code/Weapons/ToolGun/Modes/Slider.cs`
- [ ] `Code/Weapons/ToolGun/Modes/NoCollide.cs`
- [ ] `Code/Weapons/ToolGun/Modes/Remover.cs`
- [ ] `Code/Weapons/ToolGun/Modes/Resizer.cs`
- [ ] `Code/Weapons/ToolGun/Modes/Mass.cs`
- [ ] `Code/Weapons/ToolGun/Modes/Unbreakable.cs`
- [ ] `Code/Weapons/ToolGun/Modes/Trail.cs`
- [ ] `Code/Weapons/ToolGun/Modes/Trail/LineDefinition.cs`
- [ ] `Code/Weapons/ToolGun/Modes/Decal/DecalTool.cs`

### Фаза 11: Инструменты-сущности
- [ ] `Code/Weapons/ToolGun/Modes/Balloon/Balloon.cs` + `BalloonDefinition.cs` + `BalloonEntity.cs`
- [ ] `Code/Weapons/ToolGun/Modes/Hoverball/HoverballTool.cs` + `HoverballDefinition.cs` + `HoverballEntity.cs`
- [ ] `Code/Weapons/ToolGun/Modes/Thruster/ThrusterTool.cs` + `ThrusterDefinition.cs` + `ThrusterEntity.cs` + `Propeller.cs`
- [ ] `Code/Weapons/ToolGun/Modes/Wheel/WheelTool.cs` + `WheelDefinition.cs` + `WheelEntity.cs`
- [ ] `Code/Weapons/ToolGun/Modes/Hydraulic/HydraulicTool.cs` + `HydraulicEntity.cs` + `BallSocketPair.cs`
- [ ] `Code/Weapons/ToolGun/Modes/Linker/LinkerTool.cs`
- [ ] `Code/Weapons/ToolGun/Modes/Emitter/EmitterTool.cs`
- [ ] `Code/Weapons/ToolGun/Modes/Duplicator/Duplicator.cs` + `Duplicator.IconRendering.cs` + `DuplicationData.cs` + `LinkedGameObjectBuilder.cs`

### Фаза 12: Компоненты
- [ ] `Code/Components/Ownable.cs`
- [ ] `Code/Components/ConstraintCleanup.cs`
- [ ] `Code/Components/ManualLink.cs`
- [ ] `Code/Components/PhysicalProperties.cs`
- [ ] `Code/Components/MorphState.cs`
- [ ] `Code/Components/MountMetadata.cs`
- [ ] `Code/Components/SpawningProgress.cs`

### Фаза 13: Игровые сущности
- [ ] `Code/Game/Entity/ScriptedEntity.cs`
- [ ] `Code/Game/Entity/ScriptedEmitter.cs` + `ScriptedEmitterModel.cs`
- [ ] `Code/Game/Entity/DynamiteEntity.cs`
- [ ] `Code/Game/Entity/PointLightEntity.cs`
- [ ] `Code/Game/Entity/SpotLightEntity.cs`
- [ ] `Code/Game/Entity/EmitterEntity.cs`
- [ ] `Code/Game/Entity/EntitySpawnerEntity.cs`

### Фаза 14: Спавнер и меню спавна
- [ ] `Code/Spawner/ISpawner.cs`
- [ ] `Code/Spawner/PropSpawner.cs`
- [ ] `Code/Spawner/EntitySpawner.cs`
- [ ] `Code/Spawner/MountSpawner.cs`
- [ ] `Code/Spawner/DuplicatorSpawner.cs`
- [ ] `Code/Spawner/SpawnerWeapon.cs`
- [ ] `Code/UI/Components/` — все компоненты меню
- [ ] `Code/UI/SpawnMenu/SpawnMenu.razor` + `.scss`
- [ ] `Code/UI/SpawnMenuHost.razor` + `.scss`
- [ ] `Code/UI/SpawnMenu/Props/PropsPage.cs` и связанные файлы
- [ ] `Code/UI/SpawnMenu/Ents/EntityPage.cs` и связанные файлы
- [ ] `Code/UI/SpawnMenu/Spawnlists/` — все файлы
- [ ] `Code/UI/SpawnMenu/Dupes/` — все файлы
- [ ] `Code/UI/SpawnMenu/Mounts/` — все файлы
- [ ] `Code/UI/SpawnMenu/ToolsTab.razor`
- [ ] `Code/UI/SpawnMenuModeBar.razor` + `.scss`
- [ ] `Code/UI/SpawnMenu/SaveMenu.razor` + `.scss`
- [ ] `Code/UI/SpawnMenu/CleanupPage.razor`
- [ ] `Code/UI/SpawnMenu/UsersPage.razor` + `.scss`
- [ ] `Code/UI/SpawnMenu/UtilitiesPage.razor`
- [ ] `Code/UI/SpawnMenu/UtilityTab.razor`

### Фаза 15: Предметы и подбор
- [ ] `Code/Items/DroppedWeapon.cs`
- [ ] `Code/Items/Pickups/BasePickup.cs`
- [ ] `Code/Items/Pickups/AmmoPickup.cs`
- [ ] `Code/Items/Pickups/HealthPickup.cs`
- [ ] `Code/Items/Pickups/InventoryPickup.cs`

### Фаза 16: Объекты карты
- [ ] `Code/Map/BaseToggle.cs`
- [ ] `Code/Map/Button.cs`
- [ ] `Code/Map/Door.cs`
- [ ] `Code/Map/FuncMover.cs`
- [ ] `Code/Map/TriggerPush.cs`
- [ ] `Code/Map/TriggerTeleport.cs`
- [ ] `Code/Map/MapPlayerSpawner.cs`

### Фаза 17: Система управления
- [ ] `Code/Game/ControlSystem/ControlSystem.cs`
- [ ] `Code/Game/ControlSystem/ClientInput.cs`

### Фаза 18: Пост-обработка
- [ ] `Code/Game/PostProcessing/PostProcessResource.cs`
- [ ] `Code/Game/PostProcessing/PostProcessManager.cs`
- [ ] `Code/UI/Effects/` — все файлы

### Фаза 19: Контекстное меню и инспектор
- [ ] `Code/UI/ContextMenu/ContextMenuHost.razor` + `.scss`
- [ ] `Code/UI/ContextMenu/Inspector.razor` + `.scss`
- [ ] `Code/UI/ContextMenu/GameObjectInspector.razor` + `.scss`
- [ ] `Code/UI/ContextMenu/InspectorEditor.cs`
- [ ] `Code/UI/ContextMenu/InspectorEditorAttribute.cs`
- [ ] `Code/UI/ContextMenu/ComponentHandle.cs` + `.scss`

### Фаза 20: UI элементы управления
- [ ] `Code/UI/Controls/ResourceSelectAttribute.cs`
- [ ] `Code/UI/Controls/ResourceSelectControl.razor` + `.scss`
- [ ] `Code/UI/Controls/ResourceSelectPopup.razor` + `.scss`
- [ ] `Code/UI/Controls/resource-picker.scss`
- [ ] `Code/UI/Controls/GameResourceControl.razor` + `.scss`
- [ ] `Code/UI/Controls/ClientInputControl.cs` + `.scss`
- [ ] `Code/UI/Controls/StringQueryPopup.razor` + `.scss`
- [ ] `Code/UI/DragDrop/DragData.cs`
- [ ] `Code/UI/DragDrop/DragHandler.razor` + `.scss`
- [ ] `Code/UI/OwnerLabel/OwnerLabel.razor` + `.scss`

### Фаза 21: Специальные редакторы
- [ ] `Code/UI/Dresser/DresserEditor.razor` + `.scss`
- [ ] `Code/UI/FacePoser/FacePoseEditor.razor` + `.scss`
- [ ] `Code/UI/FacePoser/FacePoseMorphRow.razor` + `.scss`
- [ ] `Code/UI/Inventory/HotbarPresetsButton.razor` + `.scss`

### Фаза 22: NPC система
- [ ] `Code/Npcs/Npc.cs`
- [ ] `Code/Npcs/Npc.Schedule.cs`
- [ ] `Code/Npcs/Npc.Layers.cs`
- [ ] `Code/Npcs/Npc.Debug.cs`
- [ ] `Code/Npcs/TaskStatus.cs`
- [ ] `Code/Npcs/TaskBase.cs`
- [ ] `Code/Npcs/ScheduleBase.cs`
- [ ] `Code/Npcs/Layers/BaseNpcLayer.cs`
- [ ] `Code/Npcs/Layers/AnimationLayer.Hold.cs`
- [ ] `Code/Npcs/Layers/SensesLayer.cs`
- [ ] `Code/Npcs/Layers/SpeechLayer.cs`
- [ ] `Code/Npcs/Tasks/Wait.cs`
- [ ] `Code/Npcs/Tasks/LookAt.cs`
- [ ] `Code/Npcs/Tasks/MoveTo.cs`
- [ ] `Code/Npcs/Tasks/Say.cs`
- [ ] `Code/Npcs/Tasks/FireWeapon.cs`
- [ ] `Code/Npcs/Tasks/PickUpProp.cs`
- [ ] `Code/Npcs/Tasks/DropProp.cs`
- [ ] `Code/Npcs/Combat/CombatNpc.cs` + расписания
- [ ] `Code/Npcs/Scientist/ScientistNpc.cs` + расписания
- [ ] `Code/Npcs/Rollermine/RollermineNpc.cs` + задачи
- [ ] `Code/Npcs/Speech/SubtitleExtension.cs`

### Фаза 23: Сохранение и очистка
- [ ] `Code/Save/ISaveEvents.cs`
- [ ] `Code/Save/SaveSystem.cs`
- [ ] `Code/Cleanup/CleanupSystem.cs`

### Фаза 24: Система банов и FreeCam
- [ ] `Code/Game/BanSystem/BanSystem.cs`
- [ ] `Code/FreeCam/FreeCamGameObjectSystem.cs`
- [ ] `Code/Game/UtilityFunctions/UtilityPage.cs`

### Фаза 25: Финал
- [ ] `Editor/Assembly.cs`
- [ ] `Assets/scenes/sandbox.scene`
- [ ] `Assets/prefabs/engine/player.prefab`

## Порядок проверки

1. **Откройте проект в s&box Editor**
2. **Убедитесь, что нет ошибок компиляции** — в консоли не должно быть красных сообщений
3. **Запустите игру** — нажмите Play
4. **Проверьте основные системы:**
   - Игрок спавнится на карте
   - WASD движение работает
   - Мышь вращает камеру
   - Нажатие Q открывает меню спавна
   - Можно заспавнить пропы
   - PhysGun работает (ЛКМ хватает, ПКМ замораживает)
   - ToolGun работает (выберите инструмент в меню)
   - Tab показывает скорборд
   - Y открывает чат
   - F включает фонарик
5. **Проверьте мультиплеер:**
   - Запустите сервер
   - Подключитесь вторым клиентом
   - Оба игрока видят друг друга
   - Сетевые объекты синхронизируются

## Поздравляем! 🎉

Если все проверки пройдены — вы полностью воссоздали **Sandbox** с нуля!

Теперь вы понимаете:
- Как устроен проект s&box
- Как работают компоненты и GameObject
- Как устроена сеть
- Как создаются оружия и инструменты
- Как работает UI на Razor
- Как устроена NPC-система
- Как работают сохранения

Используйте эти знания для создания своих игр! 🚀


---

## ✅ Конец документации

Это последний файл. Вернись к [INDEX](INDEX.md), чтобы перечитать или повторить любой этап.

---

<!-- seealso -->
## 🔗 См. также

- [06.01 — BaseCarryable](06_01_BaseCarryable.md)
- [06.04 — BaseWeapon](06_04_BaseWeapon.md)
- [06.06 — BaseBulletWeapon](06_06_BaseBulletWeapon.md)
- [06.07 — IronSightsWeapon](06_07_IronSightsWeapon.md)
- [06.08 — MeleeWeapon](06_08_MeleeWeapon.md)
- [08.01 — Physgun](08_01_Physgun.md)

