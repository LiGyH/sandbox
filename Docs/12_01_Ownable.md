# 12.01 — Компонент: Владение объектом (Ownable) 🛡️

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.24 — Sync Properties](00_24_Sync_Properties.md)

## Что мы делаем?

Создаём **Ownable** — компонент, который отслеживает владельца объекта (кто его заспавнил) и реализует систему защиты пропов. При включённой защите только владелец и игроки с правами администратора (`HasPermission( "admin" )`) могут взаимодействовать с объектом через физ-пушку и тул-ган.

## Зачем это нужно?

На многопользовательском сервере игроки создают конструкции. Без защиты пропов любой может схватить или сломать чужой объект. `Ownable` решает это:
1. Запоминает `Connection` владельца через `Guid` (синхронизируемый по сети).
2. Реализует `IPhysgunEvent` и `IToolgunEvent` — перехватывает попытки взаимодействия и отклоняет их, если вызывающий не является владельцем.
3. Управляется серверной настройкой `sb.ownership_checks` (по умолчанию выключено).

## Как это работает внутри движка?

- `_ownerId` — `Guid` владельца, синхронизируемый от хоста (`SyncFlags.FromHost`). Используется вместо `Connection` напрямую, т.к. `Connection` нельзя синхронизировать.
- `Owner` — свойство-обёртка, ищет `Connection` по `_ownerId` в списке всех подключений. `[JsonIgnore]` исключает из сериализации.
- `Set()` — статический метод для удобного назначения владельца. Создаёт компонент если его нет (`GetOrAddComponent`).
- `OwnershipChecks` — `ConVar` с флагами `Replicated | Server | GameSetting`. Реплицируется на клиенты, но изменяется только на сервере.
- `HasAccess()` — статический метод проверки доступа. Игроки с правом `admin` имеют доступ всегда (`Connection.HasPermission( "admin" )`). Если владелец не задан — доступ открыт всем.
- `IPhysgunEvent.OnPhysgunGrab()` / `IToolgunEvent.OnToolgunSelect()` — явные реализации интерфейсов, отменяющие событие если нет доступа.
- `OwnableExtensions.HasAccess(GameObject, Connection)` — extension-метод для удобной проверки прав на любой `GameObject`. Если на объекте нет компонента `Ownable`, считаем доступ разрешённым (`return true`). Используется, например, в `GameManager.DeleteInspectedObject` / `BreakInspectedProp` для проверки «можно ли вызывающему через инспектор удалить/сломать этот проп».
- Метод `CallerHasAccess` помечен `internal` — внешним сборкам нужно ходить через `OwnableExtensions.HasAccess` или статический `Ownable.HasAccess(caller, owner)`.

## Создай файл

Путь: `Code/Components/Ownable.cs`

```csharp
using Sandbox;
using System.Text.Json.Serialization;

/// <summary>
/// Tracks which connection spawned this object
/// </summary>
public sealed class Ownable : Component, IPhysgunEvent, IToolgunEvent
{
	[Sync( SyncFlags.FromHost )]
	private Guid _ownerId { get; set; }

	/// <summary>
	/// I would fucking love to be able to Sync these..
	/// And it would just do this exact behaviour. Why not?
	/// </summary>
	[Property, ReadOnly, JsonIgnore]
	public Connection Owner
	{
		get => Connection.All.FirstOrDefault( c => c.Id == _ownerId );
		set => _ownerId = value?.Id ?? Guid.Empty;
	}

	public static Ownable Set( GameObject go, Connection owner )
	{
		var ownable = go.GetOrAddComponent<Ownable>();
		ownable.Owner = owner;
		return ownable;
	}

	/// <summary>
	/// When enabled, players can only physgun/toolgun objects they own.
	/// Players with the "admin" permission are always exempt. Off by default.
	/// </summary>
	[Title( "Prop Protection" )]
	[ConVar( "sb.ownership_checks", ConVarFlags.Replicated | ConVarFlags.Server | ConVarFlags.GameSetting, Help = "Enforce ownership, players can only interact with their own props." )]
	public static bool OwnershipChecks { get; set; } = false;

	internal bool CallerHasAccess( Connection caller ) => HasAccess( caller, Owner );

	public static bool HasAccess( Connection caller, Connection owner )
	{
		if ( !OwnershipChecks ) return true;
		if ( caller is null ) return false;
		if ( caller.HasPermission( "admin" ) ) return true;
		if ( owner is null ) return true;
		return owner == caller;
	}

	void IPhysgunEvent.OnPhysgunGrab( IPhysgunEvent.GrabEvent e )
	{
		if ( !CallerHasAccess( e.Grabber ) )
			e.Cancelled = true;
	}

	void IToolgunEvent.OnToolgunSelect( IToolgunEvent.SelectEvent e )
	{
		if ( !CallerHasAccess( e.User ) )
			e.Cancelled = true;
	}
}


public static class OwnableExtensions
{
	public static bool HasAccess( this GameObject go, Connection caller )
	{
		if ( go.Components.TryGet<Ownable>( out var ownable ) )
			return ownable.CallerHasAccess( caller );
		return true;
	}
}
```

## Проверка

После создания файла проект должен компилироваться. Компонент зависит от `IPhysgunEvent` (05.02) и `IToolgunEvent` (05.03). Будет добавляться ко всем спавнимым объектам.


---

## ➡️ Следующий шаг

Переходи к **[12.02 — Компонент: Очистка констрейнтов (ConstraintCleanup) 🔗](12_02_ConstraintCleanup.md)**.
