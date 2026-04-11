# 🎯 ToolMode.Helpers.cs — SelectionPoint и вспомогательные методы

## Что мы делаем?

Создаём файл `ToolMode.Helpers.cs` — расширение `partial class ToolMode`, которое содержит:
1. **`SelectionPoint`** — структуру, описывающую точку попадания луча
2. **`TraceSelect()`** — метод трассировки луча из глаз игрока
3. **`GetEasyModePlacement()`** — вычисление позиции для «лёгкого режима» сварки
4. **`ApplyPhysicsProperties()`** — применение физических свойств модели

## Зачем это нужно?

Каждый инструмент должен знать **куда** смотрит игрок и **что** он видит. `TraceSelect()` — единый метод для всех инструментов, который:
- Выпускает луч из глаз игрока на 4096 юнитов
- Игнорирует самого игрока и указанные теги
- Проверяет разрешения через `IToolgunEvent`
- Возвращает `SelectionPoint` с позицией, объектом и нормалью

## Как это работает внутри движка?

### Структура SelectionPoint

```
SelectionPoint
  ├── GameObject      — объект, в который попал луч
  ├── LocalTransform  — позиция/поворот попадания В ЛОКАЛЬНЫХ координатах объекта
  ├── IsValid()       — попали ли мы вообще во что-то?
  ├── WorldPosition() — позиция попадания в мировых координатах
  ├── WorldTransform()— полный трансформ попадания в мировых координатах
  ├── IsWorld         — это часть карты (статичный мир)?
  └── IsPlayer        — это игрок?
```

**Почему `LocalTransform`?** Потому что объекты двигаются! Если мы запомним мировую позицию, а объект сдвинется, точка «уедет». А локальная позиция привязана к объекту и двигается вместе с ним.

### Трассировка (TraceSelect)

```
Глаз игрока → Луч (4096 юнитов) → Попадание?
  ├── Да  → Проверить IToolgunEvent (Ownable может заблокировать)
  │         ├── Разрешено → Вернуть SelectionPoint
  │         └── Заблокировано → Вернуть default (пусто)
  └── Нет → Вернуть default (пусто)
```

### GetEasyModePlacement

Используется в инструменте Weld (сварка) для «лёгкого режима»:
- Берёт точку A (на первом объекте) и точку B (на втором)
- Вычисляет трансформ, который **поместит** первый объект так, чтобы точки соприкасались

### ApplyPhysicsProperties

Когда мы создаём новый объект (пропеллер, колесо), ему нужно задать физические свойства из модели:
- Масса
- Линейное/угловое затухание
- Центр масс
- Гравитация

## Создай файл

📄 `Code/Weapons/ToolGun/ToolMode.Helpers.cs`

```csharp
﻿public abstract partial class ToolMode
{
	/// <summary>
	/// A point in the world selected by the current player's toolgun. We try to keep this as minimal as possible
	/// while catering for as much as possible.
	/// </summary>
	public struct SelectionPoint
	{
		public GameObject GameObject { get; set; }
		public Transform LocalTransform { get; set; }


		/// <summary>
		/// Returns true if GameObject is valid - which means we hit something
		/// </summary>
		public bool IsValid()
		{
			return GameObject.IsValid();
		}

		/// <summary>
		/// Returns the position of the hit - in the world space. This is the transformed position of the LocalTransform relative to the GameObject.
		/// </summary>
		/// <returns></returns>
		public Vector3 WorldPosition()
		{
			return GameObject.WorldTransform.PointToWorld( LocalTransform.Position );
		}

		/// <summary>
		/// Returns the transform of the hit
		/// </summary>
		/// <returns></returns>
		public Transform WorldTransform()
		{
			return GameObject.WorldTransform.ToWorld( LocalTransform );
		}

		/// <summary>
		/// Returns true if this object is a part of the static map
		/// </summary>
		public bool IsWorld => GameObject.Tags.Has( "world" );


		/// <summary>
		/// Returns true if this object is a player
		/// </summary>
		public bool IsPlayer => GameObject.Tags.Has( "player" );
	}

	/// <summary>
	/// Get a SelectionPoint from the tool gun owner's eyes.
	/// </summary>
	public SelectionPoint TraceSelect()
	{
		var player = Toolgun?.Owner;
		if ( !player.IsValid() ) return default;

		var trace = Scene.Trace.Ray( player.EyeTransform.ForwardRay, 4096 )
		.IgnoreGameObjectHierarchy( player.GameObject );

		if ( TraceIgnoreTags.Any() )
			trace = trace.WithoutTags( TraceIgnoreTags.ToArray() );

		if ( TraceHitboxes )
			trace = trace.UseHitboxes();

		var tr = trace.Run();

		var sp = new SelectionPoint
		{
			GameObject = tr.GameObject,
			LocalTransform = tr.GameObject?.WorldTransform.ToLocal( new Transform( tr.EndPosition, Rotation.LookAt( tr.Normal ) ) ) ?? global::Transform.Zero
		};

		if ( sp.IsValid() )
		{
			// Ask the object if it allows toolgun interaction (Ownable and others can reject via IToolgunEvent)
			var selectEvent = new IToolgunEvent.SelectEvent { User = Player.Network.Owner };
			sp.GameObject.Root.RunEvent<IToolgunEvent>( x => x.OnToolgunSelect( selectEvent ) );
			if ( selectEvent.Cancelled ) return default;
		}

		return sp;
	}

	/// <summary>
	/// Given a clicked point on a, and a clicked point on b, return a transform that places the objects so the points are touching
	/// </summary>
	public Transform GetEasyModePlacement( SelectionPoint a, SelectionPoint b )
	{
		var go = a.GameObject.Network.RootGameObject ?? a.GameObject;

		var tx = b.WorldTransform();
		tx.Rotation = tx.Rotation * a.LocalTransform.Rotation.Inverse * new Angles( 180, 0, 0 );
		tx.Position += tx.Rotation * -a.LocalTransform.Position * go.WorldScale;
		tx.Scale = go.WorldScale;
		return tx;
	}

	/// <summary>
	/// Helper to apply physics properties from a model to a GameObject's <see cref="Rigidbody"/>
	/// </summary>
	/// <param name="go"></param>
	protected void ApplyPhysicsProperties( GameObject go )
	{
        var model = go.GetComponentInChildren<ModelRenderer>();

        if ( !model.IsValid() ) return;
        if ( model.Model.Physics is null ) return;

        if ( model.Model.Physics.Parts.Count == 1 )
        {
            var part = model.Model.Physics.Parts[0];
            var rb = go.GetComponent<Rigidbody>();

            rb.MassOverride = part.Mass;
            rb.LinearDamping = part.LinearDamping;
            rb.AngularDamping = part.AngularDamping;
            rb.OverrideMassCenter = part.OverrideMassCenter;
            rb.MassCenterOverride = part.MassCenterOverride;
            rb.GravityScale = part.GravityScale;
        }
    }
}
```

## Разбор ключевых моментов

### Почему `LocalTransform`?

```csharp
LocalTransform = tr.GameObject?.WorldTransform.ToLocal(
    new Transform( tr.EndPosition, Rotation.LookAt( tr.Normal ) )
) ?? global::Transform.Zero
```

Здесь мы:
1. Берём точку попадания (`tr.EndPosition`) и нормаль (`tr.Normal`)
2. Создаём `Transform` — позиция = точка, поворот = смотрит по нормали
3. Конвертируем в **локальные** координаты объекта через `ToLocal()`

### IToolgunEvent — проверка разрешений

```csharp
sp.GameObject.Root.RunEvent<IToolgunEvent>( x => x.OnToolgunSelect( selectEvent ) );
if ( selectEvent.Cancelled ) return default;
```

Компонент `Ownable` (из фазы 5) может **заблокировать** использование инструмента на объектах чужих игроков, установив `selectEvent.Cancelled = true`.

## Что проверить?

После создания всех четырёх partial-файлов (`ToolMode.cs`, `.Cookies.cs`, `.Effects.cs`, `.Helpers.cs`) класс `ToolMode` собирается в единое целое. Но для полной компиляции нужен ещё `ToolMode.SnapGrid.cs` (следующий шаг).

---

➡️ Следующий шаг: [09_05 — ToolMode.SnapGrid.cs и SnapGrid.cs](09_05_SnapGrid.md)
