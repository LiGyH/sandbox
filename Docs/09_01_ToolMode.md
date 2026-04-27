# 🛠️ ToolMode.cs — Базовый абстрактный класс режима инструмента

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)
> - [00.28 — HudPainter](00_28_HudPainter.md)

## Что мы делаем?

Создаём файл `ToolMode.cs` — абстрактный базовый класс для **всех** режимов инструментальной пушки (Toolgun). Каждый конкретный инструмент (Weld, Rope, Balloon и т.д.) будет наследоваться от `ToolMode`.

## Зачем это нужно?

`ToolMode` — это «скелет», который определяет:
- Какие кнопки у инструмента (ЛКМ, ПКМ, R)
- Как рисовать прицел (белый = всё ок, красный = нельзя)
- Как рисовать название инструмента на экране viewmodel-пушки
- Как сохранять и загружать настройки инструмента (cookies)
- Как собирать статистику контрапций для системы ачивок

Без этого класса каждый инструмент пришлось бы писать полностью с нуля.

## Как это работает внутри движка?

### Наследование

```
Component (движок s&box)
  └── ToolMode (наш абстрактный класс)
        ├── BaseConstraintToolMode (для инструментов с двумя точками)
        ├── Remover
        ├── Resizer
        └── ... (все конкретные инструменты)
```

### Ключевые свойства

| Свойство | Тип | Описание |
|----------|-----|----------|
| `Toolgun` | `Toolgun` | Ссылка на оружие-тулган, к которому прикреплён режим |
| `Player` | `Player` | Ссылка на игрока-владельца |
| `IsValidState` | `bool` | Флаг — можно ли сейчас выполнить действие? |
| `AbsorbMouseInput` | `bool` | Если `true` — мышь не двигает камеру (используется Weld для вращения) |
| `Name` | `string` | Отображаемое имя инструмента |
| `Description` | `string` | Описание инструмента |
| `PrimaryAction` | `string` | Подсказка для ЛКМ (attack1). По умолчанию — имя зарегистрированного `RegisterAction(ToolInput.Primary, …)` |
| `SecondaryAction` | `string` | Подсказка для ПКМ (attack2). По умолчанию — имя `RegisterAction(ToolInput.Secondary, …)` |
| `ReloadAction` | `string` | Подсказка для R (reload). По умолчанию — имя `RegisterAction(ToolInput.Reload, …)` |
| `TraceIgnoreTags` | `IEnumerable<string>` | Теги, которые трассировка игнорирует (по умолчанию `"player"`) |
| `TraceHitboxes` | `bool` | Если `true`, трассировка также попадает в хитбоксы |

### Жизненный цикл

1. **`OnStart()`** — получает `TypeDescription` из `TypeLibrary` (метаданные типа: иконка, заголовок)
2. **`OnEnabled()`** — если мы владелец, загружает cookies (настройки)
3. **`OnDisabled()`** — отключает snap grid и сохраняет cookies

### Отрисовка

- **`DrawScreen(rect, paint)`** — рисует иконку и название инструмента на экране viewmodel (маленький экранчик на пушке). Если текст помещается по ширине — рисуется по центру; если нет (длинное имя инструмента) — переходит в режим **марки/marquee** и плавно прокручивается справа налево, бесшовно повторяясь с зазором.
- **`DrawHud(painter, crosshair)`** — рисует прицел:
  - Белый круг — `IsValidState == true`
  - Красный круг — `IsValidState == false`

### Система достижений

`CheckContraptionStats(anchor)` — RPC-метод, который вызывается после создания соединения. Он:
1. Находит все связанные объекты через `LinkedGameObjectBuilder`
2. Считает колёса, двигатели, шарики, соединения, стулья
3. Отправляет статистику в `Sandbox.Services.Stats`

### Регистрация действий (новый API — `RegisterAction`) ⚙️

Раньше каждый тул сам читал `Input.Pressed( "attack1" )` в `OnControl()`. Теперь это делает базовый класс — тул только **регистрирует** свои действия в `OnStart()`:

```csharp
protected override void OnStart()
{
    base.OnStart();

    RegisterAction( ToolInput.Primary,   () => "#tool.hint.balloon.place_rope", OnPlaceWithRope );
    RegisterAction( ToolInput.Secondary, () => "#tool.hint.balloon.place",      OnPlaceWithoutRope );
    RegisterAction( ToolInput.Reload,    () => "#tool.hint.balloon.copy",       OnCopy );
}
```

| Поле | Назначение |
|------|------------|
| `ToolInput` | `Primary` (attack1), `Secondary` (attack2) или `Reload` (reload). См. `ToolAction.cs`. |
| `Func<string> name` | **Лямбда** для подсказки HUD — может зависеть от состояния тула (например, у Stacker на разных стадиях разный текст). Это значение автоматически отдаётся в `PrimaryAction` / `SecondaryAction` / `ReloadAction`, если вы их не переопределили. |
| `Action callback` | Что выполнить, когда сработает кнопка. |
| `InputMode mode` | `Pressed` (по умолчанию) — один раз при нажатии; `Down` — каждый кадр пока удерживается (нужно, например, для непрерывного «чёрчения»). |

Все зарегистрированные действия диспатчит приватный метод `DispatchActions()`, вызываемый из `OnControl()` базового класса. На каждом срабатывании он:

1. Проверяет соответствующий `Input.Pressed`/`Input.Down`.
2. Стреляет `IToolActionEvents.OnToolAction(...)`. Если хоть один слушатель (например, [`LimitsSystem`](04_06_LimitsSystem.md)) выставит `Cancelled = true` — колбэк не вызывается.
3. Очищает буфер `_createdObjects`, вызывает колбэк, после чего стреляет `OnPostToolAction(...)` со списком объектов из этого буфера.

Подробнее о событиях — в **[09.11 — IToolActionEvents](09_11_IToolActionEvents.md)**.

### Учёт созданных объектов — `Track(...)` 📦

Внутри колбэка действия тул может пометить созданные `GameObject`-ы:

```csharp
[Rpc.Host]
public void Spawn( … )
{
    var go = prefab.GetScene().Clone( … );
    // … настройка …
    go.NetworkSpawn( true, null );

    Track( go );  // <-- попадёт в IToolActionEvents.PostActionData.CreatedObjects
}
```

Это нужно, чтобы внешние системы (лимиты, статистика, Undo-расширения) сразу получили список созданного, не сканируя сцену. После `FirePostToolAction` буфер очищается.

### Миграция старых тулов

Если тул всё ещё читает `Input.Pressed( "attack1" )` и переопределяет `PrimaryAction`/`SecondaryAction` строкой:

```csharp
// СТАРО ❌
public override string PrimaryAction => "#tool.hint.balloon.place_rope";
public override void OnControl()
{
    base.OnControl();
    if ( Input.Pressed( "attack1" ) ) DoPlaceWithRope();
}
```

→ переписывай через `RegisterAction` (см. выше). Тулы на старом API не стреляют события и **не учитываются** [`LimitsSystem`](04_06_LimitsSystem.md).

## Ссылки на движок

- [`Component`](https://github.com/LiGyH/sbox-public) — базовый класс компонента
- [`TypeLibrary.GetType()`](https://github.com/LiGyH/sbox-public) — получение метаданных типа
- [`Rpc.Owner`](https://github.com/LiGyH/sbox-public) — RPC вызов на владельце

## Создай файл

📄 `Code/Weapons/ToolGun/ToolMode.cs`

```csharp
﻿using Sandbox.Rendering;

public abstract partial class ToolMode : Component, IToolInfo
{
	public Toolgun Toolgun => GetComponent<Toolgun>();
	public Player Player => GetComponentInParent<Player>();

	/// <summary>
	/// The mode should set this true or false in OnControl to indicate if the current state is valid for performing actions.
	/// </summary>
	public bool IsValidState { get; protected set; } = true;

	/// <summary>
	/// When true, the toolgun will absorb mouse input so the camera doesn't move.
	/// The mode can then read <see cref="Input.AnalogLook"/> to use the mouse for rotation etc.
	/// </summary>
	public virtual bool AbsorbMouseInput => false;

	/// <summary>
	/// Display name for the tool, defaults to the TypeDescription title.
	/// </summary>
	public virtual string Name => TypeDescription?.Title ?? GetType().Name;

	/// <summary>
	/// Description of what this tool does.
	/// </summary>
	public virtual string Description => string.Empty;

	/// <summary>
	/// Label for the primary action (attack1), or null if none.
	/// Auto-populated from registered <see cref="ToolActionEntry"/> when not overridden.
	/// </summary>
	public virtual string PrimaryAction => GetActionName( ToolInput.Primary );

	/// <summary>
	/// Label for the secondary action (attack2), or null if none.
	/// Auto-populated from registered <see cref="ToolActionEntry"/> when not overridden.
	/// </summary>
	public virtual string SecondaryAction => GetActionName( ToolInput.Secondary );

	/// <summary>
	/// Label for the reload action, or null if none.
	/// Auto-populated from registered <see cref="ToolActionEntry"/> when not overridden.
	/// </summary>
	public virtual string ReloadAction => GetActionName( ToolInput.Reload );

	/// <summary>
	/// Tags that TraceSelect will ignore. Override per-tool to filter out specific objects.
	/// Defaults to "player" so tools cannot target players.
	/// </summary>
	public virtual IEnumerable<string> TraceIgnoreTags => ["player"];

	/// <summary>
	/// When true, TraceSelect will also hit hitboxes.
	/// </summary>
	public virtual bool TraceHitboxes => false;

	public TypeDescription TypeDescription { get; protected set; }

	private readonly List<ToolActionEntry> _actions = new();
	private readonly List<GameObject> _createdObjects = new();

	/// <summary>
	/// Register a tool action that will be dispatched automatically by the base <see cref="OnControl"/>.
	/// The display name is a lambda so it can vary with tool state (e.g. stage-dependent hints).
	/// </summary>
	protected void RegisterAction( ToolInput input, Func<string> name, Action callback, InputMode mode = InputMode.Pressed )
	{
		if ( IsProxy ) return;
		_actions.Add( new ToolActionEntry( input, name, callback, mode ) );
	}

	/// <summary>
	/// Track a GameObject created by this tool action. These are passed through
	/// to <see cref="IToolActionEvents.PostActionData.CreatedObjects"/> when the post-event fires.
	/// </summary>
	protected void Track( params GameObject[] objects )
	{
		foreach ( var go in objects )
		{
			if ( go.IsValid() )
				_createdObjects.Add( go );
		}
	}

	private string GetActionName( ToolInput input )
	{
		foreach ( var action in _actions )
		{
			if ( action.Input == input )
				return action.Name?.Invoke();
		}
		return null;
	}

	/// <summary>
	/// Fire <see cref="IToolActionEvents.OnToolAction"/> before executing an action.
	/// Returns true if the action should proceed, false if cancelled.
	/// </summary>
	protected bool FireToolAction( ToolInput input )
	{
		var data = new IToolActionEvents.ActionData
		{
			Tool = this,
			Input = input,
			Player = Player?.PlayerData
		};

		Scene.RunEvent<IToolActionEvents>( x => x.OnToolAction( data ) );
		return !data.Cancelled;
	}

	/// <summary>
	/// Fire <see cref="IToolActionEvents.OnPostToolAction"/> after a successful action.
	/// </summary>
	protected void FirePostToolAction( ToolInput input )
	{
		var objects = _createdObjects.Count > 0 ? new List<GameObject>( _createdObjects ) : null;
		_createdObjects.Clear();

		Scene.RunEvent<IToolActionEvents>( x => x.OnPostToolAction( new IToolActionEvents.PostActionData
		{
			Tool = this,
			Input = input,
			Player = Player?.PlayerData,
			CreatedObjects = objects
		} ) );
	}

	/// <summary>
	/// Check registered actions and invoke any whose input condition is met this frame.
	/// Wraps each callback with <see cref="IToolActionEvents"/> pre/post events.
	/// </summary>
	private void DispatchActions()
	{
		foreach ( var action in _actions )
		{
			var inputName = action.InputAction;
			if ( inputName is null ) continue;

			bool active = action.Mode == InputMode.Down
				? Input.Down( inputName )
				: Input.Pressed( inputName );

			if ( active )
			{
				if ( !FireToolAction( action.Input ) )
					continue;

				_createdObjects.Clear();
				action.Callback?.Invoke();
				FirePostToolAction( action.Input );
			}
		}
	}

	protected override void OnStart()
	{
		TypeDescription = TypeLibrary.GetType( GetType() );
	}

	protected override void OnEnabled()
	{
		if ( Network.IsOwner )
		{
			this.LoadCookies();
		}
	}

	protected override void OnDisabled()
	{
		DisableSnapGrid();

		if ( Network.IsOwner )
		{
			this.SaveCookies();
		}
	}

	public virtual void DrawScreen( Rect rect, HudPainter paint )
	{
		// … рендер заголовка/marquee — см. полный исходник …
	}

	public virtual void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		// … рендер прицела (белый/красный) — см. полный исходник …
	}

	/// <summary>
	/// Called on the host after placing an entity or constraint.
	/// </summary>
	[Rpc.Owner]
	protected void CheckContraptionStats( GameObject anchor )
	{
		// … подсчёт колёс/двигателей/шариков/констрейнтов/стульев и Stats.SetValue(...) …
	}
}
```

> 📌 `DispatchActions()` приватный — конкретные тулы его не дёргают. Достаточно вызвать `base.OnControl()` в своём `OnControl()` (см. `ToolMode.Helpers.cs`/наследников), и базовый класс сам прочитает ввод и пройдётся по `_actions`.

## Что проверить?

После создания этого файла проект **не будет компилироваться** сам по себе, потому что `ToolMode` — `partial` класс и ему нужны другие части (`Cookies`, `Effects`, `Helpers`, `SnapGrid`). Создавайте следующие файлы по порядку, после всех четырёх можно проверить компиляцию.

---

➡️ Следующий шаг: [09_02 — ToolMode.Cookies.cs](09_02_ToolMode_Cookies.md)
