/** Debug Macro List. ( Only applicable on Tutorial )
 * 
 * 	DEBUG_SAVING_TUTORIAL		:	Uses PlayerPrefs on Saving instead on AccountManager.
 *  DEBUG_TUTORIAL_C_PART_2		:	Enables OnGUI button Tutorial Part 2 C
 * 	DEBUG_TUTORIAL_STATES		:	Enables OnGUI buttons for Tutorials
 * 
 **/

#define DEBUG_SAVING_TUTORIAL				
#define DEBUG_TUTORIAL_C_PART_2
//#define DEBUG_TUTORIAL_STATES

using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DragonCatcher.UI;
using DragonCatcher.Common;
using DragonCatcher.CarePlay.Petting;
using DragonCatcher.CarePlay.Feeding.MiniGame;
using DragonCatcher.AnimationTool;
using DragonCatcher.Common;

namespace DragonCatcher.CarePlay.TouchPlay
{
	public class TutorialController:MonoSingleton<TutorialController>
	{
		// +KJ:10082013 NOTE:
		//
		//	PROBLEM:
		//	
		//	Too many states.. 256 and above cannot be converted to a 'byte'
		//
		//	SOLUTION:
		//	source: http://stackoverflow.com/questions/737781/left-bit-shifting-255-as-a-byte
		//			http://stackoverflow.com/questions/737781/left-bit-shifting-255-as-a-byte/737810#737810
		//
		//  2 Ways on reducing the size.
		//
		// 	- Mask the byte value
		//		Cinematic	= ( 0x1 << 1 ) & 0xFF,
		//
		//	- Uncheked by byte values
		//		Cinematic	= unchecked((byte)(0x1 << 1)),
		//
		public enum ETutorialState
		{
			Invalid     		= ( 0x0 << 0 ),
			
			// A 1
			Cinematic 			= ( 0x1 << 1 ),
			
			// B 2
			GainTrust 			= ( 0x1 << 2 ),
			Feeding 			= ( 0x1 << 3 ),
			PostFeeding 		= ( 0x1 << 4 ),
			PrePetting 			= ( 0x1 << 5 ), // Touch moments
			PostPetting 		= ( 0x1 << 6 ), // Touch moments
			
			// +KJ:10082013 Before Act 1-C states
			BondMeterIntro		= ( 0x1 << 7 ), //
			PlayerProfileIntro	= ( 0x1 << 8 ),
			PlayerProfileOutro	= ( 0x1 << 9 ),
			
			// C 1
			// Expanded TouchPlay states
			TouchPlay			= ( 0x1 << 10 ),
			TouchPlay_Jump		= ( 0x1 << 11 ),
			TouchPlay_tSit		= ( 0x1 << 12 ),
			TouchPlay_HopRight	= ( 0x1 << 13 ),
			TouchPlay_HopLeft	= ( 0x1 << 14 ),
			
			// Thirsty Cue
			ThirstyCue			= ( 0x1 << 15 ),
			PostThirstyCue		= ( 0x1 << 16 ),
			Petting				= ( 0x1 << 17 ),
			BoredCue			= ( 0x1 << 18 ),
			Fetching			= ( 0x1 << 19 ),
			TiredCue			= ( 0x1 << 20 ),
			PostTiredCue		= ( 0x1 << 21 ),
			FeedingPart2		= ( 0x1 << 22 ),
			
			ItchyCues			= ( 0x1 << 23 ),
			ItchyIntro			= ( 0x1 << 24 ),
			ItchyRubbing		= ( 0x1 << 25 ),
			
			EndOfCPart1			= ( 0x1 << 26 ),
			
			// C 2
			TutorialCPart2		= ( 0x1 << 27 ),

			// Flight			
			FlyOutOfCove		= ( 0x1 << 28 ),
			
			// End
			Done				= ( 0x1 << 29 ),
			Max					= ( 0x1 << 30 ),
			
			DialogCallbackException =  	PostFeeding			| PrePetting 	| PostPetting 		| BondMeterIntro | 
										PlayerProfileIntro  | ThirstyCue 	| PostThirstyCue 	| Petting		 | 
										TiredCue 			| Fetching 		| PostTiredCue		| TutorialCPart2 | 
										FlyOutOfCove 		| Feeding,
			
			InvalidSceneException = Invalid | TutorialCPart2 | FlyOutOfCove | Done | Max,
		}
		

		public const int CATCH_REQUIRED 			= 5;
		private const string TOUCH_MOMENT_START 	= "Touch_Moment_Start";
		
		public static ETutorialState GTutState 		= ETutorialState.Invalid;
		public static bool Debug_TurnOffTutorial	= false;
		
		private GameObject Toothless;
		private int m_tutCounter;
		private int m_catches;
		private string m_savedModule				= string.Empty;
		private string m_savedTutorial				= string.Empty;
		private ETutorialState m_saveTutState		= ETutorialState.Invalid;
		
		private SheepManager m_sheepManager = null;
		private S6Scheduler.ScheduleController m_flyBGScheduler = null;
		private S6Scheduler.ScheduleController m_switchToFlight = null;
			
		GameObject m_swipeUI;
		GameObject m_swipeHolder;
		GameObject m_swipeFingerGO;
		
		CarePlayUIManager m_careplayTabManager;
			
		void Awake ()
		{
			DontDestroyOnLoad( this.gameObject );
			m_tutCounter 	= 0;
			m_catches		= 0;
			
			//Swipe UI's
			m_swipeUI			= GameObject.Find("SwipeUI");
			m_swipeHolder		= GameObject.Find("SwipeHolder");
			m_swipeFingerGO		= GameObject.Find("SwipeFinger");
			
			if( m_sheepManager == null )
				m_sheepManager = FindObjectOfType(typeof(SheepManager)) as SheepManager;
			
			// ready callbacks for flyout
			m_flyBGScheduler = S6Scheduler.ScheduleAction( this, () => {
				if( m_flyBGScheduler != null && TutorialController.GTutState == ETutorialState.FlyOutOfCove )
				{
					if( CarePlayNFController.GetPetAnimController().IsCurAnimState( ERigType.Body, "IdleLookAround" ) )
					{
						m_flyBGScheduler.StopScheduler();
						m_flyBGScheduler.StopCoroutine();
						m_switchToFlight.ResumeScheduler();
					}
				}
			},
			S6Scheduler.DELTA,
			S6Scheduler.INFINITE,
			false);
			m_flyBGScheduler.PauseScheduler();
			
			// test continue from saved tutorial
			this.LoadSavedTutorial();
		}
		
		void Start ()
		{
			// Handle Tutorial Button Callbacks
			this.HandleDialogueCallbacks();
			
			// Load Initial Data
		}
		
		void LoadSavedTutorial ()
		{
			// get saved tutorial
			m_savedModule 	= this.GetSaveTutorial( "Module" );
			m_savedTutorial	= this.GetSaveTutorial( "TutorialId" );
			
			if( m_savedTutorial == null )
				return;
			
			ETutorialState currState = ETutorialState.Invalid;
			
			try
			{ 
				currState = Utility.EnumFromString<ETutorialState>(m_savedTutorial); 
			}
			catch 
			{ 
				Debug.LogWarning("Error! TutorialController::ContinueFromSavedTutorial Invalid tut key:"+m_savedTutorial+" \n");
				m_savedModule = string.Empty;
				m_savedTutorial = string.Empty;
			}
			
			m_saveTutState = currState;
		}
		
		public bool ContinueFromSavedTutorial ()
		{
			switch( m_saveTutState )
			{
				case ETutorialState.Invalid:
				case ETutorialState.Done:
					return false;
				break;
				
			}
			
			int saveTutIndex = Utility.PowerOf2Range( m_saveTutState );
				saveTutIndex -= 1;
			
			m_tutCounter 	= saveTutIndex;
			GTutState 		= m_saveTutState;
			
			this.UpdateTutorial();
			
			return true;
		}

		// Debug
		public void FreePlay ()
		{
			m_tutCounter = 27;
			GTutState = ETutorialState.TutorialCPart2;
			//this.UpdateTutorial();
		}
		
		
		/// <summary>
		/// Increment & Triggers Tutorial
		/// </summary>
		public void UpdateTutorial ()
		{
			if( Debug_TurnOffTutorial ) { return; }


			Debug.Log("TutorialController::UpdateTutorial Updating State: "+GTutState+" Index:"+m_tutCounter+" \n");
			GTutState = (ETutorialState)( 0x1 << ++m_tutCounter );
			Debug.Log("TutorialController::UpdateTutorial State: "+GTutState+" Index:"+m_tutCounter+" \n");
			
			GameObject bo = GameObject.Find( "BlackOverlay" ) as GameObject;
			
			//+KJ - Optimized Multiple || (or) condition using bitmasking
			ETutorialState TutorialExc = ETutorialState.TutorialCPart2 | ETutorialState.Done | ETutorialState.Max;
			if ( ( TutorialExc & GTutState ) == GTutState )
			{
				return ;
			}


			switch ( GTutState ) 
			{
				case ETutorialState.Cinematic:
					IOSLinker.EventHooks("ACT 1-A");
					//enable 2 sheep for cinematic
					if( m_sheepManager != null ) {
						m_sheepManager.SetUpForTutorial();
					}
				
				break;
				case ETutorialState.GainTrust:
					//Hide the enabled sheep
				
					if( m_sheepManager != null )
					{
						m_sheepManager.SetAsTutorial = false;
						m_sheepManager.SheepVisible(false);
					}

				break;
				case ETutorialState.Feeding:
					StatsManager.Instance.FeedType = (int) EFoodItems.TutorialFish;
					PettingMain.Instance.OnFeedingItem( EFoodItems.TutorialFish );
				
					PettingMain.Instance.DragonStateMachine.SetIsInteractible(true);
					S6Scheduler.ScheduleAction( this, () => PettingMain.Instance.DragonStateMachine.PlayActionSequence( "Frightened_From_Spikes_PuzzledLook" ), 1.0f, 1, false);
					S6Scheduler.ScheduleAction( this, () => PettingMain.Instance.OnExitPetting( "FeedingModule" ), 4.0f, 1, false);
				
				break;
				case ETutorialState.PostFeeding:
				    FeedingMiniGame.Instance.ExitFeeding();
				break;
				case ETutorialState.PrePetting:
					
					PettingMain.Instance.DragonScript.LookAtObject("CamPlayerHead", true);
					PettingMain.Instance.HideUI( "MainUI" );
					Utility.SetIsButtonEnabled( "Open", false );
					Utility.SetIsButtonEnabled( "Close", false );
					Utility.SetIsButtonEnabled( "OpenArrw", false );
					Utility.SetIsButtonEnabled( "CloseArrw", false );
				
 				break;
				case ETutorialState.PostPetting:
				
					// Hide Tab
					PettingMain.Instance.TabManager.CloseTab();
				
					// Should display Dialogue after the Inventory Dialogue exited.
					S6Scheduler.ScheduleAction(this, () => {
						D.Log("Activating TouchMoment Tutorial... \n");
						TutorialDialogueController.Instance.ActivateTutorial( TOUCH_MOMENT_START, "CareAndPlayHUD" );
					}, 1.5f, 1, false);
				break;
				
				// +KJ:10082013 Handle Bond Introduction
				case ETutorialState.BondMeterIntro:
					// introduce bond meter.
					PettingMain.Instance.ShowUI( "EnergyBond" );
					PettingMain.Instance.ShowUI( "PauseBTN" );
					
					// disable gestures
					PettingMain.Instance.GestureManager.DisableGestures();
				
					// fill the bond upto friendly
					//AccountManager.Instance.SetStateMachineMeter(0.15f, StatsManager.KEY_BOND);
					GameObject.Find("BondProgress").GetComponent<BondProgress>().AnimateGlow();
					
					// set response to Happy
					ResponseManager.Instance.DisplayIcon( "Response_Happy" );
					
					// Pause all Event Timers
					PettingMain.Instance.DragonStateMachine.PauseGame(true);
				
					// animate toothless moving back to mg & Upright Sit Happy and Laughing
					PettingMain.Instance.DragonStateMachine.SetIsInteractible(true);
					PettingMain.Instance.DragonStateMachine.PlayActionSequence( "Tutorial_Bond_Intro" );
					
					// set uninteractible
					PettingMain.Instance.DragonStateMachine.SetIsInteractible(false);
					PettingMain.Instance.HideUI( "MainUI" );
					Utility.SetIsButtonEnabled( "BodBtn", false );
					Utility.SetIsButtonEnabled( "MapBtn", false );
					Utility.SetIsButtonEnabled( "InventoryBtn", false );
					Utility.SetIsButtonEnabled( "OpenArrw", false );
					Utility.SetIsButtonEnabled( "CloseArrw", false );
					//Utility.SetIsButtonEnabled( "PauseBTN", false );
					GameObject.Find("PauseBTN").GetComponent<TweenPosition>().Toggle();
					
					// show bond intro dialogue. 1sec delay
					S6Scheduler.ScheduleAction(this, () => TutorialDialogueController.Instance.ActivateTutorial( "Bond_Intro", "CareAndPlayHUD" ), 3.00f, 1, false);
				break;
				case ETutorialState.PlayerProfileIntro:
				
					// show player profile. MainUI
					PettingMain.Instance.TabManager.OpenTab();
					
					// disable gestures
					PettingMain.Instance.GestureManager.DisableGestures();
				
					// create black overlay. quick fix
					GameObject anchor = (GameObject.Find("Anchor"));
					GameObject overlay = (GameObject)Resources.Load("Prefabs/UI/BlackOverlay", typeof(GameObject));
					Vector3 pos = new Vector3(overlay.transform.position.x, overlay.transform.position.y, 0.1f );
					
					// show black overlay. quick fix
					GameObject blackOverlay = (GameObject)Instantiate(overlay, pos, overlay.transform.rotation);
					blackOverlay.name = "Profile_BlackOverlay";
					blackOverlay.transform.parent = anchor.transform;
					
					//DISABLE ARROW BTN
					Utility.SetIsButtonEnabled( "CloseArrw", false );
					Utility.SetIsButtonEnabled( "ProfileBtn", true );
				break;
				case ETutorialState.PlayerProfileOutro:
					IOSLinker.EventHooks("ACT 1-B");
					// Hide MainMenu
					PettingMain.Instance.HideUI( "MainUI" );
					Utility.SetIsButtonEnabled( "BodBtn", true );
					Utility.SetIsButtonEnabled( "MapBtn", true );
					Utility.SetIsButtonEnabled( "InventoryBtn", true );
					Utility.SetIsButtonEnabled( "OpenArrw", true );
					Utility.SetIsButtonEnabled( "CloseArrw", true );
					//Utility.SetIsButtonEnabled( "PauseBTN", true );
				
					GameObject button = GameObject.Find( "PauseBTN" );
					TweenPosition buttonTween = button.GetComponent<TweenPosition>();
					buttonTween.Toggle();
					StatsManager.Instance.DragonMode = StatsManager.MODE_NORMAL;
					// enable gestures
					PettingMain.Instance.GestureManager.EnableGestures();
					
					// destroy Black Overlay. quick fix
					if( bo != null )
					{
						Destroy( bo );
					}
					
					//enable ARROW BTN
					Utility.SetIsButtonEnabled( "CloseArrw", true );
				
					// Update to TouchPlay Tutorials
					TutorialController.Instance.UpdateTutorial();
				break;
				case ETutorialState.TouchPlay:
					// Disable Gestures
					PettingMain.Instance.GestureManager.DisableGestures();
				
					// Display Dialogue	
					TutorialDialogueController.Instance.ActivateTutorial( "Touch_Play_Tutorial", "CareAndPlayHUD"  );
			
					// Update to Tutorial
					//TutorialController.Instance.UpdateTutorial();
					//Disable UI
					//PettingMain.Instance.HideUI( "MainUI" );	
					Utility.SetIsButtonEnabled( "OpenArrw", false );
					Utility.SetIsButtonEnabled( "CloseArrw", false );
					
				break;
				case ETutorialState.TouchPlay_Jump:
				
					bo = GameObject.Find( "Profile_BlackOverlay" ) as GameObject;
					if( bo != null )
					{
						Destroy( bo );
					}
				
					// list out valid tutorial Event_SwipeUp";
					// Enable Gestures
					PettingMain.Instance.GestureManager.EnableGestures();
					PettingMain.Instance.ShowSwipeUI(FingerGestures.SwipeDirection.Up);
					//S6Scheduler.ScheduleAction( this, () => PettingMain.Instance.DragonStateMachine.SetIsInteractible( true ), 2.0f, 1, false );
				break;
				case ETutorialState.TouchPlay_tSit:	
					// list out valid tutorial "Event_SwipeDown";
					// Enable Gestures
					PettingMain.Instance.GestureManager.EnableGestures();
				    PettingMain.Instance.ShowSwipeUI(FingerGestures.SwipeDirection.Down);
					//S6Scheduler.ScheduleAction( this, () => PettingMain.Instance.DragonStateMachine.SetIsInteractible( true ), 2.0f, 1, false );
				break;
				case ETutorialState.TouchPlay_HopRight:
					// list out valid tutorial "Event_SwipeRight";
					// Enable Gestures
					PettingMain.Instance.GestureManager.EnableGestures();
					PettingMain.Instance.ShowSwipeUI(FingerGestures.SwipeDirection.Right);
					//S6Scheduler.ScheduleAction( this, () => PettingMain.Instance.DragonStateMachine.SetIsInteractible( true ), 2.0f, 1, false );
					// Set to Default Position
					PettingMain.Instance.DragonStateMachine.PlayActionSequence( "Idle_Stand_HappySurprised" );
				
				break;
				case ETutorialState.TouchPlay_HopLeft:
					// list out valid tutorial "Event_SwipeLeft";
					// Enable Gestures
					PettingMain.Instance.GestureManager.EnableGestures();
					
				    PettingMain.Instance.ShowSwipeUI(FingerGestures.SwipeDirection.Left);
					//S6Scheduler.ScheduleAction( this, () => PettingMain.Instance.DragonStateMachine.SetIsInteractible( true ), 2.0f, 1, false );
				break;
				case ETutorialState.ThirstyCue:
				
					//PettingMain.Instance.HideSwipeUI();
					Toothless = PettingMain.Instance.DragonGameObject;
				
					Toothless.transform.position = new Vector3(-0.191f, 0, 1.474f);
					Camera.main.transform.position = new Vector3( 0, 1.516f, -2.81f);	
					Camera.main.transform.rotation = Quaternion.Euler( 7.70f, 0, 0);
					PettingMain.Instance.DragonStateMachine.SetIsInteractible(true);
			
					/*PettingMain.Instance.HideUI( "MainUI" );
					Utility.SetIsButtonEnabled( "Open", false );
					Utility.SetIsButtonEnabled( "Close", false );*/
				
					StatsManager.Instance.SetDragonData("Thirsty", 0.0f);
					StartCoroutine( DoTutorialAnimation( ETutorialState.ThirstyCue ) );
					
				break;	
				case ETutorialState.PostThirstyCue:
				
					//PettingMain.Instance.HideUI( "MainUI" );
					//Utility.SetIsButtonEnabled( "CloseArrw", false );
					//Utility.SetIsButtonEnabled( "OpenArrw", false );
				
				break;
				
				case ETutorialState.Petting:
				
					//PettingMain.Instance.TabArrowUp.SetActive( true );
					//PettingMain.Instance.HideUI( "MainUI" );
					PettingMain.Instance.TabManager.CloseTab();
				
					Utility.SetIsButtonEnabled( "BodBtn", true );
					Utility.SetIsButtonEnabled( "MapBtn", true );
					Utility.SetIsButtonEnabled( "InventoryBtn", true );
					//Utility.SetIsButtonEnabled( "OpenArrw", false );
					//Utility.SetIsButtonEnabled( "CloseArrw", false );
				
					PettingMain.Instance.GestureManager.DisableGestures();	
					S6Scheduler.ScheduleAction( this, () => TutorialDialogueController.Instance.ActivateTutorial( "Petting_Tutorial", "CareAndPlayHUD" ), 2.0f, 1, false);

					// Should not be interactible
					PettingMain.Instance.DragonStateMachine.SetIsInteractible( false );
				break;
				case ETutorialState.BoredCue:
					StatsManager.Instance.DragonMode = StatsManager.MODE_NORMAL;
					//this.UpdateTutorial();
					// Play Bored Animation
				
					StartCoroutine( DoTutorialAnimation( ETutorialState.BoredCue ) );
				
					// Should not be interactible
					PettingMain.Instance.DragonStateMachine.SetIsInteractible( false );
				break;
				case ETutorialState.Fetching:
				
					//Disable arrow
					Utility.SetIsButtonEnabled( "OpenArrw", false );
					PettingMain.Instance.TabManager.CloseTab();
				
				break;
				case ETutorialState.PostTiredCue:
					//PLAY TIRED CUE
					StartCoroutine( DoTutorialAnimation( ETutorialState.PostTiredCue ) );
					
					// Should not be interactible
					PettingMain.Instance.DragonStateMachine.SetIsInteractible( false );
				
					//Disable arrow
					Utility.SetIsButtonEnabled( "OpenArrw", false );
					Utility.SetIsButtonEnabled( "CloseArrw", false );
				break;
				
				case ETutorialState.FeedingPart2:	
					this.UpdateTutorial();
				break;
				
				case ETutorialState.ItchyCues:	

					// Clear all cued Events
					PettingMain.Instance.DragonStateMachine.ClearAllEvents();
				
					// Play Animation 		: Tutorial_Cue_ItchyAche
					StartCoroutine( DoTutorialAnimation( ETutorialState.ItchyCues ) );
					// Show Dialogue		: Tutorial_Itchy_Cue. ( After the Animation )
					
					PettingMain.Instance.DragonStateMachine.SetIsInteractible( false );
				
					//Disable arrow
					Utility.SetIsButtonEnabled( "OpenArrw", false );
					Utility.SetIsButtonEnabled( "CloseArrw", false );
					Utility.SetIsButtonEnabled( "BodBtn", false );
					Utility.SetIsButtonEnabled( "MapBtn", false );
					Utility.SetIsButtonEnabled( "OpenArrw", false );
					Utility.SetIsButtonEnabled( "CloseArrw", false );
					Utility.SetIsButtonEnabled( "InventoryBtn", true );
				break;
				case ETutorialState.ItchyIntro:	
					// Show Inventory
					//PettingMain.Instance.ShowPettingTab();
					PettingMain.Instance.TabManager.OpenTab();
				
					// set uninteractible
					PettingMain.Instance.DragonStateMachine.SetIsInteractible(false);
					Utility.SetIsButtonEnabled( "OpenArrw", false );
					Utility.SetIsButtonEnabled( "ProfileBtn", false );
					Utility.SetIsButtonEnabled( "CloseArrw", false );
				
				break;
				case ETutorialState.ItchyRubbing:
					Utility.SetIsButtonEnabled( "CloseArrw", true );
					PettingMain.Instance.DragonStateMachine.SetIsInteractible( true );
				break;
				
				case ETutorialState.EndOfCPart1:
					IOSLinker.EventHooks("ACT 1-C Part 1");
					PettingMain.Instance.DragonStateMachine.SetIsInteractible( false );
					PettingMain.Instance.GestureManager.DisableGestures();
					TutorialDialogueController.Instance.ActivateTutorial( "Tutorial_C_Part_1_End", "CareAndPlayHUD" );
				break;
				
				case ETutorialState.TutorialCPart2:
					// Enable all Buttons, Events and Gesture
					Utility.SetIsButtonEnabled( "InventoryBtn", true );
					Utility.SetIsButtonEnabled( "BodBtn", true );
					Utility.SetIsButtonEnabled( "MapBtn", true );
					Utility.SetIsButtonEnabled( "OpenArrw", true );
					Utility.SetIsButtonEnabled( "CloseArrw", true );
					Utility.SetIsButtonEnabled( "ProfileBtn", true );
					PettingMain.Instance.PauseGame( false );
					PettingMain.Instance.DragonStateMachine.SetIsInteractible( true );
					PettingMain.Instance.GestureManager.EnableGestures();
				break;
				
				case ETutorialState.FlyOutOfCove:
					IOSLinker.EventHooks("ACT 1-C Part 2");
					PettingMain.Instance.GestureManager.DisableGestures();
					PettingMain.Instance.DragonStateMachine.PauseGame( true );
					
					PettingMain.Instance.DragonScript.DisableLookAt(true);
					PettingMain.Instance.DragonScript.DisableLookAt(false);
					PettingMain.Instance.DragonScript.LookAtObject("", true);
					PettingMain.Instance.DragonScript.LookAtObject("", false);
					
					// resume flyout scheduler
					//m_flyBGScheduler.ResumeScheduler(); // + LA 110513: Temporarily commented
					
					//Change to flight scene
				break;
				
				case ETutorialState.Done:
					PettingMain.Instance.PauseGame( false );
					PettingMain.Instance.DragonStateMachine.SetIsInteractible( true );
					PettingMain.Instance.GestureManager.EnableGestures();
				
					Utility.SetIsButtonEnabled( "InventoryBtn", true );
					Utility.SetIsButtonEnabled( "BodBtn", true );
					Utility.SetIsButtonEnabled( "MapBtn", true );
					Utility.SetIsButtonEnabled( "OpenArrw", true );
					Utility.SetIsButtonEnabled( "CloseArrw", true );
					Utility.SetIsButtonEnabled( "ProfileBtn", true );
					Utility.SetIsButtonEnabled( "CloseArrw", true );
					Utility.SetIsButtonEnabled( "PauseBTN", true );
				break;
			}
			
			// Save Tutorial on these specifc save points
			switch( GTutState )
			{
				case ETutorialState.TouchPlay:	
				case ETutorialState.ThirstyCue:
				case ETutorialState.Petting:
				case ETutorialState.BoredCue:	
				case ETutorialState.PostTiredCue:	
				case ETutorialState.ItchyCues:	
				case ETutorialState.EndOfCPart1:
				case ETutorialState.TutorialCPart2:
				//case ETutorialState.FlyOutOfCove:
					// Update and Save Tutorial
					this.SaveCoveTutorial( GTutState );
				break;
			}
		}
		
		/// <summary>
		/// This will handle the Callbacks on Dialogue.
		/// </summary>
		/// 
		//
#region Delay Animation qeues 
		private IEnumerator DoTutorialAnimation ( ETutorialState p_state )
		{
			Debug.Log("~~~~~~~~~~~~~~~~~~~~~~~~ \n");
			Debug.Log("- TutorialController::DoTutorialAnimation State:"+p_state+" \n");
			Debug.Log("------------------------ \n");
			
			switch ( p_state )
			{
				case ETutorialState.ThirstyCue:
				
					PettingMain.Instance.DragonStateMachine.PlayActionSequence("Idle_Stand_HappyAndLaughing");
					yield return new WaitForSeconds( 3.0f );
					
					if ( PettingMain.Instance.IsTabOpen ) {
						StartCoroutine( DoTutorialAnimation( p_state ) );
					} else {
						PettingMain.Instance.DragonStateMachine.PlayActionSequence("Cue_Thirsty_NeutralMood");			
					}
				break;
					
				case ETutorialState.BoredCue:
				
					PettingMain.Instance.DragonStateMachine.PlayActionSequence("Idle_Stand_HappySurprised");
					yield return new WaitForSeconds( 3.7f );
					
					if ( PettingMain.Instance.IsTabOpen ) {
						StartCoroutine( DoTutorialAnimation( p_state ) );
					} else {
						PettingMain.Instance.DragonStateMachine.PlayActionSequence( "Tutorial_Cue_Bored" );
					}
				break;
					
				case ETutorialState.ItchyCues:
					
					PettingMain.Instance.DragonStateMachine.PlayActionSequence("Idle_Stand_TeethSmile");
					yield return new WaitForSeconds( 3.5f );
				
					if ( PettingMain.Instance.IsTabOpen ) {
						StartCoroutine( DoTutorialAnimation( p_state ) );
					} else {
						PettingMain.Instance.DragonStateMachine.SetIsInteractible( true );
						PettingMain.Instance.DragonStateMachine.PlayActionSequence( "Tutorial_Cue_ItchyAche" );
					}
				break;
				
				case ETutorialState.PostTiredCue:
				
					PettingMain.Instance.DragonStateMachine.PlayActionSequence("Idle_Stand_TeethSmile");
					yield return new WaitForSeconds( 3.5f );
				
					if ( PettingMain.Instance.IsTabOpen ) {
						StartCoroutine( DoTutorialAnimation( p_state ) );
					} else {
						PettingMain.Instance.DragonStateMachine.PlayActionSequence( "Cue_Tired_NeutralMood" );
						yield return new WaitForSeconds( 11.533f );
						TutorialDialogueController.Instance.ActivateTutorial("Tutorial_Tired_Cue", "CareAndPlayHUD" );
					}
				break;
			}
								

			yield return null;
		}	
#endregion
		
#region HandleDialogue
		public void HandleDialogueCallbacks ()
		{
			// Handle Dialogue Next Button
			TutorialDialogueController.Instance.OnTutorialNext = ( string p_tutId, int p_pageIndex ) => 
			{
				switch( p_tutId )
				{
					case "Intro_Welcome":
					break;
					case "Reinforce_Feed_Tutorial":
						if( p_pageIndex == 0 )
						{ 
							PettingMain.Instance.AnimateOverlayForPostFeed();
						}
						else if( p_pageIndex == 1 )
						{
							ResponseManager.Instance.HideIcon();
							PettingMain.Instance.CarePlayHudReset();
							this.UpdateTutorial();
						}
					break;
					case "Bond_Intro":
						if( p_pageIndex == 1 )
						{
						}
						else if( p_pageIndex == 2 )
						{
							// update to next tutorial. -> PlayerProfile
							this.UpdateTutorial();
						}
					break;
				}
			};
			
			// Handle Dialogue Close Button
			TutorialDialogueController.Instance.OnTutorialClose = () =>
			{
				//Debug.Log("TouchController::HandleDialogueCallbacks Close "+TutorialController.IsInTutorialMode);
				
				if( TutorialController.IsInTutorialMode == true )
				{
					
					if ( ( GTutState & ETutorialState.DialogCallbackException ) != GTutState )
					{
						TutorialController.Instance.UpdateTutorial();
					}	
				
					
					Debug.Log("TouchController::GTutState " + TutorialController.GTutState );
					switch( TutorialController.GTutState )
					{
						case ETutorialState.PostPetting:
							//SHOW INSTRUCTION ON TOUCH MOMENT
							PettingMain.Instance.GestureManager.EnableGestures();
							
							//InstructionsManager.Instance.DisplayInstruction( "Tap to move him forward" );
							
							//SET STATE TO TOUCH MOMENT
							StatsManager.Instance.DragonState = "TouchMoment_Midground"; //Temp
							//PettingMain.Instance.DragonStateMachine.Reaction("Type_Touch", "Event_DoubleTap", true);		
						break;
						case ETutorialState.ThirstyCue:
							UpdateTutorial();
						break;
						case ETutorialState.PostThirstyCue:
							
							//PettingMain.Instance.ShowPettingTab();
							//PettingMain.Instance.HideUI( "MainUI" );
							PettingMain.Instance.TabManager.CloseTab();
							
							Utility.SetIsButtonEnabled( "BodBtn", false );
							Utility.SetIsButtonEnabled( "MapBtn", false );
							Utility.SetIsButtonEnabled( "InventoryBtn", true );
							Utility.SetIsButtonEnabled( "OpenArrw", false );
							Utility.SetIsButtonEnabled( "ProfileBtn", false );
							Utility.SetIsButtonEnabled( "CloseArrw", false );
						
							HidePlayForCarePlayItems();
							
							// should be updating tutorial here
							this.UpdateTutorial();
						break;
						case ETutorialState.TouchPlay:
							this.UpdateTutorial();
						break;
						
						case ETutorialState.Petting:
							StatsManager.Instance.DragonState = "Rubbing";
							StatsManager.Instance.DragonMode = StatsManager.MODE_ACT1;
							PettingMain.Instance.GestureManager.EnableGestures();
						break;
						case ETutorialState.PostTiredCue:
						    
							//PettingMain.Instance.ShowPettingTab();
							PettingMain.Instance.TabManager.OpenTab();
						
							PettingMain.Instance.HideUI( "MainUI" );
							Utility.SetIsButtonEnabled( "BodBtn", false );
							Utility.SetIsButtonEnabled( "MapBtn", false );
							Utility.SetIsButtonEnabled( "InventoryBtn", true );
							Utility.SetIsButtonEnabled( "OpenArrw", false );
							Utility.SetIsButtonEnabled( "ProfileBtn", false );
							Utility.SetIsButtonEnabled( "CloseArrw", false );
			
							HidePlayForCarePlayItems();
						break;
					}
				}
				
				Utility.SetIsButtonEnabled( "Next", false );
				Utility.SetIsButtonEnabled( "CloseBtn", false );
			};
		}
		
#endregion
		
		/// <summary>
		/// Closes inventory callback triggers UpdateTutorial. In some instance we need to trigger this to progress
		/// </summary>
		public void CloseInventoryCallback ()
		{
			switch( GTutState ) 
			{
			case ETutorialState.PrePetting: 
			case ETutorialState.PostThirstyCue:
				this.UpdateTutorial();
				break;
			}
		}
		
		/// <summary>
		/// Condition upon showing Inventory, ex : hide johann shop icon if the tutorial is not yet finished 
		/// </summary>
		public void ShowInventoryCallback ()
		{
			if( GTutState != ETutorialState.Invalid && GTutState != ETutorialState.Done )
			{
				GameObject johannShop	= GameObject.Find("OpenShop");
				
				TutorialDialogueController.Instance.Trigger( ETutorialEvents.Evt_Inventory );
				
				if( GTutState != ETutorialState.PostTiredCue 
					&& GTutState != ETutorialState.TutorialCPart2 
				){
					PettingMain.Instance.DragonStateMachine.SetIsInteractible(false);
				}
				
				Utility.SetIsButtonEnabled( "Open", false );
				Utility.SetIsButtonEnabled( "Close", false );
				
				johannShop.SetActive(false);
				
				HidePlayForCarePlayItems();
				
				if ( GTutState == ETutorialState.PostTiredCue 
				||	 GTutState == ETutorialState.ItchyIntro 
					)
				{
					Utility.SetIsButtonEnabled( "CloseBTN", false );	
				}
				else
				{
					Utility.SetIsButtonEnabled( "CloseBTN", true );
				}
			}
		}
		
		/// <summary>
		/// Hides the play button for care play items.
		/// </summary>
		public void HidePlayForCarePlayItems()
		{
			GameObject inventoryPanel 	= GameObject.Find("InventoryPanel");
			
			if ( inventoryPanel != null ) {
				InventoryUIHandler iP = inventoryPanel.GetComponent<InventoryUIHandler>();
				iP.HideInventoryForTutorial();
				iP.HidePlayForTutorial();	
			}
		}
		
		public int TotalTutorialCatch
		{
			get { return m_catches; }
			set { m_catches = value; }
		}
			
		int m_changePosCntr;
		public int CatchToChangePos
		{
			get { return m_changePosCntr; }
			set { m_changePosCntr = value; }
		}
		
		// +KJ:10102013 Debug turn off Tutorial
		public void TurnOffTutorial ()
		{
			TutorialController.GTutState = ETutorialState.Invalid;
			m_tutCounter = -1;
			Debug_TurnOffTutorial = true;
		}
		
		// +KJ:10112013 Helper on Tutorial Ids
		public ETutorialState TutorialIdByIndex ( int p_index )
		{
			if( p_index < 0 )
				return ETutorialState.Invalid;
				
			ETutorialState state = (ETutorialState)( 0x1 << p_index );
			
			if( state > ETutorialState.Done )
				return ETutorialState.Invalid;
			
			return state;
		}
		
		private void SaveCoveTutorial ( ETutorialState p_state )
		{
			UserPrefs.Instance.SetFirstTimeUserTutorial( "Cove", p_state.ToString(), 1 );
			this.SetSaveTutorial( "Module", "TouchPlay" );
			this.SetSaveTutorial( "TutorialId", p_state.ToString() );
		}
		
		// Utils. Tutorial Blockers
		public bool IsValidGestureForTutorial ( 
			string p_eventType,
			string p_event,
			GestureObject p_gesture
		){	
			if ( ( ( ETutorialState.Invalid | ETutorialState.Done  | ETutorialState.Cinematic | ETutorialState.TutorialCPart2 ) &  TutorialController.GTutState ) == TutorialController.GTutState )
			{
				return true;
			}
			
			// Touched Object
			GameObject TouchObject 				  	= null;
			string TouchObjectName					= string.Empty;
			if( p_gesture != null && p_gesture.hit.collider != null )
			{
				TouchObject 					    = p_gesture.hit.collider.gameObject;
				TouchObjectName						= TouchObject.name;
			}
			
			// TouchPlay tutorial state valid gestures
			//Debug.Log("EVENTTYPE : " + p_eventType + "Event : " + p_event);
			
			// invalidate on TouchPlay Up, Down, Left, Right. ( TouchPlay tutorial states )
			
			if( TutorialController.GTutState == ETutorialState.PostPetting )
			{
				if( p_event == "Event_AnyInteraction" )
				{
					return true;
				}
				else if (  p_event == "Event_DoubleTap" ||
				         p_event == "Event_Hold" ||
				         p_event == "Event_Hold_Up"
				         )
				{
					if ( p_gesture.hit.transform && p_gesture.hit.transform.name == "HeadCollider" ) 
					{
						Debug.Log("HEAD TAPPED");
						return true;
					}

					return false;
				}
				else 
					return false;
				
			}
			if( TutorialController.GTutState == ETutorialState.TouchPlay_Jump )
			{
				if ( p_event == "Event_SwipeUp" )
				{
					return true	;
				}
				return false;
			}
			if( TutorialController.GTutState == ETutorialState.TouchPlay_tSit )
			{
				//return p_event == "Event_SwipeDown" ? true : false;
				if ( p_event == "Event_SwipeDown" )
				{
					return true	;
				}
				return false;
			}
			if( TutorialController.GTutState == ETutorialState.TouchPlay_HopRight )
			{
				if ( p_event == "Event_SwipeRight" )
				{
					return true	;
				}
				return false;
			}
			if( TutorialController.GTutState == ETutorialState.TouchPlay_HopLeft )
			{
				if ( p_event == "Event_SwipeLeft" )
				{
					return true	;
				}
					
				return false;
			}
			if( TutorialController.GTutState == ETutorialState.PostThirstyCue 
				&& p_gesture.hit.transform != null
				&& p_gesture.hit.transform.parent != null
				&& p_gesture.hit.transform.parent.name == "WaterBucket"
			){
				if( p_event == "Event_Water_Bucket" )
				{
					Dragon.BurstForBoomAndFish( p_gesture );
					StatsManager.Instance.SetDragonData( StatsManager.KEY_THIRSTY, (object)100.0f );
					return true;
				}
				return false;
			}
			if( TutorialController.GTutState == ETutorialState.ItchyRubbing
			){
				return p_event ==  "Event_Move" 
					|| p_event ==  "Event_Up"
					|| p_event ==  "Event_Down" ? true : false ;
			}
			// Bored Cue tutorial state valid gestures
			if( TutorialController.GTutState == ETutorialState.Fetching
				&& TouchObject != null 
			){
				if( p_event == "Event_Tap" && TouchObjectName == "Boomerang" )
				{
					Dragon.BurstForBoomAndFish( p_gesture );
					PettingMain.Instance.DragonStateMachine.SetIsInteractible( true );
					PettingMain.Instance.GestureManager.DisableGestures();
					PettingMain.Instance.DragonStateMachine.Reaction("Type_From_Inventory", "Event_Switch_To_Fetching");
				}
				
				return false;
			}
			
			// Tired Cue Tutorial
			if( TutorialController.GTutState == ETutorialState.ItchyIntro )
			{
				if( p_event == "Event_Dragon_Salve_Ready" )
				{
					// update to ItchyRubbing Tutorial
					this.UpdateTutorial();
					return true;
				}
			}
			
			if( TutorialController.GTutState == ETutorialState.Petting )
			{
				if( p_event == "Event_DoubleTap" )
				{
					PettingMain.Instance.DragonStateMachine.PlayActionSequence( "Petting_Jump_To_Front" );
				}
				
				return false;
			}
			
			return false;
		}
		
		public void ActionOnComplete ()
		{
			// update tutorial for these states
			switch( TutorialController.GTutState )
			{
				case ETutorialState.TouchPlay_Jump:
				case ETutorialState.TouchPlay_tSit:
				case ETutorialState.TouchPlay_HopLeft:
					this.UpdateTutorial();
				break;
				case ETutorialState.TouchPlay_HopRight:
					if( StatsManager.Instance.currentReaction != "Idle_Stand_HappySurprised" )
					{
						this.UpdateTutorial();
					}
				break;
				case ETutorialState.ThirstyCue:
					if( StatsManager.Instance.currentReaction == "Cue_Thirsty_NeutralMood" )
					{
						TutorialDialogueController.Instance.ActivateTutorial("Thirst_Tutorial", "CareAndPlayHUD" );
						PettingMain.Instance.HideUI("MainUI");
					}
				break;
				case ETutorialState.PostThirstyCue:
					PettingMain.Instance.DragonScript.LookAtObject("CamPlayerHead", true);
					TutorialDialogueController.Instance.ActivateTutorial("Thirst_Tutorial_Done", "CareAndPlayHUD" );
				break;
				case ETutorialState.BoredCue:
					// Display Dialogue for Bored Cue
					if( StatsManager.Instance.currentReaction == "Tutorial_Cue_Bored" )
					{
						TutorialDialogueController.Instance.ActivateTutorial( "Tutorial_Fetching_Intro", "CareAndPlayHUD"  );
					}
					//PettingMain.Instance.DragonStateMachine.SetIsInteractible( true );
				break;
				case ETutorialState.Petting:
					this.UpdateTutorial();
				break;
				case ETutorialState.ItchyCues:
					if( StatsManager.Instance.currentReaction == "Tutorial_Cue_ItchyAche" )
					{
						TutorialDialogueController.Instance.ActivateTutorial("Tutorial_Itchy_Cue", "CareAndPlayHUD" );
					}
				break;
				case ETutorialState.ItchyRubbing:
					if( StatsManager.Instance.currentReaction == "Dragon_Salve_Done" )
					{
						this.UpdateTutorial();
					}
				break;
				case ETutorialState.TiredCue:
					this.UpdateTutorial();
				break;
				case ETutorialState.PostTiredCue:
					Debug.Log("POSTTIRED : " + StatsManager.Instance.currentReaction);
				break;
			}
		}
		
		public int GetTutorialStateIndex()
		{
			return m_tutCounter;
		}
		
		private string GetSaveTutorial ( string p_key )
		{
#if DEBUG_SAVING_TUTORIAL	
			return UserPrefs.Instance.GetSavedTutorial( p_key );
#else
			return AccountManager.Instance.GetTutorialDataCove( p_key );
#endif
		}
		
		private void SetSaveTutorial ( string p_key, string p_value )
		{
#if DEBUG_SAVING_TUTORIAL
			UserPrefs.Instance.SetSavedTutorial( p_key, p_value );
			UserPrefs.Instance.SetFirstTimeUserTutorial( "TouchPlay", "tutorial." +	p_value, 1 );
#else
			UserPrefs.Instance.SetFirstTimeUserTutorial( "TouchPlay", "tutorial." +	p_value, 1 );
			AccountManager.Instance.SetTutorialDataCove( p_key, p_value );
#endif
		}
		
		public void SetCarePlayTab(CarePlayUIManager p_careUIManager)
		{
			m_careplayTabManager = p_careUIManager;
		}
		
		public void DebugPostFeeding ()
		{
			m_tutCounter = 4;
			GTutState = ETutorialState.PostFeeding;
			SceneTracker.Instance().CurrentScene = EDCScenes.SC_Feeding;
		}
		
		/// <summary>
		/// Static Methods
		/// Gets a value indicating whether this instance is in tutorial mode.
		/// </summary>
		/// <value>
		/// <c>true</c> if this instance is in tutorial mode; otherwise, <c>false</c>.
		/// </value>
		public static bool IsInTutorialMode
		{
			get 
			{ 
				bool isOff = 	TutorialController.GTutState == ETutorialState.Done 
								|| TutorialController.GTutState == ETutorialState.Max 
								|| TutorialController.GTutState == ETutorialState.Invalid
								|| TutorialController.GTutState == ETutorialState.TutorialCPart2; // exception
				
						
				return !isOff;
			}	
		}
	}
}
