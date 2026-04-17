# 12.06 — Компонент: Метаданные монтирования (MountMetadata) 🧩

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.24 — Sync Properties](00_24_Sync_Properties.md)

## Что мы делаем?

Создаём **MountMetadata** — компонент, который прикрепляется к объектам, заспавненным из маунтов (установленных игр). Если у клиента нет нужного маунта, компонент показывает заглушку — полупрозрачный куб с подсказкой, какую игру установить.

## Зачем это нужно?

В Sandbox можно спавнить модели из других игр (Half-Life 2, CS2 и т.д.). Если у игрока на сервере нет нужной игры, модель не загрузится — вместо неё будет ошибка. `MountMetadata` решает эту проблему элегантно:
1. Запоминает размеры и центр `BBox` оригинальной модели.
2. Если модель не загрузилась — скрывает ошибочный рендерер и рисует полупрозрачный куб нужного размера.
3. Показывает текстовую подсказку «🧩 Install [Game Title]» над объектом.

## Как это работает внутри движка?

- `[Sync(SyncFlags.FromHost)]` — свойства `GameTitle`, `BoundsSize`, `BoundsCenter` синхронизируются от хоста ко всем клиентам.
- `OnStart()` — `async void`, проверяет наличие модели. Если модель отсутствует (`!prop.Model.IsValid() && !prop.Model.IsError`), скрывает все `ModelRenderer` и активирует режим заглушки.
- `OnUpdate()` — в режиме заглушки каждый кадр рисует куб через `DebugOverlay.Model` с кастомным материалом `mount_fallback.vmat` и текстовую подсказку.
- `_material` — статическое поле, материал загружается один раз и переиспользуется для всех экземпляров.
- `Transform` вычисляется из мировой трансформации объекта с учётом центра и размера `BBox`.

## Создай файл

Путь: `Code/Components/MountMetadata.cs`

```csharp
/// <summary>
/// Attached to mount-spawned props. This tries to show a graceful bounding box and hints the player what game it's from if they don't have the mount installed.
/// </summary>
[Title( "Mount Metadata" )]
[Category( "Rendering" )]
public sealed class MountMetadata : Component
{
	[Property, Sync( SyncFlags.FromHost )] public string GameTitle { get; set; }
	[Property, Sync( SyncFlags.FromHost )] public Vector3 BoundsSize { get; set; }
	[Property, Sync( SyncFlags.FromHost )] public Vector3 BoundsCenter { get; set; }

	bool _isFallback;

	static Material _material;

	protected override async void OnStart()
	{
		_material ??= Material.Load( "materials/effects/mount_fallback.vmat" );

		var prop = GameObject.GetComponentInChildren<Prop>();
		var modelMissing = prop.IsValid() && !prop.Model.IsValid() && !prop.Model.IsError;
		if ( !modelMissing ) return;

		// Client doesn't have the mount — hide the error model and show fallback overlay
		_isFallback = true;
		foreach ( var mr in GameObject.GetComponentsInChildren<ModelRenderer>() )
		{
			mr.Enabled = false;
		}
	}

	protected override void OnUpdate()
	{
		if ( !_isFallback ) return;

		var bounds = new BBox( BoundsCenter - BoundsSize / 2f, BoundsCenter + BoundsSize / 2f );

		var t = WorldTransform;
		t = new Transform( t.PointToWorld( bounds.Center ), t.Rotation, t.Scale * (bounds.Size / 50f) );

		Game.ActiveScene.DebugOverlay.Model( Model.Cube, transform: t, overlay: false, materialOveride: _material );

		if ( !string.IsNullOrEmpty( GameTitle ) )
		{
			var textPos = WorldPosition + Vector3.Up * (BoundsSize.z / 2f + 8f);
			DebugOverlay.Text( textPos, $"🧩 Install {GameTitle}", color: Color.White, duration: 0f );
		}
	}
}
```

## Проверка

После создания файла проект должен компилироваться. Компонент будет добавляться автоматически при спавне пропов из маунтов (Фаза спавна объектов).


---

## ➡️ Следующий шаг

Переходи к **[12.07 — Компонент: Прогресс спавна (SpawningProgress) ✨](12_07_SpawningProgress.md)**.
