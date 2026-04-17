# ⚙️ Инструмент Hydraulic (HydraulicTool)

## Что мы делаем?
Создаём режим тулгана для создания гидравлических соединений между двумя объектами.

## Зачем это нужно?
Гидравлика позволяет создавать выдвижные механизмы — двери, подъёмники, механические руки. Соединение может менять длину по команде игрока.

## Как это работает внутри движка?
- Наследуется от `BaseConstraintToolMode` — двухэтапный инструмент (выбор двух точек).
- Поддерживает два режима: обычные слайдеры и шаровые шарниры (`BallJoints`).
- Создаёт `SliderJoint` между двумя точками с визуальными моделями торцевых заглушек.
- `LineRenderer` рисует вал между точками крепления.
- `HydraulicEntity` управляет длиной `SliderJoint`.
- `CapsuleCollider` создаёт коллизию вдоль вала.
- Режим шаровых шарниров добавляет `BallJoint` + `BallSocketPair` для свободного вращения.

## Создай файл
`Code/Weapons/ToolGun/Modes/Hydraulic/HydraulicTool.cs`

```csharp
﻿[Hide]
[Title( "Hydraulic" )]
[Icon( "⚙️" )]
[ClassName( "HydraulicTool" )]
[Group( "Building" )]
public class HydraulicTool : BaseConstraintToolMode
{
	public override string Description => Stage == 1 ? "#tool.hint.hydraulictool.stage1" : "#tool.hint.hydraulictool.stage0";
	public override string PrimaryAction => Stage == 1 ? "#tool.hint.hydraulictool.finish" : "#tool.hint.hydraulictool.source";
	public override string ReloadAction => "#tool.hint.hydraulictool.remove";

	[Property, Sync]
	public bool BallJoints { get; set; } = false;

	protected override IEnumerable<GameObject> FindConstraints( GameObject linked, GameObject target )
	{
		foreach ( var cleanup in linked.GetComponentsInChildren<ConstraintCleanup>( true ) )
		{
			if ( linked != target && cleanup.Attachment?.Root != target ) continue;
			if ( cleanup.GameObject.GetComponentInChildren<HydraulicEntity>() is not null )
				yield return cleanup.GameObject;
		}
	}

	protected override void CreateConstraint( SelectionPoint point1, SelectionPoint point2 )
	{
		DebugOverlay.Line( point1.WorldPosition(), point2.WorldPosition(), Color.Red, 5.0f );

		if ( point1.GameObject == point2.GameObject )
			return;

		if ( BallJoints )
		{
			CreateBallJointHydraulic( point1, point2 );
			return;
		}

		var line = point1.WorldPosition() - point2.WorldPosition();

		var go1 = new GameObject( false, "hydraulic_a" );
		go1.Parent = point1.GameObject;
		go1.LocalTransform = point1.LocalTransform;
		go1.WorldRotation = Rotation.LookAt( -line );
		go1.Tags.Add( "constraint" );

		var go2 = new GameObject( false, "hydraulic_b" );
		go2.Parent = point2.GameObject;
		go2.LocalTransform = point2.LocalTransform;
		go2.WorldRotation = Rotation.LookAt( -line );
		go2.Tags.Add( "constraint" );

		var cleanup = go1.AddComponent<ConstraintCleanup>();
		cleanup.Attachment = go2;

		var len = (point1.WorldPosition() - point2.WorldPosition()).Length;

		// End caps
		var capA = new GameObject( go1, true, "hydraulic_cap_a" );
		capA.LocalPosition = Vector3.Zero;
		capA.WorldRotation = Rotation.LookAt( line ) * Rotation.FromPitch( -90f );
		capA.AddComponent<ModelRenderer>().Model = Model.Load( "hydraulics/tool_hydraulic.vmdl" );

		var capB = new GameObject( go2, true, "hydraulic_cap_b" );
		capB.LocalPosition = Vector3.Zero;
		capB.WorldRotation = Rotation.LookAt( -line ) * Rotation.FromPitch( -90f );
		capB.AddComponent<ModelRenderer>().Model = Model.Load( "hydraulics/tool_hydraulic.vmdl" );

		// Shaft, using line renderer
		var lineRenderer = go1.AddComponent<LineRenderer>();
		lineRenderer.Points = [go1, go2];
		lineRenderer.Face = SceneLineObject.FaceMode.Cylinder;
		lineRenderer.Texturing = lineRenderer.Texturing with { Material = Material.Load( "hydraulics/metal_tile_line.vmat" ), WorldSpace = true, UnitsPerTexture = 32 };
		lineRenderer.Lighting = true;
		lineRenderer.CastShadows = true;
		lineRenderer.Width = 2f;
		lineRenderer.Color = Color.White;

		SliderJoint joint = default;

		var jointGo = new GameObject( go1, true, "hydraulic" );

		// Joint
		{
			joint = jointGo.AddComponent<SliderJoint>();
			joint.Attachment = Joint.AttachmentMode.Auto;
			joint.Body = go2;
			joint.MinLength = len;
			joint.MaxLength = len;
			joint.EnableCollision = true;
		}

		//
		// If it's ourself - we want to create the rope, but no joint between
		//
		var entity = jointGo.AddComponent<HydraulicEntity>();
		entity.Length = 0.5f;
		entity.MinLength = 5.0f;
		entity.MaxLength = len * 2.0f;
		entity.Joint = joint;

		var capsule = jointGo.AddComponent<CapsuleCollider>();

		go2.NetworkSpawn( true, null );
		go1.NetworkSpawn( true, null );
		jointGo.NetworkSpawn( true, null );

		var undo = Player.Undo.Create();
		undo.Name = "Hydraulic";
		undo.Add( go1 );
		undo.Add( go2 );
		undo.Add( jointGo );
	}

	private void CreateBallJointHydraulic( SelectionPoint point1, SelectionPoint point2 )
	{
		var p1 = point1.WorldPosition();
		var p2 = point2.WorldPosition();
		var len = p1.Distance( p2 );

		var dir = (p2 - p1).Normal;
		var up = MathF.Abs( Vector3.Dot( dir, Vector3.Up ) ) > 0.99f ? Vector3.Forward : Vector3.Up;
		var axis = Rotation.LookAt( dir );

		var surfaceRotA = point1.WorldTransform().Rotation;
		var surfaceRotB = point2.WorldTransform().Rotation;

		// Visual anchors — identity rotation for LineRenderer
		var goA = new GameObject( false, "bs_hydraulic_a" );
		goA.Parent = point1.GameObject;
		goA.LocalTransform = point1.LocalTransform;
		goA.LocalRotation = Rotation.Identity;
		goA.Tags.Add( "constraint" );

		var goB = new GameObject( false, "bs_hydraulic_b" );
		goB.Parent = point2.GameObject;
		goB.LocalTransform = point2.LocalTransform;
		goB.LocalRotation = Rotation.Identity;
		goB.Tags.Add( "constraint" );

		var cleanup = goA.AddComponent<ConstraintCleanup>();
		cleanup.Attachment = goB;

		// Base mount visuals — surface normal aligned
		var baseVisA = new GameObject( goA, true, "hydraulic_base_a" );
		baseVisA.LocalPosition = Vector3.Zero;
		baseVisA.WorldRotation = surfaceRotA * Rotation.FromPitch( 90f );
		baseVisA.AddComponent<ModelRenderer>().Model = Model.Load( "hydraulics/tool_suspension_base.vmdl" );

		var baseVisB = new GameObject( goB, true, "hydraulic_base_b" );
		baseVisB.LocalPosition = Vector3.Zero;
		baseVisB.WorldRotation = surfaceRotB * Rotation.FromPitch( 90f );
		baseVisB.AddComponent<ModelRenderer>().Model = Model.Load( "hydraulics/tool_suspension_base.vmdl" );

		// Ball joint visuals
		var ballVisA = new GameObject( goA, true, "hydraulic_ball_a" );
		ballVisA.WorldPosition = goA.WorldPosition + surfaceRotA.Forward * 4.644f;
		ballVisA.WorldRotation = Rotation.LookAt( -dir, up ) * Rotation.FromPitch( -90f );
		var skinA = ballVisA.AddComponent<SkinnedModelRenderer>();
		skinA.Model = Model.Load( "hydraulics/tool_balljoint.vmdl" );
		skinA.CreateBoneObjects = true;

		var ballVisB = new GameObject( goB, true, "hydraulic_ball_b" );
		ballVisB.WorldPosition = goB.WorldPosition + surfaceRotB.Forward * 4.644f;
		ballVisB.WorldRotation = Rotation.LookAt( dir, up ) * Rotation.FromPitch( -90f );
		var skinB = ballVisB.AddComponent<SkinnedModelRenderer>();
		skinB.Model = Model.Load( "hydraulics/tool_balljoint.vmdl" );
		skinB.CreateBoneObjects = true;

		// Shaft
		var lineRenderer = goA.AddComponent<LineRenderer>();
		lineRenderer.Points = [ballVisA, ballVisB];
		lineRenderer.Face = SceneLineObject.FaceMode.Cylinder;
		lineRenderer.Texturing = lineRenderer.Texturing with { Material = Material.Load( "hydraulics/metal_tile_line.vmat" ), WorldSpace = true, UnitsPerTexture = 32 };
		lineRenderer.Lighting = true;
		lineRenderer.CastShadows = true;
		lineRenderer.Width = 1.5f;
		lineRenderer.Color = Color.White;

		var aligner = goA.AddComponent<BallSocketPair>();
		aligner.BallModelA = skinA;
		aligner.BallModelB = skinB;
		aligner.ShaftRenderer = lineRenderer;

		// Ball socket constraint
		var ballTarget = new GameObject( point2.GameObject, false, "bs_target" );
		ballTarget.LocalTransform = point2.LocalTransform;

		var ballAnchor = new GameObject( point1.GameObject, false, "bs_anchor" );
		ballAnchor.WorldTransform = ballTarget.WorldTransform;

		var ballJoint = ballAnchor.AddComponent<BallJoint>();
		ballJoint.Body = ballTarget;
		ballJoint.Friction = 0.0f;
		ballJoint.EnableCollision = false;

		// Slider joint
		var sliderA = new GameObject( false, "hydraulic_slider_a" );
		sliderA.Parent = point1.GameObject;
		sliderA.LocalTransform = point1.LocalTransform;
		sliderA.WorldRotation = axis;

		var sliderB = new GameObject( false, "hydraulic_slider_b" );
		sliderB.Parent = point2.GameObject;
		sliderB.LocalTransform = point2.LocalTransform;
		sliderB.WorldRotation = axis;

		var slider = sliderA.AddComponent<SliderJoint>();
		slider.Body = sliderB;
		slider.MinLength = len;
		slider.MaxLength = len;
		slider.EnableCollision = true;

		var entity = sliderA.AddComponent<HydraulicEntity>();
		entity.Length = 0.5f;
		entity.MinLength = 5.0f;
		entity.MaxLength = len * 2.0f;
		entity.Joint = slider;

		sliderA.AddComponent<CapsuleCollider>();

		// TODO: my lord
		goB.NetworkSpawn( true, null );
		goA.NetworkSpawn( true, null );
		ballTarget.NetworkSpawn( true, null );
		ballAnchor.NetworkSpawn( true, null );
		sliderB.NetworkSpawn( true, null );
		sliderA.NetworkSpawn( true, null );

		var undo = Player.Undo.Create();
		undo.Name = "Hydraulic (Ball Joints)";
		undo.Add( goA, goB, ballAnchor, ballTarget, sliderA, sliderB );
	}
}

```

## Проверка
- Инструмент работает в два этапа: выбор первой и второй точки.
- Между точками создаётся гидравлическое соединение с визуалом.
- Режим шаровых шарниров создаёт свободно вращающееся соединение.
- Соединение можно удалить клавишей перезарядки.


---

# ⚙️ Сущность гидравлики (HydraulicEntity)

## Что мы делаем?
Создаём компонент, управляющий длиной гидравлического соединения.

## Зачем это нужно?
`HydraulicEntity` принимает ввод игрока и изменяет длину `SliderJoint`, позволяя выдвигать и втягивать гидравлику. Поддерживает ручное управление, переключение и анимацию.

## Как это работает внутри движка?
- Реализует `IPlayerControllable`.
- `Length` (0–1) определяет текущую позицию между `MinLength` и `MaxLength`.
- Входы: `Push` (выдвинуть), `Pull` (втянуть), `Toggle` (переключить), `Activate` (удерживать).
- Режим `Animated` автоматически качает гидравлику с настраиваемыми функциями плавности.
- В `OnUpdate()` обновляет `SliderJoint.MinLength`/`MaxLength`, коллайдер и скелетную модель.
- `CapsuleCollider` динамически перестраивается между точками крепления.

## Создай файл
`Code/Weapons/ToolGun/Modes/Hydraulic/HydraulicEntity.cs`

```csharp
﻿
using Sandbox.Utility;

public class HydraulicEntity : Component, IPlayerControllable
{
	[Property, Range( 0, 1 )]
	public GameObject OnEffect { get; set; }

	[Property, Range( 0, 100 ), ClientEditable]
	public float MinLength { get; set; } = 10f;

	[Property, Range( 0, 100 ), ClientEditable]
	public float MaxLength { get; set; } = 100f;

	[Property, Range( 0, 1 ), ClientEditable]
	public float Length { get; set; } = 0.5f;

	[Property, Range( 0, 1 ), ClientEditable]
	public float Speed { get; set; } = 0.25f;

	[Property, Sync, ClientEditable]
	public ClientInput Push { get; set; }

	[Property, Range( 0, 1 ), ClientEditable]
	public float PushSpeed { get; set; } = 0.25f;


	[Property, Sync, ClientEditable]
	public ClientInput Pull { get; set; }

	[Property, Range( 0, 1 ), ClientEditable]
	public float PullSpeed { get; set; } = 0.25f;

	[Property, Sync, ClientEditable]
	public ClientInput Toggle { get; set; }

	/// <summary>
	/// While the client input is active we'll apply thrust
	/// </summary>
	[Property, Sync, ClientEditable]
	public ClientInput Activate { get; set; }

	[Property]
	public SliderJoint Joint { get; set; }

	[Property, ClientEditable, ToggleGroup( "Animated" )]
	public bool Animated { get; set; }

	[Property, ClientEditable, ToggleGroup( "Animated" ), Range( 0, 10 )]
	public float AnimationSpeed { get; set; } = 1.0f;

	[Property, ClientEditable, ToggleGroup( "Animated" )]
	public EaseType EaseIn { get; set; } = EaseType.Linear;

	[Property, ClientEditable, ToggleGroup( "Animated" )]
	public EaseType EaseOut { get; set; } = EaseType.Linear;


	public enum EaseType
	{
		Linear,
		EaseIn,
		EaseOut,
		EaseInOut,
		Bounce
	}

	protected override void OnEnabled()
	{
		base.OnEnabled();

		OnEffect?.Enabled = false;
	}

	bool _state;

	public void SetActiveState( bool state )
	{
		if ( _state == state ) return;

		_state = state;

		OnEffect?.Enabled = state;

		Network.Refresh();

	}

	public void OnStartControl()
	{
	}

	public void OnEndControl()
	{
	}

	float? _lastTargetValue;
	float? _targetValue;

	public void OnControl()
	{
		if ( Activate.Down() )
		{
			Length += Speed * Time.Delta;

		}
		else if ( Activate.Released() )
		{
			Length = 0;
		}

		if ( Push.Down() )
		{
			Length += PushSpeed * Time.Delta * 5.0f;
		}

		if ( Pull.Down() )
		{
			Length -= PullSpeed * Time.Delta * 5.0f;
		}

		if ( Toggle.Pressed() )
		{
			_targetValue = _lastTargetValue.HasValue ? (_lastTargetValue > 0.5f ? 0.0f : 1.0f) : 1;
			_lastTargetValue = _targetValue;

			Log.Info( _targetValue );
		}

		if ( _targetValue.HasValue )
		{
			if ( _targetValue > Length )
			{
				Length += PushSpeed * Time.Delta * 5.0f;

				if ( Length > 1 )
				{
					_targetValue = null;
				}
			}
			else
			{
				Length -= PullSpeed * Time.Delta * 5.0f;

				if ( Length < 0 )
				{
					_targetValue = null;
				}
			}
		}

		Length = Length.Clamp( 0, 1 );

		var analog = Activate.GetAnalog();
	}

	float _animTime = 0;

	protected override void OnUpdate()
	{
		base.OnUpdate();

		if ( !Joint.IsValid() ) return;

		var line = Joint.Body.WorldPosition - Joint.GameObject.WorldPosition;
		var line_rot = Rotation.LookAt( line, WorldRotation.Up );

		DebugOverlay.Line( Joint.GameObject.WorldPosition, Joint.Body.WorldPosition, Color.Green );

		if ( Animated )
		{
			_animTime += Time.Delta * AnimationSpeed * 0.33f;
			_animTime = _animTime % 2;

			var delta = _animTime;
			if ( delta > 1 )
			{
				delta = 2 - delta;
				delta = GetEase( 1 - delta, EaseOut );
				delta = 1 - delta;
			}
			else
			{
				delta = GetEase( delta, EaseIn );
			}

			Length = (delta);
		}


		Joint.MinLength = MinLength + (Length * (MaxLength - MinLength));
		Joint.MaxLength = MinLength + (Length * (MaxLength - MinLength));

		if ( GetComponent<CapsuleCollider>() is CapsuleCollider capsule )
		{
			capsule.Static = false;
			capsule.Start = capsule.WorldTransform.PointToLocal( Joint.GameObject.WorldPosition );
			capsule.End = capsule.WorldTransform.PointToLocal( Joint.Body.WorldPosition );
			capsule.Radius = 1.0f;
			capsule.ColliderFlags = ColliderFlags.IgnoreMass;
			capsule.Tags.Set( "trigger", true );
		}

		if ( GetComponent<SkinnedModelRenderer>() is SkinnedModelRenderer renderer )
		{
			renderer.CreateBoneObjects = true;

			var len = line.Length - MinLength;

			var a = Joint.GameObject.WorldPosition;
			var b = a + line.Normal * MinLength * 0.5f;
			var c = b + line.Normal * len;
			var d = c + line.Normal * MinLength * 0.5f;

			renderer.GetBoneObject( 0 )?.WorldTransform = new Transform( a, line_rot );
			renderer.GetBoneObject( 0 )?.Flags |= GameObjectFlags.ProceduralBone;
			renderer.GetBoneObject( 1 )?.WorldTransform = new Transform( b, line_rot ); ;
			renderer.GetBoneObject( 1 )?.Flags |= GameObjectFlags.ProceduralBone;
			renderer.GetBoneObject( 2 )?.WorldTransform = new Transform( c, line_rot ); ;
			renderer.GetBoneObject( 2 )?.Flags |= GameObjectFlags.ProceduralBone;
			renderer.GetBoneObject( 3 )?.WorldTransform = new Transform( d, line_rot ); ;
			renderer.GetBoneObject( 3 )?.Flags |= GameObjectFlags.ProceduralBone;
		}

	}

	private float GetEase( float delta, EaseType easeIn )
	{
		switch ( easeIn )
		{
			case EaseType.Linear: return delta;
			case EaseType.EaseIn: return Easing.EaseIn( delta );
			case EaseType.EaseOut: return Easing.EaseOut( delta );
			case EaseType.EaseInOut: return Easing.EaseInOut( delta );
			case EaseType.Bounce: return Easing.BounceOut( delta );
		}

		return delta;
	}
}
```

## Проверка
- Гидравлика выдвигается и втягивается по командам игрока.
- Переключатель Toggle меняет состояние между выдвинутым и втянутым.
- Режим анимации автоматически качает гидравлику.
- Коллайдер и визуал обновляются динамически.


---

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


---

## ➡️ Следующий шаг

Переходи к **[11.06 — Этап 11_06 — Linker (Линкер — связывание объектов)](11_06_Linker.md)**.
