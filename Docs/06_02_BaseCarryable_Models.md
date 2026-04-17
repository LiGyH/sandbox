# 06.02 — ViewModel переносимого предмета (BaseCarryable.ViewModel) 👁️

## Что мы делаем?

Создаём partial-часть `BaseCarryable`, отвечающую за создание и уничтожение вью-модели (модели от первого лица). Эта модель видна только локальному игроку и помечается тегами `firstperson` и `viewmodel`.

## Зачем это нужно?

- **Первое лицо**: игрок видит оружие / инструмент перед собой. Для этого создаётся отдельный GameObject из `ViewModelPrefab`.
- **Локальность**: вью-модель не сохраняется и не синхронизируется по сети (`NotSaved | NotNetworked`), она существует только на клиенте владельца.
- **Жизненный цикл**: `CreateViewModel()` создаёт модель только один раз, `DestroyViewModel()` удаляет её при переключении в третье лицо или при деактивации.

## Как это работает внутри движка?

| Элемент | Описание |
|---|---|
| `ViewModelPrefab` | Свойство-ссылка на префаб вью-модели, настраивается в редакторе (Feature = "ViewModel"). |
| `CreateViewModel()` | Клонирует префаб, устанавливает `Parent = GameObject`, задаёт флаги `NotSaved | NotNetworked | Absolute`, добавляет теги `firstperson`, `viewmodel`, затем вызывает `Deploy()` на компоненте `ViewModel`. |
| `CloneConfig` | Конфигурация клонирования: `StartEnabled = false` (включается вручную), `Transform = Transform.Zero` (локальная позиция сбрасывается). |
| `DestroyViewModel()` | Уничтожает вью-модель и сбрасывает ссылку. Проверяет `IsValid()` перед уничтожением. |
| Guard-условия | Проверяются: уже существует ли модель, есть ли префаб, есть ли владелец / контроллер / рендерер, является ли игрок локальным. |

## Создай файл

**Путь:** `Code/Game/Weapon/BaseCarryable/BaseCarryable.ViewModel.cs`

```csharp
public partial class BaseCarryable : Component
{
	[Property, Feature( "ViewModel" )] public GameObject ViewModelPrefab { get; set; }

	protected void CreateViewModel()
	{
		if ( ViewModel.IsValid() )
			return;

		DestroyViewModel();

		if ( ViewModelPrefab is null )
			return;

		var player = Owner;
		if ( player is null || player.Controller is null || player.Controller.Renderer is null || !player.IsLocalPlayer ) return;

		ViewModel = ViewModelPrefab.Clone( new CloneConfig { Parent = GameObject, StartEnabled = false, Transform = global::Transform.Zero } );
		ViewModel.Flags |= GameObjectFlags.NotSaved | GameObjectFlags.NotNetworked | GameObjectFlags.Absolute;
		ViewModel.Enabled = true;
		ViewModel.Tags.Add( "firstperson", "viewmodel" );

		var vm = ViewModel.GetComponent<ViewModel>();

		if ( vm.IsValid() )
			vm.Deploy();
	}

	protected void DestroyViewModel()
	{
		if ( !ViewModel.IsValid() )
			return;

		ViewModel?.Destroy();
		ViewModel = default;
	}
}
```

## Проверка

- [ ] Файл компилируется без ошибок
- [ ] `CreateViewModel()` создаёт вью-модель только один раз (guard `IsValid()`)
- [ ] Вью-модель помечена флагами `NotSaved | NotNetworked | Absolute`
- [ ] Теги `firstperson` и `viewmodel` добавлены
- [ ] `Deploy()` вызывается на компоненте `ViewModel`
- [ ] `DestroyViewModel()` безопасно удаляет модель


---

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


---

## ➡️ Следующий шаг

Переходи к **[06.03 — Модель оружия (WeaponModel) 🔧](06_03_WeaponModel.md)**.
