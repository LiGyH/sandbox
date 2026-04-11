# 📐 ToolMode.SnapGrid — Привязка к сетке для инструментов

## Что мы делаем?

Создаём файл `ToolMode.SnapGrid.cs` — partial-часть класса `ToolMode`, интегрирующая систему привязки к сетке (snap grid) в инструменты. Управляет отображением, обновлением и блокировкой камеры на ближайший узел сетки.

## Зачем это нужно?

Snap grid позволяет точно размещать объекты:
- Визуальная сетка на поверхности наведённого объекта показывает точки привязки
- Зажатие клавиши E блокирует камеру на ближайший угол сетки для точного нацеливания
- Каждый инструмент может включить/выключить сетку через `UseSnapGrid`
- Фильтрация объектов через `ShouldDisplaySnapGrid` (мир и игроки исключаются)

## Как это работает внутри движка?

- `UseSnapGrid` — виртуальное свойство (по умолчанию `false`), инструменты переопределяют для включения
- `SnapGrid` — экземпляр класса `SnapGrid`, создаётся лениво при наведении на объект
- `OnControl` — вызывает `TraceSelect` и обновляет сетку через `SnapGrid.Update` или скрывает через `SnapGrid.Hide`
- `OnCameraMove` — при зажатии E фиксирует `_lockedSnapTarget` на `LastSnapWorldPos` и вычисляет углы камеры для наведения
- `DisableSnapGrid` — уничтожает сетку и сбрасывает блокировку
- `ShouldDisplaySnapGrid` — виртуальный метод фильтрации: по умолчанию скрывает сетку для world и player

## Создай файл

📄 `Code/Weapons/ToolGun/ToolMode.SnapGrid.cs`

```csharp
public abstract partial class ToolMode
{
	/// <summary>
	/// When true (default), a SnapGrid overlay is shown on the hovered object surface.
	/// Override to return false to opt out.
	/// </summary>
	public virtual bool UseSnapGrid => false;

	/// <summary>
	/// The active snap grid, updated every frame in <see cref="OnControl"/>.
	/// Null when snap grid is disabled or the tool is inactive.
	/// </summary>
	protected SnapGrid SnapGrid { get; private set; }

	private Vector3? _lockedSnapTarget;

	private void DisableSnapGrid()
	{
		SnapGrid?.Destroy();
		SnapGrid = null;
		_lockedSnapTarget = null;
	}

	/// <summary>
	/// While hovering an object, hold E to lock the camera aim onto the nearest snap corner.
	/// </summary>
	public virtual void OnCameraMove( Player player, ref Angles angles )
	{
		// Skip when the mode is absorbing mouse input for its own purposes (e.g. Weld rotation stage).
		if ( AbsorbMouseInput || SnapGrid == null )
			return;

		if ( Input.Pressed( "use" ) )
			_lockedSnapTarget = SnapGrid.LastSnapWorldPos;

		if ( !Input.Down( "use" ) || _lockedSnapTarget == null )
			return;

		var eyePos = player.EyeTransform.Position;
		var desiredAngles = Rotation.LookAt( _lockedSnapTarget.Value - eyePos ).Angles();
		var currentAngles = player.Controller.EyeAngles;

		angles = desiredAngles - currentAngles;

		if ( Input.Released( "use" ) )
			_lockedSnapTarget = null;

		Input.Clear( "use" );
	}

	/// <summary>
	/// Override to control which objects show the snap grid. Returns false for world and player geometry by default.
	/// </summary>
	protected virtual bool ShouldDisplaySnapGrid( GameObject go )
	{
		return !go.Tags.Has( "world" ) && !go.Tags.Has( "player" );
	}

	public virtual void OnControl()
	{
		if ( !UseSnapGrid ) return;

		var preview = TraceSelect();
		if ( preview.IsValid() && ShouldDisplaySnapGrid( preview.GameObject ) )
		{
			SnapGrid ??= new SnapGrid();
			SnapGrid.Update( Scene.SceneWorld, preview.GameObject, preview.WorldPosition(), preview.WorldTransform().Rotation.Forward );
		}
		else
		{
			SnapGrid?.Hide();
		}
	}
}
```

## Проверка

- Файл `Code/Weapons/ToolGun/ToolMode.SnapGrid.cs` создан как partial-класс `ToolMode`
- `UseSnapGrid` по умолчанию `false`, переопределяется инструментами
- `OnCameraMove` блокирует камеру при зажатии E
- `OnControl` обновляет или скрывает snap grid
- `ShouldDisplaySnapGrid` фильтрует мир и игроков
