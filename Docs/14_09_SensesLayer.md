# 14.09 — NPC: Слой сенсоров (SensesLayer) 👁️

## Что мы делаем?

Создаём **SensesLayer** — слой, который сканирует окружение NPC, определяя видимые и слышимые цели.

## Как это работает?

Каждые 100мс (`ScanInterval`) слой:
1. Ищет объекты с целевыми тегами в радиусе слышимости
2. Проверяет дальность для «слышимых» целей
3. Проверяет дальность + линию видимости для «видимых» целей
4. Обновляет списки `VisibleTargets` и `AudibleTargets`

### Линия видимости

Трейс (луч) от «глаз» NPC (64 единицы вверх) к центру цели (32 единицы вверх). Если луч не попал в препятствие или попал в саму цель — видимость есть.

## Создай файл

Путь: `Code/Npcs/Layers/SensesLayer.cs`

```csharp
namespace Sandbox.Npcs.Layers;

/// <summary>
/// Handles awareness and environmental scanning
/// </summary>
public class SensesLayer : BaseNpcLayer
{
	public float ScanInterval { get; set; } = 0.1f; // Scan every 100ms

	[Property]
	public float SightRange { get; set; } = 500f;

	[Property]
	public float HearingRange { get; set; } = 300f;
	public float PersonalSpace { get; set; } = 80f;

	[Property]
	public TagSet TargetTags { get; set; } = ["player"];

	// Current awareness state
	public GameObject Nearest { get; private set; }
	public float DistanceToNearest { get; private set; } = float.MaxValue;
	public List<GameObject> VisibleTargets { get; private set; } = new();
	public List<GameObject> AudibleTargets { get; private set; } = new();

	private TimeSince _lastScan;

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		if ( _lastScan > ScanInterval )
		{
			ScanEnvironment();
			_lastScan = 0;
		}
	}

	public override string GetDebugString()
	{
		if ( VisibleTargets.Count == 0 && AudibleTargets.Count == 0 ) return null;
		return $"Senses: {VisibleTargets.Count} visible, {AudibleTargets.Count} audible";
	}

	/// <summary>
	/// Scan for objects of interest
	/// </summary>
	private void ScanEnvironment()
	{
		// Clear previous scan results
		VisibleTargets.Clear();
		AudibleTargets.Clear();
		Nearest = null;
		DistanceToNearest = float.MaxValue;

		// Find all potential targets in hearing range
		var nearbyObjects = Npc.Scene.FindInPhysics( new Sphere( Npc.WorldPosition, HearingRange ) );

		foreach ( var obj in nearbyObjects )
		{
			if ( !obj.Tags.HasAny( TargetTags ) ) continue;

			var distance = Npc.WorldPosition.Distance( obj.WorldPosition );

			// Track nearest
			if ( distance < DistanceToNearest )
			{
				DistanceToNearest = distance;
				Nearest = obj;
			}

			// Check if within hearing range
			if ( distance <= HearingRange )
			{
				AudibleTargets.Add( obj );
			}

			// Check if within sight range and has line of sight
			if ( distance <= SightRange && HasLineOfSight( obj ) )
			{
				VisibleTargets.Add( obj );
			}
		}
	}

	/// <summary>
	/// Check if we have line of sight to target
	/// </summary>
	private bool HasLineOfSight( GameObject target )
	{
		var eyePosition = Npc.WorldPosition + Vector3.Up * 64f; // Eye height
		var targetPosition = target.WorldPosition + Vector3.Up * 32f; // Target center

		var trace = Npc.Scene.Trace.Ray( eyePosition, targetPosition )
			.IgnoreGameObjectHierarchy( Npc.GameObject )
			.WithoutTags( "trigger" )
			.Run();

		return !trace.Hit || trace.GameObject == target || target.IsDescendant( trace.GameObject );
	}

	/// <summary>
	/// Get the nearest visible
	/// </summary>
	public GameObject GetNearestVisible()
	{
		GameObject nearest = null;
		float nearestDistance = float.MaxValue;

		foreach ( var target in VisibleTargets )
		{
			var distance = Npc.WorldPosition.Distance( target.WorldPosition );
			if ( distance < nearestDistance )
			{
				nearestDistance = distance;
				nearest = target;
			}
		}

		return nearest;
	}

	public override void Reset()
	{
		VisibleTargets.Clear();
		AudibleTargets.Clear();
		Nearest = null;
		DistanceToNearest = float.MaxValue;
	}
}
```

## Разбор кода

| Элемент | Что делает |
|---------|-----------|
| `TargetTags = ["player"]` | По умолчанию NPC реагирует только на игроков |
| `FindInPhysics(Sphere)` | Поиск физических объектов в радиусе |
| `HasLineOfSight` | Трейс с игнорированием собственной иерархии и триггеров |
| `GetNearestVisible()` | Утилита для расписаний — «ближайший видимый враг» |

---

Следующий шаг: [14.10 — NPC: Слой речи (SpeechLayer)](14_10_SpeechLayer.md)
