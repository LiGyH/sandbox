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
