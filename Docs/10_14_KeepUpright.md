# 👆🏻 KeepUpright — Инструмент «Upright» (стабилизатор поворота)

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)
> - [00.24 — Sync Properties](00_24_Sync_Properties.md)

## Что мы делаем?

Создаём файл `KeepUpright.cs` — инструмент ToolGun, который крепит к объекту **UprightJoint**. Это ограничитель ориентации: объект «держит» текущий поворот, стараясь вернуться в него после толчков. В отличие от шарниров, ограничивающих перемещение, здесь ограничивается **вращение**.

Инструмент поддерживает два режима:

- **Мировой якорь** (ЛКМ): якорит объект к мировому пространству (`joint.Body = null`).
- **Связь двух объектов** (двойной клик ПКМ): сначала ПКМ — выбрать первый объект, вторым кликом — второй. Объекты удерживают относительную ориентацию друг к другу.

Reload (R) в стадии «ждём второй клик» — отмена; иначе — снимает все KeepUpright-джоинты с объекта.

> В коде стоит атрибут `[Hide]` — инструмент скрыт из стандартной панели (доступен в меню разработчика / конструкций), но полностью рабочий.

## Зачем это нужно?

- **Стабилизация шасси/турели**: объект сам возвращается в «прямое» положение после взрыва или пинка.
- **Колёсная техника**: удерживать платформу ровно при физическом наезде.
- **Связка «родитель-дитя» по повороту**: две тележки, которые крутятся синхронно.

## Как это работает внутри движка?

### UprightJoint

`UprightJoint` — физический компонент движка s&box, создающий torque (крутящий момент), возвращающий тело к целевой ориентации. Основные параметры:

| Свойство | Диапазон / тип | Описание |
|---|---|---|
| `Body` | `GameObject` | Вторая сторона связи. `null` — якорь к миру. |
| `Hertz` | `float` (обычно 0..20) | «Жёсткость» — частота собственных колебаний пружины возврата. Чем выше — тем быстрее возвращает. |
| `DampingRatio` | `float` (обычно 0..2) | Коэффициент демпфирования. `0` — безудержная пружина, `1` — критическое демпфирование. |
| `MaxTorque` | `float` | Потолок крутящего момента, чтобы тяжёлые объекты не вели себя как бумажка. |

### Поля инструмента

| Свойство | По умолчанию | Описание |
|---|---|---|
| `Hertz` | `2.0f`, `[Range(0,20)]` | Передаётся в `UprightJoint.Hertz`. |
| `DampingRatio` | `0.7f`, `[Range(0,2)]` | Передаётся в `UprightJoint.DampingRatio`. |
| `TorqueMultiplier` | `5000f`, `[Range(1000,25000), Step(10)]` | Множитель `MaxTorque` (итоговое значение = `TorqueMultiplier * mass * torqueScale` где `torqueScale = 10f`). |
| `_point1`, `_stage` | — | Состояние «двухкликового» режима: 0 — ничего не выбрано, 1 — первый объект уже выбран. |

### Управление

| Ввод | Действие |
|---|---|
| **ЛКМ (attack1)** | `CreateWorldAnchor(select)` — создаёт якорь к миру. Сбрасывает стадию. |
| **ПКМ (attack2)**, стадия 0 | Запоминает первый объект, переходит в стадию 1. |
| **ПКМ (attack2)**, стадия 1 | Если второй объект отличается — `CreateLinked(point1, point2)`. Сбрасывает стадию. |
| **R (reload)**, стадия 0 | Удаляет все KeepUpright-соединения, связанные с объектом под прицелом. |
| **R (reload)**, стадия 1 | Отмена выбора первого объекта. |

Описание и локализационные ключи подсказок (`#tool.hint.keepupright.*`) автоматически меняются в зависимости от `_stage`.

### RPC-хосты

Все три операции выполняются только на хосте в контексте владельца:

- `[Rpc.Host( NetFlags.OwnerOnly )] CreateWorldAnchor(point)` — новый GameObject с тегом `constraint`, `UprightJoint` с `Body = null`. Масса для `MaxTorque` берётся из ближайшего `Rigidbody` в иерархии объекта, fallback `100f`. Регистрируется в `Player.Undo` с именем «Upright».
- `[Rpc.Host( NetFlags.OwnerOnly )] CreateLinked(point1, point2)` — два вложенных GameObject (в каждом из объектов), связанных через `UprightJoint` и `ConstraintCleanup`. Оба заносятся в один undo.
- `[Rpc.Host( NetFlags.OwnerOnly )] RemoveConstraints(go)` — аккуратно сносит и мировые якоря (`Body == null`), и парные соединения через `LinkedGameObjectBuilder` + `ConstraintCleanup`.

## Создай файл

📄 `Code/Weapons/ToolGun/Modes/KeepUpright.cs`

```csharp
[Hide]
[Title( "Upright" )]
[Icon( "👆🏻" )]
[ClassName( "upright" )]
[Group( "Constraints" )]
public class KeepUpright : ToolMode
{
	[Range( 0, 20 )]
	[Property, Sync]
	public float Hertz { get; set; } = 2.0f;

	[Range( 0, 2 )]
	[Property, Sync]
	public float DampingRatio { get; set; } = 0.7f;

	[Property, Sync, Range( 1000, 25000 ), Step( 10 )]
	public float TorqueMultiplier { get; set; } = 5000f;

	SelectionPoint _point1;
	int _stage = 0;
	const float torqueScale = 10f;

	public override string Description => _stage == 1
		? "#tool.hint.keepupright.stage1"
		: "#tool.hint.keepupright.description";

	public override string PrimaryAction => "#tool.hint.keepupright.apply_world";

	public override string SecondaryAction => _stage == 1
		? "#tool.hint.keepupright.finish"
		: "#tool.hint.keepupright.start_link";

	public override string ReloadAction => _stage == 1
		? "#tool.hint.keepupright.cancel"
		: "#tool.hint.keepupright.remove";

	protected override void OnDisabled()
	{
		base.OnDisabled();
		_stage = 0;
		_point1 = default;
	}

	public override void OnControl()
	{
		base.OnControl();

		var select = TraceSelect();
		IsValidState = select.IsValid();

		if ( !IsValidState ) return;

		if ( Input.Pressed( "reload" ) )
		{
			if ( _stage == 1 )
			{
				_stage = 0;
				_point1 = default;
			}
			else
			{
				var go = select.GameObject.Network.RootGameObject ?? select.GameObject;
				RemoveConstraints( go );
				ShootEffects( select );
			}
			return;
		}

		if ( Input.Pressed( "attack1" ) )
		{
			CreateWorldAnchor( select );
			ShootEffects( select );
			_stage = 0;
			_point1 = default;
			return;
		}

		if ( Input.Pressed( "attack2" ) )
		{
			if ( _stage == 0 )
			{
				_point1 = select;
				_stage = 1;
				ShootEffects( select );
			}
			else if ( _stage == 1 )
			{
				if ( _point1.IsValid() && _point1.GameObject != select.GameObject )
				{
					CreateLinked( _point1, select );
					ShootEffects( select );
				}

				_stage = 0;
				_point1 = default;
			}
		}
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	private void CreateWorldAnchor( SelectionPoint point )
	{
		if ( !point.IsValid() ) return;

		var go = new GameObject( point.GameObject, false, "keep_upright" );
		go.LocalTransform = point.LocalTransform;
		go.Tags.Add( "constraint" );

		var mass = point.GameObject.GetComponentInParent<Rigidbody>( true )?.Mass ?? 100f;

		var joint = go.AddComponent<UprightJoint>();
		joint.Body = null;
		joint.Hertz = Hertz;
		joint.DampingRatio = DampingRatio;
		joint.MaxTorque = TorqueMultiplier * mass * torqueScale;

		go.NetworkSpawn();

		var undo = Player.Undo.Create();
		undo.Name = "Upright";
		undo.Icon = "👆🏻";
		undo.Add( go );

		CheckContraptionStats( point.GameObject );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	private void CreateLinked( SelectionPoint point1, SelectionPoint point2 )
	{
		if ( !point1.IsValid() || !point2.IsValid() ) return;
		if ( point1.GameObject == point2.GameObject ) return;

		var go2 = new GameObject( point2.GameObject, false, "keep_upright" );
		go2.LocalTransform = point2.LocalTransform;
		go2.Tags.Add( "constraint" );

		var go1 = new GameObject( point1.GameObject, false, "keep_upright" );
		go1.WorldTransform = go2.WorldTransform;
		go1.Tags.Add( "constraint" );

		var cleanup = go1.AddComponent<ConstraintCleanup>();
		cleanup.Attachment = go2;

		var mass = point1.GameObject.GetComponentInParent<Rigidbody>( true )?.Mass ?? 100f;

		var joint = go1.AddComponent<UprightJoint>();
		joint.Body = go2;
		joint.Hertz = Hertz;
		joint.DampingRatio = DampingRatio;
		joint.MaxTorque = TorqueMultiplier * mass * torqueScale;

		go2.NetworkSpawn();
		go1.NetworkSpawn();

		var undo = Player.Undo.Create();
		undo.Name = "Upright";
		undo.Icon = "👆🏻";
		undo.Add( go1 );
		undo.Add( go2 );

		CheckContraptionStats( point1.GameObject );
	}

	[Rpc.Host( NetFlags.OwnerOnly )]
	private void RemoveConstraints( GameObject go )
	{
		// Remove world-anchor upright joints (Body == null means no second object)
		foreach ( var joint in go.GetComponentsInChildren<UprightJoint>( true ) )
		{
			if ( !joint.Body.IsValid() )
				joint.GameObject.Destroy();
		}

		// Remove linked upright joints via ConstraintCleanup
		var builder = new LinkedGameObjectBuilder();
		builder.AddConnected( go );

		var toRemove = new List<GameObject>();
		foreach ( var linked in builder.Objects )
		{
			foreach ( var cleanup in linked.GetComponentsInChildren<ConstraintCleanup>( true ) )
			{
				if ( linked != go && cleanup.Attachment?.Root != go ) continue;
				if ( cleanup.GameObject.GetComponentInChildren<UprightJoint>( true ) is not null )
					toRemove.Add( cleanup.GameObject );
			}
		}

		foreach ( var host in toRemove )
			host.Destroy();
	}
}
```

## Проверка

- [ ] Файл компилируется
- [ ] ЛКМ по объекту создаёт мировой якорь (`joint.Body == null`) — объект возвращается в текущую ориентацию
- [ ] Два последовательных ПКМ по разным объектам создают парное соединение
- [ ] R на объекте без активного выбора удаляет все KeepUpright-джоинты
- [ ] R в стадии 1 только отменяет выбор, не трогая объект
- [ ] Подсказки меняются в зависимости от `_stage`
- [ ] Все изменения попадают в `Player.Undo`

## Смотри также

- [10.01 — BaseConstraintToolMode](10_01_BaseConstraint.md) — базовый класс для инструментов-соединений (KeepUpright наследует напрямую от `ToolMode`, а не от него, но концепция стадий такая же).
- [12.02 — ConstraintCleanup](12_02_ConstraintCleanup.md) — используется в парном режиме для корректного удаления.

---

## ➡️ Следующий шаг

Возвращайся к началу Фазы 11 — [11.01 — Balloon](11_01_Balloon.md).
