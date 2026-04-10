# 01.13 — Тряска окружения (EnvironmentShake)

## Что мы делаем?

Этот компонент создаёт **эффект тряски камеры** от источников в игровом мире. Представьте взрыв: чем ближе игрок к эпицентру, тем сильнее трясётся экран. Чем дальше — тем слабее. Тряска может быть постоянной (работающий генератор) или временной (взрыв с затуханием).

Эффект использует **шум Перлина** для естественного, непредсказуемого покачивания камеры — не случайные дёргания, а плавные «волны».

## Как это работает внутри движка?

### `ICameraSetup`

Компонент реализует `ICameraSetup` и подключается к конвейеру камеры (см. [01.07](01_07_CameraSetup.md)). Метод `PostSetup` вызывается каждый кадр после размещения вьюмодели.

### `ITemporaryEffect`

Когда `IsActive = false` (тряска закончилась), движок автоматически уничтожает объект.

### Шум Перлина

`Noise.Perlin(x, y)` — генератор плавного шума. В отличие от `Random`, шум Перлина даёт плавные, непрерывные значения — идеально для тряски.

### `GamePreferences.Screenshake`

Глобальная настройка интенсивности тряски экрана. Игрок может уменьшить или отключить тряску в настройках.

## Создай файл

Путь: `Code/Utility/EnvironmentShake.cs`

```csharp

[Alias( "EnvShake" )]
public sealed class EnvironmentShake : Component, Component.ITemporaryEffect, ICameraSetup
{
	[Property] public float Rate { get; set; } = 10.0f;
	[Property] public Angles Scale { get; set; } = new Angles( 100, 100, 0 );
	[Property] public float MaxDistance { get; set; } = 1024;
	[Property] public Curve DistanceCurve { get; set; } = new Curve( new( 0, 1 ), new( 1, 0 ) );
	[FeatureEnabled( "Timed" ), Property] public bool TimeLimited { get; set; } = false;
	[Feature( "Timed" ), Property] public float TimeLength { get; set; } = 5;
	[Feature( "Timed" ), Property] public Curve TimeCurve { get; set; } = new Curve( new( 0, 1 ), new( 1, 0 ) );
	[Sync] public TimeUntil EndTime { get; set; }

	public bool IsActive => !TimeLimited || EndTime > 0;

	protected override void OnEnabled()
	{
		EndTime = TimeLength;
	}

	void ICameraSetup.PostSetup( CameraComponent camera )
	{
		var distance = camera.WorldPosition.Distance( WorldPosition );
		var distanceDelta = (distance / MaxDistance).Clamp( 0, 1 );

		var amount = DistanceCurve.EvaluateDelta( distanceDelta ) * GamePreferences.Screenshake;

		var noisex = Sandbox.Utility.Noise.Perlin( Time.Now * Rate, 0 ).Remap( 0, 1, -1, 1 );
		var noisey = Sandbox.Utility.Noise.Perlin( Time.Now * Rate + 830, 0 ).Remap( 0, 1, -1, 1 );
		var noisez = Sandbox.Utility.Noise.Perlin( Time.Now * Rate + 340, 0 ).Remap( 0, 1, -1, 1 );

		if ( TimeLimited )
		{
			var timeDelta = (EndTime.Relative / TimeLength).Remap( 1, 0 );

			if ( timeDelta >= 1 )
				return;

			amount *= TimeCurve.Evaluate( timeDelta );
		}

		camera.LocalRotation *= Rotation.From( noisex * Scale.pitch, noisey * Scale.yaw, noisez * Scale.roll ) * amount;
	}
}
```

## Разбор кода

### Атрибуты класса

```csharp
[Alias( "EnvShake" )]
```
`Alias` — позволяет найти компонент по альтернативному имени `"EnvShake"` (сокращение).

### Настраиваемые свойства

| Свойство | Тип | По умолчанию | Описание |
|---|---|---|---|
| `Rate` | `float` | `10.0` | Скорость тряски (частота шума) |
| `Scale` | `Angles` | `(100, 100, 0)` | Масштаб по осям (pitch, yaw, roll) |
| `MaxDistance` | `float` | `1024` | Максимальная дальность действия |
| `DistanceCurve` | `Curve` | Линейное затухание | Как сила зависит от расстояния |
| `TimeLimited` | `bool` | `false` | Ограничена ли тряска по времени |
| `TimeLength` | `float` | `5` | Длительность (если ограничена) |
| `TimeCurve` | `Curve` | Линейное затухание | Как сила меняется со временем |
| `EndTime` | `TimeUntil` | — | Таймер окончания (синхронизируется по сети) |

### Проверка активности

```csharp
public bool IsActive => !TimeLimited || EndTime > 0;
```
Эффект активен, если:
- Он **не** ограничен по времени (`!TimeLimited`), ИЛИ
- Таймер ещё не истёк (`EndTime > 0`).

### Основная логика — `PostSetup`

#### Расчёт расстояния

```csharp
var distance = camera.WorldPosition.Distance( WorldPosition );
var distanceDelta = (distance / MaxDistance).Clamp( 0, 1 );
var amount = DistanceCurve.EvaluateDelta( distanceDelta ) * GamePreferences.Screenshake;
```
1. Расстояние от камеры до источника тряски.
2. Нормализуем в диапазон `[0, 1]`.
3. Применяем кривую расстояния и пользовательскую настройку тряски.

#### Генерация шума

```csharp
var noisex = Sandbox.Utility.Noise.Perlin( Time.Now * Rate, 0 ).Remap( 0, 1, -1, 1 );
var noisey = Sandbox.Utility.Noise.Perlin( Time.Now * Rate + 830, 0 ).Remap( 0, 1, -1, 1 );
var noisez = Sandbox.Utility.Noise.Perlin( Time.Now * Rate + 340, 0 ).Remap( 0, 1, -1, 1 );
```
Три независимых шума Перлина (для pitch, yaw, roll):
- `Time.Now * Rate` — чем больше `Rate`, тем быстрее тряска.
- `+ 830`, `+ 340` — смещения, чтобы оси не двигались синхронно.
- `.Remap(0, 1, -1, 1)` — перевод из диапазона `[0, 1]` в `[-1, 1]`.

#### Временное затухание

```csharp
if ( TimeLimited )
{
    var timeDelta = (EndTime.Relative / TimeLength).Remap( 1, 0 );
    if ( timeDelta >= 1 ) return;
    amount *= TimeCurve.Evaluate( timeDelta );
}
```
Если тряска ограничена по времени — применяем временну́ю кривую. Тряска плавно затухает по `TimeCurve`.

#### Применение к камере

```csharp
camera.LocalRotation *= Rotation.From( noisex * Scale.pitch, noisey * Scale.yaw, noisez * Scale.roll ) * amount;
```
Умножаем текущий поворот камеры на случайное отклонение, масштабированное по `amount`.

## Результат

После создания этого файла:
- Проект имеет компонент тряски камеры, привязанный к позиции в мире.
- Тряска затухает с расстоянием и опционально по времени.
- Шум Перлина обеспечивает плавное, натуральное покачивание.
- Интенсивность настраивается игроком через `GamePreferences.Screenshake`.
- Поддерживается сетевая синхронизация через `[Sync]`.

---

Следующий шаг: [01.14 — Шум камеры (CameraNoise)](01_14_CameraNoise.md)
