# Этап 14_01 — ISpawner

## Что это такое?

**ISpawner** — это интерфейс (контракт), который описывает, что должен уметь любой «спаунер» в игре.

### Аналогия для новичков

Представьте себе **чертёж-шаблон**. Сам по себе он ничего не создаёт, но говорит:
«Любой, кто хочет размещать объекты в мире, **обязан** уметь показывать превью и создавать объект».
Все конкретные спаунеры (`PropSpawner`, `EntitySpawner`, `MountSpawner`) следуют этому шаблону.

---

## Полный исходный код

```csharp
/// <summary>
/// Describes something that can be spawned into the world
/// Implementations handle their own preview rendering and spawn logic.
/// </summary>
public interface ISpawner
{
	/// <summary>
	/// Display name shown in the HUD while holding this payload.
	/// </summary>
	string DisplayName { get; }

	/// <summary>
	/// Icon path for this payload, used for inventory display via <c>thumb:path</c>.
	/// </summary>
	string Icon { get; }

	/// <summary>
	/// The local-space bounds of the thing being spawned, used to offset placement so it sits on surfaces.
	/// </summary>
	BBox Bounds { get; }

	/// <summary>
	/// Whether all required resources (packages, models, etc.) are loaded and ready to place.
	/// </summary>
	bool IsReady { get; }

	/// <summary>
	/// A task that completes with true when loading succeeds, or false if it fails.
	/// </summary>
	Task<bool> Loading { get; }

	/// <summary>
	/// The raw data needed to reconstruct this spawner (e.g. a cloud ident, or JSON).
	/// </summary>
	string Data { get; }

	/// <summary>
	/// Draw a ghost preview at the given world transform.
	/// </summary>
	void DrawPreview( Transform transform, Material overrideMaterial );

	/// <summary>
	/// Actually spawn the thing at the given transform. Called on the host.
	/// Returns the root GameObject(s) that were spawned so they can be added to undo.
	/// </summary>
	Task<List<GameObject>> Spawn( Transform transform, Player player );
}
```

---

## Разбор важных частей

### Свойства-идентификаторы

- **`DisplayName`** — имя, которое видит игрок в интерфейсе (HUD), когда держит объект.
- **`Icon`** — путь к иконке для отображения в инвентаре.
- **`Data`** — «сырые» данные для воссоздания спаунера (например, cloud-идентификатор или JSON).

### Состояние загрузки

- **`IsReady`** — возвращает `true`, когда все ресурсы (модели, пакеты) загружены и можно размещать объект.
- **`Loading`** — асинхронная задача (`Task<bool>`), которая завершается `true` при успешной загрузке и `false` при ошибке.

### Геометрия

- **`Bounds`** — ограничивающий параллелепипед (bounding box) объекта в локальных координатах. Используется, чтобы объект корректно «стоял» на поверхности, а не проваливался.

### Методы

- **`DrawPreview(Transform, Material)`** — рисует полупрозрачный «призрак» объекта в указанной позиции. Это то, что игрок видит перед размещением.
- **`Spawn(Transform, Player)`** — фактически создаёт объект в мире. Вызывается на хосте (сервере). Возвращает список созданных `GameObject`, чтобы их можно было отменить (undo).

---

## Почему это важно?

Благодаря интерфейсу `ISpawner` весь остальной код (инструмент размещения, инвентарь, сеть) **не зависит** от конкретного типа объекта. Ему всё равно, размещаете ли вы пропс, сущность или объект из смонтированной игры — он работает с единым контрактом.
