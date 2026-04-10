# 06.03 — WorldModel переносимого предмета (BaseCarryable.WorldModel) 🌍

## Что мы делаем?

Создаём partial-часть `BaseCarryable`, которая управляет мировой моделью (WorldModel) — тем, что видят другие игроки, когда кто-то держит предмет в руках. Также определяем интерфейс `IEvent` для уведомлений о создании/уничтожении мировой модели.

## Зачем это нужно?

- **Визуализация для других**: когда игрок держит оружие, остальные видят мировую модель, прикреплённую к кости руки.
- **Крепление к скелету**: модель привязывается к кости `hold_r` (или другой заданной) скелета персонажа через `SkinnedModelRenderer.GetBoneObject()`.
- **Жизненный цикл drop/pickup**: `SetDropped(true/false)` переключает физику, коллайдер и компонент `DroppedWeapon`.
- **Событийная система**: `IEvent` — вложенный интерфейс `ISceneEvent`, позволяющий компонентам мировой модели реагировать на создание/уничтожение.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `IEvent : ISceneEvent<IEvent>` | Интерфейс с методами `OnCreateWorldModel()` / `OnDestroyWorldModel()`, рассылаемыми через `PostToGameObject`. |
| `WorldModelPrefab` | Префаб мировой модели, настраивается в редакторе. |
| `DroppedGameObject` | Ссылка на объект «выброшенного» предмета (физическая оболочка). |
| `HoldType` | Тип удержания из `CitizenAnimationHelper` — влияет на анимацию аватара. |
| `ParentBone` | Имя кости, к которой крепится модель (по умолчанию `hold_r`). |
| `SetDropped(bool)` | Включает/отключает `Rigidbody`, `ModelCollider`, `DroppedWeapon` и `DroppedGameObject`. |
| `CreateWorldModel(SkinnedModelRenderer)` | Клонирует префаб, прикрепляет к кости, ставит флаги `NotSaved | NotNetworked`, рассылает `OnCreateWorldModel()`. |
| `DestroyWorldModel()` | Рассылает `OnDestroyWorldModel()`, уничтожает объект, на хосте ставит `IsItem = true`. |

## Создай файл

**Путь:** `Code/Game/Weapon/BaseCarryable/BaseCarryable.WorldModel.cs`

```csharp
using Sandbox.Citizen;

public partial class BaseCarryable : Component
{
	public interface IEvent : ISceneEvent<IEvent>
	{
		public void OnCreateWorldModel() { }
		public void OnDestroyWorldModel() { }
	}

	[Property, Feature( "WorldModel" )] public GameObject WorldModelPrefab { get; set; }
	[Property, Feature( "WorldModel" )] public GameObject DroppedGameObject { get; set; }
	[Property, Feature( "WorldModel" )] public CitizenAnimationHelper.HoldTypes HoldType { get; set; } = CitizenAnimationHelper.HoldTypes.HoldItem;
	[Property, Feature( "WorldModel" )] public string ParentBone { get; set; } = "hold_r";

	protected void CreateWorldModel()
	{
		var player = GetComponentInParent<PlayerController>();
		if ( player?.Renderer is null ) return;

		CreateWorldModel( player.Renderer );
	}

	
	/// <summary>
	/// Enables or disables the physics/dropped components of this carryable.
	/// Call with <c>false</c> when picking up/holding, <c>true</c> when dropping.
	/// </summary>
	public void SetDropped( bool dropped )
	{
		var rb = GetComponent<Rigidbody>( true );
		if ( rb.IsValid() ) rb.Enabled = dropped;

		var col = GetComponent<ModelCollider>( true );
		if ( col.IsValid() ) col.Enabled = dropped;

		var droppedWeapon = GetComponent<DroppedWeapon>( true );
		if ( droppedWeapon.IsValid() ) droppedWeapon.Enabled = dropped;

		if ( DroppedGameObject.IsValid() ) DroppedGameObject.Enabled = dropped;
	}

	/// <summary>
	/// Creates and attaches the world model to the given renderer's bone.
	/// Use this overload when the weapon is held by something other than a player (e.g. an NPC).
	/// </summary>
	public void CreateWorldModel( SkinnedModelRenderer renderer )
	{
		if ( renderer is null ) return;

		if ( Networking.IsHost )
		{
			IsItem = false;
		}

		SetDropped( false );

		var worldModel = WorldModelPrefab?.Clone( new CloneConfig
		{
			Parent = renderer.GetBoneObject( ParentBone ) ?? GameObject,
			StartEnabled = true,
			Transform = global::Transform.Zero
		} );
		if ( worldModel.IsValid() )
		{
			worldModel.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked;
			WorldModel = worldModel;
			IEvent.PostToGameObject( WorldModel, x => x.OnCreateWorldModel() );
		}
	}

	protected void DestroyWorldModel()
	{
		if ( WorldModel.IsValid() )
			IEvent.PostToGameObject( WorldModel, x => x.OnDestroyWorldModel() );

		WorldModel?.Destroy();
		WorldModel = default;

		if ( Networking.IsHost )
			IsItem = true;
	}
}
```

## Проверка

- [ ] Файл компилируется без ошибок
- [ ] `CreateWorldModel()` крепит модель к заданной кости скелета
- [ ] `SetDropped(false)` отключает физику и коллайдер при подборе
- [ ] `SetDropped(true)` включает их при выбросе
- [ ] При создании/уничтожении рассылаются события `IEvent`
- [ ] Мировая модель помечена `NotSaved | NotNetworked`
