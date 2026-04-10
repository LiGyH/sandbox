# 02.03 — События игрока (PlayerEvent) 📡

## Что мы делаем?

Этот файл создаёт **систему событий игрока** — два интерфейса, через которые компоненты узнают о действиях игрока: спавн, получение урона, смерть, прыжок, приземление, подбор предмета и настройка камеры.

Представьте, что игрок прыгнул. Об этом должны узнать: аниматор (запустить анимацию прыжка), звуковая система (проиграть звук), UI (показать эффект), камера (подпрыгнуть). Вместо того чтобы вызывать каждую систему вручную, мы **рассылаем событие**, и все подписчики реагируют самостоятельно.

## Как это работает внутри движка?

### Паттерн ISceneEvent

В s&box `ISceneEvent<T>` — это механизм рассылки событий. Когда вы вызываете:

```csharp
IPlayerEvent.Post( x => x.OnJump() );
```

Движок находит все компоненты, реализующие `IPlayerEvent`, и вызывает у них `OnJump()`. Это паттерн **«наблюдатель»** (Observer) — компоненты подписываются на события, просто реализуя интерфейс.

### Два уровня событий

```
IPlayerEvent (на GameObject игрока)
  └── Получают только компоненты на самом игроке
      Пример: контроллер движения, инвентарь, здоровье

ILocalPlayerEvent (на всю сцену)
  └── Получают все компоненты в сцене
      Пример: HUD, музыка, камера, эффекты окружения
```

**Почему два интерфейса?** Не всё должно знать обо всём. Контроллер здоровья находится на объекте игрока — ему нужен `IPlayerEvent`. А вот HUD — это отдельный объект в сцене — ему нужен `ILocalPlayerEvent`.

### Структуры параметров

Вместо длинного списка аргументов (`float damage, GameObject attacker, GameObject weapon, ...`) используются **структуры** (struct):

```csharp
void OnDamage( DamageParams args );
// вместо
void OnDamage( float damage, GameObject attacker, GameObject weapon, TagSet tags, ... );
```

Преимущества: легко добавлять новые поля, не ломая существующий код.

## Создай файл

Путь: `Code/Player/PlayerEvent.cs`

```csharp
/// <summary>
/// Called only on the Player's GameObject for their actions or events
/// </summary>
public interface IPlayerEvent : ISceneEvent<IPlayerEvent>
{
	void OnSpawned() { }

	public struct DamageParams
	{
		public float Damage { get; set; }
		public GameObject Attacker { get; set; }
		public GameObject Weapon { get; set; }
		public TagSet Tags { get; set; }
		public Vector3 Position { get; set; }
		public Vector3 Origin { get; set; }
	}
	void OnDamage( DamageParams args ) { }

	public struct DiedParams
	{
		public GameObject Attacker { get; set; }
	}
	void OnDied( DiedParams args ) { }

	void OnJump() { }
	void OnLand( float distance, Vector3 velocity ) { }
	void OnSuicide() { }
	void OnPickup( BaseCarryable item ) { }
	void OnCameraMove( ref Angles angles ) { }
	void OnCameraSetup( CameraComponent camera ) { }
	void OnCameraPostSetup( CameraComponent camera ) { }
}

/// <summary>
/// Broadcasted to the entire scene on the local player's actions or events
/// </summary>
public interface ILocalPlayerEvent : ISceneEvent<ILocalPlayerEvent>
{
	void OnJump() { }
	void OnLand( float distance, Vector3 velocity ) { }
	void OnPickup( BaseCarryable weapon ) { }
}
```

## Разбор кода

### Интерфейс IPlayerEvent

```csharp
public interface IPlayerEvent : ISceneEvent<IPlayerEvent>
```
- Наследует `ISceneEvent<IPlayerEvent>` — подключается к системе рассылки событий движка.
- События получают **только компоненты на GameObject игрока**.

### Событие OnSpawned

```csharp
void OnSpawned() { }
```
Вызывается, когда игрок появляется в мире. Используется для инициализации: выдать стартовое оружие, сбросить здоровье, обновить UI.

### Структура DamageParams

```csharp
public struct DamageParams
{
    public float Damage { get; set; }
    public GameObject Attacker { get; set; }
    public GameObject Weapon { get; set; }
    public TagSet Tags { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Origin { get; set; }
}
```

| Поле | Назначение |
|---|---|
| `Damage` | Количество урона (например, `25.0f`). |
| `Attacker` | Кто нанёс урон (GameObject атакующего). |
| `Weapon` | Чем нанёс урон (GameObject оружия). |
| `Tags` | Теги урона: `"head"` (хедшот), `"explosion"` (взрыв) — см. DamageTags (02.04). |
| `Position` | Точка попадания в мире (где именно ударила пуля). |
| `Origin` | Откуда пришёл урон (позиция стрелка). |

`struct` вместо `class` — потому что параметры урона одноразовые: создали, передали, забыли. Struct хранится на стеке и не нагружает сборщик мусора.

### Событие OnDamage

```csharp
void OnDamage( DamageParams args ) { }
```
Вызывается при получении урона. Компонент здоровья уменьшает HP, компонент анимации проигрывает реакцию на попадание, HUD показывает индикатор направления урона.

### Структура DiedParams и событие OnDied

```csharp
public struct DiedParams
{
    public GameObject Attacker { get; set; }
}
void OnDied( DiedParams args ) { }
```
Вызывается при смерти. `Attacker` — кто убил (нужен для ленты убийств и начисления очков).

### События действий

```csharp
void OnJump() { }
```
Игрок прыгнул. Используется для анимации и звука прыжка.

```csharp
void OnLand( float distance, Vector3 velocity ) { }
```
Игрок приземлился. `distance` — высота падения (для расчёта урона от падения). `velocity` — скорость в момент приземления.

```csharp
void OnSuicide() { }
```
Игрок покончил с собой (команда `kill` в консоли).

```csharp
void OnPickup( BaseCarryable item ) { }
```
Игрок подобрал предмет. `BaseCarryable` — базовый класс переносимых предметов (оружие, инструменты).

### События камеры

```csharp
void OnCameraMove( ref Angles angles ) { }
```
Вызывается при движении камеры. Ключевое слово `ref` означает, что метод может **изменить** углы камеры. Например, отдача оружия добавляет вертикальное отклонение.

```csharp
void OnCameraSetup( CameraComponent camera ) { }
void OnCameraPostSetup( CameraComponent camera ) { }
```
`OnCameraSetup` — настройка камеры до рендера (позиция, FOV). `OnCameraPostSetup` — после рендера вьюмодели (эффекты поверх).

### Интерфейс ILocalPlayerEvent

```csharp
public interface ILocalPlayerEvent : ISceneEvent<ILocalPlayerEvent>
{
    void OnJump() { }
    void OnLand( float distance, Vector3 velocity ) { }
    void OnPickup( BaseCarryable weapon ) { }
}
```
Рассылается **по всей сцене**, а не только на объекте игрока. Содержит подмножество событий — только те, о которых должна знать вся сцена (HUD, звуковой дизайн, эффекты окружения).

Обратите внимание: `OnJump` и `OnLand` есть в обоих интерфейсах. Компонент на игроке реализует `IPlayerEvent`, а глобальный компонент звука — `ILocalPlayerEvent`.

## Результат

После создания этого файла:
- Компоненты на объекте игрока могут реагировать на спавн, урон, смерть, прыжок и другие действия через `IPlayerEvent`.
- Глобальные компоненты (HUD, камера, звук) подписываются на `ILocalPlayerEvent`.
- Параметры урона и смерти аккуратно упакованы в структуры.
- Все методы имеют реализацию по умолчанию — компонент реализует только те события, которые ему нужны.

> 📖 Подробнее о системе событий: [Исходный код s&box](https://github.com/LiGyH/sbox-public)

---

Следующий шаг: [02.04 — Теги урона (DamageTags)](02_04_DamageTags.md)
