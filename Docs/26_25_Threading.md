# 26.25 — Настоящая многопоточность (Thread, Task.Run, потокобезопасность)

## Что мы делаем?

В [26.10](26_10_Async_Components.md) мы разобрали `async/await` — **«корутины» на одном (главном) потоке**. Этого достаточно для 95% задач, но иногда правда нужна **настоящая многопоточность**: пересчитать огромный массив, распаковать гигабайт данных, прогнать тяжёлую процедурную генерацию. Этот этап рассказывает, как **аккуратно** уйти с главного потока в s&box, что на других потоках можно/нельзя, и как безопасно вернуть результат назад.

> 🧭 Этот этап — **продвинутый**. Перед тем как запускать `Thread`/`Task.Run`, убедись, что задача действительно тяжёлая и блокирует FPS. Async-корутина из [26.10](26_10_Async_Components.md) проще и подходит чаще, чем кажется.

## Когда нужна настоящая многопоточность

| Подходит | Не подходит |
|---|---|
| Процедурная генерация мира (perlin-noise 4096×4096) | Любая работа со `Scene`/`GameObject`/`Component` |
| Распаковка/парсинг большого файла | Любые UI-операции (`Sandbox.UI`, Razor) |
| Тяжёлый CPU-алгоритм (флудфилл, поиск пути по ОГРОМНОМУ графу) | Физика, трассировка (`Scene.Trace`), физические тела |
| Отдельные числовые расчёты (массивы `float[]`) | Загрузка `Model`/`Texture`/`Material` |
| Криптография, хеширование, сжатие | RPC, `[Sync]`, сетевая видимость |

**Главное правило:** на стороннем потоке тебе доступны **только чистые .NET-данные** — `int[]`, `float[]`, `byte[]`, `string`, твои собственные POCO-классы и структуры. Всё, что трогает движок, — **обязательно** на главном потоке.

## Способ 1: `Task.Run` (рекомендуемый)

Самый удобный способ выкинуть кусок CPU-работы в пул потоков:

```csharp
async Task GenerateAsync()
{
    // Готовим вход на главном потоке
    int seed = Game.Random.Next();
    int size = 1024;

    // Уходим в пул потоков
    float[] heights = await Task.Run( () =>
    {
        var arr = new float[size * size];
        for ( int y = 0; y < size; y++ )
        for ( int x = 0; x < size; x++ )
            arr[y * size + x] = Noise.Perlin( x * 0.01f, y * 0.01f, seed );
        return arr;
    } );

    // ★ После await мы снова на главном потоке (см. ниже)
    if ( !this.IsValid() ) return;
    ApplyToTerrain( heights );
}
```

`Task.Run` запускает делегат на **пуле потоков .NET** (`ThreadPool`). Пока он работает — игра не блокируется. В момент `await` управление возвращается, и **по умолчанию ты снова на главном потоке** (s&box ставит свой `SynchronizationContext`, как WinForms/WPF).

## Способ 2: Свой `Thread`

Когда нужен **долгоживущий** фоновый поток (логгер, watcher файлов, фоновый чат-бот), а не разовая задача:

```csharp
private Thread _worker;
private CancellationTokenSource _cts;

protected override void OnStart()
{
    _cts = new CancellationTokenSource();
    _worker = new Thread( WorkerLoop ) { IsBackground = true, Name = "MyWorker" };
    _worker.Start();
}

protected override void OnDestroy()
{
    _cts.Cancel();
    _worker?.Join( 1000 );  // подождать до 1 сек, чтобы корректно завершился
}

void WorkerLoop()
{
    while ( !_cts.IsCancellationRequested )
    {
        DoBackgroundWork();
        Thread.Sleep( 250 );
    }
}
```

`IsBackground = true` означает: процесс не будет ждать этот поток при выходе. Без этого флага зависший фоновый поток способен задержать выгрузку игры.

## Способ 3: `Parallel.For` для массивов

Если задача — **обработать каждый элемент массива независимо**, не плоди вручную потоки, используй `Parallel.For`:

```csharp
await Task.Run( () =>
{
    Parallel.For( 0, heights.Length, i =>
    {
        heights[i] = ProcessHeight( heights[i] );
    } );
} );
```

`Parallel.For` сам поделит диапазон, разложит по доступным ядрам, дождётся всех. Идеально для постобработки массивов высот, цветов, voxel-чанков.

## Возврат на главный поток

После `await Task.Run(...)` ты снова на главном потоке — это поведение по умолчанию. Но если по какой-то причине ты внутри `Thread`'а (или `Task.Run` сцепил `.ContinueWith( ..., TaskScheduler.Default )`), нужно **явно** прыгнуть обратно:

```csharp
// Внутри стороннего потока:
heights[42] = 0.5f;

// Хочу применить к сцене — обязан вернуться:
GameTask.MainThread( () =>
{
    ApplyToTerrain( heights );   // ← теперь безопасно, мы на главном потоке
} );
```

(Точное имя API может меняться: `Sandbox.GameTask.MainThread`, `Task.MainThread()`. Сверься с актуальным `code/advanced-topics/...` Facepunch.)

> ❗ Нарушение этого правила приводит к **самым неприятным багам в s&box**: рандомные крэши, наполовину применённые изменения, тихий corrupt сцены. Не тестируется в обычном QA — проявляется у одного игрока из ста.

## Потокобезопасность данных

Когда два потока трогают одни и те же данные, нужны **примитивы синхронизации**:

| Инструмент | Когда |
|---|---|
| `lock ( obj ) { ... }` | простой mutual exclusion, короткие критические секции |
| `Interlocked.Increment( ref counter )` | атомарные счётчики |
| `volatile int x` | флаг, читаемый/записываемый из разных потоков |
| `ConcurrentQueue<T>`, `ConcurrentBag<T>`, `ConcurrentDictionary<TK,TV>` | потокобезопасные коллекции |
| `SemaphoreSlim`, `ManualResetEventSlim` | сигналы и ограничители параллелизма |
| `CancellationToken` | кооперативная отмена долгих операций |

```csharp
private readonly object _lock = new();
private int _processed;

void Worker()
{
    while ( more )
    {
        var item = GetNext();
        var result = Heavy( item );

        lock ( _lock )
        {
            _results.Add( result );
            _processed++;
        }
    }
}
```

Внутри `lock` делай **минимум работы** — ничего долгого, никаких `Thread.Sleep`, никакой работы с UI. Только обновление общих структур.

## Передача данных между потоками: `ConcurrentQueue`

Часто фоновому потоку нужно «отдавать» результаты главному потоку по мере готовности:

```csharp
private readonly ConcurrentQueue<Chunk> _ready = new();

void Worker()
{
    foreach ( var chunk in chunks )
    {
        var processed = Process( chunk );
        _ready.Enqueue( processed );
    }
}

protected override void OnUpdate()
{
    // На главном потоке вычерпываем готовые куски
    while ( _ready.TryDequeue( out var chunk ) )
        ApplyChunk( chunk );
}
```

Это классический producer/consumer-паттерн без `lock` — `ConcurrentQueue` сама потокобезопасна.

## Отмена через `CancellationToken`

Когда игрок выходит со сцены, тяжёлая фоновая задача должна **прекратиться**, а не продолжать бесполезную работу:

```csharp
private CancellationTokenSource _cts;

protected override void OnStart()
{
    _cts = new CancellationTokenSource();
    _ = GenerateAsync( _cts.Token );
}

protected override void OnDestroy() => _cts?.Cancel();

async Task GenerateAsync( CancellationToken ct )
{
    var data = await Task.Run( () =>
    {
        var arr = new float[1_000_000];
        for ( int i = 0; i < arr.Length; i++ )
        {
            ct.ThrowIfCancellationRequested();   // отмена «тут можно»
            arr[i] = Heavy( i );
        }
        return arr;
    }, ct );

    if ( !this.IsValid() ) return;
    Apply( data );
}
```

`OperationCanceledException` ловится `await`-ом и не считается ошибкой — это нормальный сценарий.

## Производительность: накладные расходы

Поток — не бесплатный. Затраты:

- Создание потока — десятки микросекунд.
- Передача данных через `lock`/`ConcurrentQueue` — наносекунды-микросекунды на операцию.
- Слишком много мелких задач в `ThreadPool` могут перегрузить очередь и **дать обратный эффект**.

Правило большого пальца: **задача должна занимать минимум 1–2 мс**, иначе оверхед съест выигрыш. Для коротких операций просто оставь их на главном потоке.

## Антипаттерны

- ❌ **Трогать `Scene`, `Components`, `Network` со стороннего потока.** Любая мутация — гарантия будущих странных багов.
- ❌ **`Thread.Sleep` на главном потоке.** Замораживает игру. Используй `await Task.DelaySeconds`.
- ❌ **`.Result` / `.Wait()` на `Task` в главном потоке.** Дедлок: задача ждёт главный поток, главный ждёт задачу.
- ❌ **`async void` в фоновых задачах.** Исключение «провалится» молча, ты узнаешь по багу через месяц.
- ❌ **Гонка за коллекцию без `lock` или `Concurrent*`.** Невоспроизводимый corrupt.
- ❌ **Игнорировать `CancellationToken`.** При выходе из сцены задача продолжит работать впустую.

## Минимальный рецепт «тяжёлый расчёт без лагов»

```csharp
public sealed class HeavyJob : Component
{
    private CancellationTokenSource _cts;

    [Button( "Запустить" )]
    public void Run()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        _ = RunAsync( _cts.Token );
    }

    protected override void OnDestroy() => _cts?.Cancel();

    async Task RunAsync( CancellationToken ct )
    {
        Log.Info( "Старт фоновой задачи" );

        var result = await Task.Run( () => HeavyCompute( ct ), ct );

        if ( !this.IsValid() ) return;
        Apply( result );
        Log.Info( "Готово" );
    }

    static float[] HeavyCompute( CancellationToken ct )
    {
        var arr = new float[2_000_000];
        for ( int i = 0; i < arr.Length; i++ )
        {
            if ( (i & 0xFFFF) == 0 ) ct.ThrowIfCancellationRequested();
            arr[i] = MathF.Sqrt( i ) * MathF.Sin( i * 0.001f );
        }
        return arr;
    }

    void Apply( float[] data ) { /* трогаем сцену здесь, мы на главном потоке */ }
}
```

Этот шаблон закрывает большинство реальных кейсов: процедурная генерация, парсинг, тяжёлая аналитика.

## Что важно запомнить

- **Async из [26.10](26_10_Async_Components.md) — однопоточный**, не использует ядра CPU. Многопоточность — про реальную параллельность.
- **На фоновом потоке нельзя трогать движок** (Scene, Components, Network, UI). Только чистые .NET-данные.
- **`Task.Run`** — самый удобный способ; **`Thread`** — для долгоживущих фоновых сервисов; **`Parallel.For`** — для массивов.
- После `await Task.Run(...)` ты **обычно** возвращаешься на главный поток. Если нет — **`GameTask.MainThread( ... )`**.
- Используй **`lock`**, **`Interlocked`** или **`Concurrent*`-коллекции** — никогда не пиши в общие данные «на удачу».
- Передавай результаты через **`ConcurrentQueue`**, опрашивая её в `OnUpdate`.
- Всегда поддерживай **`CancellationToken`** и проверяй **`IsValid()`** после `await`.
- Не запускай миллион мелких задач — оверхед съест выгоду; задача должна стоить минимум 1–2 мс.

## Что дальше?

Этот этап — настоящая «продвинутая» вершина Фазы 26. Остальные подэтапы ([26.01](26_01_Physics_Bodies.md)–[26.24](26_24_Services.md)) дают широкое покрытие официальной документации; этот — узкий, но критически важный инструмент, без которого не сделать процедурный мир, тяжёлый импорт и крупный offline-расчёт.

После Фазы 26 переходи к **[Фазе 27 — Dedicated Server](27_01_Dedicated_Server_Intro.md)**: глубокий разбор выделенных серверов, устройство сетевого кода, паттерны (prediction, lag-comp, interest management) и масштабирование под разные жанры от 16 до 1000+ игроков.
