# 03.12 — Индикаторы урона (PlayerDamageIndicators) 🎯

## Что мы делаем?

Создаём **экранные индикаторы направления урона** — красные стрелки по краям экрана, показывающие откуда прилетел урон. Как в Call of Duty или Halo.

## Как это работает?

### Принцип

Когда игрок получает урон, записываем позицию атакующего. Каждый кадр рисуем радиальный индикатор — красную текстуру, повёрнутую в сторону атакующего.

### Хранение индикаторов

```csharp
List<(Vector3 WorldPos, TimeSince Lifetime)> radialIndicators = new();
```

Список кортежей: мировая позиция атакующего + время жизни. `TimeSince` автоматически считает прошедшее время.

### Математика поворота

```csharp
var dir = (entry.WorldPos - focalPoint).Normal;
var angle = -MathF.Atan2( dir.y, dir.x ) + playerRot.Angles().yaw.DegreeToRadian() - (MathF.PI / 2f);
```

1. `dir` — направление от игрока к атакующему (в мировых координатах)
2. `Atan2` — угол в радианах на плоскости XY
3. `+ yaw` — компенсация поворота игрока (чтобы «вперёд» всегда было сверху экрана)
4. `- PI/2` — смещение на 90° (atan2 считает от оси X, а «вверх» экрана — ось Y)

### Отрисовка через Hud

```csharp
Matrix matrix = Matrix.CreateRotation( Rotation.From( 0, angle.RadianToDegree(), 0 ) );
matrix *= Matrix.CreateTranslation( center );
hud.SetMatrix( matrix );
hud.DrawTexture( RadialDamageIcon, rect, Color.Red.WithAlpha( alpha ) );
```

- `SetMatrix` — устанавливает матрицу трансформации для HUD-отрисовки
- Поворачиваем вокруг центра экрана на вычисленный угол
- Рисуем текстуру на расстоянии `RadialDistanceFromCenter` (128px) от центра
- Прозрачность уменьшается со временем: `1 - (lifetime / 2с)`

### Focal Point (точка фокуса)

```csharp
var focalPoint = playerPos + playerRot.Forward * 16;
```

Вместо точной позиции глаз используется точка на 16 единиц впереди. Это делает индикаторы более точными для близких атакующих — без этого атакующий прямо перед игроком мог бы показать стрелку сбоку.

## Создай файл

Путь: `Code/Player/PlayerDamageIndicators.cs`

```csharp
public sealed class PlayerDamageIndicators : Component, IPlayerEvent
{
	[RequireComponent] Player Player { get; set; }

	float RadialDistanceFromCenter => 128f;
	float RadialIndicatorLifetime => 2f;

	List<(Vector3 WorldPos, TimeSince Lifetime)> radialIndicators = new();

	[Property] public Texture RadialDamageIcon { get; set; }

	protected override void OnPreRender()
	{
		if ( !Player.IsLocalPlayer ) return;
		if ( Scene.Camera is null ) return;

		UpdateRadialIndicators();
	}

	void UpdateRadialIndicators()
	{
		if ( RadialDamageIcon is null )
			return;

		var hud = Scene.Camera.Hud;
		var playerPos = Player.EyeTransform.Position;
		var playerRot = Player.EyeTransform.Rotation;
		var center = Screen.Size / 2f;

		// rough approx of where the crosshair is in worldspace, makes close-up directions more easily parsable/accurate
		var focalPoint = playerPos + playerRot.Forward * 16;

		for ( int i = radialIndicators.Count - 1; i >= 0; i-- )
		{
			var entry = radialIndicators[i];
			if ( entry.Lifetime >= RadialIndicatorLifetime )
			{
				radialIndicators.RemoveAt( i );
				continue;
			}

			var dir = (entry.WorldPos - focalPoint).Normal;
			var angle = -MathF.Atan2( dir.y, dir.x ) + playerRot.Angles().yaw.DegreeToRadian() - (MathF.PI / 2f);

			Matrix matrix = Matrix.CreateRotation( Rotation.From( 0, angle.RadianToDegree(), 0 ) );
			matrix *= Matrix.CreateTranslation( center );
			hud.SetMatrix( matrix );

			var size = new Vector2( 256, 512 ) * Hud.Scale;
			var rect = new Rect( new Vector2( RadialDistanceFromCenter * Hud.Scale, -size.y / 2 ), size );

			// scale alpha based on damage dealt or something?
			hud.DrawTexture( RadialDamageIcon, rect, Color.Red.WithAlpha( 1f - (entry.Lifetime / RadialIndicatorLifetime) ) );
		}

		hud.SetMatrix( Matrix.Identity );
	}

	void IPlayerEvent.OnDamage( IPlayerEvent.DamageParams args )
	{
		if ( !args.Attacker.IsValid() ) return;
		
		radialIndicators.Add( (args.Attacker.WorldPosition, 0f) );
	}
}
```

## Ключевые концепции

### OnPreRender vs OnUpdate

```csharp
protected override void OnPreRender() { ... }
```

`OnPreRender` вызывается непосредственно перед отрисовкой кадра — это лучшее место для HUD-отрисовки, потому что позиции камеры уже финальные.

### Обратный цикл

```csharp
for ( int i = radialIndicators.Count - 1; i >= 0; i-- )
```

Итерируем с конца, потому что удаляем элементы из списка (`RemoveAt`). Если бы итерировали с начала, индексы сдвинулись бы после удаления.

### Hud.Scale

```csharp
var size = new Vector2( 256, 512 ) * Hud.Scale;
```

`Hud.Scale` учитывает разрешение экрана и настройки масштабирования интерфейса. Без него на 4K мониторе индикаторы были бы крошечными.

## Проверка

1. Встань перед NPC → получи урон → красная стрелка указывает в сторону NPC
2. Повернись спиной → стрелка сверху экрана (NPC впереди)
3. Стрелка плавно исчезает за 2 секунды

## Следующий файл

Переходи к **03.13 — Инвентарь игрока (PlayerInventory)**.
