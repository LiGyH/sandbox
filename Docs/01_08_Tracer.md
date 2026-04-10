# 01.08 — Трассер пуль (Tracer)

## Что мы делаем?

Этот файл создаёт **визуальный трассер** — светящуюся линию, которая летит от дула оружия к точке попадания, имитируя полёт пули. Также определяется вспомогательная структура `WorldPoint` для задания точки в мировых координатах с привязкой к объекту.

Представьте трассирующую пулю в темноте: яркая полоска света быстро пролетает от оружия к цели и исчезает. Этот код создаёт именно такой эффект.

## Как это работает внутри движка?

### `Renderer` и `ITemporaryEffect`

- `Renderer` — базовый класс для компонентов, которые что-то рисуют на экране.
- `ITemporaryEffect` — интерфейс, который сообщает движку, что эффект временный. Когда `IsActive` возвращает `false`, движок уничтожает объект.
- `ExecuteInEditor` — эффект работает и в редакторе (для предпросмотра).

### `SceneLineObject`

`SceneLineObject` — низкоуровневый объект для рисования линий в 3D-пространстве. Поддерживает цвет, толщину, заглушки на концах (caps).

### `SceneLight`

Опциональный точечный источник света, который летит вместе с трассером и подсвечивает окружение.

## Создай файл

Путь: `Code/Utility/Tracer.cs`

```csharp
﻿public sealed class Tracer : Renderer, Component.ExecuteInEditor, Component.ITemporaryEffect
{
	[Header( "Position" )]
	[Property, Feature( "Tracer" )] public WorldPoint EndPoint { get; set; }

	[Header( "Speed" )]
	[Property, Feature( "Tracer" )] public float DistancePerSecond { get; set; } = 1.0f;
	[Property, Feature( "Tracer" )] public float Length { get; set; } = 100.0f;
	[Property, Feature( "Tracer" )] public float StartDistance { get; set; } = 200.0f;

	[Header( "Rendering" )]
	[Property, Feature( "Tracer" )] public Gradient LineColor { get; set; } = new Gradient( new Gradient.ColorFrame( 0, Color.White ), new Gradient.ColorFrame( 1, Color.White.WithAlpha( 0 ) ) );
	[Property, Feature( "Tracer" )] public Curve LineWidth { get; set; } = new Curve( new Curve.Frame( 0, 2 ), new Curve.Frame( 1, 0 ) );
	[Property, Feature( "Tracer" )] public SceneLineObject.CapStyle StartCap { get; set; }
	[Property, Feature( "Tracer" )] public SceneLineObject.CapStyle EndCap { get; set; }

	[Group( "Rendering" )]
	[Property] public bool Opaque { get; set; } = true;

	[Group( "Rendering" )]
	[Property] public bool CastShadows { get; set; } = true;

	[Property, FeatureEnabled( "Light", Icon = "💡" )]
	public bool EnableLight { get; set; }

	[Property, Feature( "Light" )]
	public Gradient LightColor { get; set; } = new Gradient( new Gradient.ColorFrame( 0, Color.White ), new Gradient.ColorFrame( 1, Color.White ) );

	[Property, Feature( "Light" )]
	public Curve LightRadius { get; set; } = new Curve( new Curve.Frame( 0, 128 ), new Curve.Frame( 0.5f, 256 ), new Curve.Frame( 1, 128 ) );

	[Property, Feature( "Light" ), Range( 0, 1 )]
	public float LightPosition { get; set; } = 0;

	bool ITemporaryEffect.IsActive => !_finished;

	float _distance = 0.0f;
	bool _finished = false;

	SceneLineObject _so;
	SceneLight _light;
	protected override void OnEnabled()
	{
		_so = new SceneLineObject( Scene.SceneWorld );
		_so.Transform = Transform.World;

		_distance = StartDistance;
	}

	protected override void OnDisabled()
	{
		_so?.Delete();
		_so = null;

		_light?.Delete();
		_light = null;
	}

	protected override void OnUpdate()
	{
		var len = WorldPosition.Distance( EndPoint.Get() );

		_distance += DistancePerSecond * Time.Delta;

		if ( _distance > len + Length )
		{
			_finished = true;

			if ( Scene.IsEditor )
				_distance = 0;
		}
	}

	protected override void OnPreRender()
	{
		if ( !_so.IsValid() )
			return;

		var travel = EndPoint.Get() - WorldPosition;
		var maxlen = travel.Length;

		if ( _distance - Length > maxlen )
		{
			_so.RenderingEnabled = false;

			_light?.Delete();
			_light = null;
			return;
		}

		var midDistance = (_distance - Length * 0.5f).Clamp( 0, maxlen );
		var delta = midDistance.LerpInverse( 0, maxlen );

		if ( EnableLight )
		{
			_light ??= new SceneLight( Scene.SceneWorld );
			_light.Transform = Transform.World;
			_light.QuadraticAttenuation = 10;
			_light.LightColor = LightColor.Evaluate( delta );
			_light.ShadowsEnabled = false;
			_light.Radius = LightRadius.Evaluate( delta );
			_light.Position = WorldPosition + travel.Normal * (_distance - Length * LightPosition).Clamp( 0, maxlen - 5 );
		}
		else
		{
			_light?.Delete();
			_light = null;
		}

		_so.StartCap = StartCap;
		_so.EndCap = EndCap;
		_so.Opaque = Opaque;

		_so.RenderingEnabled = true;
		_so.Transform = WorldTransform;
		_so.Flags.CastShadows = CastShadows;
		_so.Attributes.Set( "BaseTexture", Texture.White );
		_so.Attributes.SetCombo( "D_BLEND", Opaque ? 0 : 1 );

		_so.StartLine();

		for ( float x = 0; x <= 1.0f; x += 0.1f )
		{
			var s = (_distance - Length * x).Clamp( 0, maxlen );
			var lineStart = (_distance - maxlen).Clamp( 0, Length ) / Length;

			_so.AddLinePoint( WorldPosition + travel.Normal * s, LineColor.Evaluate( x ), LineWidth.Evaluate( x ) );
		}

		{
			var s = (_distance - Length).Clamp( 0, maxlen );
			var lineStart = (_distance).Clamp( 0, Length ) / Length;
		}

		_so.EndLine();
	}
}

public struct WorldPoint
{
	[KeyProperty]
	public Vector3 Origin { get; set; }
	public GameObject Parent { get; set; }

	public Vector3 Get()
	{
		if ( Parent.IsValid() )
			return Parent.WorldTransform.PointToWorld( Origin );

		return Origin;
	}

	public static implicit operator WorldPoint( Vector3 v )
	{
		return new WorldPoint { Origin = v };
	}
}
```

## Разбор кода

### Объявление класса

```csharp
public sealed class Tracer : Renderer, Component.ExecuteInEditor, Component.ITemporaryEffect
```
- `Renderer` — базовый класс для рисующих компонентов.
- `ExecuteInEditor` — работает в редакторе.
- `ITemporaryEffect` — автоудаление, когда `IsActive = false`.

### Настраиваемые свойства

Свойства сгруппированы атрибутами:
- `[Header("Position")]` — заголовок группы в инспекторе.
- `[Property]` — показывает свойство в инспекторе редактора.
- `[Feature("Tracer")]` — группирует под переключаемой фичей.

Основные параметры:
- `EndPoint` — конечная точка полёта (тип `WorldPoint`).
- `DistancePerSecond` — скорость трассера.
- `Length` — длина видимого «хвоста».
- `StartDistance` — начальное смещение (чтобы трассер начинался не от дула, а чуть дальше).

### Жизненный цикл

```csharp
protected override void OnEnabled()  // Создание SceneLineObject
protected override void OnDisabled() // Удаление объектов
protected override void OnUpdate()   // Обновление позиции каждый кадр
protected override void OnPreRender() // Отрисовка линии перед рендером
```

### Логика движения (`OnUpdate`)

```csharp
_distance += DistancePerSecond * Time.Delta;
```
Каждый кадр трассер продвигается на `скорость × время кадра`.

```csharp
if ( _distance > len + Length )
    _finished = true;
```
Когда трассер пролетел всю дистанцию + длину хвоста — он «умер».

### Отрисовка (`OnPreRender`)

Линия рисуется через `SceneLineObject`:
1. Вычисляется вектор полёта (`travel`).
2. В цикле `for` добавляются точки линии с градиентным цветом и шириной.
3. Если включён свет — обновляется `SceneLight`, летящий вместе с трассером.

### Структура `WorldPoint`

```csharp
public struct WorldPoint
{
    public Vector3 Origin { get; set; }
    public GameObject Parent { get; set; }
```
Точка в пространстве, которая может быть привязана к объекту:
- Если `Parent` задан — координаты пересчитываются относительно него.
- Если нет — используются напрямую.

```csharp
public static implicit operator WorldPoint( Vector3 v )
```
Неявное преобразование: `Vector3` автоматически превращается в `WorldPoint`.

## Результат

После создания этого файла:
- Проект может создавать визуальные трассеры пуль с настраиваемым цветом, толщиной, скоростью.
- Поддерживается опциональное освещение от трассера.
- Структура `WorldPoint` упрощает работу с точками, привязанными к объектам.
- Трассер автоматически уничтожается после завершения полёта.

---

Следующий шаг: [01.09 — Система куки (CookieSource)](01_09_CookieSource.md)
