# Этап 14_14 — Mounts (Подключённые игры)

## Что мы делаем?

Создаём **вкладку Mounts** (в меню она называется «Games») — раздел спавн-меню, позволяющий спавнить модели из **других установленных игр** (например, Half-Life 2, Counter-Strike и т.д.).

Вкладка состоит из двух файлов:
- **MountsPage.cs** — левое меню со списком установленных и недоступных игр;
- **MountContent.razor** + **.scss** — правая панель с виртуальной сеткой моделей выбранной игры.

### Аналогия

Представьте **файловый менеджер**, где в левой колонке — список подключённых дисков (игр), а справа — содержимое выбранного диска. Вы можете искать файлы, менять размер иконок и фильтровать по имени.

---

## Как это работает внутри движка

1. `Sandbox.Mounting.Directory` — API движка для работы с монтируемыми ресурсами других игр. Метод `GetAll()` возвращает все известные игры, а `Mount(ident)` — асинхронно подключает нужную.
2. `BaseGameMount` — объект подключённой игры. У него есть `Resources` (все ресурсы), `RootFolder` (корневая папка), `Title` (название).
3. `ResourceType.Model` — фильтр: нас интересуют только 3D-модели.
4. `ResourceFlags.DeveloperOnly` — флаг для «мусорных» моделей разработчика, которые мы скрываем.
5. `VirtualGrid` рендерит только видимые ячейки — это критично, потому что у одной игры могут быть **тысячи** моделей.
6. `SpawnMenuIcon` — переиспользуемый компонент иконки из предыдущих этапов.
7. `MountSpawner.SerializeMetadata` — сериализует метаданные для создания объекта при спавне.

---

## Файлы этого этапа

| Путь | Назначение |
|------|-----------|
| `Code/UI/SpawnMenu/Mounts/MountsPage.cs` | Левое меню — список игр |
| `Code/UI/SpawnMenu/Mounts/MountContent.razor` | Правая панель — сетка моделей |
| `Code/UI/SpawnMenu/Mounts/MountContent.razor.scss` | Стили для MountContent |

---

## Полный код

### MountsPage.cs

**Путь:** `Code/UI/SpawnMenu/Mounts/MountsPage.cs`

```csharp
/// <summary>
/// This component has a kill icon that can be used in the killfeed, or somewhere else.
/// </summary>
[Title( "Games" ), Order( 2000 ), Icon( "🧩" )]
public class MountsPage : BaseSpawnMenu
{
	protected override void Rebuild()
	{
		var available = Sandbox.Mounting.Directory.GetAll().Where( x => x.Available ).ToArray();
		var unavailable = Sandbox.Mounting.Directory.GetAll().Where( x => !x.Available ).ToArray();

		if ( available.Any() )
		{
			AddHeader( "Local" );

			foreach ( var entry in available.OrderBy( x => x.Title ) )
			{
				AddOption( entry.Title, () => new MountContent() { Ident = entry.Ident } );
			}
		}

		if ( unavailable.Any() )
		{
			AddHeader( "Not Installed" );
			
			foreach ( var entry in unavailable.OrderBy( x => x.Title ) )
			{
				AddOption( entry.Title, null );
			}
		}
	}
}
```

---

### MountContent.razor

**Путь:** `Code/UI/SpawnMenu/Mounts/MountContent.razor`

```razor
@using Sandbox;
@using Sandbox.UI;
@using Sandbox.Mounting;
@namespace Sandbox
@inherits Panel

@if ( mount is null )
{
    <root>
        Error getting mount.
    </root>
    return;
}


<SpawnMenuContent>

    <Header>
        <SpawnMenuToolbar>
			<Left>
				@*
				<Button Icon="home" @onclick="@GoHome"></Button>
				<Button Icon="arrow_upward" Disabled=@( currentDir?.Parent == null ) @onclick="@GoUp"></Button>
				*@
			</left>
			<Body>
				@*
				<SpawnMenuPath Path=@CurrentPath></SpawnMenuPath>
				*@

				<label>@( $"{mount.Title}, {@GetData().Count()} items" )</label>
			</Body>
			<Right>
				<SpawnMenuFilter Query:bind="@Filter"></SpawnMenuFilter>
				<SpawnMenuIconOptions Size:bind="@ItemSize"></SpawnMenuIconOptions>
			</Right>
		</SpawnMenuToolbar>
    </Header>

    <Body>
		
        <VirtualGrid Items=@( GetData() ) ItemSize=@ItemSize>
            <Item Context="item">

				@if (item is ResourceFolder dir)
                {
                    <div class="folder" @onclick=@( () => currentDir = dir )>
						<div><i>folder</i></div>
						<div class="name">@dir.Name</div>
					</div>
                }

                @if (item is ResourceLoader loader)
                {
                    var nameWithoutExt = System.IO.Path.GetFileNameWithoutExtension(loader.Name);
                    <SpawnMenuIcon HideText=@(ItemSize <= 60) Ident=@($"mount:{loader.Path}") Title="@nameWithoutExt" Metadata=@MountSpawner.SerializeMetadata( mount.Title )></SpawnMenuIcon>
                }

            </Item>
        </VirtualGrid>
    </Body>

</SpawnMenuContent>

@code
{
    public string Ident { get; set; }
    public string Filter { get; set; }

    public static int ItemSize { get; set; } = 120;

	ResourceFolder currentDir;

    BaseGameMount mount;

	protected override int BuildHash() => HashCode.Combine(Ident, ItemSize, Filter);

    protected override async Task OnParametersSetAsync()
    {
        mount = await Sandbox.Mounting.Directory.Mount( Ident );
    }

	void GoHome()
	{
		currentDir = mount.RootFolder;
	}

	void GoUp()
	{
		if ( currentDir.Parent != null )
		{
			currentDir = currentDir.Parent;
		}
	}

	string CurrentPath
	{
		get => currentDir?.Path ?? "/";
	}

    IEnumerable<object> GetData()
    {
		var q = mount.Resources
						.Where( x => x.Type == ResourceType.Model )
						.Where( x => !x.Flags.Contains( Sandbox.Mounting.ResourceFlags.DeveloperOnly ) ); // Hide dev-only junk models

		if ( !string.IsNullOrWhiteSpace( Filter ) )
		{
			q = q.Where( x => x.Path.Contains( Filter, StringComparison.OrdinalIgnoreCase ) );
		}

		return q;
    }

}
```

---

### MountContent.razor.scss

**Путь:** `Code/UI/SpawnMenu/Mounts/MountContent.razor.scss`

```scss
@import "/UI/Theme.scss";

MountContent
{
	
}

MountContent > .body
{
	flex-grow: 1;
	position: absolute;
	width: 100%;
	height: 100%;
	top: 0px;
}

.spawnicon
{
	width: 100%;
	height: 100%;
	cursor: pointer;
}

.folder
{
	width: 100%;
	height: 100%;
	flex-direction: column;
	justify-content: space-evenly;
	align-items: center;
	cursor: pointer;
	font-family: $body-font;
	white-space: nowrap;
	overflow: hidden;

	i
	{
		color: #e6db74;
		font-size: 40px;
	}
}
```

---

## Разбор кода

### MountsPage.cs — Левое меню с играми

```csharp
[Title( "Games" ), Order( 2000 ), Icon( "🧩" )]
public class MountsPage : BaseSpawnMenu
```
Атрибуты:
- `Title("Games")` — в меню вкладка называется **Games**, а не Mounts. Это то, что видит игрок.
- `Order(2000)` — позиция в списке вкладок (между Props и Dupes).
- `Icon("🧩")` — иконка-пазл.

```csharp
protected override void Rebuild()
```
Метод заполняет левую панель. Логика:

1. **Получаем все игры** через `Sandbox.Mounting.Directory.GetAll()`.
2. **Разделяем** на `available` (установленные) и `unavailable` (не установленные) по свойству `x.Available`.
3. Для **установленных** — `AddHeader("Local")`, затем для каждой игры `AddOption(entry.Title, ...)`. При клике создаётся `MountContent` с идентификатором игры.
4. Для **не установленных** — `AddHeader("Not Installed")`, затем `AddOption(entry.Title, null)`. Обратите внимание: второй аргумент `null` — кнопка будет **некликабельной** (показывается, но ничего не делает).

```csharp
available.OrderBy( x => x.Title )
```
Сортировка по алфавиту — чтобы игры всегда были в одном порядке.

---

### MountContent.razor — Сетка моделей

Это **основной контент** вкладки. Разберём по частям:

#### Проверка монтирования

```razor
@if ( mount is null )
{
    <root>Error getting mount.</root>
    return;
}
```
Если игра не подключилась (ошибка загрузки, игра удалена) — показываем сообщение об ошибке. `return` прерывает рендер — дальше ничего не рисуется.

#### Панель инструментов (Header)

```razor
<SpawnMenuToolbar>
```
Тулбар содержит три секции:

- **Left** — закомментированные кнопки «Home» и «Up» (навигация по папкам — отключена в текущей версии, код оставлен для будущего использования).
- **Body** — метка с названием игры и количеством элементов: `{mount.Title}, {GetData().Count()} items`.
- **Right** — два компонента:
  - `SpawnMenuFilter` — поле поиска, привязанное к свойству `Filter` через `Query:bind`;
  - `SpawnMenuIconOptions` — ползунок размера иконок, привязанный к `ItemSize` через `Size:bind`.

#### Сетка (Body)

```razor
<VirtualGrid Items=@( GetData() ) ItemSize=@ItemSize>
```

Внутри сетки два типа элементов:

1. **ResourceFolder** — папка. Рисуется как иконка папки (`folder`) с именем. При клике `currentDir = dir` — переход внутрь папки.
2. **ResourceLoader** — ресурс (модель). Рисуется через `SpawnMenuIcon` с параметрами:
   - `HideText` — скрывает текст при маленьком размере (≤60 px);
   - `Ident` — идентификатор для спавна, формат `mount:путь/к/модели`;
   - `Metadata` — сериализованные данные от `MountSpawner`.

#### Code-блок

```csharp
public string Ident { get; set; }
```
Идентификатор игры, передаётся из `MountsPage`.

```csharp
protected override async Task OnParametersSetAsync()
{
    mount = await Sandbox.Mounting.Directory.Mount( Ident );
}
```
**Асинхронное монтирование** игры. `Mount()` возвращает `BaseGameMount` — объект с ресурсами.

```csharp
protected override int BuildHash() => HashCode.Combine(Ident, ItemSize, Filter);
```
Перерисовка происходит только при изменении идентификатора, размера иконок или фильтра.

```csharp
IEnumerable<object> GetData()
```
Фильтрует ресурсы:
1. Только **модели** (`ResourceType.Model`).
2. Исключает **девелоперские** (`ResourceFlags.DeveloperOnly`).
3. Если задан `Filter` — ищет вхождение строки в путь (без учёта регистра).

Функции `GoHome`, `GoUp`, `CurrentPath` — **навигация по папкам** (закомментирована в разметке, но логика готова).

---

### MountContent.razor.scss — Стили

```scss
MountContent > .body
{
    flex-grow: 1;
    position: absolute;
    width: 100%;
    height: 100%;
    top: 0px;
}
```
Тело занимает **всё доступное пространство** панели (абсолютное позиционирование).

```scss
.spawnicon
{
    width: 100%;
    height: 100%;
    cursor: pointer;
}
```
Иконки моделей заполняют ячейку целиком.

```scss
.folder
```
Папки — **вертикальная колонка** с иконкой (жёлтый `folder`, 40px) и именем. `white-space: nowrap` и `overflow: hidden` обрезают длинные имена.

---

## Что проверить

1. Откройте меню спавна — вкладка **Games** (🧩) должна быть видна.
2. В левой панели — разделы **Local** (установленные игры) и **Not Installed**.
3. Кликните на установленную игру — справа должна появиться **сетка моделей**.
4. В заголовке — название игры и **количество моделей**.
5. Введите текст в поле поиска — сетка должна **отфильтроваться** по имени файла.
6. Измените размер иконок ползунком — ячейки должны **уменьшиться/увеличиться**.
7. При маленьком размере (≤60) текст под иконками должен **скрыться**.
8. Не установленные игры — кнопки должны быть **некликабельными**.
9. Кликните на модель — она должна **заспавниться** в мире.
