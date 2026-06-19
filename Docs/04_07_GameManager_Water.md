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
using Sandbox.Audio;

[Icon( "water" )]
public partial class WaterVolume : Component, Component.ITriggerListener
{
    [Property, Group( "Sound" )] private SoundEvent SoundEnter { get; set; }
        = ResourceLibrary.Get<SoundEvent>( "sounds/water_enter.sound" );

    [Property, Group( "Sound" )] private SoundEvent SoundExit { get; set; }
        = ResourceLibrary.Get<SoundEvent>( "sounds/water_exit.sound" );

    [RequireComponent] private BoxCollider Collider { get; set; }

    /// <summary>
    /// Roots of objects currently in the water.
    /// Handy for playing sounds only once per object, even if it has multiple colliders (like ragdolls)
    /// </summary>
    private HashSet<GameObject> Objects = new();
    private List<Rigidbody> Bodies = new();

    private BBox Bounds => BBox.FromPositionAndSize( WorldTransform.PointToWorld( Collider.Center ), WorldScale * Collider.Scale );

    protected override void OnFixedUpdate()
    {
        if ( Bodies is null || !Collider.IsValid() )
            return;

        var bbox = Bounds;
        var waterSurface = bbox.Center + Vector3.Up * bbox.Extents.z;
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

    bool _wasCamUnderwater;
    protected override void OnUpdate()
    {
        if ( !Collider.IsValid() )
            return;

        var camera = Scene.Camera;
        bool isCamUnderwater = camera.IsValid() && Bounds.Contains( camera.WorldPosition );
        if ( isCamUnderwater != _wasCamUnderwater )
        {
            if ( isCamUnderwater ) OnCameraEnter();
            else OnCameraExit();
        }

        _wasCamUnderwater = isCamUnderwater;
    }

    DspProcessor _dsp;
    void OnCameraEnter()
    {
        var gameMixer = Mixer.FindMixerByName( "Game" );
        if ( gameMixer is null ) return;

        _dsp ??= new DspProcessor( "water.small" );
        gameMixer.AddProcessor( _dsp );
    }

    void OnCameraExit()
    {
        var gameMixer = Mixer.FindMixerByName( "Game" );
        if ( gameMixer is null ) return;

        gameMixer.RemoveProcessor( _dsp );
        _dsp = null;
    }

    void ITriggerListener.OnTriggerEnter( Collider other )
    {
        var body = other.Rigidbody;
        if ( !body.IsValid() || Bodies.Contains( body ) )
            return;

        Bodies.Add( body );

        var root = other.GameObject.Root;
        if ( Objects.Add( root ) )
        {
            if ( SoundEnter.IsValid() )
            {
                other.GameObject.PlaySound( SoundEnter );
            }
        }
    }

    void ITriggerListener.OnTriggerExit( Collider other )
    {
        var body = other.Rigidbody;
        if ( !body.IsValid() ) return;

        Bodies.Remove( body );

        var root = other.GameObject.Root;
        if ( Objects.Remove( root ) )
        {
            if ( SoundExit.IsValid() )
            {
                other.GameObject.PlaySound( SoundExit );
            }
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
            volume.Surface ??= Surface.FindByName( "water" );
            volume.GetOrAddComponent<WaterVolume>();
        }
    }
}
```

## Разбор кода

### `WaterVolume : Component, ITriggerListener`

- **Triggers.** Чтобы движок присылал нам `OnTriggerEnter`/`OnTriggerExit`, на том же `GameObject` должен быть коллайдер с включённым `IsTrigger` и тегом `water`. См. [26.05 — Triggers](26_05_Triggers.md).
- **`OnTriggerEnter`** — когда любая физическая штука пересекла объём, мы берём `other.Rigidbody` (новый удобный доступ к телу коллайдера). Корень объекта (`other.GameObject.Root`) добавляем в `HashSet<GameObject> Objects` — так звук входа в воду играется **один раз на объект**, даже если у него много коллайдеров (как у рэгдолла), и проигрываем `SoundEnter`.
- **`OnTriggerExit`** — симметрично убираем `Rigidbody` из списка и, если объект полностью покинул воду, проигрываем `SoundExit`.
- **Звук и DSP под водой.** Свойства `SoundEnter`/`SoundExit` (группа `Sound`) задают звуки входа/выхода. В `OnUpdate` компонент следит за камерой: когда она оказывается внутри `Bounds`, на микшер `"Game"` навешивается `DspProcessor("water.small")` (подводный эффект), а при выходе — снимается.
- **`[RequireComponent] BoxCollider Collider`** — коллайдер теперь обязательная зависимость; `Bounds` считается из его `Center`/`Scale` в мировых координатах.

### `OnFixedUpdate` и `Rigidbody.ApplyBuoyancy`

```csharp
var bbox = Bounds;
var waterSurface = bbox.Center + Vector3.Up * bbox.Extents.z;
var waterPlane = new Plane( waterSurface, Vector3.Up );

body.ApplyBuoyancy( waterPlane, Time.Delta );
```

- Поверхность воды считаем из `Bounds` (BBox по `BoxCollider`): центр объёма + половина высоты по Z.
- На каждом шаге физики (`OnFixedUpdate`, не `OnUpdate`!) вызываем встроенный `Rigidbody.ApplyBuoyancy(plane, dt)`. Это движковый метод — Facepunch реализует силу Архимеда сам, нам остаётся только сказать «вот такая поверхность, толкай вверх с такой дельтой».
- Перед вызовом проверяем `body.IsValid()` — пока пропы плавают, их могут удалить (Cleanup, Undo, и т.п.); невалидные сразу выкидываем из списка.

> ⚠️ `BoxCollider` теперь помечен `[RequireComponent]`, поэтому компонент гарантированно его получает; `OnFixedUpdate`/`OnUpdate` дополнительно проверяют `Collider.IsValid()` и тихо выходят, если коллайдера нет.

### `GameManager : ISceneLoadingEvents`

`GameManager` — `partial class` ([04.01](04_01_GameManager.md)). Этот файл добавляет ему ещё один интерфейс — `ISceneLoadingEvents` от движка. Метод `AfterLoad` вызывается **после** загрузки сцены, но до того, как игрок появится:

```csharp
void ISceneLoadingEvents.AfterLoad( Scene scene )
{
    var waterVolumes = scene.GetAll<Collider>().Where( x => x.Tags.Has( "water" ) );
    foreach ( var volume in waterVolumes )
    {
        volume.Surface ??= Surface.FindByName( "water" );
        volume.GetOrAddComponent<WaterVolume>();
    }
}
```

- `scene.GetAll<Collider>()` — линейный обход всех коллайдеров сцены. Для огромных карт это «дорого один раз», но `AfterLoad` стреляет редко.
- `volume.Surface ??= Surface.FindByName( "water" )` — если у коллайдера не задана поверхность, назначаем стандартную `water` (звуки, трение, всплески берутся из неё).
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
