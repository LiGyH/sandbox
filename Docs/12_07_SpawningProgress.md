# 12.07 — Компонент: Прогресс спавна (SpawningProgress) ✨

## Что мы делаем?

Создаём **SpawningProgress** — компонент визуальной обратной связи, который отображает анимированную рамку (bounding box) во время загрузки и спавна объекта.

## Зачем это нужно?

Когда игрок спавнит объект (проп, NPC, транспорт), модель может загружаться несколько секунд — особенно при первом спавне или с медленным соединением. Без визуальной обратной связи игрок не понимает, что происходит. `SpawningProgress` рисует мерцающий полупрозрачный куб в месте спавна, показывая что объект загружается. Как только объект готов — компонент удаляется.

## Как это работает внутри движка?

- `SpawnBounds` — `BBox?` (nullable), задаётся извне. Определяет размер и положение рамки. Если `null` — ничего не рисуется.
- `base.OnUpdate()` — вызов базового метода `Component.OnUpdate()`. Обеспечивает корректную работу базовой логики компонента.
- Цикл `for (int i = 0; i < 8; i++)` — рисует 8 перекрывающихся кубов за кадр, создавая эффект мерцания.
- `Color.Lerp(Color.White, Color.Cyan, Random.Shared.Float(0, 1))` — случайный цвет между белым и голубым для каждого куба.
- `color.WithAlpha(Random.Shared.Float(0.1f, 0.4f))` — случайная прозрачность, создающая мерцание.
- `bounds.Mins += Vector3.Random * 1.0f` / `bounds.Maxs += Vector3.Random * 1.0f` — случайное смещение границ, создающее эффект «дрожания» рамки.
- `DebugOverlay.Box()` — рисует куб с заданными размерами, цветом и трансформацией.

## Создай файл

Путь: `Code/Components/SpawningProgress.cs`

```csharp
﻿public class SpawningProgress : Component
{
	public BBox? SpawnBounds { get; internal set; }

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( SpawnBounds.HasValue )
		{
			for ( int i = 0; i < 8; i++ )
			{
				var color = Color.Lerp( Color.White, Color.Cyan, Random.Shared.Float( 0, 1 ) );
				color = color.WithAlpha( Random.Shared.Float( 0.1f, 0.4f ) );
				var bounds = SpawnBounds.Value;
				bounds.Mins += Vector3.Random * 1.0f;
				bounds.Maxs += Vector3.Random * 1.0f;

				DebugOverlay.Box( bounds, color, transform: WorldTransform );
			}
		}
	}
}
```

## Проверка

После создания файла проект должен компилироваться. Компонент будет добавляться временно при спавне объектов и удаляться после завершения загрузки.


---

## ➡️ Следующий шаг

Фаза завершена. Переходи к **[13.01 — Этап 13_01 — ScriptedEntity (Пользовательская сущность)](13_01_ScriptedEntity.md)** — начало следующей фазы.
