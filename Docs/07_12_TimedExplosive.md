# 07.12 — Взрывчатка с таймером (TimedExplosive) 💥

## Что мы делаем?

Создаём компонент `TimedExplosive` — таймерную взрывчатку, которая автоматически взрывается через заданное время.

## Зачем это нужно?

- **Универсальный взрыватель**: компонент добавляется к любому объекту (граната, бомба, мина) и вызывает взрыв через `Lifetime` секунд.
- **Настраиваемые параметры**: радиус, урон и сила взрыва задаются через инспектор или программно (из `HandGrenadeWeapon`).
- **Сетевая логика**: взрыв происходит только на хосте (`Networking.IsHost`), обеспечивая авторитетность.
- **Переиспользуемость**: `Explode()` доступен как `[Rpc.Host]`, что позволяет вызвать взрыв досрочно извне.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `Component` | Базовый компонент s&box. |
| `Lifetime` | `[Property]` — время до взрыва (3 сек по умолчанию). |
| `Radius` | `[Property]` — радиус поражения (256 юнитов). |
| `Damage` | `[Property]` — максимальный урон (125). |
| `Force` | `[Property]` — множитель физической силы взрыва (1.0). |
| `TimeSinceCreated` | Отсчёт времени с момента активации. |
| `OnEnabled()` | Сбрасывает таймер при включении компонента. |
| `OnFixedUpdate()` | Проверяет каждый физический тик: если время вышло и мы хост — вызываем `Explode()`. |
| `[Rpc.Host] Explode()` | Загружает префаб взрыва (`explosion_med.prefab`), конфигурирует `RadiusDamage` (радиус, сила, урон), спавнит в сети и уничтожает исходный объект. |
| `RadiusDamage` | Компонент на префабе взрыва, который наносит урон всем объектам в радиусе. |

## Создай файл

**Путь:** `Code/Weapons/HandGrenade/TimedExplosive.cs`

```csharp
using Sandbox;

/// <summary>
/// Explodes after a set time. Spawns an explosion prefab with configurable radius, damage, and force.
/// </summary>
public sealed class TimedExplosive : Component
{
	[Property] public float Lifetime { get; set; } = 3f;
	[Property] public float Radius { get; set; } = 256f;
	[Property] public float Damage { get; set; } = 125f;
	[Property] public float Force { get; set; } = 1f;

	TimeSince TimeSinceCreated { get; set; }

	protected override void OnEnabled()
	{
		TimeSinceCreated = 0;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost ) return;
		if ( TimeSinceCreated < Lifetime ) return;

		Explode();
	}

	[Rpc.Host]
	public void Explode()
	{
		var explosionPrefab = ResourceLibrary.Get<PrefabFile>( "/prefabs/engine/explosion_med.prefab" );
		if ( explosionPrefab == null )
		{
			Log.Warning( "Can't find /prefabs/engine/explosion_med.prefab" );
			return;
		}

		var go = GameObject.Clone( explosionPrefab, new CloneConfig { Transform = WorldTransform.WithScale( 1 ), StartEnabled = false } );
		if ( !go.IsValid() ) return;

		go.RunEvent<RadiusDamage>( x =>
		{
			x.Radius = Radius;
			x.PhysicsForceScale = Force;
			x.DamageAmount = Damage;
			x.Attacker = go;
		}, FindMode.EverythingInSelfAndDescendants );

		go.Enabled = true;
		go.NetworkSpawn( true, null );

		GameObject.Destroy();
	}
}
```

## Проверка

1. Бросьте гранату и дождитесь 3 секунд — должен произойти взрыв с визуальным эффектом.
2. Проверьте, что объекты в радиусе 256 юнитов получают урон.
3. Проверьте, что физические объекты рядом отлетают от взрыва.
4. Убедитесь, что объект гранаты уничтожается после взрыва.
5. В мультиплеере проверьте, что взрыв авторитетен — происходит только на хосте.
