using Sandbox.Utility;

public sealed class HoverballMorphs : Component
{
	private HoverballEntity _hoverball;
	private SkinnedModelRenderer _renderer;
	private Material _glowMaterialCopy;

	private float _coils;
	private float _pins;
	private float _brightnessTarget;
	private float _brightnessCurrent;
	private float _brightnessTimer;

	private float _coilsFrom;
	private float _coilsTo;
	private float _coilsTime;
	private float _pinsFrom;
	private float _pinsTo;
	private float _pinsTime;

	[Property] public float Speed { get; set; } = 15f;
	[Property] public float TransitionDuration { get; set; } = 0.5f;
	[Property] public Material GlowMaterial { get; set; }

	public Color IllumTint => Color.FromBytes( 20, 165, 200 );
	public float IllumBrightness => 8f;

	protected override void OnStart()
	{
		_hoverball = GetComponent<HoverballEntity>();
		_renderer = GetComponentInChildren<SkinnedModelRenderer>();

		if ( GlowMaterial is not null && _renderer.IsValid() )
		{
			_glowMaterialCopy = GlowMaterial.CreateCopy();
			_renderer.MaterialOverride = _glowMaterialCopy;
			_renderer.SceneModel.Batchable = false;
		}
	}

	protected override void OnUpdate()
	{
		if ( !_hoverball.IsValid() || !_renderer.IsValid() ) return;

		var targetCoils = _hoverball.IsEnabled ? 1f : 0f;
		var targetPins = Math.Clamp( _hoverball.AirResistance / 5f, 0f, 1f );

		if ( targetCoils != _coilsTo )
		{
			_coilsFrom = _coils;
			_coilsTo = targetCoils;
			_coilsTime = 0f;
		}

		if ( targetPins != _pinsTo )
		{
			_pinsFrom = _pins;
			_pinsTo = targetPins;
			_pinsTime = 0f;
		}

		_coilsTime = Math.Min( _coilsTime + Time.Delta / TransitionDuration, 1f );
		_pinsTime = Math.Min( _pinsTime + Time.Delta / TransitionDuration, 1f );

		_coils = MathX.Lerp( _coilsFrom, _coilsTo, Easing.BounceOut( _coilsTime ) );
		_pins = MathX.Lerp( _pinsFrom, _pinsTo, Easing.BounceOut( _pinsTime ) );

		_renderer.SceneModel?.Morphs.Set( "Coils_Deployed", _coils );
		_renderer.SceneModel?.Morphs.Set( "Pins_Deployed", _pins );

		UpdateGlowMaterial();
	}

	void UpdateGlowMaterial()
	{
		if ( _glowMaterialCopy is null ) return;

		var brightness = _hoverball.IsEnabled ? IllumBrightness : 0f;

		if ( _hoverball.IsEnabled )
		{
			_brightnessTimer -= Time.Delta;
			if ( _brightnessTimer <= 0f )
			{
				_brightnessTarget = Random.Shared.Float( 6f, 8f );
				_brightnessTimer = Random.Shared.Float( 0.1f, 0.4f );
			}
			_brightnessCurrent = MathX.Approach( _brightnessCurrent, _brightnessTarget, Time.Delta * 7f );
			brightness = _brightnessCurrent;
		}

		_glowMaterialCopy.Set( "g_vSelfIllumTint", _hoverball.IsEnabled ? IllumTint : Color.Black );
		_glowMaterialCopy.Set( "g_flSelfIllumBrightness", brightness * _coils );
	}
}
