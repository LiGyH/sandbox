# 26.12 — HTTP-запросы (Http.RequestAsync)

## Что мы делаем?

Если хочешь, чтобы игра обращалась к **внешнему API** (свой бекенд, сторонний веб-сервис, лидерборд, аналитика), нужны HTTP-запросы. s&box даёт класс `Sandbox.Http` с методами `RequestStringAsync`, `RequestJsonAsync`, `RequestAsync` и хелпером `Http.CreateJsonContent`.

## Простейший GET

```csharp
string body = await Http.RequestStringAsync( "https://example.com/api/ping" );
Log.Info( body );
```

`RequestStringAsync` — `GET`, возвращает тело ответа как строку.

## GET с JSON

```csharp
record ServerStatus( string Name, int Players );

var status = await Http.RequestJsonAsync<ServerStatus>( "https://example.com/api/status" );
Log.Info( $"Сервер {status.Name}, {status.Players} игроков" );
```

s&box сам распарсит JSON в указанный тип через `System.Text.Json`. Поля совпадают по именам (case-insensitive).

## POST с JSON

```csharp
var payload = new { player = "ligyh", score = 9001 };

await Http.RequestAsync(
    "https://example.com/api/score",
    "POST",
    Http.CreateJsonContent( payload )
);
```

`Http.CreateJsonContent( object )` сериализует объект в JSON и ставит правильный `Content-Type: application/json`.

Если ответ нужен:

```csharp
record SubmitResponse( bool Ok, int Rank );

var resp = await Http.RequestJsonAsync<SubmitResponse>(
    "https://example.com/api/score",
    "POST",
    Http.CreateJsonContent( payload )
);
```

## Заголовки и аутентификация

```csharp
var headers = new Dictionary<string, string>
{
    { "Authorization", "Bearer my-token" },
    { "X-Client", "sandbox" }
};

string body = await Http.RequestStringAsync( "https://example.com/api/me", headers );
```

Для встроенных сервисов s&box-у часто хватает **auth-токена**:

```csharp
string token = await Sandbox.Services.Auth.GetToken( "MyService" );
```

См. также [26.13](26_13_WebSockets.md), там тот же паттерн.

## Что разрешено

| Что можно | Что нельзя |
|---|---|
| `https://` к домену | `http://` к публичному IP |
| `http://localhost:80/443/8080/8443` | Любые другие порты на `localhost` |
| Любые публичные REST-API | Доступ к локальной сети сервера |

> Чтобы тестировать локальный бекенд на нестандартных портах, запускай движок с флагом `-allowlocalhttp`.

## Обработка ошибок

`Http.RequestAsync` бросает `HttpRequestException` при сетевых ошибках или статусах ≥ 400. Оборачивай в `try/catch`:

```csharp
try
{
    var data = await Http.RequestJsonAsync<MyData>( url );
    Apply( data );
}
catch ( Exception ex )
{
    Log.Warning( $"Не смогли загрузить {url}: {ex.Message}" );
    ApplyDefaults();
}
```

## HTTP — это **серверный** запрос

В сетевой игре HTTP-запрос делает только тот, кто **должен**, не все клиенты разом. Обычно:

- **Лидерборды читает только хост**, потом раскидывает результат через `[Sync]` или RPC.
- **Сабмит счёта** делает хост (или dedicated server), один раз за матч.

Иначе 32 клиента дружно пойдут к твоему API, и ты получишь rate limit.

```csharp
async Task UpdateLeaderboard()
{
    if ( !Networking.IsHost ) return;
    var top = await Http.RequestJsonAsync<TopList>( "https://api.../top" );
    SyncLeaderboard( top );
}
```

## Не блокируй UI

`await Http.RequestStringAsync` **не блокирует** игровой поток (это асинхронный таск, см. [26.10](26_10_Async_Components.md)). Но не забывай, что в момент возвращения управление снова у тебя — компонент может уже не существовать:

```csharp
async Task LoadAvatar()
{
    var bytes = await Http.RequestStringAsync( url );
    if ( !this.IsValid() ) return;     // компонент мог быть уничтожен
    Apply( bytes );
}
```

## Ограничения

- Размер ответа ограничен (несколько мегабайт). Для больших файлов — `WebSocket` или `Cloud Assets`.
- Нет произвольного `TCP`/`UDP` — только HTTP/HTTPS и WebSocket.
- Нельзя ходить во внутренние IP (172.16.*, 10.*) и т.п.

## Что важно запомнить

- `Http.RequestStringAsync(url)` — простой GET.
- `Http.RequestJsonAsync<T>(url)` — GET с автоматическим парсингом.
- `Http.RequestAsync(url, method, content)` — произвольный метод; `Http.CreateJsonContent(obj)` для тела.
- HTTP делает **хост**, не каждый клиент.
- Только HTTPS-домены, исключения только для `localhost`.
- Всегда проверяй `IsValid()` после `await` и оборачивай в `try/catch`.

## Что дальше?

В [26.13](26_13_WebSockets.md) — постоянные двунаправленные соединения через WebSocket.
