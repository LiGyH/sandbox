# 🌀 Пропеллер (Propeller)

## Что мы делаем?
Создаём компонент, который вращает модель пропеллера пропорционально силе тяги двигателя.

## Зачем это нужно?
`Propeller` добавляет визуальную обратную связь — когда двигатель работает, пропеллер вращается. Скорость вращения зависит от текущей силы тяги `ThrusterEntity`.

## Как это работает внутри движка?
- Ищет компонент `ThrusterEntity` на том же объекте при старте.
- В `OnUpdate()` читает `ThrustAmount` из двигателя.
- Вычисляет угол поворота за кадр: `amount * SpinSpeed * 360° * Time.Delta`.
- Применяет вращение вокруг локальной оси `Vector3.Up` к целевому `ModelRenderer`.

## Создай файл
`Code/Weapons/ToolGun/Modes/Thruster/Propeller.cs`

```csharp
/// <summary>
/// Attach to a thruster GameObject to spin a target model based on thrust output.
/// The model spins around its local up axis proportional to ThrustAmount.
/// </summary>
public class Propeller : Component
{
	/// <summary>
	/// The model renderer to spin.
	/// </summary>
	[Property]
	public ModelRenderer Target { get; set; }

	/// <summary>
	/// Spin speed in full rotations per second at maximum thrust.
	/// </summary>
	[Property, Range( 0, 10 )]
	public float SpinSpeed { get; set; } = 3f;

	ThrusterEntity _thruster;

	protected override void OnStart()
	{
		_thruster = GetComponent<ThrusterEntity>( true );
	}

	protected override void OnUpdate()
	{
		if ( !Target.IsValid() || !_thruster.IsValid() ) return;

		var amount = _thruster.ThrustAmount;
		if ( amount.AlmostEqual( 0f ) ) return;

		var degreesThisFrame = amount * SpinSpeed * 360f * Time.Delta;
		Target.GameObject.LocalRotation *= Rotation.FromAxis( Vector3.Up, degreesThisFrame );
	}
}
```

## Проверка
- Пропеллер вращается когда двигатель активен.
- Скорость вращения пропорциональна силе тяги.
- При нулевой тяге пропеллер не вращается.
