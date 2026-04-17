# Этап 14_12 — Spawnlists

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.26 — Razor UI](00_26_Razor_Basics.md)

## Что это такое?

**Spawnlists** — это вкладка меню спавна, позволяющая создавать пользовательские коллекции предметов, управлять ими и делиться через мастерскую.

### Аналогия

Представьте **плейлисты в музыкальном приложении**: вы создаёте свой список, добавляете любимые песни (предметы), переименовываете, удаляете, и даже делитесь ими с друзьями. Spawnlists работают точно так же — только вместо песен в них пропы, сущности и другие игровые объекты.

---

## Исходный код

### SpawnlistsPage.cs

```csharp
using Sandbox.UI;

namespace Sandbox;

/// <summary>
/// Top-level spawn menu tab for user-created spawnlists.
/// </summary>
[Title( "Spawnlists" ), Order( 1000 ), Icon( "📋" )]
public class SpawnlistsPage : BaseSpawnMenu
{
	public SpawnlistCollection Collection { get; } = new();

	public SpawnlistsPage()
	{
		Collection.Changed += () =>
		{
			if ( Collection.Entries.Count == 0 )
				_firstViewed = false;

			OnParametersSet();
		};
		Collection.Installed += name =>
		{
			OnParametersSet();
			SelectOption( name );
		};
		Collection.Uninstalled += () =>
		{
			DeselectOption();
			OnParametersSet();
		};
		SpawnlistData.SpawnlistCreated += Collection.Refresh;
		Collection.Refresh();
	}

	protected override void Rebuild()
	{
		if ( Collection.Entries.Count > 0 || Collection.PendingCount > 0 )
		{
			AddHeader( "Local" );

			foreach ( var entry in Collection.Entries )
			{
				var captured = entry;
				AddOption( entry.Icon, entry.Name,
					() => new SpawnlistView { Entry = captured.StorageEntry },
					entry.IsEditable
						? () => OnEditableRightClick( captured )
						: () => OnInstalledRightClick( captured ) );
			}

			AddSkeletons( Collection.PendingCount );
		}

		AddHeader( "Workshop" );
		AddOption( "🎖️", "Popular", () => new SpawnlistWorkshop { SortOrder = Storage.SortOrder.RankedByVote } );
		AddOption( "🐣", "Newest", () => new SpawnlistWorkshop { SortOrder = Storage.SortOrder.RankedByPublicationDate } );
	}

	protected override void OnMenuFooter( Panel footer )
	{
		footer.AddChild<SpawnlistFooter>();
	}

	/// <summary>Refresh after external changes (create, etc.).</summary>
	public void RefreshList() => Collection.Refresh();

	void OnEditableRightClick( SpawnlistCollection.Entry entry )
	{
		var menu = MenuPanel.Open( this );

		menu.AddOption( "edit", "Rename", () =>
		{
			var data = SpawnlistData.Load( entry.StorageEntry );
			var popup = new StringQueryPopup
			{
				Title = "Rename Spawnlist",
				Prompt = "Enter a new name for your spawnlist.",
				Placeholder = "Spawnlist name...",
				ConfirmLabel = "Rename",
				InitialValue = data.Name,
				OnConfirm = newName =>
				{
					SpawnlistData.Rename( entry.StorageEntry, newName );
					Collection.Refresh();
				}
			};
			popup.Parent = FindPopupPanel();
		} );

		menu.AddOption( "delete", "Delete", () => Collection.Delete( entry.StorageEntry ) );
	}

	void OnInstalledRightClick( SpawnlistCollection.Entry entry )
	{
		var menu = MenuPanel.Open( this );
		menu.AddOption( "delete", "Remove", () => Collection.Uninstall( entry.WorkshopId ) );
	}
}
```

### SpawnlistCollection.cs

```csharp
namespace Sandbox;

/// <summary>
/// Class that handles all the spawnlist data fetching and workshop fetching so it's not a huge mess everywhere.
/// </summary>
public class SpawnlistCollection
{
	public record Entry(
		string Icon,
		string Name,
		Storage.Entry StorageEntry,
		ulong WorkshopId,
		bool IsEditable
	);

	/// <summary>
	/// Raised whenever the visible entry list changes.
	/// </summary>
	public event Action Changed;

	/// <summary>
	/// Raised after a workshop install, with the installed entry name.
	/// </summary>
	public event Action<string> Installed;

	/// <summary>
	/// Raised after an uninstall.
	/// </summary>
	public event Action Uninstalled;

	public IReadOnlyList<Entry> Entries => _entries;

	/// <summary>
	/// Number of cookie-tracked workshop entries not yet resolved from storage or the cloud.
	/// Non-zero while the initial cloud fetch is in progress.
	/// </summary>
	public int PendingCount
	{
		get
		{
			if ( !_loading ) return 0;
			var loaded = _entries.Select( e => e.WorkshopId ).Where( id => id > 0 ).ToHashSet();
			return new SavedSpawnlists().Installed.Count( id => !loaded.Contains( id ) );
		}
	}

	List<Entry> _entries = new();
	Dictionary<ulong, Storage.Entry> _cloudEntries = new();
	bool _queried;
	bool _loading;

	/// <summary>
	/// Rebuilds the visible list and triggers a cloud re-fetch.
	/// </summary>
	public void Refresh()
	{
		_queried = false;
		Rebuild();
	}

	/// <summary>
	/// Install a workshop item, persist its ID to cookie, and refresh.
	/// </summary>
	public async Task InstallAsync( Storage.QueryItem item )
	{
		var entry = await item.Install();
		if ( entry is null ) return;

		var saved = new SavedSpawnlists();
		saved.Add( item.Id );
		saved.Save();

		Installed?.Invoke( item.Title );
		Refresh();
	}

	/// <summary>
	/// Uninstall a workshop-installed entry: remove from cookie, cloud list, and rebuild.
	/// </summary>
	public void Uninstall( ulong workshopId )
	{
		if ( workshopId == 0 ) return;

		var saved = new SavedSpawnlists();
		if ( saved.Remove( workshopId ) )
			saved.Save();

		_cloudEntries.Remove( workshopId );
		Uninstalled?.Invoke();
		Rebuild();
	}

	/// <summary>
	/// Delete a locally editable spawnlist and rebuild.
	/// </summary>
	public void Delete( Storage.Entry entry )
	{
		try
		{
			SpawnlistData.Delete( entry );
		}
		catch ( Exception e )
		{
			Log.Warning( e, $"Something went wrong while deleting an entry: {e.Message}" );
		}
		finally
		{
			Refresh();
		}
	}

	struct SavedSpawnlists
	{
		public List<ulong> Installed { get; set; }

		public SavedSpawnlists()
		{
			Installed = Game.Cookies.Get<List<ulong>>( "spawnlists.installed", new() );
		}

		public void Save() => Game.Cookies.Set( "spawnlists.installed", Installed );

		public void Add( ulong id ) { if ( !Installed.Contains( id ) ) Installed.Add( id ); }
		public bool Remove( ulong id ) => Installed.Remove( id );
		public HashSet<ulong> ToHashSet() => Installed.ToHashSet();
	}

	void Rebuild()
	{
		var installedIds = new SavedSpawnlists().ToHashSet();
		var result = new List<Entry>();

		foreach ( var storageEntry in SpawnlistData.GetAll() )
		{
			SpawnlistData data;

			try
			{
				if ( storageEntry.Files.IsReadOnly ) continue;
				data = SpawnlistData.Load( storageEntry );
			}
			catch
			{
				continue;
			}

			result.Add( new Entry( "📁", data.Name, storageEntry, 0, true ) );
		}

		foreach ( var (workshopId, storageEntry) in _cloudEntries )
		{
			if ( !installedIds.Contains( workshopId ) ) continue;

			var data = SpawnlistData.Load( storageEntry );
			result.Add( new Entry( "☁️", data.Name, storageEntry, workshopId, false ) );
		}

		_entries = result;

		if ( !_queried ) _loading = true;

		Changed?.Invoke();

		if ( !_queried )
		{
			_queried = true;
			_ = FetchCloudSpawnlists();
		}
	}

	async Task FetchCloudSpawnlists()
	{
		var query = new Storage.Query();
		query.KeyValues["package"] = "facepunch.sandbox";
		query.KeyValues["type"] = "spawnlist";
		query.Author = Game.SteamId;

		var result = await query.Run();

		if ( result?.Items is not null )
		{
			foreach ( var item in result.Items )
			{
				if ( _cloudEntries.ContainsKey( item.Id ) ) continue;

				var installed = await item.Install();
				if ( installed == null ) continue;

				_cloudEntries[item.Id] = installed;
			}
		}

		var missingIds = new SavedSpawnlists().Installed
			.Where( id => !_cloudEntries.ContainsKey( id ) )
			.ToList();

		if ( missingIds.Count > 0 )
		{
			var missingResult = await new Storage.Query { FileIds = missingIds }.Run();
			if ( missingResult?.Items is not null )
			{
				foreach ( var item in missingResult.Items )
				{
					if ( _cloudEntries.ContainsKey( item.Id ) ) continue;

					var installed = await item.Install();
					if ( installed == null ) continue;

					_cloudEntries[item.Id] = installed;
				}
			}
		}

		_loading = false;
		Rebuild();
	}
}
```

### SpawnlistData.cs

```csharp
namespace Sandbox;

using Sandbox.UI;
using System.Text.Json.Serialization;

/// <summary>
/// A spawnlist item -- lots of cleanup needed, docs, etc
/// </summary>
public class SpawnlistItem
{
	[JsonPropertyName( "ident" )]
	public string Ident { get; set; }

	[JsonPropertyName( "title" )]
	public string Title { get; set; }

	[JsonPropertyName( "icon" )]
	public string Icon { get; set; }

	public static string MakeIdent( string type, string path, string source = "local" )
	{
		// TODO: hate this special case
		if ( type == "dupe" )
			return $"dupe.{source}:{path}";

		return $"{type}:{path}";
	}

	public static (string Type, string Path, string Source) ParseIdent( string ident )
	{
		if ( string.IsNullOrEmpty( ident ) )
			return (null, null, "local");

		var colonIndex = ident.IndexOf( ':' );
		if ( colonIndex < 0 )
			return (ident, ident, "local");

		var prefix = ident[..colonIndex];
		var data = ident[(colonIndex + 1)..];

		// TODO: hate this special case
		if ( prefix.StartsWith( "dupe." ) )
		{
			var source = prefix["dupe.".Length..];
			return ("dupe", data, source);
		}

		return (prefix, data, "local");
	}
}

public class SpawnlistData
{
	/// <summary>
	/// Raised whenever a new spawnlist is created, so UI can refresh without needing a panel ancestor walk.
	/// </summary>
	public static event Action SpawnlistCreated;

	[JsonPropertyName( "name" )]
	public string Name { get; set; } = "Untitled";

	[JsonPropertyName( "description" )]
	public string Description { get; set; } = "";

	[JsonPropertyName( "items" )]
	public List<SpawnlistItem> Items { get; set; } = new();

	public static SpawnlistData Create( string name )
	{
		var data = new SpawnlistData { Name = name };
		var entry = Storage.CreateEntry( "spawnlist" );
		entry.SetMeta( "name", name );
		Save( entry, data );
		SpawnlistCreated?.Invoke();
		return data;
	}

	public static void Save( Storage.Entry entry, SpawnlistData data )
	{
		entry.Files.WriteJson( "/spawnlist.json", data );
		entry.SetMeta( "name", data.Name );
		entry.SetMeta( "item_count", data.Items.Count.ToString() );
	}

	public static SpawnlistData Load( Storage.Entry entry )
	{
		if ( !entry.Files.FileExists( "/spawnlist.json" ) )
			return new SpawnlistData { Name = entry.GetMeta<string>( "name" ) ?? "Untitled" };

		return entry.Files.ReadJson<SpawnlistData>( "/spawnlist.json" )
			?? new SpawnlistData { Name = "Untitled" };
	}

	public static IEnumerable<Storage.Entry> GetAll()
	{
		return Storage.GetAll( "spawnlist" ).OrderByDescending( x => x.Created );
	}

	public static void Rename( Storage.Entry entry, string newName )
	{
		var data = Load( entry );
		data.Name = newName;
		Save( entry, data );
	}

	public static void Delete( Storage.Entry entry )
	{
		entry.Delete();
	}

	public static void Publish( Storage.Entry entry )
	{
		var options = new Modals.WorkshopPublishOptions { Title = "My Spawnlist" };
		entry.Publish( options );
	}

	public static void AddItem( Storage.Entry entry, SpawnlistItem item )
	{
		var data = Load( entry );
		data.Items.Add( item );
		Save( entry, data );
	}

	public static void RemoveItem( Storage.Entry entry, int index )
	{
		var data = Load( entry );
		if ( index >= 0 && index < data.Items.Count )
		{
			data.Items.RemoveAt( index );
			Save( entry, data );
		}
	}

	public static void PopulateContextMenu( MenuPanel menu, SpawnlistItem item, Storage.Entry skipEntry = null )
	{
		var entries = GetAll()
			.Where( e => skipEntry is null || e.Id != skipEntry.Id )
			.ToList();

		if ( entries.Count > 0 )
		{
			menu.AddSubmenu( "📋", "Add to Spawnlist", sub =>
			{
				foreach ( var entry in entries )
				{
					var data = Load( entry );
					var capturedEntry = entry;
					sub.AddOption( "📋", data.Name, () => AddItem( capturedEntry, item ) );
				}
			} );

			menu.AddSpacer();
		}

		menu.AddOption( "➕", "Create New Spawnlist", () =>
		{
			Create( item.Title ?? "New Spawnlist" );
			var created = GetAll().FirstOrDefault();
			if ( created is not null )
				AddItem( created, item );
		} );
	}
}
```

### SpawnlistView.razor

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox

@{
    var data = GetData();
}

<SpawnMenuContent>

    <Header>
        <SpawnMenuToolbar>
            <Left>
                <h2>@data.Name</h2>
            </Left>
            <Right>
                @{
                    var workshopId = Entry.GetMeta( "_workshopId", 0u );
                    var isPublished = workshopId > 0;
                }
                <div class="menu-icon-toggle-group">
                    @if ( isPublished )
                    {
                        <IconPanel Tooltip="Copy Url" Text="link" @onclick=@( () => Clipboard.SetText( $"https://steamcommunity.com/sharedfiles/filedetails/?id={workshopId}" ) ) />
                        <IconPanel Tooltip="Sync" Text="sync" @onclick=@( () => OnPublish() ) />
                    }
                    else
                    {
                        <IconPanel Tooltip="Publish" Text="cloud_upload" @onclick=@( () => OnPublish() ) />
                    }
                    @if ( !Entry.Files.IsReadOnly )
                    {
                        <IconPanel Tooltip="Delete" Text="delete" @onclick=@( () => OnDelete() ) />
                    }
                    else
                    {
                        <IconPanel Tooltip="Remove" Text="delete" @onclick=@( () => OnUninstall() ) />
                    }
                </div>
            </Right>
        </SpawnMenuToolbar>
    </Header>

    <Body>
        @if ( data.Items.Count == 0 )
        {
            <div class="empty-state">
                <p>This spawnlist is empty.</p>
                <p>Right-click items in other tabs to add them here.</p>
            </div>
        }
        else
        {
            <VirtualGrid Items=@data.Items ItemSize=@(120)>
                <Item Context="item">
                    @if ( item is SpawnlistItem spawnItem )
                    {
                        <SpawnMenuIcon Ident="@spawnItem.Ident" Title="@spawnItem.Title" Icon="@spawnItem.Icon" />
                    }
                </Item>
            </VirtualGrid>
        }
    </Body>

</SpawnMenuContent>

@code
{
    public Storage.Entry Entry { get; set; }

    SpawnlistData _cachedData;
    RealTimeSince _lastRefresh;

    SpawnlistData GetData()
    {
        if ( _cachedData == null )
            RefreshCache();

        return _cachedData;
    }

    public void RefreshCache()
    {
        _cachedData = SpawnlistData.Load( Entry );
        _lastRefresh = 0;
    }

    public override void Tick()
    {
        base.Tick();

        // Periodically check for changes from other tabs
        if ( _lastRefresh > 1f )
        {
            var fresh = SpawnlistData.Load( Entry );
            if ( fresh.Items.Count != _cachedData?.Items?.Count )
            {
                _cachedData = fresh;
                StateHasChanged();
            }
            _lastRefresh = 0;
        }
    }

    protected override int BuildHash() => HashCode.Combine( _cachedData?.Items?.Count );

    void OnPublish()
    {
        if ( Entry is null ) return;
        SpawnlistData.Publish( Entry );
    }

    void OnDelete()
    {
        if ( Entry is null ) return;
        var page = Ancestors.OfType<SpawnlistsPage>().FirstOrDefault();
        page?.Collection.Delete( Entry );
    }

    void OnUninstall()
    {
        if ( Entry is null ) return;
        var workshopId = Entry.GetMeta( "_workshopId", 0ul );
        var page = Ancestors.OfType<SpawnlistsPage>().FirstOrDefault();
        page?.Collection.Uninstall( workshopId );
    }
}
```

### SpawnlistView.razor.scss

```scss
SpawnlistView
{
	.empty-state
	{
		display: flex;
		flex-direction: column;
		align-items: center;
		justify-content: center;
		height: 100%;
		width: 100%;
		opacity: 0.5;
		gap: 4px;

		p
		{
			font-size: 14px;
		}
	}

	h2
	{
		font-size: 16px;
		font-weight: 600;
		margin: 0;
	}
}
```

### SpawnlistWorkshop.razor

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox

<SpawnMenuContent>

    <Body>
        <VirtualList Items=@Items ItemHeight=@(48) OnLastCell="@(() => { _ = QueryNext(); })">
            <Item Context="context">
                @if (context is Storage.QueryItem item && GetItemCount( item ) > 0)
                {
                    <div class="spawnlist-row" onclick=@( () => _ = OnInstall( item ) )>
                        <div class="avatar" style="background-image: url('@item.Owner.Avatar');"></div>
                        <div class="info">
                            <div class="name">@item.Title</div>
                            <div class="author">By @item.Owner.Name</div>
                        </div>

                        <div class="item-count">@GetItemCount( item ) items</div>
                    </div>
                }
            </Item>
        </VirtualList>
    </Body>

</SpawnMenuContent>

@code
{
    public Storage.SortOrder SortOrder { get; set; } = Storage.SortOrder.RankedByVote;

    protected override async Task OnParametersSetAsync()
    {
        Items.Clear();
        LastResult = null;
        await QueryNext();
    }

    List<Storage.QueryItem> Items = new();
    Storage.QueryResult LastResult;

    async Task QueryNext()
    {
        if ( LastResult != null )
        {
            if ( !LastResult.HasMoreResults() )
                return;

            LastResult = await LastResult.GetNextResults();
            if ( LastResult.Items == null ) return;

            Items.AddRange( LastResult.Items );
            StateHasChanged();
            return;
        }

        var query = new Storage.Query();
        query.KeyValues["package"] = "facepunch.sandbox";
        query.KeyValues["type"] = "spawnlist";
        query.SortOrder = SortOrder;

        LastResult = await query.Run();
        if ( LastResult.Items == null ) return;

        Items.AddRange( LastResult.Items );
        StateHasChanged();
    }

    async Task OnInstall( Storage.QueryItem item )
    {
        var page = Ancestors.OfType<SpawnlistsPage>().FirstOrDefault();
        if ( page is null ) return;

        await page.Collection.InstallAsync( item );
    }

    int GetItemCount( Storage.QueryItem item )
    {
        try
        {
            var doc = Json.ParseToJsonObject( item.Metadata );
            return int.Parse( doc["Meta"]["item_count"]?.ToString()?.Trim( '"' ) ?? "0" );
        }
        catch { }

        return 0;
    }
}
```

### SpawnlistWorkshop.razor.scss

```scss
@import "/UI/Theme.scss";

VirtualList
{
	width: 100%;
	flex-grow: 1;

	.cell
	{
		width: 100%;
	}
}

.spawnlist-row
{
	display: flex;
	flex-direction: row;
	align-items: center;
	gap: 12px;
	padding: 8px 12px;
	cursor: pointer;
	border-radius: 4px;
	width: 100%;
	height: 48px;

	&:hover
	{
		background-color: #7773;
		sound-in: ui.button.over;
	}

	&:active
	{
		background-color: $menu-accent;
	}

	.avatar
	{
		width: 32px;
		height: 32px;
		border-radius: 50%;
		background-size: cover;
		flex-shrink: 0;
	}

	.info
	{
		display: flex;
		flex-direction: column;
		flex-grow: 1;
		gap: 2px;

		.name
		{
			font-size: 14px;
			font-weight: 600;
		}

		.author
		{
			font-size: 12px;
			opacity: 0.5;
		}
	}

	.item-count
	{
		font-size: 12px;
		opacity: 0.5;
		flex-shrink: 0;
	}
}
```

### SpawnlistCreatePopup.razor

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox

<root class="popup">

    <div class="inner">
        <div class="popup-header">
            <h2>Create a Spawnlist</h2>
        </div>

        <h3>Give your new spawnlist a name.</h3>

        <TextEntry class="menu-input" Placeholder="Spawnlist name..." Value:bind=@Name />

        <span class="grow">
            <Button Text="Create" Icon="add" class="menu-action primary" Disabled=@(string.IsNullOrWhiteSpace(Name)) @onclick=@OnCreate />
            <Button Text="Cancel" Icon="close" class="menu-action primary" @onclick=@(() => Delete() ) />
        </span>
    </div>
</root>

@code
{

    public SpawnlistCreatePopup() { }

    public Action OnCreated { get; set; }

    string Name { get; set; } = "";

    void OnCreate()
    {
        if ( string.IsNullOrWhiteSpace( Name ) ) return;

        SpawnlistData.Create( Name.Trim() );

        Notices.AddNotice( "list_alt", Color.Cyan, $"Created spawnlist '{Name}'" );

        OnCreated?.Invoke();

        Delete();
    }
}
```

### SpawnlistCreatePopup.razor.scss

```scss
@import "/UI/Popup.scss";

SpawnlistCreatePopup .inner
{
	width: 800px;
}
```

### SpawnlistFooter.razor

```razor
@using Sandbox;
@using Sandbox.UI;
@using Sandbox.Mounting;
@inherits Panel
@namespace Sandbox

<root>

<Button class="menu-action primary wide" Icon="➕" Text="New Spawnlist" Disabled=@( !CanCreate() ) @onclick=@CreatePopup></Button>

</root>
```

### SpawnlistFooter.razor.cs

```csharp
using Sandbox.UI;
namespace Sandbox;

public partial class SpawnlistFooter : Panel
{
	protected override int BuildHash() => HashCode.Combine( CanCreate() );

	bool CanCreate()
	{
		return true;
	}

	void CreatePopup()
	{
		var popup = new SpawnlistCreatePopup();
		popup.Parent = FindPopupPanel();
		popup.OnCreated = () => Ancestors.OfType<SpawnlistsPage>().FirstOrDefault()?.RefreshList();
	}

	void Refresh()
	{
		Ancestors.OfType<SpawnlistsPage>().FirstOrDefault()?.RefreshList();
	}
}
```

### SpawnlistFooter.razor.scss

```scss
SpawnlistFooter
{
    width: 100%;
    flex-shrink: 0;
    flex-direction: column;
    padding: 8px;
    gap: 4px;
}

.menu-action
{
    padding: 8px 16px;
    gap: 1rem;
}
```

### SpawnlistSkeleton.razor

```razor
@using Sandbox;
@using Sandbox.UI;
@inherits Panel
@namespace Sandbox

<root>
	<div class="shimmer"></div>
</root>
```

### SpawnlistSkeleton.razor.scss

```scss
@import "/UI/Theme.scss";

@keyframes skeleton-shimmer
{
    0%   { transform: translateX( -100% ); }
    40%  { transform: translateX( 200% ); }
    100% { transform: translateX( 200% ); }
}

SpawnlistSkeleton
{
    align-self: stretch;
    height: 22px;
    border-radius: 5px;
    margin: 2px 0;
    background-color: rgba( 255, 255, 255, 0.08 );
    overflow: hidden;
    flex-direction: row;
    flex-shrink: 0;
}

SpawnlistSkeleton .shimmer
{
    width: 100%;
    height: 100%;
    background: linear-gradient(
        to right,
        rgba( 255, 255, 255, 0 ) 0%,
        rgba( 255, 255, 255, 0.15 ) 50%,
        rgba( 255, 255, 255, 0 ) 100%
    );
    animation: skeleton-shimmer 2.5s linear infinite;
}
```

---

## Разбор важных частей кода

### SpawnlistsPage — точка входа вкладки

- **`[Title("Spawnlists"), Order(1000), Icon("📋")]`** — атрибуты: название, порядок (между Props и Entity), иконка.
- **`SpawnlistCollection Collection`** — объект, управляющий загрузкой, установкой и удалением спавнлистов.
- **События коллекции**: `Changed` перестраивает UI, `Installed` автоматически выбирает установленный спавнлист, `Uninstalled` сбрасывает выбор.
- **`Rebuild()`** — строит левое меню: локальные спавнлисты (с иконкой 📁), облачные (☁️), а также секцию Workshop (Popular, Newest).
- **`OnEditableRightClick()`** — контекстное меню для своих спавнлистов: переименовать или удалить.
- **`OnInstalledRightClick()`** — контекстное меню для установленных из мастерской: только удаление.

### SpawnlistCollection — менеджер данных

- **`Entry`** — record с полями: иконка, имя, ссылка на Storage, ID мастерской, флаг редактируемости.
- **`Refresh()`** — сбрасывает флаг запроса и перестраивает список.
- **`InstallAsync()`** — устанавливает спавнлист из мастерской, сохраняет его ID в куки.
- **`Uninstall()`** — удаляет ID из куки и из облачного кэша.
- **`SavedSpawnlists`** — вложенная структура для работы с куками: хранит список установленных workshop ID.
- **`FetchCloudSpawnlists()`** — асинхронно загружает спавнлисты автора из облака и докачивает недостающие по ID.
- **`PendingCount`** — показывает, сколько спавнлистов ещё загружается (для отображения скелетонов).

### SpawnlistData — модель данных спавнлиста

- **`SpawnlistItem`** — один элемент списка с полями `Ident`, `Title`, `Icon`.
- **`MakeIdent()` / `ParseIdent()`** — утилиты для формирования и разбора идентификаторов формата `type:path`.
- **`Create()`** — создаёт новый спавнлист в Storage и вызывает событие `SpawnlistCreated`.
- **`Save()` / `Load()`** — сериализация/десериализация в JSON-файл `/spawnlist.json`.
- **`AddItem()` / `RemoveItem()`** — добавление/удаление элемента по индексу.
- **`PopulateContextMenu()`** — заполняет контекстное меню опциями «Добавить в спавнлист» для всех существующих списков.
- **`Publish()`** — публикует спавнлист в мастерскую Steam.

### SpawnlistView — отображение содержимого спавнлиста

- **`GetData()`** — кэширует данные спавнлиста; обновляет каждую секунду через `Tick()`.
- **Тулбар**: название списка слева; справа — кнопки Publish/Sync, Copy URL и Delete/Remove.
- **Пустое состояние**: если в списке нет элементов, показывает подсказку «Right-click items in other tabs to add them here.»
- **`OnPublish()` / `OnDelete()` / `OnUninstall()`** — делегируют действия родительской странице `SpawnlistsPage`.

### SpawnlistWorkshop — браузер мастерской

- **`VirtualList`** — виртуализированный список (не сетка); каждый элемент — строка высотой 48px.
- **`OnLastCell`** — при прокрутке до конца автоматически подгружает следующую страницу (`QueryNext()`).
- **`GetItemCount()`** — парсит JSON-метаданные пакета для извлечения количества элементов.
- **`OnInstall()`** — устанавливает выбранный спавнлист через `Collection.InstallAsync()`.

### SpawnlistCreatePopup — попап создания

- **`TextEntry`** — поле ввода имени нового спавнлиста.
- **`Disabled=@(string.IsNullOrWhiteSpace(Name))`** — кнопка «Create» неактивна, пока имя пустое.
- **`SpawnlistData.Create(Name.Trim())`** — создаёт спавнлист и вызывает уведомление.
- **`OnCreated`** — колбэк для обновления родительского списка.

### SpawnlistFooter — кнопка «New Spawnlist»

- **`CreatePopup()`** — открывает попап создания спавнлиста.
- **`FindPopupPanel()`** — находит ближайший контейнер для попапов в дереве UI.
- **`RefreshList()`** — после создания обновляет список в `SpawnlistsPage`.

### SpawnlistSkeleton — анимация загрузки

- **Скелетон** — заглушка, которая отображается пока спавнлисты загружаются из облака.
- **`skeleton-shimmer`** — CSS-анимация «мерцания»: полупрозрачный градиент плавно двигается слева направо.
- **`background-color: rgba(255,255,255,0.08)`** — еле заметный фон, имитирующий строку списка.


---

## ➡️ Следующий шаг

Переходи к **[14.13 — Этап 14_13 — Dupes (Дубликаты)](14_13_Dupes.md)**.
