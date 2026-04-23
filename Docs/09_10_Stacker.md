# 09.10 — Stacker — Инструмент «Стэкер» 📚

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.13 — Атрибуты компонентов](00_13_Component_Attributes.md)
> - [00.16 — Prefabs (Clone, NetworkSpawn)](00_16_Prefabs.md)
> - [00.22 — Ownership](00_22_Ownership.md)
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)

## Что мы делаем?

Создаём `StackerTool` — режим тулгана, который **клонирует выбранный объект в стек** заданной длины вдоль выбранной оси, с возможностью угла поворота между копиями (получаются стены, дуги, спирали и кольца) и автоматической заморозкой результата.

Это аналог `wire_stacker` / `Easy Bodygroup`-стэкера из Garry's Mod, но реализованный поверх обычной системы `ToolMode` + `Player.Undo`.

## Зачем это нужно?

- **Быстрая постройка стен**: один клик создаёт стопку до 50 кубов или панелей вдоль выбранной оси.
- **Дуги и кольца**: ненулевой `AngleOffset X/Y` позволяет строить арки и кольца — каждая копия поворачивается относительно предыдущей.
- **Точная упаковка**: размер шага вычисляется по реальному ориентированному bounding box модели + дополнительный `Gap`.
- **Автозаморозка**: по флагу `FreezeAll` копии создаются с выключенной физикой — стена не разваливается.
- **Корректный undo**: всё содержимое стэка добавляется в одну запись undo, поэтому одно нажатие `Z` снимает весь стэк.
- **Сетевая безопасность**: спавн происходит **только на хосте** (`[Rpc.Host]`), причём трансформы пересчитываются на сервере из синхронизированных полей — клиент не может прислать произвольные позиции.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `StackDirection` | Перечисление: `Up / Down / Left / Right / Forward / Back`. |
| `StackAlignMode` | `World` — ось стэка в мире; `Object` — ось вдоль локальных осей цели. |
| `StackCount` | `[Property, Sync, Range(1,50), ClientEditable]` — сколько копий создать. |
| `Direction` / `AlignMode` | `[Property, Sync]` — задаются на клиенте, синхронизируются на хост. |
| `AngleOffsetX / AngleOffsetY` | Углы поворота между соседними копиями вокруг двух осей, перпендикулярных направлению стэка. Превращает стек в дугу/кольцо. |
| `PositionOffset` (`Gap`) | Дополнительный зазор между копиями (в юнитах). |
| `FreezeAll` | Если включено — у каждой копии выключается `Rigidbody.MotionEnabled`. |
| `MaxStackCount = 50` | Жёсткий лимит, чтобы случайно не положить сервер 1000 пропами. |
| `TraceIgnoreTags` | Игнорирует `player`, `constraint`, `collision` при трассе наведения. |
| `OnControl()` | Каждый кадр: трассирует под прицел, проверяет цель, рисует ghost-превью, обрабатывает клавиши. |
| `CycleDirection()` | По нажатию `R` (reload) циклически меняет `Direction` (Up → Right → Down → Left → Forward → Back → Up). |
| `CycleAlignment()` | По нажатию ПКМ (`attack2`) переключает `AlignMode` World ↔ Object. |
| `ResolveRoot(go)` | Поднимается до корневого сетевого объекта (`Network.RootGameObject`), чтобы клонировать целый проп, а не его дочерний коллайдер. |
| `GetStackExtent()` | Берёт `BBox` модели, **проектирует все 8 углов на ось стэка** (с учётом мирового вращения и масштаба) — это даёт точный размер шага для произвольно повёрнутых объектов. К полученной длине прибавляется `PositionOffset`. |
| `ComputeStackTransforms()` | Итеративно строит массив `Transform[]` для копий: каждая следующая позиция и поворот вычисляются от предыдущих, поэтому ненулевые углы дают **правильные дуги**, а не спирали. |
| `DrawStackPreview()` | Рисует полупрозрачное превью каждой будущей копии через `DebugOverlay.GameObject(...)`. |
| `SpawnStack()` | `[Rpc.Host]` — пересчитывает трансформы на сервере, клонирует корневой объект, помечает копию тегом `"removable"` (для CleanupSystem), при `FreezeAll` гасит `Rigidbody.MotionEnabled`, регистрирует копию в сети и добавляет в общий `Undo`-entry с именем `"Stack"`. |

> **Атрибуты класса:** `[Icon("📚")]`, `[ClassName("stacker")]`, `[Group("Building")]`, `[Title("Stacker")]`. Группа `"Building"` появляется в SpawnMenu отдельной категорией — стэкер логически ближе к строительным инструментам (Hoverball, Thruster), чем к констрейнтам.

## Создай файл

📄 `Code/Weapons/ToolGun/Modes/Stacker/StackerTool.cs`

```csharp
/// <summary>
/// Direction to stack objects in.
/// </summary>
public enum StackDirection
{
	Up,
	Down,
	Left,
	Right,
	Forward,
	Back
}

/// <summary>
/// How to interpret the stack direction axis.
/// </summary>
public enum StackAlignMode
{
	/// <summary>
	/// Stack along world-space axes regardless of object orientation.
	/// </summary>
	World,

	/// <summary>
	/// Stack along the target object's local axes.
	/// </summary>
	Object
}

[Icon( "📚" )]
[ClassName( "stacker" )]
[Group( "Building" )]
[Title( "Stacker" )]
public class StackerTool : ToolMode
{
	private const int MaxStackCount = 50;

	public override IEnumerable<string> TraceIgnoreTags => ["player", "constraint", "collision"];

	[Property, Sync, Range( 1, MaxStackCount ), Step( 1 ), Title( "Count" ), ClientEditable]
	public float StackCount { get; set; } = 2;

	[Property, Sync]
	public StackDirection Direction { get; set; } = StackDirection.Up;

	[Property, Sync, Title( "Alignment" )]
	public StackAlignMode AlignMode { get; set; } = StackAlignMode.Object;

	[Property, Sync, Title( "Angle X" ), Range( -180, 180 ), Step( 1 )]
	public float AngleOffsetX { get; set; } = 0f;

	[Property, Sync, Title( "Angle Y" ), Range( -180, 180 ), Step( 1 )]
	public float AngleOffsetY { get; set; } = 0f;

	[Property, Sync, Range( 0, 128 ), Title( "Gap" )]
	public float PositionOffset { get; set; } = 0f;

	[Property, Sync, Title( "Freeze" )]
	public bool FreezeAll { get; set; } = true;

	public override string Description => "#tool.hint.stacker.description";
	public override string PrimaryAction => "#tool.hint.stacker.stack";
	public override string SecondaryAction => "#tool.hint.stacker.cycle_alignment";
	public override string ReloadAction => "#tool.hint.stacker.cycle_direction";

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();

		IsValidState = IsValidTarget( select );

		if ( Input.Pressed( "reload" ) )    CycleDirection();
		if ( Input.Pressed( "attack2" ) )   CycleAlignment();

		if ( !IsValidState ) return;

		var target = ResolveRoot( select.GameObject );
		var transforms = ComputeStackTransforms( target );

		DrawStackPreview( target, transforms );

		if ( Input.Pressed( "attack1" ) )
		{
			SpawnStack( target );
			ShootEffects( select );
		}
	}

	[Rpc.Host]
	private void SpawnStack( GameObject target )
	{
		if ( !target.IsValid() ) return;

		var root = ResolveRoot( target );

		// Пересчитываем трансформы на сервере — клиент не может прислать произвольные позиции.
		var transforms = ComputeStackTransforms( root );

		var undo = Player.Undo.Create();
		undo.Name = "Stack";
		undo.Icon = "📚";

		for ( int i = 0; i < transforms.Length; i++ )
		{
			var clone = root.Clone( new CloneConfig
			{
				Transform = transforms[i],
				StartEnabled = true
			} );

			clone.Tags.Add( "removable" );

			if ( FreezeAll )
			{
				var rb = clone.GetComponent<Rigidbody>();
				if ( rb.IsValid() ) rb.MotionEnabled = false;
			}

			clone.NetworkSpawn( true, null );
			undo.Add( clone );
		}
	}
}
```

> Полная реализация (включая `GetStackExtent`, `ComputeStackTransforms`, `GetPerpendicularAxes`, `GetStepRotation`) — см. файл `Code/Weapons/ToolGun/Modes/Stacker/StackerTool.cs` в репозитории. Логика построения трансформов вынесена в отдельные методы и хорошо покрыта документирующими комментариями.

## Локализация

Стэкер использует те же ключи `#tool.hint.stacker.*`, что и остальные инструменты. Их нужно добавить в `Localization/en/tools.json` (и в любые другие языки):

```json
{
  "tool.hint.stacker.description": "Stack copies of an object",
  "tool.hint.stacker.stack": "Stack",
  "tool.hint.stacker.cycle_alignment": "Cycle alignment (World / Object)",
  "tool.hint.stacker.cycle_direction": "Cycle direction"
}
```

## Что проверить?

1. Заспавни куб → выбери Stacker → наведись на куб, ЛКМ → появится столбик из `Count` копий.
2. Покрути `Angle X` или `Angle Y` в свойствах инструмента — стэк превратится в дугу/кольцо. При `Angle X = 18°` и `Count = 20` получается замкнутое кольцо на 360°.
3. Нажми `R` несколько раз — направление стэка циклически меняется (вверх → право → вниз → лево → вперёд → назад).
4. Нажми ПКМ — переключается режим выравнивания: `Object` (по локальным осям прицеленного объекта) ↔ `World` (по глобальным осям).
5. Нажми `Z` (undo) — весь стэк удаляется одной операцией с уведомлением «Undo Stack».
6. Включи `Freeze = false` — копии будут падать под собственным весом.
7. Поставь `Count = 50` и наведись на большой объект — лимит должен сработать (`Math.Clamp`), и сервер не залить.

---

## 🔗 См. также

- [09.01 — ToolMode (базовый класс)](09_01_ToolMode.md)
- [09.06 — Toolgun](09_06_Toolgun.md)
- [03.17 — UndoSystem](03_17_UndoSystem.md)
- [10.01 — BaseConstraintToolMode](10_01_BaseConstraint.md) — как устроен альтернативный класс инструментов с двумя стадиями выбора

## ➡️ Следующий шаг

Переходи к **[10.01 — 🔗 BaseConstraintToolMode](10_01_BaseConstraint.md)** — продолжение фазы инструментов-констрейнтов.
