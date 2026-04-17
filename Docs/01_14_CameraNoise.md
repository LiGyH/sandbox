# 01.14 — Шум камеры (CameraNoise)

## Что мы делаем?

Эта группа из 4 файлов создаёт **систему эффектов камеры**: тряска, отдача, удары. В отличие от `EnvironmentShake` (01.13), которая привязана к точке в мире, эти эффекты **глобальные** — они применяются к камере напрямую, без учёта расстояния. Используются для:

- **Punch** — резкий удар камеры (попадание пули, приземление).
- **Shake** — дрожание с затуханием (взрыв рядом).
- **Recoil** — отдача оружия (с покачиванием).

Все эффекты управляются центральной системой `CameraNoiseSystem`, которая обновляет и применяет их каждый кадр.

## Как это работает внутри движка?

### Архитектура

```
CameraNoiseSystem (синглтон, ICameraSetup)
  └── List<BaseCameraNoise> — список активных эффектов
       ├── Punch    — удар с пружинным затуханием
       ├── Shake    — дрожание с шумом Перлина
       └── Recoil   → создаёт RollShake (покачивание)
```

### `GameObjectSystem<T>`

`CameraNoiseSystem` — синглтон на сцену. Доступен через `CameraNoiseSystem.Current`.

### `ICameraSetup`

Система подключается к конвейеру камеры:
- `PreSetup` — обновление всех эффектов + удаление завершённых.
- `PostSetup` — применение эффектов к камере.

### `Vector3.SpringDamped`

Пружинная система затухания: значение «пружинит» к цели, затухая со временем. Идеально для «ударных» эффектов.

## Создай файлы

### Файл 1 — Система и базовый класс

Путь: `Code/Utility/CameraNoise/CameraNoiseSystem.cs`

```csharp
﻿namespace Sandbox.CameraNoise;

public class CameraNoiseSystem : GameObjectSystem<CameraNoiseSystem>, ICameraSetup
{
	List<BaseCameraNoise> _all = new();

	public CameraNoiseSystem( Scene scene ) : base( scene )
	{
	}

	void ICameraSetup.PreSetup( Sandbox.CameraComponent cc )
	{
		foreach ( var effect in _all )
		{
			effect.Update();
		}

		_all.RemoveAll( x => x.IsDone );
	}

	void ICameraSetup.PostSetup( CameraComponent cc )
	{
		foreach ( var effect in _all )
		{
			effect.ModifyCamera( cc );
		}
	}

	public void Add( BaseCameraNoise noise )
	{
		_all.Add( noise );
	}
}

public abstract class BaseCameraNoise
{
	public float LifeTime { get; protected set; }
	public float CurrentTime { get; protected set; }
	public float Delta => CurrentTime.LerpInverse( 0, LifeTime, true );
	public float DeltaInverse => 1 - Delta;

	public BaseCameraNoise()
	{
		CameraNoiseSystem.Current.Add( this );
	}

	public virtual bool IsDone => CurrentTime > LifeTime;

	public virtual void Update()
	{
		CurrentTime += Time.Delta;
	}

	public virtual void ModifyCamera( CameraComponent cc ) { }
}
```

### Файл 2 — Удар (Punch)

Путь: `Code/Utility/CameraNoise/Punch.cs`

```csharp
﻿namespace Sandbox.CameraNoise;

class Punch : BaseCameraNoise
{
	float deathTime;
	float lifeTime = 0.0f;

	public Punch( Vector3 target, float time, float frequency, float damp )
	{
		damping.Current = target * GamePreferences.Screenshake;
		damping.Target = 0;
		damping.Frequency = frequency;
		damping.Damping = damp;

		deathTime = time;
	}

	public override bool IsDone => deathTime <= 0;

	Vector3.SpringDamped damping;

	public override void Update()
	{
		deathTime -= Time.Delta;
		lifeTime += Time.Delta;

		damping.Update( Time.Delta );
	}

	public override void ModifyCamera( CameraComponent cc )
	{
		var amount = lifeTime.Remap( 0, 0.3f, 0, 1 );

		cc.WorldRotation *= new Angles( damping.Current * amount );
	}
}
```

### Файл 3 — Отдача (Recoil)

Путь: `Code/Utility/CameraNoise/Recoil.cs`

```csharp
﻿using Sandbox.Utility;

namespace Sandbox.CameraNoise;

/// <summary>
/// Creates a bunch of other common effects
/// </summary>
class Recoil : BaseCameraNoise
{
	public Recoil( float amount, float speed = 1 )
	{
		new RollShake() { Size = 0.5f * amount * GamePreferences.Screenshake, Waves = 3 * speed };
	}

	public override void ModifyCamera( CameraComponent cc )
	{
	}
}

/// <summary>
/// Shake the screen in a roll motion
/// </summary>
class RollShake : BaseCameraNoise
{
	public float Size { get; set; } = 3.0f;
	public float Waves { get; set; } = 3.0f;

	public RollShake()
	{
		LifeTime = 0.3f;
	}

	public override void ModifyCamera( CameraComponent cc )
	{
		var delta = Delta;
		var s = MathF.Sin( delta * MathF.PI * Waves * 2 );
		cc.WorldRotation *= new Angles( 0, 0, s * Size ) * Easing.EaseOut( DeltaInverse );
	}
}
```

### Файл 4 — Тряска (Shake)

Путь: `Code/Utility/CameraNoise/Shake.cs`

```csharp
﻿using Sandbox.Utility;

namespace Sandbox.CameraNoise;

class Shake : BaseCameraNoise
{
	float lifeTime;
	float deathTime;
	float amount = 0.0f;

	public Shake( float amount, float time )
	{
		this.amount = amount * GamePreferences.Screenshake;
		deathTime = time;
		lifeTime = time;
	}

	public override bool IsDone => deathTime <= 0;

	Vector3.SpringDamped damping;

	public override void Update()
	{
		deathTime -= Time.Delta;
		damping.Update( Time.Delta );
	}

	public override void ModifyCamera( CameraComponent cc )
	{
		var x = Noise.Perlin( Time.Now * 1000.0f, 2345 );
		var y = Noise.Perlin( Time.Now * 1000.0f, 21 );
		var z = Noise.Perlin( Time.Now * 1000.0f, 865 );

		var delta = MathX.Remap( deathTime, 0, lifeTime, 0, 1 );

		cc.WorldRotation *= new Angles( x, y, z ) * delta * amount;
	}
}
```

## Разбор кода

### CameraNoiseSystem — центральная система

```csharp
List<BaseCameraNoise> _all = new();
```
Список всех активных эффектов камеры.

```csharp
void ICameraSetup.PreSetup( CameraComponent cc )
{
    foreach ( var effect in _all ) effect.Update();
    _all.RemoveAll( x => x.IsDone );
}
```
**Перед** рендером: обновляем все эффекты и удаляем завершённые.

```csharp
void ICameraSetup.PostSetup( CameraComponent cc )
{
    foreach ( var effect in _all ) effect.ModifyCamera( cc );
}
```
**После** размещения вьюмодели: каждый эффект модифицирует камеру.

### BaseCameraNoise — базовый класс

```csharp
public float Delta => CurrentTime.LerpInverse( 0, LifeTime, true );
```
`Delta` — прогресс эффекта от `0` (начало) до `1` (конец). `DeltaInverse = 1 - Delta` — обратный прогресс.

```csharp
public BaseCameraNoise()
{
    CameraNoiseSystem.Current.Add( this );
}
```
При создании эффект **автоматически** регистрируется в системе.

### Punch — резкий удар

Использует **пружинное затухание** (`Vector3.SpringDamped`):
1. Камера «ударяется» к целевой точке.
2. «Пружинит» обратно с затуханием.
3. Плавно нарастает первые 0.3 секунды (`lifeTime.Remap(0, 0.3f, 0, 1)`).

Параметры:
- `target` — направление и сила удара.
- `frequency` — частота колебаний пружины.
- `damp` — сила затухания (0 = нет затухания, 1 = мгновенно).

### Recoil — отдача

Создаёт дочерний эффект `RollShake` — покачивание камеры вдоль оси `roll`. Сам `Recoil` ничего не делает — он «фабрика» для других эффектов.

### RollShake — покачивание

```csharp
var s = MathF.Sin( delta * MathF.PI * Waves * 2 );
cc.WorldRotation *= new Angles( 0, 0, s * Size ) * Easing.EaseOut( DeltaInverse );
```
Синусоидальное покачивание вокруг оси `roll` с плавным затуханием через `Easing.EaseOut`.

### Shake — дрожание

Использует шум Перлина (как `EnvironmentShake`), но без привязки к расстоянию:
```csharp
var x = Noise.Perlin( Time.Now * 1000.0f, 2345 );
```
Высокая частота (`1000.0f`) даёт быстрое дрожание. Разные «зёрна» (2345, 21, 865) обеспечивают независимость осей.

```csharp
var delta = MathX.Remap( deathTime, 0, lifeTime, 0, 1 );
```
Линейное затухание: от полной силы до нуля.

### Пример использования

```csharp
// Попадание пули — удар камеры
new CameraNoise.Punch( new Vector3( -2, 0, 0 ), 0.5f, 10f, 0.8f );

// Взрыв — сильная тряска 1 секунду
new CameraNoise.Shake( 5.0f, 1.0f );

// Выстрел — отдача
new CameraNoise.Recoil( 1.0f, 2.0f );
```

## Результат

После создания этих файлов:
- Проект имеет полную систему камерных эффектов: удар (Punch), тряска (Shake), отдача (Recoil).
- Все эффекты автоматически регистрируются, обновляются и удаляются.
- Интенсивность уважает пользовательскую настройку `GamePreferences.Screenshake`.
- Система расширяема: новые эффекты создаются наследованием от `BaseCameraNoise`.
- **Фаза 1 (Foundation) завершена!** 🎉

---

← Вернуться к: [01.13 — Тряска окружения (EnvironmentShake)](01_13_EnvironmentShake.md)


---

## ➡️ Следующий шаг

Фаза завершена. Переходи к **[02.01 — Источник убийства (IKillSource) 🎯](02_01_IKillSource.md)** — начало следующей фазы.
