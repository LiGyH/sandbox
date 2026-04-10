# 05.01 — Компоненты: Очистка констрейнтов (ConstraintCleanup) 🔗

## Что мы делаем?

Создаём **ConstraintCleanup** — компонент, который автоматически удаляет связанный объект (`Attachment`) при уничтожении самого компонента, и самоуничтожается если связанный объект уже удалён.

## Как это работает?

Когда ToolGun создаёт физическое соединение (Weld, Rope, Elastic), он создаёт вспомогательные объекты. `ConstraintCleanup` следит за парой: если один объект удалён — удаляем другой.

```csharp
protected override void OnDestroy()
{
    if ( Attachment.IsValid() )
        Attachment.Destroy();
}

protected override void OnUpdate()
{
    if ( !Attachment.IsValid() )
        DestroyGameObject();
}
```

## Создай файл

Путь: `Code/Components/ConstraintCleanup.cs`

```csharp
internal class ConstraintCleanup : Component
{
	[Property]
	public GameObject Attachment { get; set; }

	protected override void OnDestroy()
	{
		if ( Attachment.IsValid() )
		{
			Attachment.Destroy();
		}

		base.OnDestroy();
	}

	protected override void OnUpdate()
	{
		if (  !Attachment.IsValid() )
		{
			DestroyGameObject();
			return;
		}
	}
}
```

---

# 05.02 — Событие Physgun (IPhysgunEvent) 🧲

Интерфейс, позволяющий объектам **отклонять захват** физганом. Компоненты реализуют `OnPhysgunGrab` и могут установить `Cancelled = true`.

Путь: `Code/Components/IPhysgunEvent.cs`

```csharp
public interface IPhysgunEvent : ISceneEvent<IPhysgunEvent>
{
	public class GrabEvent
	{
		public Connection Grabber { get; init; }
		public bool Cancelled { get; set; }
	}

	void OnPhysgunGrab( GrabEvent e ) { }
}
```

---

# 05.03 — Событие Toolgun (IToolgunEvent) 🔧

Аналог `IPhysgunEvent` для тулгана. Позволяет объектам отклонять взаимодействие с инструментами.

Путь: `Code/Components/IToolgunEvent.cs`

```csharp
public interface IToolgunEvent : ISceneEvent<IToolgunEvent>
{
	public class SelectEvent
	{
		public Connection User { get; init; }
		public bool Cancelled { get; set; }
	}

	void OnToolgunSelect( SelectEvent e ) { }
}
```

---

# 05.04 — Ручная связь (ManualLink) 🔗

Логическая (нефизическая) связь между двумя объектами. Используется Linker Tool для группировки объектов, чтобы Duplicator копировал их вместе.

Путь: `Code/Components/ManualLink.cs`

```csharp
public sealed class ManualLink : Component
{
	[Property, Sync]
	public GameObject Body { get; set; }

	protected override void OnDestroy()
	{
		if ( Body.IsValid() )
			Body.Destroy();

		base.OnDestroy();
	}

	protected override void OnUpdate()
	{
		if ( !Body.IsValid() )
			DestroyGameObject();
	}
}
```

---

# 05.05 — Переопределение массы (MassOverride) ⚖️

Сохраняет и применяет пользовательскую массу к Rigidbody. Нужно для дупликации — без этого компонента масса терялась бы при копировании.

Путь: `Code/Components/MassOverride.cs`

```csharp
public sealed class MassOverride : Component
{
	[Property, Sync]
	public float Mass { get; set; } = 100f;

	protected override void OnStart() => Apply();
	protected override void OnEnabled() => Apply();

	public void Apply()
	{
		var rb = GameObject.Root.GetComponent<Rigidbody>();
		if ( rb.IsValid() ) rb.MassOverride = Mass;
	}
}
```

---

# 05.06 — Состояние морфов (MorphState) 🎭

Сохраняет и синхронизирует по сети значения морфов (деформаций лица/тела). Используется для Face Poser и сохранения в дупликатах.

Путь: `Code/Components/MorphState.cs`

```csharp
[Title( "Morph State" )]
[Category( "Rendering" )]
public sealed class MorphState : Component
{
	[Property]
	public string SerializedMorphs { get; set; }

	protected override void OnStart() { Apply(); }

	public void ApplyBatch( string morphsJson ) { /* ... */ }
	public void ApplyPreset( string morphsJson ) { /* ... */ }
	public void Capture( SkinnedModelRenderer smr ) { /* ... */ }
	public void Apply() { /* ... */ }
}
```

Ключевой механизм: хост изменяет морфы → `Capture()` сериализует в JSON → `BroadcastBatch`/`BroadcastPreset` рассылает всем клиентам.

---

# 05.07 — Метаданные маунтов (MountMetadata) 🧩

Отображает «заглушку» (bounding box) для пропов из маунтов (HL2, CS:GO), которые не установлены у клиента. Показывает название игры и размер объекта.

Путь: `Code/Components/MountMetadata.cs`

*(полный код — см. исходник)*

---

# 05.08 — Владение объектом (Ownable) 🔒

Отслеживает, **кто создал объект**. Если включена «Prop Protection» (`sb.ownership_checks`), только владелец (или хост) может взаимодействовать с объектом через Physgun/Toolgun.

Путь: `Code/Components/Ownable.cs`

```csharp
public sealed class Ownable : Component, IPhysgunEvent, IToolgunEvent
{
	[Sync( SyncFlags.FromHost )]
	private Guid _ownerId { get; set; }

	public Connection Owner { get; set; }

	public static Ownable Set( GameObject go, Connection owner ) { ... }

	// Prop Protection ConVar
	[ConVar( "sb.ownership_checks", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting )]
	public static bool OwnershipChecks { get; set; } = false;

	// Проверяем доступ при захвате Physgun/Toolgun
	void IPhysgunEvent.OnPhysgunGrab( IPhysgunEvent.GrabEvent e )
	{
		if ( !CallerHasAccess( e.Grabber ) ) e.Cancelled = true;
	}
}
```

---

# 05.09 — Прогресс спавна (SpawningProgress) ⏳

Визуальный индикатор во время спавна объекта — мерцающие кубоиды, показывающие зону размещения.

Путь: `Code/Components/SpawningProgress.cs`

```csharp
public class SpawningProgress : Component
{
	public BBox? SpawnBounds { get; internal set; }

	protected override void OnUpdate()
	{
		if ( SpawnBounds.HasValue )
		{
			for ( int i = 0; i < 8; i++ )
			{
				var color = Color.Lerp( Color.White, Color.Cyan, Random.Shared.Float( 0, 1 ) );
				color = color.WithAlpha( Random.Shared.Float( 0.1f, 0.4f ) );
				DebugOverlay.Box( SpawnBounds.Value, color, transform: WorldTransform );
			}
		}
	}
}
```

## 🎉 Фаза 5 завершена!

Все компоненты документированы. Следующая фаза — **Фаза 6: Система оружия (Weapons)**.
