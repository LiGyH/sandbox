# 09.11 — Интерфейс событий тул-экшенов (IToolActionEvents) ⚡

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.25 — События сети (ISceneEvent)](00_25_Network_Events.md)
> - [00.19 — Input: основы](00_19_Input_Basics.md)

> **🔗 Связанные документы:**
>
> - [09.01 — ToolMode](09_01_ToolMode.md) — кто стреляет события (`RegisterAction` + `DispatchActions`)
> - [04.06 — LimitsSystem](04_06_LimitsSystem.md) — пример слушателя
> - [14.01b — ISpawnEvents](14_01b_ISpawnEvents.md) — параллельный интерфейс для системы спавна

## Что мы делаем?

Создаём два связанных файла:

1. **`ToolAction.cs`** — `enum ToolInput`, `enum InputMode`, и `record ToolActionEntry`. Это «словарь» того, что вообще такое «действие тула».
2. **`IToolActionEvents.cs`** — сценовый интерфейс, через который любая система может перехватить или просто отнаблюдать действие тулгана.

Вместе они дают единый, типобезопасный способ описать «у моего инструмента ЛКМ ставит шарик, ПКМ его лопает» — и в одном месте навешивать на это лимиты/античит/статистику.

## Зачем это нужно?

Раньше каждый `ToolMode` сам читал `Input.Pressed( "attack1" )` в `OnControl()`, сам решал когда что делать, сам вешал статистику и Undo. Это было дублированием и лишало возможности **глобально** перехватить действие — например, отменить его, если у игрока кончился лимит шариков.

Теперь:

- Тулы регистрируют свои действия через `RegisterAction(...)` (см. [09.01](09_01_ToolMode.md)).
- Базовый класс автоматически читает ввод и оборачивает каждый колбэк в пару `OnToolAction` → действие → `OnPostToolAction`.
- Любой компонент / `GameObjectSystem`, реализующий `IToolActionEvents`, получает оба коллбэка и может отменить (`Cancelled = true`) или просто сосчитать.

## Как это работает внутри движка?

### `ToolAction.cs` — типы

```csharp
public enum ToolInput
{
    Primary,    // attack1 (ЛКМ)
    Secondary,  // attack2 (ПКМ)
    Reload      // reload  (R)
}

public enum InputMode
{
    Pressed,    // один раз при нажатии
    Down        // каждый кадр пока удерживается
}

public sealed record ToolActionEntry(
    ToolInput Input,
    Func<string> Name,           // динамическое имя для подсказки HUD
    Action Callback,             // что выполнять
    InputMode Mode = InputMode.Pressed
)
{
    public string InputAction => Input switch
    {
        ToolInput.Primary   => "attack1",
        ToolInput.Secondary => "attack2",
        ToolInput.Reload    => "reload",
        _                    => null
    };
}
```

Особенности:

- **`Func<string> Name`** — лямбда, а не строка: имя действия может зависеть от состояния инструмента (например, у `Stacker` подсказка зависит от того, выбран ли первый объект).
- **`record`** — иммутабельная, удобно сравнивать и хранить в списке `_actions`.
- **`InputAction`** — мост между нашим высокоуровневым `ToolInput` и строковыми именами действий движка (`"attack1"`/`"attack2"`/`"reload"`), которые ждёт `Input.Pressed`/`Input.Down`.

### `IToolActionEvents.cs` — события

```csharp
public interface IToolActionEvents : ISceneEvent<IToolActionEvents>
{
    public class ActionData
    {
        public ToolMode Tool   { get; init; }   // какой тул сработал
        public ToolInput Input { get; init; }   // какая кнопка
        public PlayerData Player { get; init; } // кто
        public bool Cancelled  { get; set; }    // отменить действие
    }

    public class PostActionData
    {
        public ToolMode Tool   { get; init; }
        public ToolInput Input { get; init; }
        public PlayerData Player { get; init; }
        public List<GameObject> CreatedObjects { get; init; } // что создал тул (опционально)
    }

    void OnToolAction( ActionData e ) { }
    void OnPostToolAction( PostActionData e ) { }
}
```

`PostActionData.CreatedObjects` заполняется тем, что тул кладёт через `Track(...)` (см. [09.01 — ToolMode](09_01_ToolMode.md)). Это позволяет, например, `LimitsSystem` сразу пометить созданные объекты как принадлежащие игроку, не сканируя сцену.

### Поток вызовов

```text
Игрок нажимает ЛКМ
  ↓
ToolMode.DispatchActions()      ← в OnControl() базового класса
  ↓
FireToolAction( ToolInput.Primary )
  → Scene.RunEvent<IToolActionEvents>( x => x.OnToolAction(data) )
    → LimitsSystem.OnToolAction(...)   // может выставить data.Cancelled = true
    → AntiCheat.OnToolAction(...)
  ↓ (если не Cancelled)
action.Callback?.Invoke()        ← непосредственно поведение тула (OnPlace, OnWeld, …)
  ↓
FirePostToolAction( ToolInput.Primary )
  → Scene.RunEvent<IToolActionEvents>( x => x.OnPostToolAction(post) )
    → LimitsSystem.OnPostToolAction(...) // трекает CreatedObjects
    → Stats.OnPostToolAction(...)
```

> ⚠️ Те же оговорки, что и у [`ISpawnEvents`](14_01b_ISpawnEvents.md): события стреляются на той стороне, где живёт тул (обычно у владельца — `IsProxy == false`), а серверная логика идёт через `[Rpc.Host]` внутри колбэков.

## Создай файлы

**`Code/Weapons/ToolGun/ToolAction.cs`** — см. полный исходник в репозитории, ключевые типы перечислены выше.

**`Code/Weapons/ToolGun/IToolActionEvents.cs`** — см. полный исходник в репозитории, единственный публичный интерфейс с двумя классами данных и двумя методами с дефолтной реализацией.

## Пример: блокировать тулган в зоне «без строительства»

```csharp
public sealed class NoBuildZone : Component, IToolActionEvents
{
    [Property] public BBox Zone { get; set; }

    void IToolActionEvents.OnToolAction( IToolActionEvents.ActionData e )
    {
        if ( e.Player is null ) return;
        var pos = e.Player.GameObject.WorldPosition;
        if ( Zone.Contains( pos ) )
            e.Cancelled = true;
    }
}
```

## Пример: статистика по тулам

```csharp
public sealed class ToolStats : GameObjectSystem<ToolStats>, IToolActionEvents
{
    public ToolStats( Scene scene ) : base( scene ) { }

    void IToolActionEvents.OnPostToolAction( IToolActionEvents.PostActionData e )
    {
        var name = e.Tool?.GetType().Name ?? "Unknown";
        e.Player?.AddStat( $"tool.{name}.{e.Input}" );
    }
}
```

## Проверка

1. Любой тул, использующий `RegisterAction(...)` (Balloon, Mass, Wheel, Thruster, Hoverball, Emitter, Resizer, Decal, Stacker, Trail, …), стреляет `OnToolAction` перед каждым колбэком.
2. Если хотя бы один слушатель выставит `Cancelled = true`, колбэк **не вызывается**, `OnPostToolAction` не стреляется.
3. Если внутри колбэка тул вызвал `Track(go)` — этот объект появится в `PostActionData.CreatedObjects`.
4. Старые тулы, которые ещё читают `Input.Pressed( "attack1" )` напрямую в `OnControl()`, события **не стреляют** — миграция таких тулов на `RegisterAction` обязательна, чтобы они корректно учитывались `LimitsSystem` и подобными системами.

## Ссылки на движок

- [`ISceneEvent<T>`](https://github.com/Facepunch/sbox-docs/) — диспатч сценовых событий.
- [`Input.Pressed` / `Input.Down`](https://github.com/Facepunch/sbox-docs/) — как `DispatchActions` решает, активно ли действие.

---

## ➡️ Следующий шаг

Возвращайся к **[09.01 — ToolMode](09_01_ToolMode.md)**, чтобы посмотреть, как именно базовый класс использует `RegisterAction` и оборачивает колбэки в эти события.
