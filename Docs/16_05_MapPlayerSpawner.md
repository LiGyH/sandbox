# 12.01 — Спавнер игроков на карте (MapPlayerSpawner) 🗺️

## Что мы делаем?

Создаём **MapPlayerSpawner** — компонент, который при загрузке карты телепортирует всех игроков на случайные точки спавна (`SpawnPoint`).

## Как это работает?

`MapPlayerSpawner` вешается на тот же объект, что и `MapInstance`. Когда карта загружена (событие `OnMapLoaded`), он находит все `SpawnPoint` на сцене и телепортирует каждого локального игрока на случайную из них.

## Создай файл

Путь: `Code/Map/MapPlayerSpawner.cs`

```csharp
public sealed class MapPlayerSpawner : Component
{
	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( Components.TryGet<MapInstance>( out var mapInstance ) )
		{
			mapInstance.OnMapLoaded += RespawnPlayers;

			// already loaded
			if ( mapInstance.IsLoaded )
			{
				RespawnPlayers();
			}
		}
	}

	protected override void OnDisabled()
	{
		if ( Components.TryGet<MapInstance>( out var mapInstance ) )
		{
			mapInstance.OnMapLoaded -= RespawnPlayers;
		}

	}

	void RespawnPlayers()
	{
		var spawnPoints = Scene.GetAllComponents<SpawnPoint>().ToArray();

		foreach ( var player in Scene.GetAllComponents<Player>().ToArray() )
		{
			if ( player.IsProxy )
				continue;

			var randomSpawnPoint = Random.Shared.FromArray( spawnPoints );
			if ( randomSpawnPoint is null ) continue;

			player.WorldPosition = randomSpawnPoint.WorldPosition;
			player.Controller.EyeAngles = randomSpawnPoint.WorldRotation.Angles();
		}
	}
}
```

## Разбор кода

| Элемент | Что делает |
|---------|-----------|
| `MapInstance` | Компонент движка, управляющий загрузкой карты |
| `OnMapLoaded` | Событие, срабатывающее после полной загрузки карты |
| `SpawnPoint` | Встроенный компонент движка — точка спавна, размещённая в редакторе |
| `IsProxy` | Пропускаем «чужих» игроков — каждый клиент телепортирует только себя |
| `Random.Shared.FromArray` | Выбирает случайный элемент из массива |
| `EyeAngles` | Устанавливаем направление взгляда по ротации точки спавна |

## Результат

- При загрузке/перезагрузке карты все игроки телепортируются на случайные SpawnPoint
- Если карта уже загружена — спавн происходит немедленно

---

Следующий шаг: [12.02 — Базовый переключатель (BaseToggle)](12_02_BaseToggle.md)
