# 12.05 — Компонент: Состояние морфов (MorphState) 🎭

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.23 — RPC сообщения](00_23_Rpc_Messages.md)

## Что мы делаем?

Создаём **MorphState** — компонент, который сохраняет и восстанавливает значения морфов (blend shapes) модели. Морфы переживают дупликацию, сейвы и передаются по сети через RPC.

## Зачем это нужно?

Морфы позволяют деформировать 3D-модели: менять выражение лица, форму тела, размеры деталей. Когда игрок настраивает морфы через инструмент, эти значения должны:
1. **Сохраняться** — при дупликации и сохранении мира.
2. **Синхронизироваться** — все игроки на сервере видят одинаковые морфы.
3. **Поддерживать пакетное и полное обновление** — можно изменить несколько морфов за раз или сбросить все и применить пресет.

## Как это работает внутри движка?

- `SerializedMorphs` — строка JSON со всеми значениями морфов. `[Property]` обеспечивает сериализацию при дупликации/сохранении.
- `OnStart()` — при старте применяет сохранённые морфы. Это восстанавливает состояние после загрузки дупа/сейва.
- `ApplyBatch()` — применяет частичный набор морфов (только изменённые). Вызывает `Capture()` для сохранения и `BroadcastBatch()` для сетевой рассылки.
- `ApplyPreset()` — сбрасывает все морфы и применяет полный пресет. Аналогично рассылает через `BroadcastPreset()`.
- `Capture()` — снимает текущие значения всех морфов из `SkinnedModelRenderer` и сериализует в JSON.
- `Apply()` — десериализует `SerializedMorphs` и применяет к модели, предварительно сбросив все морфы.
- `[Rpc.Broadcast]` — атрибут движка для методов, которые вызываются на всех клиентах. На хосте пропускаются (`if (Networking.IsHost) return`), т.к. хост уже применил изменения.

## Создай файл

Путь: `Code/Components/MorphState.cs`

```csharp
/// <summary>
/// Persists morph values on a GameObject so they survive over the network and dupes. 
/// </summary>
[Title( "Morph State" )]
[Category( "Rendering" )]
public sealed class MorphState : Component
{
	/// <summary>
	/// Serialized morphs. Persisted for dupes/saves — not synced over the network.
	/// </summary>
	[Property]
	public string SerializedMorphs { get; set; }

	protected override void OnStart()
	{
		Apply();
	}

	/// <summary>
	/// Apply a partial morph batch on the host, then broadcast to all clients.
	/// </summary>
	public void ApplyBatch( string morphsJson )
	{
		var smr = GameObject.GetComponentInChildren<SkinnedModelRenderer>();
		if ( !smr.IsValid() ) return;

		var morphs = Json.Deserialize<Dictionary<string, float>>( morphsJson );
		if ( morphs is null ) return;

		foreach ( var (name, val) in morphs )
			smr.SceneModel.Morphs.Set( name, val );

		Capture( smr );
		BroadcastBatch( morphsJson );
	}

	/// <summary>
	/// Apply a full morph preset on the host (resets all first), then broadcast to all clients.
	/// </summary>
	public void ApplyPreset( string morphsJson )
	{
		var smr = GameObject.GetComponentInChildren<SkinnedModelRenderer>();
		if ( !smr.IsValid() ) return;

		var morphs = Json.Deserialize<Dictionary<string, float>>( morphsJson );
		if ( morphs is null ) return;

		foreach ( var name in smr.Morphs.Names )
			smr.SceneModel.Morphs.Reset( name );

		foreach ( var (name, val) in morphs )
			smr.SceneModel.Morphs.Set( name, val );

		Capture( smr );
		BroadcastPreset( morphsJson );
	}

	[Rpc.Broadcast]
	private void BroadcastBatch( string morphsJson )
	{
		if ( Networking.IsHost ) return;

		var smr = GameObject.GetComponentInChildren<SkinnedModelRenderer>();
		if ( !smr.IsValid() ) return;

		var morphs = Json.Deserialize<Dictionary<string, float>>( morphsJson );
		if ( morphs is null ) return;

		foreach ( var (name, val) in morphs )
			smr.SceneModel.Morphs.Set( name, val );
	}

	[Rpc.Broadcast]
	private void BroadcastPreset( string morphsJson )
	{
		if ( Networking.IsHost ) return;

		var smr = GameObject.GetComponentInChildren<SkinnedModelRenderer>();
		if ( !smr.IsValid() ) return;

		var morphs = Json.Deserialize<Dictionary<string, float>>( morphsJson );
		if ( morphs is null ) return;

		foreach ( var name in smr.Morphs.Names )
			smr.SceneModel.Morphs.Reset( name );

		foreach ( var (name, val) in morphs )
			smr.SceneModel.Morphs.Set( name, val );
	}

	/// <summary>
	/// Snapshot all current morph values from <paramref name="smr"/> into <see cref="SerializedMorphs"/>.
	/// </summary>
	public void Capture( SkinnedModelRenderer smr )
	{
		SerializedMorphs = Json.Serialize( smr.Morphs.Names.ToDictionary( n => n, n => smr.SceneModel?.Morphs.Get( n ) ?? 0f ) );
	}

	/// <summary>
	/// Apply the stored <see cref="SerializedMorphs"/> to the first <see cref="SkinnedModelRenderer"/> we find.
	/// Called on spawn/dupe restore.
	/// </summary>
	public void Apply()
	{
		if ( string.IsNullOrEmpty( SerializedMorphs ) ) return;

		var smr = GameObject.GetComponentInChildren<SkinnedModelRenderer>();
		if ( !smr.IsValid() ) return;

		var morphs = Json.Deserialize<Dictionary<string, float>>( SerializedMorphs );
		if ( morphs is null ) return;

		foreach ( var name in smr.Morphs.Names )
			smr.SceneModel.Morphs.Reset( name );

		foreach ( var (name, val) in morphs )
			smr.SceneModel.Morphs.Set( name, val );
	}
}
```

## Проверка

После создания файла проект должен компилироваться. Компонент будет использоваться инструментом Morph в тул-гане.


---

## ➡️ Следующий шаг

Переходи к **[12.06 — Компонент: Метаданные монтирования (MountMetadata) 🧩](12_06_MountMetadata.md)**.
