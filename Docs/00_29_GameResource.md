# 00.29 — GameResource (кастомные ресурсы)

## Что мы делаем?

Разбираем **GameResource** — способ сделать **собственный тип ассета**. Не код, не сцена, не модель — а твой формат данных, который открывается как файл, редактируется в инспекторе, сохраняется на диск в читаемом JSON. В Sandbox это используется для: описаний оружия, рецептов дюпликатора, пресетов NPC, post-process настроек.

## Почему не просто класс?

Обычный класс `WeaponStats` живёт только в памяти. Если ты хочешь:

- Дизайнеру менять урон без программиста — нужен файл.
- Один «шаблон» для разных оружий (`glock.weapon`, `m4a1.weapon`) — нужен ассет.
- Перетаскивать ресурс в `[Property]` компонента — нужен ассет.

`GameResource` это всё даёт в одну обёртку.

## Минимальный пример

```csharp
using Sandbox;

[GameResource( "Weapon Stats", "weapon", "Описание параметров оружия", Icon = "badge" )]
public class WeaponStats : GameResource
{
    public string DisplayName { get; set; } = "Unnamed";
    public int Damage { get; set; } = 10;
    public float FireRate { get; set; } = 0.1f;
    public int MagSize { get; set; } = 30;
    public float Spread { get; set; } = 0.02f;

    public string ViewModelPath { get; set; }
    public string WorldModelPath { get; set; }
}
```

Разбор атрибута:

```csharp
[GameResource(
    "Weapon Stats",              // красивое имя в меню создания
    "weapon",                    // расширение файла (.weapon)
    "Описание параметров оружия", // подсказка
    Icon = "badge"                // иконка Material Icons
)]
```

## Создать ассет в редакторе

1. Правый клик в папке Asset Browser → **Create → Weapon Stats**.
2. Назови файл: `glock.weapon`.
3. Открой его — справа в инспекторе появятся все свойства.
4. Заполни: `Damage = 25`, `FireRate = 0.15f`, и т.д.
5. Сохрани.

Файл на диске будет обычный JSON:

```json
{
    "DisplayName": "Glock 17",
    "Damage": 25,
    "FireRate": 0.15,
    "MagSize": 17,
    "Spread": 0.015,
    "ViewModelPath": "weapons/glock/v_glock.vmdl",
    "WorldModelPath": "weapons/glock/w_glock.vmdl"
}
```

## Использование в коде

### Ссылка через `[Property]`

```csharp
public sealed class BaseWeapon : Component
{
    [Property] public WeaponStats Stats { get; set; }

    public override void OnShoot()
    {
        var damage = Stats.Damage;
        var rate = Stats.FireRate;
    }
}
```

В редакторе перетаскиваешь `glock.weapon` в поле Stats.

### Найти все ассеты такого типа

```csharp
var all = ResourceLibrary.GetAll<WeaponStats>();
foreach ( var stats in all )
    Log.Info( stats.DisplayName );
```

Используется для: показать список всего доступного оружия в меню выбора.

### Загрузить конкретный по пути

```csharp
var glock = ResourceLibrary.Get<WeaponStats>( "weapons/glock.weapon" );
```

## Сложные поля — ссылки на другие ресурсы

`GameResource` может содержать `Model`, `Material`, `SoundEvent`, вложенные `GameResource` и т.д.:

```csharp
public class WeaponStats : GameResource
{
    public Model ViewModel { get; set; }
    public Model WorldModel { get; set; }
    public SoundEvent ShootSound { get; set; }
    public List<WeaponStats> Alternatives { get; set; } = new();
}
```

В инспекторе всё отображается как выпадающий список / поле для перетаскивания.

## Когда использовать

✅ **Отлично подходит:**
- Шаблоны оружия / брони / скилов.
- Пресеты настроек (post-process, физика, NPC).
- Справочники предметов / достижений.
- Уровни сложности.

❌ **Не подходит:**
- Данные, которые меняются в рантайме (сохранение прогресса игрока). Это не ресурс, это save data.
- Огромные массивы (тысячи записей). JSON будет еле открываться.

## Атрибуты внутри GameResource

Внутри `GameResource` можно использовать те же `[Property]`-подобные атрибуты для управления инспектором:

```csharp
public class WeaponStats : GameResource
{
    [Category( "Combat" )]
    public int Damage { get; set; } = 10;

    [Category( "Combat" )]
    [Range( 0.01f, 1f )]
    public float FireRate { get; set; } = 0.1f;

    [Category( "Visuals" )]
    public Model ViewModel { get; set; }

    [TextArea]
    public string Description { get; set; }

    [Hide]
    public int InternalVersion { get; set; } = 1;
}
```

## Events: когда ресурс загрузился / изменился

Переопределяя методы, можно реагировать:

```csharp
public class WeaponStats : GameResource
{
    protected override void PostLoad()
    {
        // ресурс только что загрузился с диска
    }

    protected override void PostReload()
    {
        // ресурс перезагрузили (например, при hotload)
    }
}
```

Используется редко, но полезно для валидации.

## GameResource в Sandbox

Примеры, которые ты создашь позже:

- **PostProcessResource** (Фаза 18) — пресет пост-обработки (цветокоррекция, dof, грейн).
- **ClientInput config** — список клавиш проекта.
- **Spawnlist / Mount metadata** (Фаза 14) — списки пропов и маунты.

## Результат

После этого этапа ты знаешь:

- ✅ Что такое `GameResource` и чем он отличается от обычного класса.
- ✅ Как объявить его через `[GameResource(...)]` атрибут.
- ✅ Как создать ассет в редакторе и редактировать его в инспекторе.
- ✅ Как ссылаться на ресурс из компонента через `[Property]`.
- ✅ Как найти все ресурсы одного типа через `ResourceLibrary`.

---

📚 **Facepunch docs:** [assets/resources/index.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/assets/resources/index.md)

**Следующий шаг:** [00.30 — Логирование и диагностика (Log, Assert)](00_30_Log_Assert.md)

---

<!-- seealso -->
## 🔗 См. также

- [06.04 — BaseWeapon](06_04_BaseWeapon.md)
- [18.01 — PostProcessResource](18_01_PostProcessResource.md)

