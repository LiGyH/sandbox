# 22.06 — NPC: Слой сенсоров (SensesLayer) 👁️

<!-- phase05-links -->
> **📚 Основы движка (см. Фаза 0.5):**
>
> - [00.22 — Ownership](00_22_Ownership.md)
> - [00.24 — Sync Properties](00_24_Sync_Properties.md)

## Что мы делаем?

Создаём **SensesLayer** — слой, который сканирует окружение NPC, определяя видимые и слышимые цели.

## Как это работает?

Каждые 100мс (`ScanInterval`) слой:
1. Ищет объекты с целевыми тегами в радиусе слышимости
2. Проверяет дальность для «слышимых» целей
3. Проверяет дальность + линию видимости для «видимых» целей
4. Обновляет списки `VisibleTargets` и `AudibleTargets`

### Линия видимости

Трейс (луч) от «глаз» NPC (64 единицы вверх) к центру цели (32 единицы вверх). Если луч не попал в препятствие или попал в саму цель — видимость есть.

## Создай файл

Путь: `Code/Npcs/Layers/SensesLayer.cs`

```csharp
namespace Sandbox.Npcs.Layers;

/// <summary>
/// Handles awareness and environmental scanning
/// </summary>
public class SensesLayer : BaseNpcLayer
{
	public float ScanInterval { get; set; } = 0.1f; // Scan every 100ms

	[Property]
	public float SightRange { get; set; } = 500f;

	[Property]
	public float HearingRange { get; set; } = 300f;
	public float PersonalSpace { get; set; } = 80f;

	[Property]
	public TagSet TargetTags { get; set; } = ["player"];

	// Current awareness state
	public GameObject Nearest { get; private set; }
	public float DistanceToNearest { get; private set; } = float.MaxValue;
	public List<GameObject> VisibleTargets { get; private set; } = new();
	public List<GameObject> AudibleTargets { get; private set; } = new();

	private TimeSince _lastScan;

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		if ( _lastScan > ScanInterval )
		{
			ScanEnvironment();
			_lastScan = 0;
		}
	}

	public override string GetDebugString()
	{
		if ( VisibleTargets.Count == 0 && AudibleTargets.Count == 0 ) return null;
		return $"Senses: {VisibleTargets.Count} visible, {AudibleTargets.Count} audible";
	}

	/// <summary>
	/// Scan for objects of interest
	/// </summary>
	private void ScanEnvironment()
	{
		// Clear previous scan results
		VisibleTargets.Clear();
		AudibleTargets.Clear();
		Nearest = null;
		DistanceToNearest = float.MaxValue;

		// Find all potential targets in hearing range
		var nearbyObjects = Npc.Scene.FindInPhysics( new Sphere( Npc.WorldPosition, HearingRange ) );

		foreach ( var obj in nearbyObjects )
		{
			if ( !obj.Tags.HasAny( TargetTags ) ) continue;

			var distance = Npc.WorldPosition.Distance( obj.WorldPosition );

			// Track nearest
			if ( distance < DistanceToNearest )
			{
				DistanceToNearest = distance;
				Nearest = obj;
			}

			// Check if within hearing range
			if ( distance <= HearingRange )
			{
				AudibleTargets.Add( obj );
			}

			// Check if within sight range and has line of sight
			if ( distance <= SightRange && HasLineOfSight( obj ) )
			{
				VisibleTargets.Add( obj );
			}
		}
	}

	/// <summary>
	/// Check if we have line of sight to target
	/// </summary>
	private bool HasLineOfSight( GameObject target )
	{
		var eyePosition = Npc.WorldPosition + Vector3.Up * 64f; // Eye height
		var targetPosition = target.WorldPosition + Vector3.Up * 32f; // Target center

		var trace = Npc.Scene.Trace.Ray( eyePosition, targetPosition )
			.IgnoreGameObjectHierarchy( Npc.GameObject )
			.WithoutTags( "trigger" )
			.Run();

		return !trace.Hit || trace.GameObject == target || target.IsDescendant( trace.GameObject );
	}

	/// <summary>
	/// Get the nearest visible
	/// </summary>
	public GameObject GetNearestVisible()
	{
		GameObject nearest = null;
		float nearestDistance = float.MaxValue;

		foreach ( var target in VisibleTargets )
		{
			var distance = Npc.WorldPosition.Distance( target.WorldPosition );
			if ( distance < nearestDistance )
			{
				nearestDistance = distance;
				nearest = target;
			}
		}

		return nearest;
	}

	public override void Reset()
	{
		VisibleTargets.Clear();
		AudibleTargets.Clear();
		Nearest = null;
		DistanceToNearest = float.MaxValue;
	}
}
```

## Разбор кода

| Элемент | Что делает |
|---------|-----------|
| `TargetTags = ["player"]` | По умолчанию NPC реагирует только на игроков |
| `FindInPhysics(Sphere)` | Поиск физических объектов в радиусе |
| `HasLineOfSight` | Трейс с игнорированием собственной иерархии и триггеров |
| `GetNearestVisible()` | Утилита для расписаний — «ближайший видимый враг» |

---

Следующий шаг: [14.10 — NPC: Слой речи (SpeechLayer)](14_10_SpeechLayer.md)


---

# 14.10 — NPC: Слой речи (SpeechLayer) 💬

## Что мы делаем?

Создаём **SpeechLayer** — слой, который управляет речью NPC: воспроизведение звуков, субтитры над головой, кулдаун между фразами.

## Создай файл: SubtitleExtension

Путь: `Code/Npcs/Speech/SubtitleExtension.cs`

```csharp
namespace Sandbox.Npcs;

/// <summary>
/// Extends a SoundFile to add subtitle text that will be shown when the sound is played. This is used for Npc speech.
/// </summary>
[AssetType( Name = "Subtitle", Category = "Sounds", Extension = "ssub" )]
public partial class SubtitleExtension : ResourceExtension<SoundFile, SubtitleExtension>
{
	/// <summary>
	/// The text to show in the subtitle for this sound event. If empty, no subtitle will be shown.
	/// </summary>
	[Property, TextArea] public string Text { get; set; }
}
```

### Что такое ResourceExtension?

`ResourceExtension<SoundFile, SubtitleExtension>` — расширение ресурса. Это позволяет прикрепить дополнительные данные (субтитры) к существующему `.sound` файлу, создавая `.ssub` файл рядом.

## Создай файл: SpeechLayer

Путь: `Code/Npcs/Layers/SpeechLayer.cs`

```csharp
namespace Sandbox.Npcs.Layers;

/// <summary>
/// Manages NPC speech state, plays sound files, and renders subtitle text above their head.
/// </summary>
public class SpeechLayer : BaseNpcLayer
{
	/// <summary>
	/// The subtitle text currently being shown, if any. Synced to all clients.
	/// </summary>
	[Sync] public string CurrentSpeech { get; set; }

	/// <summary>
	/// Whether the NPC is currently speaking.
	/// </summary>
	public bool IsSpeaking => CurrentSpeech is not null;

	/// <summary>
	/// Minimum seconds between speeches.
	/// </summary>
	public float Cooldown { get; set; } = 8f;

	/// <summary>
	/// A generic fallback sound (e.g. a grunt or mumble) played when we're talking without a specific sound.
	/// </summary>
	public SoundEvent FallbackSound { get; set; }

	private SoundHandle _soundHandle;
	private TimeSince _lastSpoke;
	private TimeUntil _subtitleEnd;

	/// <summary>
	/// Whether the cooldown has elapsed and the NPC can speak again.
	/// </summary>
	public bool CanSpeak => _lastSpoke > Cooldown;

	/// <summary>
	/// Play a sound event and show its subtitle (if one exists) above the NPC.
	/// </summary>
	public void Say( SoundEvent sound, float duration = 0f )
	{
		Say( sound, null, duration );
	}

	/// <summary>
	/// Play a sound event with an explicit subtitle override.
	/// </summary>
	public void Say( SoundEvent sound, string subtitle, float duration = 0f )
	{
		if ( sound is null ) return;

		// Stop any existing speech
		Stop();

		// Resolve the next sound file from the event
		var soundFile = Game.Random.FromList( sound.Sounds );
		if ( !soundFile.IsValid() ) return;

		// Play using the event's volume and pitch
		_soundHandle = Sound.PlayFile( soundFile, sound.Volume.GetValue(), sound.Pitch.GetValue() );

		if ( _soundHandle.IsValid() )
		{
			_soundHandle.Parent = Npc.GameObject;
		}

		// Use the explicit subtitle, or fall back to the extension on the resolved sound file
		if ( !string.IsNullOrEmpty( subtitle ) )
		{
			CurrentSpeech = subtitle;
		}
		else
		{
			var ext = SubtitleExtension.FindForResourceOrDefault( soundFile );
			CurrentSpeech = ext?.Text;
		}

		_subtitleEnd = duration;
		_lastSpoke = 0;
	}

	/// <summary>
	/// Say a string message using the fallback sound, with the string shown as a subtitle.
	/// </summary>
	public void Say( string message, float duration = 3f )
	{
		if ( string.IsNullOrEmpty( message ) ) return;

		if ( FallbackSound is not null )
		{
			Say( FallbackSound, message, duration );
		}
		else
		{
			// No fallback sound — just show the subtitle for the duration
			Stop();
			CurrentSpeech = message;
			_subtitleEnd = duration;
			_lastSpoke = 0;
		}
	}

	/// <summary>
	/// Stop any current speech and sound.
	/// </summary>
	public void Stop()
	{
		if ( _soundHandle.IsValid() )
		{
			_soundHandle.Stop();
		}

		CurrentSpeech = null;
	}

	/// <summary>
	/// Whether the sound has finished and the subtitle duration has elapsed.
	/// </summary>
	private bool IsFinished
	{
		get
		{
			var soundDone = !_soundHandle.IsValid() || _soundHandle.IsStopped;
			return soundDone && _subtitleEnd;
		}
	}

	protected override void OnUpdate()
	{
		// Only the host manages speech state (sound playback, duration tracking)
		if ( !IsProxy && CurrentSpeech is not null && IsFinished )
		{
			CurrentSpeech = null;
		}

		// All clients draw the subtitle when speech is active
		if ( CurrentSpeech is not null )
		{
			DrawSpeech();
		}
	}

	/// <summary>
	/// Draw a simple speech bubble above the NPC.
	/// </summary>
	private void DrawSpeech()
	{
		var worldPos = Npc.WorldPosition + Vector3.Up * 80f;
		var screenPos = Npc.Scene.Camera.PointToScreenPixels( worldPos, out var behind );
		if ( behind ) return;

		var text = TextRendering.Scope.Default;
		text.Text = CurrentSpeech;
		text.FontSize = 14;
		text.FontName = "Poppins";
		text.FontWeight = 500;
		text.TextColor = Color.White;
		text.Outline = new TextRendering.Outline { Color = Color.Black.WithAlpha( 0.8f ), Size = 3, Enabled = true };
		text.FilterMode = Rendering.FilterMode.Point;

		Npc.DebugOverlay.ScreenText( screenPos, text, TextFlag.CenterBottom );
	}

	public override void Reset()
	{
		Stop();
	}
}
```

## Разбор кода

| Элемент | Что делает |
|---------|-----------|
| `[Sync] CurrentSpeech` | Субтитры синхронизируются — все клиенты видят текст |
| `CanSpeak` | Кулдаун между фразами (8 секунд по умолчанию) |
| `Say(SoundEvent)` | Воспроизводит звук + показывает субтитры из расширения |
| `Say(string)` | Показывает текст как субтитр, использует fallback-звук |
| `DrawSpeech()` | Рисует текст над головой NPC для всех клиентов |

---

Следующий шаг: [14.11 — NPC: Задачи (Tasks)](14_11_Tasks.md)
