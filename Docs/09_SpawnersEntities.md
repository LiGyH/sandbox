# 09 — Система спавна (Spawners) и Сущности (Entities) 🏭

## Обзор

Система спавна отвечает за **размещение объектов в мире**: пропы, сущности (lights, emitters), дупликаты и объекты из маунтов. Всё объединено интерфейсом `ISpawner`.

---

## 09.01 — ISpawner (интерфейс спавна)

```csharp
public interface ISpawner
{
    string DisplayName { get; }          // имя для UI
    string Icon { get; }                 // иконка (путь или URL)
    BBox Bounds { get; }                 // локальные границы
    bool IsReady { get; }                // загружен ли ресурс
    Task<bool> Loading { get; }          // задача загрузки
    string Data { get; }                 // данные для реконструкции

    void DrawPreview( Transform, Material );  // призрак для предпросмотра
    Task<List<GameObject>> Spawn( Transform, Player );  // создать объект(ы)
}
```

Все спавнеры реализуют этот интерфейс. `GameManager.Spawn()` создаёт нужный спавнер по типу и вызывает `Spawn()`.

---

## 09.02 — PropSpawner (спавн пропов)

```
"prop:models/citizen_props/crate01.vmdl" → PropSpawner
```

1. **Загрузка**: `ResourceLibrary.LoadAsync<Model>(path)` или `Cloud.Load<Model>(path)`
2. **Превью**: `DebugOverlay.Model()` с полупрозрачным материалом
3. **Спавн**:
   - Создаёт `GameObject` с `Prop` компонентом
   - Добавляет `Ownable` (владелец = спавнивший)
   - Тег `"removable"` — можно удалить Remover Tool
   - Если нет физики в модели → добавляет `BoxCollider` + `Rigidbody`
   - `NetworkSpawn( true )` — авторитет у сервера

---

## 09.03 — EntitySpawner (спавн сущностей)

```
"entity:light_point" → EntitySpawner
```

1. Загружает `ScriptedEntity` — ресурс, описывающий сущность
2. Клонирует `Entity.Prefab` в мир
3. `ScriptedEntity` содержит: Title, Description, Category, Prefab

---

## 09.04 — MountSpawner (спавн из маунтов)

```
"mount:hl2/models/..." → MountSpawner
```

Как PropSpawner, но добавляет `MountMetadata` — если клиент не имеет нужной игры (HL2, CS), показывает заглушку с подсказкой «Install HL2».

---

## 09.05 — DuplicatorSpawner (спавн дупликатов)

```
"dupe:12345" → DuplicatorSpawner
```

Самый сложный спавнер:
1. Загружает JSON дупликата (из Workshop или локального хранилища)
2. Десериализует `DuplicationData` — список объектов + связи + свойства
3. При спавне воссоздаёт всю конструкцию: объекты, компоненты, Joint'ы, связи
4. Устанавливает зависимости между пакетами (`InstallPackages`)

---

## 09.06 — SpawnerWeapon (оружие-спавнер)

```csharp
public partial class SpawnerWeapon : ScreenWeapon, IToolInfo
```

Специальное оружие, которое хранит `ISpawner` и позволяет:
- Видеть превью на прицеле
- ЛКМ → разместить объект
- E → вращать превью
- Shift → привязка к сетке
- Экран на viewmodel'е показывает иконку

`SpawnerData` синхронизируется → все клиенты видят правильное превью. При изменении `SpawnerData` локально реконструируется `ISpawner`.

---

## 09.07 — Сущности (Entities)

Все сущности — компоненты, которые можно разместить через EntitySpawner.

### ScriptedEntity (ресурс)

```csharp
public class ScriptedEntity : GameResource
{
    string Title;
    string Description;
    string Category;
    GameObject Prefab;        // префаб для клонирования
    bool IsDeveloperEntity;   // скрыт ли по умолчанию
}
```

### Конкретные сущности

| Сущность | Файл | Описание |
|----------|------|---------|
| PointLightEntity | `Code/Game/Entity/PointLightEntity.cs` | Точечный свет |
| SpotLightEntity | `Code/Game/Entity/SpotLightEntity.cs` | Направленный свет |
| DynamiteEntity | `Code/Game/Entity/DynamiteEntity.cs` | Динамит (взрывается) |
| EmitterEntity | `Code/Game/Entity/EmitterEntity.cs` | Излучатель частиц |
| EntitySpawnerEntity | `Code/Game/Entity/EntitySpawnerEntity.cs` | Спавнит другие сущности |
| ScriptedEmitter | `Code/Game/Entity/ScriptedEmitter.cs` | Настраиваемый излучатель |
| ScriptedEmitterModel | `Code/Game/Entity/ScriptedEmitterModel.cs` | Излучатель моделей |

---

## 09.08 — Система банов (BanSystem)

```csharp
public class BanSystem : GameObjectSystem<BanSystem>
```

Синглтон для блокировки игроков:
- `Ban(steamId, reason, duration)` — забанить
- `Unban(steamId)` — разбанить
- `IsBanned(steamId)` — проверить
- Баны хранятся в `LocalData` (персистентно)

---

## 09.09 — Прочие системы

### ControlSystem / ClientInput

Система управления для объектов с сиденьями (транспорт). `IPlayerControllable` позволяет оружию/транспорту принимать ввод.

### PostProcessManager

Управление пост-обработкой через `PostProcessResource` — ресурс с настройками (bloom, color grading, vignette).

### UtilityPage

Страница утилит в меню — команды для быстрого доступа (cleanup, god mode и т.д.).

---

## Проверка

1. `spawn prop:models/citizen_props/crate01.vmdl` → проп спавнится
2. `spawn entity:light_point` → свет спавнится
3. SpawnerWeapon показывает превью перед размещением
4. Дупликатор сохраняет и загружает конструкции

## 🎉 Фаза 9 завершена!
