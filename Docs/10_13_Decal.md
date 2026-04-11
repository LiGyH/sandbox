# 🖌️ Decal — Инструмент «Декали»

## Что мы делаем?

Создаём файл `DecalTool.cs` — инструмент для **размещения декалей** (картинок-наклеек) на поверхностях.

## Зачем это нужно?

- Рисунки и надписи на стенах
- Следы пуль, кровь, грязь
- Логотипы и украшения
- Указатели и стрелки

## Как это работает внутри движка?

### Управление

| Кнопка | Действие |
|--------|----------|
| **ЛКМ** | Разместить одну декаль |
| **ПКМ** (зажать) | Непрерывная «покраска» (каждые 50мс) |

### Настройка

```csharp
[Property, ResourceSelect( Extension = "decal", AllowPackages = true ), Title( "Decal" )]
public string Decal { get; set; }
```

В инспекторе выбирается файл `.decal` — определение декали (текстура, размер, и т.д.).

### DecalDefinition

`DecalDefinition` — ресурс движка (`.decal`), описывающий:
- Текстуру декали
- Размер проекции
- Глубину проникновения
- Прозрачность

### Создание декали

```csharp
var go = new GameObject( true, "decal" );
go.Tags.Add( "removable" );
go.WorldPosition = pos.Position + pos.Rotation.Forward * 1f;  // слегка вперёд от поверхности
go.WorldRotation = Rotation.LookAt( -pos.Rotation.Forward );  // смотрит на поверхность
go.SetParent( point.GameObject, true );  // привязана к объекту

var decal = go.AddComponent<Decal>();
decal.Decals = [def];
decal.SortLayer = _layer++;  // каждая следующая поверх предыдущей
```

### SortLayer

```csharp
uint _layer = 0;
decal.SortLayer = _layer++;
```

Каждая новая декаль получает увеличенный `SortLayer`, чтобы она рисовалась **поверх** предыдущих. Без этого декали могли бы «мерцать» (z-fighting).

### Привязка к объекту

```csharp
go.SetParent( point.GameObject, true );
```

Декаль привязывается к объекту. Если объект двигается — декаль двигается вместе с ним.

## Создай файл

📄 `Code/Weapons/ToolGun/Modes/Decal/DecalTool.cs`

```csharp
﻿﻿
using Sandbox.UI;

[Title( "Decal" )]
[Icon( "🖌️" )]
[ClassName( "decaltool" )]
[Group( "Render" )]
public class DecalTool : ToolMode
{
	[Property, ResourceSelect( Extension = "decal", AllowPackages = true ), Title( "Decal" )]
	public string Decal { get; set; }

	public override string Description => "#tool.hint.decaltool.description";
	public override string PrimaryAction => "#tool.hint.decaltool.place";
	public override string SecondaryAction => "#tool.hint.decaltool.paint";

	TimeSince timeSinceShoot = 0;

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		if ( !select.IsValid() ) return;

		var resource = ResourceLibrary.Get<DecalDefinition>( Decal );
		if ( resource == null ) return;

		var def = Decal;
		if ( def == null ) return;

		var pos = select.WorldTransform();

		if ( Input.Pressed( "attack1" ) )
		{
			SpawnDecal( select, resource );
		}

		if ( Input.Down( "attack2" ) && timeSinceShoot > 0.05f )
		{
			timeSinceShoot = 0;
			SpawnDecal( select, resource );
		}
	}

	uint _layer = 0;

	[Rpc.Host]
	public void SpawnDecal( SelectionPoint point, DecalDefinition def )
	{
		if ( def == null ) return;

		var pos = point.WorldTransform();

		var go = new GameObject( true, "decal" );
		go.Tags.Add( "removable" );
		go.WorldPosition = pos.Position + pos.Rotation.Forward * 1f;
		go.WorldRotation = Rotation.LookAt( -pos.Rotation.Forward );
		go.SetParent( point.GameObject, true );

		var decal = go.AddComponent<Decal>();
		decal.Decals = [def];
		decal.SortLayer = _layer++;

		go.NetworkSpawn();

		var undo = Player.Undo.Create();
		undo.Name = "Decal";
		undo.Add( go );
	}
}
```

## Что проверить?

1. Выбираем `.decal` ресурс в инспекторе
2. ЛКМ на стену → появляется декаль
3. Зажимаем ПКМ → быстрое рисование
4. Remover (ЛКМ) → декаль удаляется (тег `"removable"`)
5. Z (Undo) → последняя декаль удаляется
6. Декаль движется вместе с объектом

---

✅ **Фаза 10 завершена!** Все базовые инструменты тулгана созданы:

| Инструмент | Тип | Файл |
|-----------|-----|------|
| BaseConstraintToolMode | Базовый класс соединений | 10_01 |
| Rope | Верёвка | 10_02 |
| Elastic | Пружина | 10_03 |
| Weld | Сварка (с Easy Mode) | 10_04 |
| BallSocket | Шарнир | 10_05 |
| Slider | Ползунок | 10_06 |
| NoCollide | Отключение столкновений | 10_07 |
| Remover | Удалитель | 10_08 |
| Resizer | Масштабирование | 10_09 |
| Mass | Изменение массы | 10_10 |
| Unbreakable | Неразрушимость | 10_11 |
| Trail | Следы + LineDefinition | 10_12 |
| Decal | Декали | 10_13 |

➡️ Следующая фаза: [11_01 — Balloon (шарик)](11_01_Balloon.md)
