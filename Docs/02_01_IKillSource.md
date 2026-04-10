# 02.01 — Источник убийства (IKillSource) 🎯

## Что мы делаем?

Начинаем **Фазу 2 — Игровые интерфейсы**. Теперь мы создаём «контракты», которые описывают поведение объектов в игре.

Первый файл — интерфейс `IKillSource`. Он отвечает за **ленту убийств** (kill feed) — ту полоску в углу экрана, где показано «Игрок A убил Игрока B из дробовика».

Убить может не только игрок. Взрывная бочка, турель, NPC — всё это тоже «источники убийства». Интерфейс `IKillSource` объединяет их общим контрактом: «у меня есть имя, и я могу убить кого-то».

## Как это работает внутри движка?

### Интерфейсы в C#

Интерфейс — это **контракт**. Он говорит: «любой класс, реализующий меня, обязан иметь эти свойства и методы». Сам интерфейс не содержит данных — только описание «что должно быть».

```
IKillSource (контракт)
  ├── Player        — реализует: имя = ник Steam, SteamId = реальный ID
  ├── Npc           — реализует: имя = "Zombie", SteamId = 0
  └── ExplosiveBarrel — реализует: имя = "Бочка", Tags = "explosion"
```

### Методы по умолчанию (default interface methods)

Начиная с C# 8, интерфейсы могут содержать **реализацию по умолчанию**. Если класс не переопределяет метод — используется версия из интерфейса.

В `IKillSource` три члена имеют значения по умолчанию:
- `SteamId => default` — по умолчанию `0` (значит, это не игрок).
- `Tags => ""` — по умолчанию пустая строка (обычное убийство).
- `OnKill() { }` — по умолчанию ничего не делает.

Это означает, что для простого случая (например, бочка) достаточно реализовать только `DisplayName`.

### Kill feed — лента убийств

Когда один объект убивает другой, игра берёт данные из `IKillSource`:
1. `DisplayName` — чтобы показать имя убийцы.
2. `SteamId` — чтобы подсветить строку, если убийца — это вы.
3. `Tags` — чтобы выбрать иконку (NPC, взрыв и т.д.).
4. `OnKill()` — чтобы начислить очки, обновить статистику.

## Создай файл

Путь: `Code/Game/IKillSource.cs`

```csharp
/// <summary>
/// Implement on any component that can appear as an attacker in the kill feed.
/// Examples: Player, Npc, explosive barrel, turret, whatever the fuck.
/// </summary>
public interface IKillSource
{
	/// <summary>
	/// Display name
	/// </summary>
	string DisplayName { get; }

	/// <summary>
	/// Steam ID for the local "is-me" highlight. Defaults to 0 (not a player).
	/// </summary>
	long SteamId => default;

	/// <summary>
	/// Entity-type tag passed as <c>attackerTags</c>.
	/// Return an empty string for plain player kills. Examples: "npc"
	/// </summary>
	string Tags => "";

	/// <summary>
	/// Called on the host when this source kills something.
	/// Credit kills, update stats, etc. Default is no-op.
	/// </summary>
	void OnKill( GameObject victim ) { }
}
```

## Разбор кода

### XML-документация

```csharp
/// <summary>
/// Implement on any component that can appear as an attacker in the kill feed.
/// </summary>
```
Тройной слеш `///` — это XML-комментарий. Он отображается в подсказках IDE при наведении на тип. Всегда документируйте интерфейсы — это контракт, и другим разработчикам нужно понимать, зачем он нужен.

### Объявление интерфейса

```csharp
public interface IKillSource
```
- `public` — доступен из любого файла.
- `interface` — это контракт, не класс.
- `IKillSource` — имя. По конвенции C# интерфейсы начинаются с буквы `I`.

### Обязательное свойство

```csharp
string DisplayName { get; }
```
Только `{ get; }` — свойство только для чтения. Каждый класс, реализующий `IKillSource`, **обязан** предоставить `DisplayName`. Значения по умолчанию нет — это единственный обязательный член.

### Свойства с реализацией по умолчанию

```csharp
long SteamId => default;
```
Оператор `=>` — это «expression body». `default` для `long` = `0`. Если класс не переопределяет это свойство, `SteamId` будет `0`. Игрок переопределит его своим Steam ID, а бочка оставит `0`.

```csharp
string Tags => "";
```
По умолчанию — пустая строка. NPC может вернуть `"npc"`, чтобы в ленте убийств отобразилась специальная иконка.

### Метод с реализацией по умолчанию

```csharp
void OnKill( GameObject victim ) { }
```
Вызывается на хосте (сервере), когда этот источник убивает `victim`. Пустое тело `{ }` означает «ничего не делать». Игрок переопределит этот метод, чтобы начислить себе фраг. Бочка — нет.

## Результат

После создания этого файла:
- Любой компонент (Player, NPC, турель, бочка) может реализовать `IKillSource` и появиться в ленте убийств.
- Обязательно только `DisplayName` — остальное имеет разумные значения по умолчанию.
- Система убийств становится расширяемой: новые источники добавляются без изменения существующего кода.

> 📖 Подробнее об интерфейсах в движке: [Исходный код s&box](https://github.com/LiGyH/sbox-public)

---

Следующий шаг: [02.02 — Управляемые объекты (IPlayerControllable)](02_02_IPlayerControllable.md)
