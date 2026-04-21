# 26.10 — Async/Task в компонентах

## Что мы делаем?

В s&box `async/await` работают как **корутины Unity** — это однопоточная асинхронность. Никакого хаоса с потоками: всё крутится на главном потоке, просто ты можешь сказать «подожди 2 секунды» или «дождись следующего кадра», не ломая управляющий поток.

## Простейший пример

```csharp
async Task PrintLater( float seconds, string text )
{
    await Task.DelaySeconds( seconds );
    Log.Info( text );
}

protected override void OnEnabled()
{
    _ = PrintLater( 1.5f, "Прошло полторы секунды!" );
}
```

`_ =` говорит компилятору «я не дожидаюсь этого таска, оставь меня в покое».

## Полезные `Task.*`-хелперы

| Метод | Что делает |
|---|---|
| `Task.DelaySeconds( s )` | подождать `s` игровых секунд |
| `Task.DelayRealtimeSeconds( s )` | подождать реальное время (игнорит `Time.Scale`) |
| `Task.Frame()` | подождать **один** кадр |
| `Task.MainThread()` | вернуться в главный поток (если ушёл с него) |
| `Task.WhenAll( a, b, ... )` | подождать все таски разом |
| `Task.WhenAny( a, b )` | подождать первый завершившийся |

## `Component.Task` — авто-отмена при разрушении

У каждого `Component` есть свойство `Task`, которое привязано к жизни компонента. Если компонент удалён или выключен — задача отменяется автоматически. Это важно: иначе ты можешь продолжить «лечить игрока» после того, как он умер.

```csharp
async Task FlashRed()
{
    while ( true )
    {
        Renderer.Tint = Color.Red;
        await Task.DelaySeconds( 0.1f );
        Renderer.Tint = Color.White;
        await Task.DelaySeconds( 0.1f );
    }
}

protected override void OnEnabled()
{
    _ = FlashRed();   // остановится сама, когда компонент выключат
}
```

## Возврат значения

```csharp
async Task<int> CountDown( int from )
{
    for ( int i = from; i > 0; i-- )
    {
        Log.Info( i );
        await Task.DelaySeconds( 1f );
    }
    return 0;
}

async Task RunIt()
{
    int result = await CountDown( 3 );
    Log.Info( $"Готово: {result}" );
}
```

## Композиция: «сделай два дела параллельно»

```csharp
async Task DoBoth()
{
    Task a = PrintLater( 2f, "A" );
    Task b = PrintLater( 3f, "B" );
    await Task.WhenAll( a, b );
    Log.Info( "Оба готовы" );
}
```

«Параллельно» — в кавычках. Это всё на одном потоке: пока один таск ждёт `DelaySeconds`, второй тоже ждёт. Ничего не «выполняется» одновременно — но писать так удобнее, чем плодить таймеры.

## Цепочка с проверкой жизни

Всегда проверяй, что компонент жив **после `await`**:

```csharp
async Task RespawnAfter( float seconds )
{
    await Task.DelaySeconds( seconds );
    if ( !this.IsValid() ) return;     // компонент уже уничтожен
    Player.Respawn();
}
```

`Component.Task` обычно сам отменит задачу через `OperationCanceledException`, но проверка `IsValid()` — страховка, особенно если ты ждёшь не `Component.Task`.

## `CancellationToken` — отмена «по требованию»

Когда пользователь отменил действие (отпустил кнопку, переключил оружие), нужно прервать долгий таск:

```csharp
private CancellationTokenSource _cts;

void StartLongAction()
{
    _cts?.Cancel();
    _cts = new CancellationTokenSource();
    _ = LongAction( _cts.Token );
}

async Task LongAction( CancellationToken ct )
{
    for ( int i = 0; i < 100; i++ )
    {
        ct.ThrowIfCancellationRequested();
        await Task.DelaySeconds( 0.1f );
    }
}

void Cancel() => _cts?.Cancel();
```

## Чего НЕ делать

- ❌ **Не используй `Thread.Sleep`** — заморозит игру.
- ❌ **Не запускай новые потоки** через `Task.Run( ... )` — большая часть API не потокобезопасна.
- ❌ **Не складывай таски бесконечно**: если нажатие кнопки запускает 10-секундный таск, не давай нажать её повторно (флаг `_busy = true` или `CancellationToken`).
- ❌ **Не пиши `async void`** в компонентах — исключения «провалятся в никуда». Используй `async Task` и `_ = ...`.

## Async vs `OnFixedUpdate`-таймер

Простой случай «через 2 секунды восстановить здоровье»:

```csharp
// Вариант A: async
async Task HealLater() { await Task.DelaySeconds( 2 ); Heal(); }

// Вариант B: TimeSince
TimeSince _t = -2f; // начальное значение
protected override void OnFixedUpdate()
{
    if ( _t > 2f ) { Heal(); _t = -1000f; }
}
```

Async читается человекоподобнее («подожди, потом сделай»), но сложнее ловить ошибки. `TimeSince` примитивнее, зато его легко увидеть в инспекторе и поставить брейкпоинт. Выбирай по вкусу.

## Что важно запомнить

- `async/await` в s&box = **корутины** на одном потоке.
- `Task.DelaySeconds`, `Task.Frame`, `Task.WhenAll` — основные кирпичи.
- Используй `Component.Task`, чтобы задача автоматически отменялась при разрушении компонента.
- Всегда проверяй `IsValid()` после `await`.
- Не плоди потоки и не пиши `async void`.

## Что дальше?

В [26.11](26_11_Scene_Events.md) — сценовые события: `ISceneStartup`, `IGameObjectNetworkEvents` и другие хуки в жизненный цикл сцены.
