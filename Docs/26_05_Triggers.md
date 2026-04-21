# 26.05 — Триггеры: OnTriggerEnter/Exit

## Что мы делаем?

**Триггер** — это коллайдер, который **не толкается**, но фиксирует, кто в него вошёл и кто вышел. Это «невидимая зона»: телепорт, чекпоинт, область под прыгающую платформу, аура регенерации, зона безопасности на спавне.

## Как сделать триггер

1. Добавь на `GameObject` любой `Collider` (Box, Sphere, Capsule).
2. Поставь галочку **`IsTrigger`** в инспекторе (или `collider.IsTrigger = true` в коде).
3. Реализуй на этом `GameObject` компонент с интерфейсом `Component.ITriggerListener`.

```csharp
public sealed class HealZone : Component, Component.ITriggerListener
{
    [Property] public float HealPerSecond { get; set; } = 10f;

    void Component.ITriggerListener.OnTriggerEnter( Collider other )
    {
        Log.Info( $"{other.GameObject.Name} зашёл в зону" );
    }

    void Component.ITriggerListener.OnTriggerExit( Collider other )
    {
        Log.Info( $"{other.GameObject.Name} вышел из зоны" );
    }
}
```

| Метод | Когда |
|---|---|
| `OnTriggerEnter( Collider )` | в кадре, когда объект впервые касается зоны |
| `OnTriggerExit( Collider )` | в кадре, когда объект перестал касаться |

> ❗ Промежуточного `OnTriggerUpdate` **нет**. Если нужно «делать что-то, пока внутри», см. ниже про `Touching`.

## Получаем хозяина

Параметр `other` — это сам `Collider`. Чтобы добраться до игрока/компонента:

```csharp
void Component.ITriggerListener.OnTriggerEnter( Collider other )
{
    var player = other.GameObject.Components.GetInAncestorsOrSelf<Player>();
    if ( player == null ) return;

    player.Heal( 50 );
    Sound.Play( "pickup.health", WorldPosition );
}
```

`GetInAncestorsOrSelf` нужен, потому что игрок обычно состоит из нескольких объектов: контроллер сверху, физическая капсула снизу. В триггер «попадает» именно капсула.

## Делаем что-то постоянно («пока внутри»)

Пример: «пока ты в зоне — лечишься на 10 HP/сек». Лучше всего хранить список и работать с ним в `OnFixedUpdate`:

```csharp
public sealed class HealZone : Component, Component.ITriggerListener
{
    private readonly HashSet<Player> _players = new();

    void Component.ITriggerListener.OnTriggerEnter( Collider other )
    {
        if ( other.GameObject.Components.GetInAncestorsOrSelf<Player>() is { } p )
            _players.Add( p );
    }

    void Component.ITriggerListener.OnTriggerExit( Collider other )
    {
        if ( other.GameObject.Components.GetInAncestorsOrSelf<Player>() is { } p )
            _players.Remove( p );
    }

    protected override void OnFixedUpdate()
    {
        foreach ( var p in _players )
        {
            if ( !p.IsValid() ) continue;
            p.Heal( 10 * Time.Delta );
        }
    }
}
```

> Не забудь чистить `_players` при `OnDisabled` / `OnDestroy`, иначе после повторного включения там останутся «призраки».

## Фильтрация: только игроки, без пуль

Триггер сработает на любое тело. Если хочешь реагировать только на игроков, либо:

1. **Через теги** — на капсуле игрока стоит тег `"player"`, в `OnTriggerEnter` фильтруй:

   ```csharp
   if ( !other.Tags.Has( "player" ) ) return;
   ```

2. **Через матрицу столкновений** ([00.05](00_05_Настройки_столкновений.md)) — настрой, чтобы тег `"trigger_playeronly"` сталкивался только с тегом `"player"`. Тогда фильтр не нужен в коде.

Второй способ предпочтительнее: проверка делается на уровне физики, а не C#.

## Один триггер — несколько листенеров

`Component.ITriggerListener` можно реализовать **на нескольких компонентах** одного объекта — события придут всем. Это удобно для разделения логики: один компонент пишет в лог, второй проигрывает звук, третий лечит.

## Триггеры и сеть

События `OnTriggerEnter/Exit` приходят и на хосте, и на клиентах. Если ты делаешь телепорт, **меняй позицию игрока только на хосте** — иначе будет рассинхрон. Используй `if ( IsProxy ) return;` (см. [00.22 — Ownership](00_22_Ownership.md)).

```csharp
void Component.ITriggerListener.OnTriggerEnter( Collider other )
{
    if ( IsProxy ) return;          // только на владельце триггера
    other.GameObject.WorldPosition = TargetPoint;
}
```

## Что важно запомнить

- Триггер = `Collider` с `IsTrigger = true`. Толкания нет, события есть.
- Реализуй **`Component.ITriggerListener`** — иначе `OnTriggerEnter/Exit` не придёт.
- В `other.GameObject` сидит коллайдер — поднимайся к игроку через `GetInAncestorsOrSelf<T>()`.
- `OnTriggerUpdate` нет — храни список вошедших и обрабатывай в `OnFixedUpdate`.
- Для фильтрации лучше использовать **матрицу столкновений**, а не `if` в коде.
- В сетевой игре проверяй `IsProxy`, чтобы не дублировать логику.

## Что дальше?

В [26.06](26_06_Sound.md) — система звука: как играть звук в точке, в 3D, с затуханием.
