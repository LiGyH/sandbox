# 04.07 — Вода и плавучесть (GameManager.Water) 🌊

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.11 — Tags](00_11_Tags.md) — компонент-вода ищется по тегу `water`
> - [00.12 — Component](00_12_Component.md)
>
> **🔗 Связанные документы:**
>
> - [04.01 — GameManager](04_01_GameManager.md) — куда подключается этот partial
> - [26.01 — Physics_Bodies](26_01_Physics_Bodies.md) — `Rigidbody.ApplyBuoyancy`

## Что мы делаем?

Создаём **`WaterVolume`** — компонент, который превращает любой триггер с тегом `water` в объём воды: попавшие туда `Rigidbody` начинают плавать (получают силу Архимеда от движка), при выходе — перестают.

И добавляем в `GameManager` обработчик `ISceneLoadingEvents.AfterLoad`: после загрузки сцены он находит все коллайдеры с тегом `water` и автоматически вешает на них `WaterVolume`. Маппер просто ставит триггер с тегом — игровой режим сам делает остальное.

Это новый файл, добавленный в актуальной версии sandbox. Без него вода с уровня (например, в карте `zoo`) — просто красивая картинка, без физики плавучести.

## Путь к файлу

```
Code/GameLoop/GameManager.Water.cs
```

## Полный код

```csharp
using Sandbox.UI;

public partial class WaterVolume : Component, Component.ITriggerListener
{
    List<Rigidbody> Bodies = new();

    protected override void OnFixedUpdate()
    {
        if ( Bodies is null )
            return;

        var collider = GetComponent<BoxCollider>();
        var waterSurface = WorldPosition + Vector3.Up * (collider.Scale.z * 0.5f);
        var waterPlane = new Plane( waterSurface, Vector3.Up );

        for ( int i = Bodies.Count - 1; i >= 0; i-- )
        {
            var body = Bodies[i];
            if ( !body.IsValid() )
            {
                Bodies.RemoveAt( i );
                continue;
            }

            body.ApplyBuoyancy( waterPlane, Time.Delta );
        }
    }

    void ITriggerListener.OnTriggerEnter( Collider other )
    {
        var body = other.GameObject.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndParent );
        if ( body.IsValid() && !Bodies.Contains( body ) )
        {
            Bodies.Add( body );
        }
    }

    void ITriggerListener.OnTriggerExit( Collider other )
    {
        var body = other.GameObject.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndParent );
        if ( body.IsValid() )
        {
            Bodies.Remove( body );
        }
    }
}

public sealed partial class GameManager : ISceneLoadingEvents
{
    void ISceneLoadingEvents.AfterLoad( Scene scene )
    {
        var waterVolumes = scene.GetAll<Collider>().Where( x => x.Tags.Has( "water" ) );
        if ( waterVolumes.Count() < 1 ) return;

        foreach ( var volume in waterVolumes )
        {
            volume.GetOrAddComponent<WaterVolume>();
        }
    }
}
```

## Разбор кода

### `WaterVolume : Component, ITriggerListener`

- **Triggers.** Чтобы движок присылал нам `OnTriggerEnter`/`OnTriggerExit`, на том же `GameObject` должен быть коллайдер с включённым `IsTrigger` и тегом `water`. См. [26.05 — Triggers](26_05_Triggers.md).
- **`OnTriggerEnter`** — когда любая физическая штука пересекла объём, мы поднимаемся по иерархии (`FindMode.EverythingInSelfAndParent`) и ищем `Rigidbody`. Это нужно потому, что у пропа коллайдер обычно лежит на дочернем GO, а сам `Rigidbody` — на корневом.
- **`OnTriggerExit`** — симметрично убираем `Rigidbody` из списка.

### `OnFixedUpdate` и `Rigidbody.ApplyBuoyancy`

```csharp
var collider = GetComponent<BoxCollider>();
var waterSurface = WorldPosition + Vector3.Up * (collider.Scale.z * 0.5f);
var waterPlane = new Plane( waterSurface, Vector3.Up );

body.ApplyBuoyancy( waterPlane, Time.Delta );
```

- Берём `BoxCollider` объёма и считаем плоскость поверхности воды: центр объёма + половина высоты.
- На каждом шаге физики (`OnFixedUpdate`, не `OnUpdate`!) вызываем встроенный `Rigidbody.ApplyBuoyancy(plane, dt)`. Это движковый метод — Facepunch реализует силу Архимеда сам, нам остаётся только сказать «вот такая поверхность, толкай вверх с такой дельтой».
- Перед вызовом проверяем `body.IsValid()` — пока пропы плавают, их могут удалить (Cleanup, Undo, и т.п.); невалидные сразу выкидываем из списка.

> ⚠️ Текущая реализация считает поверхность по `BoxCollider`. Если на объёме другой коллайдер (`Sphere`, `Capsule`, mesh), `GetComponent<BoxCollider>()` вернёт `null` и в `OnFixedUpdate` упадёт. Для своих карт держи воду как `BoxCollider` либо расширь компонент.

### `GameManager : ISceneLoadingEvents`

`GameManager` — `partial class` ([04.01](04_01_GameManager.md)). Этот файл добавляет ему ещё один интерфейс — `ISceneLoadingEvents` от движка. Метод `AfterLoad` вызывается **после** загрузки сцены, но до того, как игрок появится:

```csharp
void ISceneLoadingEvents.AfterLoad( Scene scene )
{
    var waterVolumes = scene.GetAll<Collider>().Where( x => x.Tags.Has( "water" ) );
    foreach ( var volume in waterVolumes )
        volume.GetOrAddComponent<WaterVolume>();
}
```

- `scene.GetAll<Collider>()` — линейный обход всех коллайдеров сцены. Для огромных карт это «дорого один раз», но `AfterLoad` стреляет редко.
- `GetOrAddComponent<WaterVolume>()` — идемпотентно: если вода уже была подготовлена (например, заскриптована префабом), второй компонент не появится.

## Как добавить воду на свою карту

1. Создай в Hammer / в редакторе сцены пустой `GameObject`.
2. Повесь на него `BoxCollider`, выставь `IsTrigger = true`, размер по объёму воды.
3. Добавь тег `water` в `Tags`.
4. Сохрани сцену. При следующей загрузке `GameManager.AfterLoad` сам повесит `WaterVolume`, и пропы начнут плавать.

Никаких ручных регистраций не нужно — это и есть смысл «соглашение по тегу».

## Что проверить

- Заспавни деревянный ящик над водой на карте `zoo` — он должен медленно всплыть на поверхность и качаться.
- Толкни тяжёлый металлический ящик в воду — должен утонуть медленнее, чем без воды (плавучесть всё равно работает, но `Rigidbody.MassDensity` его перевешивает).
- Удалить ящик через тулган-Remover, пока он плавает — `WaterVolume` не должен упасть с `NullReferenceException` (защищено `body.IsValid()`).
- Создай свой `BoxCollider` с тегом `water` в реальном времени — он не получит `WaterVolume` (мы вешаем только в `AfterLoad`); это интенциональное ограничение для производительности.

## Ссылки

- Движковый метод [`Rigidbody.ApplyBuoyancy`](https://github.com/Facepunch/sbox-docs/) описан в официальной документации Facepunch (раздел Physics → Rigidbody).
- [`ISceneLoadingEvents`](https://github.com/Facepunch/sbox-docs/) — события загрузки сцены, движок зовёт `BeforeLoad`/`AfterLoad` на любом компоненте, реализующем интерфейс.

---

## ➡️ Следующий шаг

Переходи обратно к фазе игрока — например, к **[05.01 — Стили Theme/HUD](05_01_Стили_Theme_Hud.md)** — или продолжай изучение `GameManager`-вселенной с **[04.06 — LimitsSystem](04_06_LimitsSystem.md)**.
