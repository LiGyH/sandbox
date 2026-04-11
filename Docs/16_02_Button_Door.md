# 12.04 — Кнопка (Button) 🔘

## Что мы делаем?

Создаём **Button** — интерактивную кнопку с тремя режимами работы: Toggle, Continuous и Immediate. Поддерживает анимацию движения, звуки и тултипы.

## Режимы работы

| Режим | Описание |
|-------|----------|
| `Toggle` | Нажал — включилась, нажал снова — выключилась. Может автосбрасываться |
| `Continuous` | Работает только пока удерживаешь (как педаль газа) |
| `Immediate` | Мгновенно включается и сразу выключается (импульс) |

## Создай файл

Путь: `Code/Map/Button.cs`

```csharp
﻿[Alias( "Button" ), EditorHandle( Icon = "touch_app" )]
public sealed class Button : Component, Component.IPressable
{
	/// <summary>
	/// The button's behavior mode.
	/// </summary>
	public enum ButtonMode
	{
		/// <summary>
		/// Normal toggle button - click to turn on, click again to turn off.
		/// </summary>
		Toggle,
		/// <summary>
		/// Button is only on while being held down.
		/// </summary>
		Continuous,
		/// <summary>
		/// Button briefly turns on then immediately off when pressed.
		/// </summary>
		Immediate
	}

	[Property] public ButtonMode Mode { get; set; } = ButtonMode.Toggle;

	[Property, ShowIf( "Mode", ButtonMode.Toggle )] public bool AutoReset { get; set; } = true;
	[Property, ShowIf( "AutoReset", true )] public float ResetTime { get; set; } = 1.0f;

	[Property, FeatureEnabled( "Movement", Icon = "edgesensor_high" )] public bool Move { get; set; }
	[Property, Feature( "Movement" ), ShowIf( nameof( Move ), true )] public GameObject MoveTarget { get; set; }
	[Property, Feature( "Movement" ), ShowIf( nameof( Move ), true )] public Vector3 MoveDelta { get; set; }

	/// <summary>
	/// Animation curve to use, X is the time between 0-1 and Y is how much the button is pressed from 0-1.
	/// </summary>
	[Property, Feature( "Movement" ), ShowIf( nameof( Move ), true )] public Curve AnimationCurve { get; set; } = new Curve( new Curve.Frame( 0f, 0f ), new Curve.Frame( 1f, 1.0f ) );

	/// <summary>
	/// How long in seconds should it take to animate this button.
	/// </summary>
	[Property, Group( "Movement" ), ShowIf( nameof( Move ), true )] public float AnimationTime { get; set; } = 0.5f;

	/// <summary>
	/// Sound to play when the button is pressed.
	/// </summary>
	[Property, Feature( "Sound", Icon = "volume_up" )] public SoundEvent OnSound { get; set; }

	/// <summary>
	/// Sound to play when the button is released.
	/// </summary>
	[Property, Feature( "Sound" )] public SoundEvent OffSound { get; set; }

	/// <summary>
	/// Called when the button's state changes.
	/// </summary>
	[Property, Feature( "Events", Icon = "double_arrow" )]
	public Action<bool> OnStateChanged { get; set; }

	Vector3 _initialPosition;
	Transform _startTransform;
	bool _isBeingPressed;
	bool _shouldTurnOffNextFrame;

	[Sync] private TimeSince LastUse { get; set; }
	[Sync] private bool _isOn { get; set; }

	[Doo.ArgumentHint<GameObject>( "user" )]
	[Property] public Doo OnPressed { get; set; }

	/// <summary>
	/// True if the button is currently on
	/// </summary>
	public bool IsOn
	{
		get => _isOn;
		private set
		{
			if ( _isOn == value )
				return;

			_isOn = value;
			OnButtonStateChanged( value );
		}
	}

	/// <summary>
	/// True if the button is currently animating
	/// </summary>
	public bool IsAnimating { get; private set; }

	void OnButtonStateChanged( bool isOn )
	{
		OnStateChanged?.Invoke( isOn );
	}

	protected override void DrawGizmos()
	{
		if ( !Gizmo.IsSelected )
			return;

		if ( !Move )
			return;

		Gizmo.Transform = WorldTransform;

		var bbox = GameObject.GetLocalBounds();
		bbox += MoveDelta * MathF.Sin( RealTime.Now * 2.0f ).Remap( -1, 1 );

		Gizmo.Draw.Color = Color.Yellow;
		Gizmo.Draw.LineThickness = 3;
		Gizmo.Draw.LineBBox( bbox );
		Gizmo.Draw.IgnoreDepth = true;

		Gizmo.Draw.LineThickness = 1;
		Gizmo.Draw.Color = Gizmo.Draw.Color.WithAlpha( 0.3f );
		Gizmo.Draw.LineBBox( bbox );
	}

	protected override void OnStart()
	{
		var moveTarget = MoveTarget.IsValid() ? MoveTarget : GameObject;
		_startTransform = moveTarget.LocalTransform;
		_initialPosition = _startTransform.Position;
	}

	bool IPressable.CanPress( IPressable.Event e )
	{
		return !IsAnimating;
	}

	bool IPressable.Pressing( IPressable.Event e )
	{
		_isBeingPressed = true;
		return Mode == ButtonMode.Continuous;
	}

	bool IPressable.Press( IPressable.Event e )
	{
		Press( e.Source.GameObject );
		return true;
	}

	void IPressable.Release( IPressable.Event e )
	{
		_isBeingPressed = false;
		Release( e.Source.GameObject );
	}

	/// <summary>
	/// Turns the button on. Does nothing if already on or animating.
	/// </summary>
	[Rpc.Host]
	[ActionGraphNode( "sandbox.button.on" ), Pure]
	public void TurnOn( GameObject presser = null )
	{
		// Don't do anything if already on or animating
		if ( IsOn || IsAnimating )
			return;

		LastUse = 0;
		IsAnimating = Move;
		IsOn = true;

		if ( OnSound is not null )
			PlaySound( OnSound );

		if ( Mode == ButtonMode.Immediate )
		{
			_shouldTurnOffNextFrame = true;
		}
	}

	/// <summary>
	/// Turns the button off. Does nothing if already off or animating.
	/// </summary>
	[Rpc.Host]
	[ActionGraphNode( "sandbox.button.off" ), Pure]
	public void TurnOff()
	{
		// Don't do anything if already off or animating
		if ( !IsOn || IsAnimating )
			return;

		LastUse = 0;
		IsAnimating = Move;
		IsOn = false;

		if ( OffSound is not null )
			PlaySound( OffSound );
	}

	/// <summary>
	/// Toggles the button between on and off states.
	/// </summary>
	[Rpc.Host]
	[ActionGraphNode( "sandbox.button.toggle" ), Pure]
	public void Toggle( GameObject presser )
	{
		if ( !IsOn )
		{
			TurnOn( presser );
		}
		else
		{
			TurnOff();
		}
	}

	[Rpc.Host]
	private void Press( GameObject presser )
	{
		if ( IsAnimating )
			return;

		Run( OnPressed, c => c.SetArgument( "user", presser ) );

		switch ( Mode )
		{
			case ButtonMode.Toggle:
				if ( IsOn && !AutoReset )
				{
					TurnOff();
				}
				else
				{
					TurnOn( presser );
				}
				break;

			case ButtonMode.Continuous:
				TurnOn( presser );
				break;

			case ButtonMode.Immediate:
				TurnOn( presser );
				break;
		}
	}

	[Rpc.Host]
	private void Release( GameObject presser )
	{
		// For continuous mode, turn off when released
		if ( Mode == ButtonMode.Continuous && IsOn )
		{
			TurnOff();
		}
	}

	[Rpc.Broadcast]
	private void PlaySound( SoundEvent sound )
	{
		GameObject.PlaySound( sound );
	}

	protected override void OnFixedUpdate()
	{
		//
		// Okay, this part could be way better 
		//
		if ( Mode == ButtonMode.Immediate && _shouldTurnOffNextFrame && IsOn && !IsAnimating )
		{
			_shouldTurnOffNextFrame = false;
			TurnOff();
			return;
		}

		if ( Mode == ButtonMode.Toggle && !IsAnimating && IsOn && AutoReset && ResetTime >= 0.0f && LastUse >= ResetTime )
		{
			TurnOff();
			return;
		}

		if ( Mode == ButtonMode.Continuous && IsOn && !IsAnimating && !_isBeingPressed )
		{
			TurnOff();
			return;
		}

		// Continuous: Reset the pressing flag each frame - it will be set again if still being pressed
		if ( Mode == ButtonMode.Continuous )
		{
			_isBeingPressed = false;
		}

		// Don't do anything if we're not animating
		if ( !IsAnimating )
			return;

		// Normalize the last use time to the amount of time to animate
		var time = LastUse.Relative.Remap( 0.0f, AnimationTime, 0.0f, 1.0f );

		// Evaluate our animation curve
		var curve = AnimationCurve.Evaluate( time );

		// Animate backwards if we're turning off (IsOn is false when turning off)
		if ( !IsOn ) curve = 1.0f - curve;

		if ( Move )
		{
			var target = MoveTarget.IsValid() ? MoveTarget : GameObject;
			var targetPosition = _initialPosition + target.LocalTransform.Rotation * (MoveDelta * curve);
			target.LocalTransform = _startTransform.WithPosition( targetPosition );
		}

		// If we're done, finalize the animation
		if ( time < 1f ) return;

		IsAnimating = false;
	}

	[Property, Feature( "Tooltip" )]
	public string TooltipTitle { get; set; } = "Press";

	[Property, Feature( "Tooltip" )]
	public string TooltipIcon { get; set; } = "touch_app";

	[Property, Feature( "Tooltip" )]
	public string TooltipDescription { get; set; } = "";

	[Header( "Off State" )]
	[ShowIf( "Mode", ButtonMode.Toggle )]
	[Property, Feature( "Tooltip" )]
	public string TooltipTitleOff { get; set; } = "Press";

	[ShowIf( "Mode", ButtonMode.Toggle )]
	[Property, Feature( "Tooltip" )]
	public string TooltipIconOff { get; set; } = "touch_app";

	[ShowIf( "Mode", ButtonMode.Toggle )]
	[Property, Feature( "Tooltip" )]
	public string TooltipDescriptionOff { get; set; } = "";

	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		if ( string.IsNullOrWhiteSpace( TooltipTitle ) && string.IsNullOrWhiteSpace( TooltipIcon ) )
			return default;

		if ( Mode == ButtonMode.Toggle && IsOn )
		{
			return new IPressable.Tooltip( TooltipTitleOff, TooltipIconOff, TooltipDescriptionOff );
		}

		return new IPressable.Tooltip( TooltipTitle, TooltipIcon, TooltipDescription );
	}
}
```

## Ключевые атрибуты редактора

| Атрибут | Описание |
|---------|----------|
| `[ShowIf]` | Показывает поле только при определённом значении другого поля |
| `[FeatureEnabled]` | Создаёт «фичу» с галочкой — группа полей скрывается если отключена |
| `[Feature]` | Привязывает поле к фиче |
| `[Doo.ArgumentHint]` | Подсказка для Action Graph — «этот аргумент — GameObject» |

## Результат

- Кнопка с тремя режимами работы для карт
- Поддержка анимации движения, звуков, тултипов
- Работает в мультиплеере через Rpc

---

Следующий шаг: [12.05 — Движущийся объект (FuncMover)](12_05_FuncMover.md)


---

# 12.03 — Дверь (Door) 🚪

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

Следующий шаг: [12.04 — Кнопка (Button)](12_04_Button.md)
