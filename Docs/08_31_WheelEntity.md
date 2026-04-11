# 🛞 Сущность колеса (WheelEntity)

## Что мы делаем?
Создаём компонент управления колесом — мотор, тормоз и рулевое управление.

## Зачем это нужно?
`WheelEntity` превращает физическое колесо в управляемый элемент транспорта. Игрок может ускоряться, тормозить и поворачивать через привязанные клавиши.

## Как это работает внутри движка?
- Реализует `IPlayerControllable` для получения ввода от игрока.
- Управляет `WheelJoint` — мотором вращения (`SpinMotor`) и рулевым управлением (`Steering`).
- `Forward`/`Reverse` задают скорость вращения мотора.
- `Break` включает торможение (мотор на скорости 0 с высоким крутящим моментом).
- `TurnLeft`/`TurnRight` управляют углом поворота до ±45°.
- `Speed`, `Power`, `Reversed` настраивают поведение колеса.

## Создай файл
`Code/Weapons/ToolGun/Modes/Wheel/WheelEntity.cs`

```csharp
﻿public class WheelEntity : Component, IPlayerControllable
{
	[Property, Range( 0, 1 ), ClientEditable]
	public bool Reversed { get; set; } = false;

	[Property, Range( 0, 1 ), ClientEditable]
	public float Speed { get; set; } = 0.5f;

	[Property, Range( 0, 1 ), ClientEditable]
	public float Power { get; set; } = 0.5f;

	[Property, Sync, ClientEditable]
	public ClientInput Forward { get; set; }

	[Property, Sync, ClientEditable]
	public ClientInput Reverse { get; set; }

	[Property, Sync, ClientEditable]
	public ClientInput Break { get; set; }

	[Property, Sync, ClientEditable]
	public ClientInput TurnLeft { get; set; }

	[Property, Sync, ClientEditable]
	public ClientInput TurnRight { get; set; }

	protected override void OnEnabled()
	{
		base.OnEnabled();
	}

	public void OnStartControl()
	{
	}

	public void OnEndControl()
	{
	}

	public void OnControl()
	{
		var joint = GetComponentInChildren<WheelJoint>();
		if ( !joint.IsValid() ) return;

		var forward = Forward.GetAnalog();
		var reverse = Reverse.GetAnalog();
		var speed = (forward - reverse).Clamp( -1, 1 );

		if ( Break.GetAnalog() > 0.1f )
		{
			joint.EnableSpinMotor = true;
			joint.SpinMotorSpeed = 0;
			joint.MaxSpinTorque = 500000 * Power;
		}
		else if ( speed.AlmostEqual( 0.0f ) )
		{
			joint.EnableSpinMotor = false;
		}
		else
		{
			if ( Reversed ) speed = -speed;

			joint.EnableSpinMotor = true;
			joint.SpinMotorSpeed = -2000 * speed * Speed;
			joint.MaxSpinTorque = 200000 * Power;
		}

		var left = TurnLeft.GetAnalog();
		var right = TurnRight.GetAnalog();
		var dir = (right - left).Clamp( -1, 1 );

		if ( !dir.AlmostEqual( 0.0f ) )
		{
			joint.EnableSteering = true;
			joint.SteeringDampingRatio = 1.0f;
			joint.MaxSteeringTorque = 500000;
			joint.SteeringLimits = new Vector2( -45, 45 );
			joint.TargetSteeringAngle = 30 * dir;
		}
		else
		{
			joint.TargetSteeringAngle = 0;
		}

	}
}

```

## Проверка
- Колесо вращается при нажатии клавиш вперёд/назад.
- Тормоз останавливает вращение.
- Рулевое управление поворачивает колесо влево/вправо.
- Свойство `Reversed` инвертирует направление вращения.
