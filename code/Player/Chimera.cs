﻿using Sandbox;
using System;
using System.Linq;

partial class PlayerBase
{
	public bool ActiveChimera;

	private TimeSince timeLastBite;
	private TimeSince timeLastFlew;
	private TimeSince timeLastRoar;
	private TimeSince timeLastTailwhip;

	[Net]
	public float ChimeraStaminaAmount { get; private set; } = 200.0f;

	[Net]
	public float ChimeraTailStaminaAmount { get; private set; } = 100.0f;

	public void SpawnAsChimera()
	{
		CurrentTeam = TeamEnum.Chimera;

		SetModel( "models/player/chimera/chimera.vmdl" );

		CameraMode = new UCHChimeraCamera();
		Controller = new ChimeraController();

		timeLastBite = 0;

		ActiveChimera = true;

		EnableHitboxes = true;
		EnableDrawing = true;
		EnableHideInFirstPerson = false;
		EnableShadowInFirstPerson = false;
		EnableAllCollisions = true;

		using ( Prediction.Off() )
			Game.Current.PlaySoundToClient( To.Single( this ), "chimera_spawn" );

		var spawnpoints = Entity.All.OfType<ChimeraSpawn>();
		var randomSpawnPoint = spawnpoints.OrderBy( x => Guid.NewGuid() ).FirstOrDefault();

		if ( randomSpawnPoint == null )
		{
			Log.Error( "THIS MAP ISN'T SUPPORTED FOR ULTIMATE CHIMERA HUNT" );
			base.Respawn();
			return;
		}

		Position = randomSpawnPoint.Position;
	}

	//When the button on the chimera's back is pressed, turn off the chimera
	public void BackButtonPressed()
	{
		Sound.FromEntity( "button_press", this);
		ActiveChimera = false;
		EnableDrawing = false;
		EnableAllCollisions = false;

		OnKilled();
		BecomeChimeraRagdollClient( Velocity );
	}

	public bool CanBite()
	{
		if ( timeLastBite >= 1.25f && GroundEntity != null)
			return true;

		return false;
	}

	public bool CanUseStamina()
	{
		if ( ChimeraStaminaAmount > 0.0f )
			return true;

		return false;
	}
	public bool CanTailwhip()
	{
		if ( ChimeraTailStaminaAmount < 50.0f || timeLastTailwhip < 0.8f)
			return false;

		return true;
	}

	public bool CanFly()
	{
		if ( timeLastFlew < 0.3f )
			return false;

		return true;
	}

	public void FlyUpwards()
	{
		if ( !CanUseStamina() )
			return;

		if ( !CanFly() )
			return;

		timeLastFlew = 0.0f;
		ChimeraStaminaAmount -= 25.0f;
		timeLastSprinted = 0;
		Velocity += Vector3.Up * 220;

		using(Prediction.Off())
			Sound.FromEntity( "double_jump", this );
	}

	public void Roar()
	{
		CanMove = false;

		ChimeraStaminaAmount = 0.0f;
		timeLastSprinted = 0;
		timeLastRoar = 0;

		using ( Prediction.Off() )
			Sound.FromEntity( "roar", this );

		SetAnimParameter( "b_roar", true );
	}


	public void Tailwhip()
	{
		if ( !CanTailwhip() )
			return;

		using ( Prediction.Off() )
			Sound.FromEntity( "tailwhip", this );

		SetAnimParameter( "b_tailwhip", true );

		var tr = Trace.Sphere( 64, EyePosition, EyePosition - EyeRotation.Forward * 105 )
			.Size( 2 )
			.Ignore( this )
			.Run();

		if ( !tr.Hit || tr.StartedSolid )
			return;

		var totalEnts = FindInSphere( tr.EndPosition, 122 );
		foreach ( var ent in totalEnts )
		{
			if ( ent is not PlayerBase || ent == this )
				continue;

			if ( ent is PlayerBase player )
				if ( player.CurrentTeam == TeamEnum.Pigmask )
				{
					player.Position += Vector3.Up * 35;
					player.Velocity += Vector3.Up * 1000;
					player.isWhipped = true;
					player.timeLastWhipped = 0;
				}
		}

		ChimeraTailStaminaAmount -= 50.0f;
		timeLastTailwhip = 0;
	}

	public void Bite()
	{
		if ( Game.Current.CurrentRoundStatus != Game.RoundEnum.Active ) return;

		using ( Prediction.Off() )
			Sound.FromEntity( "bite", this );

		SetAnimParameter( "b_bite", true );

		timeLastBite = 0;
		CanMove = false;

		var tr = Trace.Sphere( 64, EyePosition, EyePosition + EyeRotation.Forward * 105 )
			.Size( 2 )
			.Ignore( this )
			.Run();

		if ( tr.Entity is BreakableWall wall )
		{
			DamageInfo dmgInfo = new DamageInfo();
			dmgInfo.Attacker = this;
			dmgInfo.Damage = 1;

			wall.TakeDamage( dmgInfo );
			return;
		}

		if ( !tr.Hit || tr.StartedSolid )
			return;

		var totalEnts = FindInSphere( tr.EndPosition, 64 );
		foreach(var ent in totalEnts) 
		{
			if ( ent is not PlayerBase || ent == this )
				continue;

			if (ent is PlayerBase player)
				if ( player.CurrentTeam == TeamEnum.Pigmask )
				{
					Game.Current.RoundTimer += 30;

					using(Prediction.Off() )
						Sound.FromEntity( "squeal", player ); 

					player.BecomeRagdollOnClient( Velocity, EyePosition + Rotation.Forward );
					player.OnKilled();
					Game.Current.CheckRoundStatus();
				}
		}
	}
}
