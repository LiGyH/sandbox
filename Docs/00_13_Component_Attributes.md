# 00.13 — Атрибуты компонентов

## Что мы делаем?

Разбираем **атрибуты C#**, которые ты будешь видеть над каждым полем и методом в коде Sandbox: `[Property]`, `[Sync]`, `[RequireComponent]`, `[Button]`, `[Category]`, `[Range]`, `[Change]` и другие. Без понимания этих атрибутов код читается как шифр.

> 💡 **Атрибут** в C# — это метаданные, которые пишутся в квадратных скобках `[]` над полем, методом или классом. Сам по себе атрибут ничего не делает — его читает движок и меняет своё поведение.

## `[Property]` — показать в инспекторе

Самый частый атрибут. Делает поле видимым в окне **Inspector** редактора, чтобы дизайнер мог его поменять без программиста.

```csharp
public sealed class Enemy : Component
{
    [Property] public float Speed { get; set; } = 100f;
    [Property] public int Health { get; set; } = 100;
    [Property] public Color TintColor { get; set; } = Color.Red;

    public float InternalState;   // НЕ показывается (без атрибута)
}
```

В редакторе: выдели `GameObject` с этим компонентом — и увидишь поля Speed / Health / TintColor с возможностью менять.

### Уточняющие атрибуты для Property

```csharp
[Property, Range( 0, 500 )] public float Speed { get; set; }        // слайдер
[Property, Category( "Combat" )] public int Damage { get; set; }    // группа
[Property, Title( "Максимум HP" )] public int MaxHp { get; set; }   // название
[Property, Description( "Скорость в юнитах/сек" )] public float Spd { get; set; }
[Property, Hide] public int Internal { get; set; }                  // скрыть
[Property, ReadOnly] public int Computed { get; set; }              // только чтение
[Property, TextArea] public string Description { get; set; }        // многострочный
```

## `[RequireComponent]` — автоматическая ссылка

Гарантирует, что на этом `GameObject` будет указанный компонент (создаст его, если нет), и сразу даёт ссылку:

```csharp
public sealed class Player : Component
{
    [RequireComponent] public CharacterController Controller { get; set; }

    protected override void OnFixedUpdate()
    {
        Controller.Move();  // просто работает, не надо GetComponent
    }
}
```

## `[Sync]` — синхронизировать по сети

Делает свойство **сетевым** — когда владелец меняет значение, оно автоматически отправляется всем остальным клиентам. Подробнее см. [00.24 — Sync Properties](00_24_Sync_Properties.md).

```csharp
public sealed class Player : Component
{
    [Sync] public int Kills { get; set; }
    [Sync] public bool IsAlive { get; set; } = true;
}
```

Можно комбинировать:

```csharp
[Property, Sync] public int MaxHp { get; set; } = 100;
```

## `[Change]` — реагировать на изменение Sync

```csharp
[Sync, Change( "OnHealthChanged" )] public int Health { get; set; }

void OnHealthChanged( int oldValue, int newValue )
{
    if ( newValue <= 0 ) OnDeath();
}
```

Вызывается на **всех** клиентах, у которых значение обновилось.

## `[Button]` — кнопка в инспекторе

Делает из метода кнопку в редакторе, которую можно нажать мышкой (удобно для тестов):

```csharp
[Button( "Добить врага" )]
public void KillEnemy()
{
    Health = 0;
}
```

## Атрибуты RPC — `[Rpc.Broadcast]`, `[Rpc.Owner]`, `[Rpc.Host]`

Делают метод **удалённым вызовом** — когда ты вызываешь его, он выполнится не только у тебя, но и у других клиентов. Подробнее: [00.23 — RPC сообщения](00_23_Rpc_Messages.md).

```csharp
[Rpc.Broadcast]
public void PlayShootSound()
{
    Sound.FromWorld( "weapon.shoot", WorldPosition );
}
```

## Атрибуты для консоли — `[ConVar]`, `[ConCmd]`

Делают статическое поле — переменной консоли, а статический метод — командой. Подробнее: [00.18 — ConVar и ConCmd](00_18_ConVar_ConCmd.md).

```csharp
[ConVar] public static bool debug_bullets { get; set; } = false;

[ConCmd( "hello" )]
public static void HelloCmd() => Log.Info( "Hi!" );
```

## `[GameResource]` — свой формат ресурса

Делает класс **ассетом**, который можно создать в редакторе через Create → [имя]. Подробнее: [00.29 — GameResource](00_29_GameResource.md).

```csharp
[GameResource( "Weapon", "weapon", "Описание оружия" )]
public class WeaponResource : GameResource
{
    public int Damage { get; set; }
    public string ViewModel { get; set; }
}
```

## Шпаргалка

| Атрибут | На что ставят | Что делает |
|---|---|---|
| `[Property]` | поле / свойство | Показать в Inspector |
| `[Sync]` | свойство | Синхронизировать по сети |
| `[Change("OnX")]` | Sync свойство | Вызвать метод при изменении |
| `[RequireComponent]` | свойство типа Component | Гарантировать и подтянуть |
| `[Button("Имя")]` | метод | Кнопка в Inspector |
| `[Rpc.Broadcast]` | метод | Вызывается у всех клиентов |
| `[Rpc.Owner]` | метод | Вызывается только у владельца |
| `[Rpc.Host]` | метод | Вызывается только у хоста |
| `[ConVar]` | static свойство | Переменная консоли |
| `[ConCmd("name")]` | static метод | Команда консоли |
| `[GameResource(...)]` | класс ресурса | Создаваемый ассет |
| `[Category("X")]` | Property | Группа в Inspector |
| `[Range(a, b)]` | Property | Слайдер от a до b |
| `[Title("X")]` | Property | Название в UI |
| `[Hide]` / `[ReadOnly]` | Property | Спрятать / только чтение |

## Результат

После этого этапа ты **понимаешь, что означает каждая квадратная скобка над полем** в коде Sandbox. В остальных фазах эти атрибуты встречаются постоянно — теперь они не будут сбивать с толку.

---

📚 **Facepunch docs:**
- [editor/property-attributes.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/editor/property-attributes.md)
- [networking/sync-properties.md](https://github.com/Facepunch/sbox-docs/blob/master/docs/networking/sync-properties.md)

**Следующий шаг:** [00.14 — Порядок выполнения (Execution Order)](00_14_Execution_Order.md)
