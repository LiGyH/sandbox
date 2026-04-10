# 🎱 Инструмент Hoverball (HoverballTool)

## Что мы делаем?
Создаём режим тулгана для размещения левитирующих шаров (Hoverball) на объектах.

## Зачем это нужно?
Hoverball удерживает объект на заданной высоте, компенсируя гравитацию. Это позволяет создавать парящие конструкции и летательные аппараты.

## Как это работает внутри движка?
- Наследуется от `ToolMode`.
- Загружает определение из `.hdef`-ресурса (`HoverballDefinition`).
- ЛКМ размещает ховербол и приваривает его к объекту через `FixedJoint`.
- Если объект — мир (`point.IsWorld`), приварка не выполняется.
- Клонирует префаб с `startEnabled: false`, затем настраивает и включает через `NetworkSpawn`.

## Создай файл
`Code/Weapons/ToolGun/Modes/Hoverball/HoverballTool.cs`

```csharp
﻿using Sandbox.UI;

[Hide]
[Title( "Hoverball" )]
[Icon( "🎱" )]
[ClassName( "hoverballtool" )]
[Group( "Building" )]
public class HoverballTool : ToolMode
{
	public override IEnumerable<string> TraceIgnoreTags => ["constraint", "collision"];

	[Property, ResourceSelect( Extension = "hdef", AllowPackages = true ), Title( "Hoverball" )]
	public string Definition { get; set; } = "entities/hoverball/basic.hdef";

	public override string Description => "#tool.hint.hoverballtool.description";
	public override string PrimaryAction => "#tool.hint.hoverballtool.place";

	public override bool UseSnapGrid => true;

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var pos = select.WorldTransform();
		var placementTrans = new Transform( pos.Position );

		var hoverballDef = ResourceLibrary.Get<HoverballDefinition>( Definition );
		if ( hoverballDef == null ) return;

		if ( Input.Pressed( "attack1" ) )
		{
			Spawn( select, hoverballDef.Prefab, placementTrans );
			ShootEffects( select );
		}

		DebugOverlay.GameObject( hoverballDef.Prefab.GetScene(), transform: placementTrans, castShadows: true, color: Color.White.WithAlpha( 0.9f ) );
	}

	[Rpc.Host]
	public void Spawn( SelectionPoint point, PrefabFile hoverballPrefab, Transform tx )
	{
		if ( hoverballPrefab == null )
			return;

		var go = hoverballPrefab.GetScene().Clone( global::Transform.Zero, startEnabled: false );
		go.Tags.Add( "removable" );
		go.Tags.Add( "constraint" );
		go.WorldTransform = tx;

		if ( !point.IsWorld )
		{
			var hoverball = go.GetComponent<HoverballEntity>( true );

			var joint = hoverball.AddComponent<FixedJoint>();
			joint.Attachment = Joint.AttachmentMode.LocalFrames;
			joint.LocalFrame2 = point.GameObject.WorldTransform.WithScale( 1 ).ToLocal( tx );
			joint.LocalFrame1 = new Transform();
			joint.AngularFrequency = 0;
			joint.LinearFrequency = 0;
			joint.Body = point.GameObject;
			joint.EnableCollision = false;
		}

		ApplyPhysicsProperties( go );

		go.NetworkSpawn( true, null );

		var undo = Player.Undo.Create();
		undo.Name = "Hoverball";
		undo.Icon = "🎱";
		undo.Add( go );

		CheckContraptionStats( point.GameObject );
	}
}
```

## Проверка
- Тулган в режиме Hoverball показывает призрак шара.
- ЛКМ размещает ховербол и приваривает к объекту.
- На мировой поверхности ховербол размещается без приварки.
