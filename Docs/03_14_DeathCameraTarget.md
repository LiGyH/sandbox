# 03.14 — Камера смерти (DeathCameraTarget) 💀

## Что мы делаем?

Создаём **компонент-маркер**, который прикрепляется к рэгдоллу или гибу после смерти игрока. Камера наблюдателя (`PlayerObserver`) ищет этот компонент, чтобы знать, за чем следить.

## Как это работает?

```csharp
public partial class DeathCameraTarget : Component
{
    public Connection Connection { get; set; }  // чей это труп
    public DateTime Created { get; set; }        // когда создан

    protected override void OnEnabled()
    {
        Invoke( 60.0f, GameObject.Destroy );  // самоуничтожение через 60 секунд
    }
}
```

### Жизненный цикл

1. Игрок умирает → `Player.CreateRagdoll()` или `Player.Gib()` создаёт рэгдолл/гибы
2. К первому объекту добавляется `DeathCameraTarget` с `Connection = Network.Owner`
3. `PlayerObserver` ищет `DeathCameraTarget` для этого Connection и вращает камеру вокруг
4. Через 60 секунд рэгдолл самоуничтожается

### Зачем Connection, а не Player?

Потому что к этому моменту `Player` уже уничтожен (`GameObject.Destroy()`). Единственная стабильная ссылка — `Connection` (сетевое подключение), которое живёт пока клиент подключён.

### Зачем DateTime?

```csharp
.OrderByDescending( x => x.Created )
.FirstOrDefault();
```

Если у игрока несколько трупов (умер, заспавнился, умер снова), камера следит за **самым свежим**.

## Создай файл

Путь: `Code/Player/DeathCameraTarget.cs`

```csharp
public partial class DeathCameraTarget : Component
{
	public Connection Connection { get; set; }
	public DateTime Created { get; set; }

	protected override void OnEnabled()
	{
		Invoke( 60.0f, GameObject.Destroy );
	}
}
```

## Ключевые концепции

### Invoke — отложенный вызов

```csharp
Invoke( 60.0f, GameObject.Destroy );
```

Через 60 реальных секунд вызовет `GameObject.Destroy()`. Это встроенный механизм s&box — аналог `setTimeout` в JavaScript или `Invoke` в Unity.

### Маркер-компонент (Marker Component)

`DeathCameraTarget` не содержит сложной логики — он просто **отмечает** объект как «труп такого-то игрока». Это паттерн «компонент-маркер»: другие системы (`PlayerObserver`) ищут объекты с этим компонентом.

## Проверка

1. Умри в игре → камера вращается вокруг рэгдолла
2. Подожди 60 секунд → рэгдолл исчезает
3. Умри дважды подряд → камера следит за последним трупом

## Следующий файл

Переходи к **03.07 — Режим Noclip (NoclipMoveMode)**.
