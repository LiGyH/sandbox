# 28.08 — События анимаций: теги, триггеры, callback’и

## Что мы делаем?

Учимся **синхронизировать игровую логику с моментами внутри анимации**. Шаг ноги касается пола → надо сыграть звук шага. Анимация выстрела доходит до кадра «гильза вылетает» → надо заспаунить эффект. Анимация удара доходит до пика замаха → надо нанести урон. Это и есть **anim events**.

## Откуда берутся события

Источников три:

| Источник | Где задано | Что приносит |
|---|---|---|
| **Tags** на клипе | В редакторе модели — на временной шкале `.vanim`. | Имя тега, время начала/конца. |
| **Generic events** на клипе | Аналогично, но это «точечный» вызов. | Имя события + опц. данные. |
| **Footstep events** | Специальный тип события, ставится на клипах ходьбы. | Тип ноги (left/right), surface tag. |

Граф во время проигрывания клипа **поднимает** эти события на `SkinnedModelRenderer`, и ты можешь подписаться на них из C#.

## Подписка на события

`SkinnedModelRenderer` поднимает события в виде делегатов и сценовых событий:

```csharp
public sealed class FootstepSounds : Component, Component.IDamageable
{
    [RequireComponent] public SkinnedModelRenderer Renderer { get; set; }

    protected override void OnEnabled()
    {
        Renderer.OnFootstepEvent  += OnFootstep;
        Renderer.OnGenericEvent   += OnGeneric;
    }

    protected override void OnDisabled()
    {
        Renderer.OnFootstepEvent  -= OnFootstep;
        Renderer.OnGenericEvent   -= OnGeneric;
    }

    void OnFootstep( SceneModel.FootstepEvent e )
    {
        // e.FootId       — 0 = left, 1 = right
        // e.WorldPosition — где приземлилась стопа
        // e.Volume        — нормализованная громкость (зависит от скорости)
        Sound.Play( "footstep.concrete", e.WorldPosition );
    }

    void OnGeneric( SceneModel.GenericEvent e )
    {
        if ( e.Type == "AE_MUZZLE_FLASH" )
        {
            // эффект из анимации выстрела
        }
    }
}
```

> **Имена и сигнатуры** в текущих сборках s&box могут немного отличаться (`OnAnimEvent`, `OnAnimGraphEvent` — встречаются разные варианты). Главное — **подписка идёт на сам Renderer**, а событие приносит имя тега и время.

## Footstep events — типичный паттерн

1. Художник на каждом клипе ходьбы ставит **footstep tag** в моменты касания пола (обычно 2 на цикл шага).
2. В коде подписываешься на `OnFootstepEvent`.
3. Внутри обработчика делаешь **трейс вниз** на 4–8 unit’ов, читаешь `surface` поверхности и играешь звук под него:

```csharp
void OnFootstep( SceneModel.FootstepEvent e )
{
    var tr = Scene.Trace
        .Ray( e.WorldPosition + Vector3.Up * 8f, e.WorldPosition + Vector3.Down * 8f )
        .Run();

    if ( !tr.Hit ) return;

    var surf = tr.Surface;
    var snd  = surf?.Sounds.FootLand;
    if ( snd is not null )
        Sound.Play( snd, e.WorldPosition, volume: e.Volume );
}
```

В Sandbox для этого уже есть `Code/Player/SandboxVoice.cs` и общий механизм `Surface.Sounds` (см. `Assets/surface`). Звуки проигрываются с громкостью, пропорциональной скорости — никакого «топания» при ходьбе крадучись.

## Generic events для оружия

Художник кладёт на `.vanim`-клип выстрела событие `AE_MUZZLE_FLASH` и `AE_EJECT_BRASS` в нужных кадрах:

- **AE_MUZZLE_FLASH** — спаунить вспышку.
- **AE_EJECT_BRASS** — выбросить гильзу.
- **AE_MELEE_HIT**   — нанести урон в анимации удара.

В коде:

```csharp
void OnGeneric( SceneModel.GenericEvent e )
{
    switch ( e.Type )
    {
        case "AE_MUZZLE_FLASH": SpawnMuzzleFlash(); break;
        case "AE_EJECT_BRASS":  EjectBrass();       break;
        case "AE_MELEE_HIT":    DealMeleeDamage();  break;
    }
}
```

В Sandbox’е те же действия для view-модели делаются **из обработчиков `OnAttack`** напрямую — ровно потому, что точные кадры событий жёстко привязаны к клипу. Если переключиться на «запуск из anim event», эффекты автоматически синхронизируются с клипом, даже если художник его укоротил/удлинил.

## Tags vs generic events

- **Tag** живёт в течение интервала: `start..end`. Удобно для «сейчас идёт анимация замаха» — пока тег активен, нельзя отменить удар.
- **Generic event** — один кадр (точка). Удобно для «сейчас в этом кадре нужно действие».

Тег читается из C# через **граф** (как условие перехода) или через `Renderer.GetTag("name")`-подобные API.

## Сетевые тонкости

Для **косметических** событий (вспышка, гильза, звук шага):

- Анимация **синхронизируется** между клиентами автоматически (parameter sync).
- Поэтому событие срабатывает на каждом клиенте локально — `Sound.Play`/`Particle` запускать **без RPC**.
- Никогда не шли вспышку через `[Rpc]` — будет двойная вспышка на хосте.

Для **геймплейных** действий (нанесение урона, расход патрона):

- Они должны срабатывать **только на хосте** (или у владельца, если так спроектировано).
- Поэтому в обработчике события проверяй: `if ( IsProxy ) return;` (или `if ( !Networking.IsHost ) return;`).

## Подсказки на практике

- **Не подписывайся в `OnUpdate`** — только в `OnEnabled`/`OnDisabled`.
- **Имена тегов фиксируй в `const string`-полях** в одном месте, иначе typo съест событие.
- **Если событие не приходит** — проверь, что:
  1. Граф **проигрывает** именно тот клип.
  2. Скорость воспроизведения не = 0.
  3. Тег действительно **сохранён** в `.vanim` (через редактор модели → Animations → Events).
- **Не подменяй anim event таймером** (`Invoke(0.5f, ...)`) — рассыпется при изменении скорости анимации.

## Результат

После этого этапа ты знаешь:

- ✅ Откуда берутся anim events: tags, generic events, footstep.
- ✅ Как подписаться на них в C# через `SkinnedModelRenderer`.
- ✅ Стандартный паттерн footstep’ов с трейсом вниз и `Surface.Sounds`.
- ✅ Когда использовать anim event, а когда — `Invoke`/RPC.
- ✅ Сетевые правила: косметика без RPC, геймплей только на хосте.

---

📚 **Официальная документация Facepunch:** [animation/animation-events.md](https://github.com/Facepunch/sbox-docs/tree/master/docs/animation)

**Следующий шаг:** [28.09 — Морфы и FacePose](28_09_MorphState_FacePose.md)
