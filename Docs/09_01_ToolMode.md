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
| `PrimaryAction` | `string` | Подсказка для ЛКМ (attack1) |
| `SecondaryAction` | `string` | Подсказка для ПКМ (attack2) |
| `ReloadAction` | `string` | Подсказка для R (reload) |
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
	/// </summary>
	public virtual string PrimaryAction => null;

	/// <summary>
	/// Label for the secondary action (attack2), or null if none.
	/// </summary>
	public virtual string SecondaryAction => null;

	/// <summary>
	/// Label for the reload action, or null if none.
	/// </summary>
	public virtual string ReloadAction => null;

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
		var t = $"{TypeDescription.Icon} {TypeDescription.Title}";

		var text = new TextRendering.Scope( t, Color.White, 64 );
		text.LineHeight = 0.75f;
		text.FontName = "Poppins";
		text.TextColor = Color.Orange;
		text.FontWeight = 700;

		var measured = text.Measure();
		float textW = measured.x;
		float textH = measured.y;

		if ( textW <= rect.Width )
		{
			paint.DrawText( text, rect, TextFlag.Center );
			return;
		}

		// Marquee: scroll text right-to-left, looping seamlessly.
		// The render target viewport naturally clips anything outside [0, rect.Width].
		const float scrollSpeed = 80f;
		const float gap = 60f;
		float cycle = textW + gap;
		float offset = (Time.Now * scrollSpeed) % cycle;

		float y = rect.Top + (rect.Height - textH) * 0.5f;

		float x = rect.Width - offset;
		paint.DrawText( text, new Rect( x, y, textW, textH ), TextFlag.SingleLine | TextFlag.Left );
		paint.DrawText( text, new Rect( x - cycle, y, textW, textH ), TextFlag.SingleLine | TextFlag.Left );
	}

	public virtual void DrawHud( HudPainter painter, Vector2 crosshair )
	{
		if ( IsValidState )
		{
			painter.SetBlendMode( BlendMode.Normal );
			painter.DrawCircle( crosshair, 5, Color.Black );
			painter.DrawCircle( crosshair, 3, Color.White );
		}
		else
		{
			Color redColor = "#e53";
			painter.SetBlendMode( BlendMode.Normal );
			painter.DrawCircle( crosshair, 5, redColor.Darken( 0.3f ) );
			painter.DrawCircle( crosshair, 3, redColor );
		}
	}

	/// <summary>
	/// Called on the host after placing an entity or constraint. Fires an RPC to the owning
	/// client so it can walk the contraption graph and record achievement stats locally.
	/// </summary>
	[Rpc.Owner]
	protected void CheckContraptionStats( GameObject anchor )
	{
		var builder = new LinkedGameObjectBuilder();
		builder.AddConnected( anchor );

		var wheels = builder.Objects.Sum( o => o.GetComponentsInChildren<WheelEntity>().Count() );
		var thrusters = builder.Objects.Sum( o => o.GetComponentsInChildren<ThrusterEntity>().Count() );
		var hoverballs = builder.Objects.Sum( o => o.GetComponentsInChildren<HoverballEntity>().Count() );
		var constraints = builder.Objects.Sum( o => o.GetComponentsInChildren<ConstraintCleanup>().Count() );
		var chairs = builder.Objects.Sum( o => o.GetComponentsInChildren<BaseChair>().Count() );

		Sandbox.Services.Stats.Increment( "tool.constraint.create", 1 );
		Sandbox.Services.Stats.SetValue( "tool.contraption.wheel", wheels );
		Sandbox.Services.Stats.SetValue( "tool.contraption.thruster", thrusters );
		Sandbox.Services.Stats.SetValue( "tool.contraption.hoverball", hoverballs );
		Sandbox.Services.Stats.SetValue( "tool.contraption.constraint", constraints );
		Sandbox.Services.Stats.SetValue( "tool.contraption.chair", chairs );
	}
}
```

## Что проверить?

После создания этого файла проект **не будет компилироваться** сам по себе, потому что `ToolMode` — `partial` класс и ему нужны другие части (`Cookies`, `Effects`, `Helpers`, `SnapGrid`). Создавайте следующие файлы по порядку, после всех четырёх можно проверить компиляцию.

---

➡️ Следующий шаг: [09_02 — ToolMode.Cookies.cs](09_02_ToolMode_Cookies.md)
