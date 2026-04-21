# 26.13 — WebSockets

## Что мы делаем?

Когда нужен **постоянный двунаправленный канал** между игрой и сервером — поверх HTTP не выйдет. Здесь спасают **WebSockets**: один TCP-коннект, обе стороны могут слать сообщения, низкая задержка. Подходит для кастомного networking, синхронизации с веб-панелью, real-time чата с внешним сервисом.

## Базовая схема

`WebSocket` — это обычный класс, не компонент. Привязываешь его к компоненту, который держит его жизненный цикл:

```csharp
public sealed class ApiSocket : Component
{
    [Property] public string Uri { get; set; } = "wss://example.com/ws";
    public WebSocket Socket { get; private set; }

    protected override void OnStart()
    {
        Socket = new WebSocket();
        Socket.OnMessageReceived += HandleMessage;
        Socket.OnDisconnected    += HandleDisconnected;
        _ = ConnectAsync();
    }

    protected override void OnDestroy()
    {
        Socket?.Dispose();
    }

    async Task ConnectAsync()
    {
        await Socket.Connect( Uri );
        await Socket.Send( "hello" );
    }

    void HandleMessage( string msg )      { Log.Info( $"<- {msg}" ); }
    void HandleDisconnected( int code, string reason )
    {
        Log.Warning( $"WS отключён: {code} {reason}" );
    }
}
```

## События

| Событие | Когда |
|---|---|
| `OnMessageReceived(string)` | пришло текстовое сообщение |
| `OnDataReceived(byte[])` | пришли бинарные данные (если используешь) |
| `OnDisconnected(int code, string reason)` | соединение закрылось |

## Отправка сообщений

```csharp
await Socket.Send( "any text" );
await Socket.Send( new byte[] { 1, 2, 3, 4 } );
```

`Send` — асинхронный, но не блокирующий рендер. Если соединение упало — бросит исключение.

## Заголовки и аутентификация

```csharp
var headers = new Dictionary<string, string>
{
    { "Authorization", "Bearer " + token }
};
await Socket.Connect( Uri, headers );
```

Часто токен берут из `Sandbox.Services.Auth.GetToken( "MyService" )` — это **подписанный** токен, проверяемый на твоём бекенде через API Facepunch. Не нужно ничего своего изобретать.

## JSON через WebSocket

WebSocket сам по себе передаёт **строки/байты**. Если хочешь JSON — сериализуй вручную:

```csharp
await Socket.Send( System.Text.Json.JsonSerializer.Serialize( payload ) );
```

…и парсь в `OnMessageReceived`:

```csharp
void HandleMessage( string raw )
{
    var msg = System.Text.Json.JsonSerializer.Deserialize<MyMessage>( raw );
    HandleTyped( msg );
}
```

Удобно сделать дискриминатор-поле `type`, чтобы один сокет нёс много типов сообщений.

## Реконнект

WebSockets закрываются (плохая сеть, перезагрузка сервера). Сделай простую стратегию: при `OnDisconnected` подожди и попробуй снова.

```csharp
async void HandleDisconnected( int code, string reason )
{
    Log.Warning( $"WS lost ({code}): {reason}" );
    await Task.DelaySeconds( 3f );
    if ( !this.IsValid() ) return;
    _ = ConnectAsync();
}
```

> Используй экспоненциальную задержку (3, 6, 12, 24 секунды), иначе при падении сервера ты задолбаешь его повторами.

## Кто открывает сокет?

Как и HTTP, WebSocket к внешнему API в сетевой игре открывает **только хост**. Клиенты в эту дверь не лезут. Если очень нужно «у каждого свой сокет» (например, к их аккаунту на Steam/Discord) — это **личный сокет клиента**, и логику нужно изолировать с помощью `if ( !IsProxy ) ...` (см. [00.22](00_22_Ownership.md)).

## Что НЕ положено

- ❌ Открывать сокеты на каждом клиенте «на всякий случай» — деньги и rate limit твоего бекенда улетят.
- ❌ Использовать WebSocket для game-to-game трафика (между игроками). Для этого есть встроенная сеть s&box (RPC, Sync) — она ходит через peer-to-peer/relay, не нагружая внешнее.
- ❌ Хранить токен/пароль в коде. Используй Auth Tokens.

## Когда нужен WebSocket, а не HTTP

| Задача | Лучше |
|---|---|
| Один раз получить топ-100 лидерборда | HTTP |
| Постоянно слушать обновления чата | WebSocket |
| Отправить статистику матча | HTTP |
| Real-time матчмейкинг с очередью | WebSocket |
| Скачать конфиг при старте | HTTP |

## Что важно запомнить

- `WebSocket` живёт в компоненте; не забудь `Dispose` в `OnDestroy`.
- `Connect`, `Send`, `OnMessageReceived`, `OnDisconnected` — основной API.
- Для авторизации — `Sandbox.Services.Auth.GetToken( ... )` + заголовок `Authorization`.
- Сериализуй JSON вручную; добавь поле `type` для маршрутизации.
- Реконнект — обязателен; делай экспоненциальную задержку.
- Сокет к внешнему API открывает **хост**, не каждый клиент.

## Что дальше?

В [26.14](26_14_Network_Visibility.md) — Network Visibility: как **скрыть** сетевые объекты от тех, кому они не нужны (важно для оптимизации больших миров).
