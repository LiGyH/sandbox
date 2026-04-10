# 🔗 Пара шаровых шарниров (BallSocketPair)

## Что мы делаем?
Создаём компонент, который выравнивает два шаровых шарнира друг к другу и обновляет вал между ними.

## Зачем это нужно?
`BallSocketPair` обеспечивает правильную визуальную ориентацию моделей шаровых шарниров гидравлики. Шары всегда смотрят друг на друга, а вал (LineRenderer) соединяет их концы.

## Как это работает внутри движка?
- В `OnUpdate()` вычисляет направление между двумя шарами.
- Поворачивает модели `BallModelA` и `BallModelB` так, чтобы они смотрели друг на друга.
- Обновляет точки `ShaftRenderer` (LineRenderer), используя кости `"end"` скелетных моделей.
- Корректно обрабатывает вырожденный случай, когда направление почти вертикально.

## Создай файл
`Code/Weapons/ToolGun/Modes/Hydraulic/BallSocketPair.cs`

```csharp

/// <summary>
/// A pair of ball sockets, we try to align the balls towards eachother.
/// </summary>
public class BallSocketPair : Component
{
	[Property]
	public SkinnedModelRenderer BallModelA { get; set; }

	[Property]
	public SkinnedModelRenderer BallModelB { get; set; }

	[Property]
	public LineRenderer ShaftRenderer { get; set; }

	protected override void OnUpdate()
	{
		if ( !BallModelA.IsValid() || !BallModelB.IsValid() ) return;

		var ballDir = (BallModelB.GameObject.WorldPosition - BallModelA.GameObject.WorldPosition).Normal;
		var ballUp = MathF.Abs( Vector3.Dot( ballDir, Vector3.Up ) ) > 0.99f ? Vector3.Forward : Vector3.Up;

		BallModelA.GameObject.WorldRotation = Rotation.LookAt( -ballDir, ballUp ) * Rotation.FromPitch( -90f );
		BallModelB.GameObject.WorldRotation = Rotation.LookAt( ballDir, ballUp ) * Rotation.FromPitch( -90f );

		if ( ShaftRenderer.IsValid() )
		{
			var endA = BallModelA.GetBoneObject( "end" );
			var endB = BallModelB.GetBoneObject( "end" );

			if ( endA.IsValid() && endB.IsValid() )
			{
				ShaftRenderer.Points = [endA, endB];
			}
		}
	}
}
```

## Проверка
- Шаровые шарниры всегда направлены друг к другу.
- Вал корректно соединяет концы шарниров.
- При движении объектов визуал обновляется в реальном времени.
