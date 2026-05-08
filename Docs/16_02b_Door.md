# 16.02b — Дверь (Door) 🚪 (УСТАРЕВШИЙ ДОКУМЕНТ ⚠️)

> ## ⚠️ Этот документ устарел
>
> В актуальной версии sandbox **компонент `Door` (`Code/Map/Door.cs`) удалён** из исходного кода вместе с парной [`Button`](16_02_Button_Door.md). Сейчас Facepunch рекомендует использовать движковые префабы и компоненты для дверей (см. [официальные доки sbox-docs](https://github.com/Facepunch/sbox-docs/), раздел Components → Pressable / Animated Doors), а в `Code/Map/` остались только `BaseToggle`, `FuncMover`, `MapPlayerSpawner` и `Trigger*`-компоненты.
>
> Если в твоей карте/режиме всё ещё нужна старая `Door` — скопируй приведённый ниже код в `Code/Map/Door.cs` своего проекта; он работоспособен, но больше не сопровождается апстримом.
>
> Ниже сохранён старый текст документа как историческая справка.

---

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)
> - [00.22 — Ownership](00_22_Ownership.md)
> - [00.24 — Sync Properties](00_24_Sync_Properties.md)


## Что мы делаем?

Создаём **Door** — компонент двери, которую игрок может открывать/закрывать нажатием (IPressable). Дверь вращается вокруг точки поворота с анимационной кривой и звуковыми эффектами.

## Как это работает?

Дверь реализует `IPressable` — интерфейс движка для интерактивных объектов (нажатие E). При нажатии:
1. Проверяется, заблокирована ли дверь (`IsLocked`)
2. Определяется направление открытия (от игрока, если `OpenAwayFromPlayer`)
3. Запускается анимация вращения по `AnimationCurve` за `OpenTime` секунд
4. Играются звуки открытия/закрытия

### Состояния двери

```
Closed → Opening → Open → Closing → Closed
```

## Создай файл

Путь: `Code/Map/Door.cs`

```csharp
﻿namespace Facepunch;

public sealed class Door : Component, Component.IPressable
{
	/// <summary>
	/// Animation curve to use, X is the time between 0-1 and Y is how much the door is open to its target angle from 0-1.
	/// </summary>
	[Property] public Curve AnimationCurve { get; set; } = new Curve( new Curve.Frame( 0f, 0f ), new Curve.Frame( 1f, 1.0f ) );

	/// <summary>
	/// Sound to play when a door is opened.
	/// </summary>
	[Property, Group( "Sound" )] public SoundEvent OpenSound { get; set; }

	/// <summary>
	/// Sound to play when a door is interacted with while locked.
	/// </summary>
	[Property, Group( "Sound" )] public SoundEvent LockedSound { get; set; }

	/// <summary>
	/// Sound to play when a door is fully opened.
	/// </summary>
	[Property, Group( "Sound" )] public SoundEvent OpenFinishedSound { get; set; }

	/// <summary>
	/// Sound to play when a door is closed.
	/// </summary>
	[Property, Group( "Sound" )] public SoundEvent CloseSound { get; set; }

	/// <summary>
	/// Sound to play when a door has finished closing.
	/// </summary>
	[Property, Group( "Sound" )] public SoundEvent CloseFinishedSound { get; set; }

	/// <summary>
	/// Optional pivot point, origin will be used if not specified.
	/// </summary>
	[Property] public GameObject Pivot { get; set; }

	/// <summary>
	/// How far should the door rotate.
	/// </summary>
	[Property, Range( 0.0f, 90.0f )] public float TargetAngle { get; set; } = 90.0f;

	/// <summary>
	/// How long in seconds should it take to open this door.
	/// </summary>
	[Property] public float OpenTime { get; set; } = 0.5f;

	/// <summary>
	/// Open away from the person who uses this door.
	/// </summary>
	[Property] public bool OpenAwayFromPlayer { get; set; } = true;

	/// <summary>
	/// The door's state
	/// </summary>
	public enum DoorState
	{
		Open,
		Opening,
		Closing,
		Closed
	}

	Transform _startTransform;
	Vector3 _pivotPosition;
	bool _reverseDirection;

	/// <summary>
	/// Is this door locked?
	/// </summary>
	[Property, Sync] public bool IsLocked { get; set; }

	[Sync] private TimeSince LastUse { get; set; }
	[Sync] private DoorState _state { get; set; }

	/// <summary>
	/// Called when the door's state changes. When it opens, closed, is closing, etc..
	/// </summary>
	[Property, Group( "Events" )]
	public Action<DoorState> OnStateChanged { get; set; }

	public DoorState State
	{
		get => _state;
		private set
		{
			if ( _state == value )
				return;

			_state = value;
			OnDoorStateChanged( value );
		}
	}

	void OnDoorStateChanged( DoorState value )
	{
		OnStateChanged?.Invoke( value );

		if ( IsProxy )
			return;

		if ( value == DoorState.Open )
		{
			if ( OpenFinishedSound is not null )
				PlaySound( OpenFinishedSound );
		}
		else if ( value == DoorState.Closed )
		{
			if ( CloseFinishedSound is not null )
				PlaySound( CloseFinishedSound );
		}
	}

	protected override void OnStart()
	{
		_startTransform = Transform.Local;
		_pivotPosition = Pivot is not null ? Pivot.WorldPosition : _startTransform.Position;
	}

	bool IPressable.CanPress( IPressable.Event e )
	{
		return State is DoorState.Open or DoorState.Closed;
	}

	bool IPressable.Press( IPressable.Event e )
	{
		Toggle( e.Source.GameObject );
		return true;
	}

	/// <summary>
	/// Opens the door. Does nothing if already open or opening.
	/// </summary>
	[Rpc.Host]
	[ActionGraphNode( "sandbox.door.open" ), Pure]
	public void Open( GameObject presser )
	{
		if ( State is DoorState.Open or DoorState.Opening )
		{
			return;
		}

		if ( IsLocked )
		{
			if ( LockedSound is not null )
				PlaySound( LockedSound );
			return;
		}

		LastUse = 0;
		State = DoorState.Opening;

		if ( OpenSound is not null )
		PlaySound( OpenSound );

		if ( OpenAwayFromPlayer && presser.IsValid() )
		{
			var doorToPlayer = (presser.WorldPosition - _pivotPosition).Normal;
			var doorForward = Transform.Local.Rotation.Forward;

			_reverseDirection = Vector3.Dot( doorToPlayer, doorForward ) > 0;
		}
	}

	/// <summary>
	/// Closes the door. Does nothing if already closed or closing.
	/// </summary>
	[Rpc.Host]
	[ActionGraphNode( "sandbox.door.close" ), Pure]
	public void Close()
	{
		// Don't do anything if already closed or closing
		if ( State is DoorState.Closed or DoorState.Closing )
			return;

		LastUse = 0;
		State = DoorState.Closing;

		if ( CloseSound is not null )
			PlaySound( CloseSound );
	}

	/// <summary>
	/// Toggles the door between open and closed states.
	/// </summary>
	[Rpc.Host]
	[ActionGraphNode( "sandbox.door.toggle" ), Pure]
	public void Toggle( GameObject presser )
	{
		if ( State is DoorState.Closed )
		{
			Open( presser );
		}
		else if ( State is DoorState.Open )
		{
			Close();
		}
	}

	[Rpc.Broadcast]
	private void PlaySound( SoundEvent sound )
	{
		GameObject.PlaySound( sound );
	}

	protected override void OnFixedUpdate()
	{
		// Don't do anything if we're not opening or closing
		if ( State != DoorState.Opening && State != DoorState.Closing )
			return;

		// Normalize the last use time to the amount of time to open
		var time = LastUse.Relative.Remap( 0.0f, OpenTime, 0.0f, 1.0f );

		// Evaluate our animation curve
		var curve = AnimationCurve.Evaluate( time );

		// Rotate backwards if we're closing
		if ( State == DoorState.Closing ) curve = 1.0f - curve;

		var targetAngle = TargetAngle;
		if ( _reverseDirection ) targetAngle *= -1.0f;

		// Do the rotation
		Transform.Local = _startTransform.RotateAround( _pivotPosition, Rotation.FromYaw( curve * targetAngle ) );

		// If we're done finalize the state and play the sound
		if ( time < 1f ) return;

		State = State == DoorState.Opening ? DoorState.Open : DoorState.Closed;
	}
}
```

## Ключевые механизмы

| Механизм | Описание |
|----------|----------|
| `IPressable` | Интерфейс «нажимаемого» — игрок нажимает E для взаимодействия |
| `[Rpc.Host]` | Метод выполняется только на хосте — серверная авторизация |
| `[Rpc.Broadcast]` | Метод вызывается на всех клиентах — для звуков |
| `RotateAround` | Поворот вокруг точки (pivot), не центра объекта |
| `AnimationCurve` | Кривая анимации — плавное ускорение/замедление |
| `OpenAwayFromPlayer` | Дверь определяет, с какой стороны стоит игрок, и открывается от него |

## Результат

После создания этого файла:
- Двери на карте можно открывать/закрывать нажатием E
- Поддержка блокировки, звуков, кривой анимации
- Работает в мультиплеере (Rpc.Host/Broadcast)

---

## ➡️ Следующий шаг

Переходи к **[16.03 — FuncMover](16_03_FuncMover.md)**.
