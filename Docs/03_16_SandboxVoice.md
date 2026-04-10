# 03.16 — Голосовой чат (SandboxVoice) 🎤

## Что мы делаем?

Создаём **систему голосового чата** с поддержкой мьютинга отдельных игроков. Наследуем от встроенного `Voice` и добавляем фильтрацию.

## Как это работает?

### Наследование от Voice

```csharp
public partial class SandboxVoice : Voice
```

`Voice` — встроенный компонент s&box, который обрабатывает передачу голоса через микрофон. `SandboxVoice` добавляет только одну функцию — **мьют-лист**.

### Мьют-лист

```csharp
public static HashSet<SteamId> MutedList { get; } = new();
```

`HashSet<SteamId>` — набор заглушенных SteamId. `HashSet` выбран для O(1) операции проверки `Contains()`.

### Переключение мьюта

```csharp
public static void Mute( SteamId id )
{
    if ( MutedList.Contains( id ) )
    {
        MutedList.Remove( id );
        return;
    }
    MutedList.Add( id );
}
```

Простой toggle: если уже в списке — убираем, если нет — добавляем.

### Фильтрация голоса

```csharp
protected override bool ShouldHearVoice( Connection connection )
{
    return !MutedList.Contains( connection.SteamId );
}
```

Переопределяем метод из базового `Voice`. Движок вызывает его для каждого входящего голосового пакета. Если возвращаем `false` — голос не воспроизводится.

## Создай файл

Путь: `Code/Player/SandboxVoice.cs`

```csharp
namespace Sandbox;

public partial class SandboxVoice : Voice
{
	/// <summary>
	/// A list of muted users.
	/// </summary>
	public static HashSet<SteamId> MutedList { get; } = new();

	/// <summary>
	/// Toggles mute for a user
	/// </summary>
	/// <param name="id"></param>
	public static void Mute( SteamId id )
	{
		if ( MutedList.Contains( id ) )
		{
			MutedList.Remove( id );
			return;
		}

		MutedList.Add( id );
	}

	/// <summary>
	/// Is this user muted?
	/// </summary>
	/// <param name="id"></param>
	/// <returns></returns>
	public static bool IsMuted( SteamId id ) => MutedList.Contains( id );

	protected override bool ShouldHearVoice( Connection connection )
	{
		return !MutedList.Contains( connection.SteamId );
	}
}
```

## Ключевые концепции

### namespace Sandbox

```csharp
namespace Sandbox;
```

Этот файл единственный в проекте, который явно указывает namespace. Это нужно потому что базовый класс `Voice` находится в `Sandbox`, и чтобы наследование работало корректно, `SandboxVoice` тоже должен быть в `Sandbox`.

### static HashSet — глобальное состояние

`MutedList` — `static`, то есть один на все экземпляры. Это правильно: мьют-лист — это настройка **клиента**, а не конкретного объекта. Даже если Voice-компонент пересоздаётся, список сохраняется.

### SteamId — не long

`SteamId` — это обёртка вокруг `long` в s&box. Она обеспечивает типобезопасность и удобные методы (аватарка, профиль).

## Проверка

1. Зайди в мультиплеер → другой игрок говорит в микрофон → слышишь
2. Вызови `SandboxVoice.Mute( steamId )` → перестаёшь слышать
3. Вызови ещё раз → снова слышишь

## Следующий файл

Переходи к **03.17 — Система отмены (UndoSystem)**.
