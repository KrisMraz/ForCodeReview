#define DEBUG_STATEMACHINE_VALUES
//#define SUPPORT_LEFT_RIGHT_ARROW
#define DEBUG_ENERGY_TEST
//#define DEBUG_FLYIN_TEST
//#define DEBUG_USAGE_TRACKING
//#define DEBUG_SEQUENCIAL_TUTORIALS
//#define DEBUG_EVENTS
//#define DEBUG_DRAGON_WALK_POINTS

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using DragonCatcher.UI;
using DragonCatcher.Common;
using DragonCatcher.Common.StateMachine;
using DragonCatcher.AnimationTool;
using DragonCatcher.CarePlay.Feeding;
using DragonCatcher.CarePlay.TouchPlay;
using DragonCatcher.CarePlay.Feeding.MiniGame;
using DragonCatcher.CarePlay.UsageFrequency;

namespace DragonCatcher.CarePlay.Petting
{
	// Alliases
	using ListTracker = Dictionary<string, UsageFreqTracker>;
	
	/** Metatags ***************************************************/
	//[RequireComponent (typeof(FETCH_NF_AnimController))]
	
	public class PettingMain : MonoBehaviour 
	{
		/****************************************************
		 * Debugger
		 **/
#if DEBUG_DRAGON_WALK_POINTS
		public GameObject targetWalkPoint;
#endif
		
		/****************************************************
		 * UI 
		 **/
		public enum Gauge : byte
		{
			Energy,
			Bond
		}
		
		// Instance
		private static PettingMain m_instance = null;
		
		// Gesture
		[SerializeField]
		private GestureManager m_gestureManager = null;
		
		// Constants
		private readonly Vector3 FOREGROUND_POS		= new Vector3( -0.8416371f, 0.0f, -2.813091f );
		private readonly Vector3 MIDDLE_GROUND_POS 	= new Vector3( -0.1911829f, 0.0f, 1.47473f );
		private readonly Vector3 MIDDLE_GROUND_ROT 	= new Vector3( 0.0f, 200.0f, 0.0f );
		private readonly Vector3 BACKGROUND_POS		= Vector3.zero;
		
		// Dragon
		[SerializeField]
		private DragonScript m_dragonAnimScript;							// DragonAnimationController
		[SerializeField]
		private Dragon m_dragonStateMachine;								// DragpmStateReader
		[SerializeField]
		private GameObject m_petDragon;										// DragonGameObject
		[SerializeField]
		private DragonAnimation m_dragonAnimation;
		[SerializeField]
		private GameObject m_mainCamera;
		[SerializeField]
		private JawCruncherStateController m_jcController;
		
		CameraRigController m_cameraController				= null;
		
		// Bridge Item
		[SerializeField]
		private SheepManager m_sheepManager;
		
		// Dragon Colliders
		[SerializeField]
		private GameObject m_dragonHeadCollider;
		[SerializeField]
		private GameObject m_dragonBodyCollider;
		private GameObject m_mouthJoint;
		private GameObject m_cameraFocusPoint			= null; // + LA 08152013:
		
		private GameObject m_headSoundContainer			= null;
		private GameObject m_bodySoundContainer			= null;
		private GameObject m_headAndBodySoundContainer 	= null;
		
		// UI
		private GameObject m_carePlayUI;
		private GameObject m_carePlayTab;
		private GameObject m_energyGauge;
		public GameObject m_bondGauge;
		public GameObject m_profileFlag;
		
		private GameObject m_swipeUI;
		private GameObject m_swipeHolder;
		private GameObject m_swipeFingerGO;
		private TweenPosition m_swipeFinger;
		private CarePlayUIManager m_uiManager;
		private CarePlayTabManager m_tabManager;
		private Hashtable m_uiRefs;
		private GameObject m_energyBondUI;
		private GameObject m_pauseUI;
		// UI FINGER GUIDE TAP/DOUBLE TAP
		private GameObject m_uiFingerSingleTap = null;
		private GameObject m_uiFingerDoubleTap = null;
		private GameObject m_uiFingerHold	   = null;
		
		private UITexture m_swipeUITexture;
		
		//DEBUG FOR TUTORIAL PURPOSES ONLY - KJ
		public TutorialController.ETutorialState TutorialState;
		
		// DEBUG so that you don't have to wait for intro
		public bool m_bWillTriggerIntro = false;
		private GameObject m_introRock;
		private TweenPosition m_doubleTapTweenPos;
		private TweenAlpha  m_successIconTweenAlpha;
		
		// DEBUG so that you can control that display of dialogues
		public bool bOFFDialogues = false;
		
		private float m_waitTime = 1.0f;
		private bool m_bIsTutorialOn = false; // Please remove this one
		private S6Scheduler.ScheduleController m_usageTrackerScheduler = null;
		
		// UI flags
		public bool bIsUIPresent						= false;
		public bool bIsPaused							= false;
		
		// Array holder of the items
		private List<GameObject> m_fishItems;
		private List<GameObject> m_chickenItems;
		private List<GameObject> m_seafoodItems;
		private List<GameObject> m_fruitsAndVeggiesItems;
		private List<GameObject> m_fetchingItems;
		private List<GameObject> m_currItems;
		private GameObject m_currItem;
		
		public GameObject m_camTutorialJoint = null;
		
		// +KJ:10302013 Paused Timers
		private List<S6Scheduler.ScheduleController> m_pausedTimers	= null;
		
		/****************************************************
		 * Initialization 
		 **/
		public void Awake ()
		{
#if DEBUG_DRAGON_WALK_POINTS
			targetWalkPoint = GameObject.Find("TargetPointMarker");
#endif
			// Trigger if the intro will play or not - BC//
#if !UNITY_EDITOR
			m_bWillTriggerIntro = StatsManager.Instance.CoveIntro;
#endif
			if( ! m_bWillTriggerIntro
				&& SceneTracker.Instance().CurrentScene == EDCScenes.SC_Invalid 
				&& ( TutorialController.GTutState & TutorialController.ETutorialState.InvalidSceneException ) != TutorialController.GTutState 
				) {
				TutorialController.Instance.TurnOffTutorial();
			} else {
				TutorialController.Debug_TurnOffTutorial = false;
			}
			
			if ( m_camTutorialJoint == null ) {
				m_camTutorialJoint 				= GameObject.Find( "cam_joint" ).gameObject;
			}
			
			m_fishItems							= new List<GameObject>();
			m_chickenItems						= new List<GameObject>();
			m_seafoodItems						= new List<GameObject>();
			m_fruitsAndVeggiesItems				= new List<GameObject>();
			m_fetchingItems						= new List<GameObject>();
			m_currItems							= null;
			m_currItem							= null;
			TutorialDialogueController.Instance.Delegate 	= this;
			TutorialBondProgress tb = TutorialBondProgress.Instance;
			m_pausedTimers						= new List<S6Scheduler.ScheduleController>();
		}
		
		public void Start ()
		{	
			// initialize tables
			EventReader.GetInstance();
			ReactionReader.GetInstance();
			SequenceReader.GetInstance();
			AffectedActionReader.GetInstance();
			MoodPointsReader.GetInstance();
			ReactionTrackingReader.GetInstance();
			TutorialReader.GetInstance();
			
			ResponseManager.Instance.HideIcon();
			InstructionsManager.Instance.HideInstruction();
			
			// hold instance
			m_instance = this;
			
			//Load intro rocks
			m_introRock = Instantiate( Resources.Load( "AnimationTool/INTRO_ROCK" ) ) as GameObject;
			m_introRock.SetActive( false );
			
			// init anim controller utility
			GameObject cfController = GameObject.Find("CAREPLAY_NF_Controller") as GameObject;
			cfController.AddComponent<CarePlayNFController>();
			cfController.AddComponent<BiteItemController>();
			
			// init cam controller utility
			m_cameraController = GameObject.Find ("CAREPLAY_Cam_Controller").GetComponent<CameraRigController>();
			
			// init dragon
			m_petDragon = GameObject.FindGameObjectWithTag("PetDragon") as GameObject;
			m_petDragon.gameObject.AddComponent<DragonScript>();
			m_petDragon.gameObject.AddComponent<DragonAnimation>();
			m_petDragon.gameObject.AddComponent<Dragon>();
			m_petDragon.gameObject.AddComponent<JawCruncherStateController>();

			m_jcController = m_petDragon.gameObject.GetComponent<JawCruncherStateController>();
			
			// init gestures
			m_mainCamera = GameObject.FindGameObjectWithTag("MainCamera").gameObject;
			m_mainCamera.AddComponent<GestureManager>();
			m_gestureManager = Camera.mainCamera.gameObject.GetComponent<GestureManager>();
			m_gestureManager.g_pettingListener = this.GestureListener;
			m_gestureManager.SetPettingDelegate(this);
			CameraController.Instance.SetPettingDelegate(this);
			//m_gestureManager.DisableGestures();
			
			// init bridge items
			m_sheepManager 			= GameObject.Find("SheepManager").GetComponent<SheepManager>();
			m_sheepManager.InitializeSheep();
			
			// init dragon collider
			GameObject HeadCollider = (GameObject)Resources.Load("Prefabs/Petting/DragonColliders/HeadCollider");
			GameObject BodyCollider = (GameObject)Resources.Load("Prefabs/Petting/DragonColliders/BodyCollider");
			m_dragonHeadCollider 	= GameObject.Instantiate(HeadCollider) as GameObject;
			m_dragonBodyCollider 	= GameObject.Instantiate(BodyCollider) as GameObject;
			
			m_dragonHeadCollider.transform.parent = m_petDragon.transform.Find("joint_Spine/joint_Neck/joint_Head").transform;
			m_dragonHeadCollider.name = "HeadCollider";
			m_dragonHeadCollider.transform.localPosition 	= new Vector3(-0.00785967f, -0.03883493f, 0.2275544f);
			m_dragonHeadCollider.transform.localRotation 	= Quaternion.Euler(new Vector3(335.4119f, 184.195f, 1.887401f));
			m_dragonHeadCollider.transform.localScale 		= new Vector3(0.6969064f, 0.4911779f, 1.063113f);
			m_dragonBodyCollider.transform.parent = m_petDragon.transform.Find("joint_Spine").transform;
			m_dragonBodyCollider.name = "BodyCollider";
			m_dragonBodyCollider.transform.localPosition 	= new Vector3(0.0176101f, -0.2866409f, -0.8635432f);
			m_dragonBodyCollider.transform.localRotation 	= Quaternion.Euler(new Vector3(270.0f, 180.0f, 0.0f));
			//m_dragonBodyCollider.transform.localScale 		= new Vector3(1.043613f, 1.421112f, 1.043613f);
			
			//init camera focus point
			m_cameraFocusPoint = new GameObject ( "FocusPoint" );
			m_cameraFocusPoint.tag = "ToothlessFocusPoint";
			GameObject toothlessAxis = GameObject.Find("joint_Spine");
			m_cameraFocusPoint.transform.parent = toothlessAxis.transform;
			m_cameraFocusPoint.transform.position = toothlessAxis.transform.position;
			m_cameraFocusPoint.transform.rotation = Quaternion.identity;
			
			//Sound audio source container
			m_headSoundContainer = new GameObject( "SoundContainer_Head" );
			m_headSoundContainer.transform.parent = toothlessAxis.transform;
			m_headSoundContainer.transform.position = toothlessAxis.transform.position;
			m_headSoundContainer.transform.rotation = Quaternion.identity;
			
			m_bodySoundContainer = new GameObject( "SoundContainer_Body" );
			m_bodySoundContainer.transform.parent = toothlessAxis.transform;
			m_bodySoundContainer.transform.position = toothlessAxis.transform.position;
			m_bodySoundContainer.transform.rotation = Quaternion.identity;
			
			m_headAndBodySoundContainer = new GameObject( "SoundContainer_HeadAndBody" );
			m_headAndBodySoundContainer.transform.parent = toothlessAxis.transform;
			m_headAndBodySoundContainer.transform.position = toothlessAxis.transform.position;
			m_headAndBodySoundContainer.transform.rotation = Quaternion.identity;
			
			// Camera Look Points
			GameObject cHead		  		= new GameObject( "CamPlayerHead" );
			GameObject cBody		  		= new GameObject( "CamPlayerBody" );
			GameObject cLeft		  		= new GameObject( "CamPlayerLeft" );
			GameObject cRight				= new GameObject( "CamPlayerRight" );
			
			cHead.tag 				  		= "CamPlayerHead";
			cBody.tag 				  		= "CamPlayerBody";
			cLeft.tag 				  		= "CamPlayerLeft";
			cRight.tag 				  		= "CamPlayerRight";
			
			cHead.transform.parent 			= m_mainCamera.gameObject.transform;
			cBody.transform.parent 			= m_mainCamera.gameObject.transform;
			cLeft.transform.parent 			= m_mainCamera.gameObject.transform;
			cRight.transform.parent 		= m_mainCamera.gameObject.transform;
			
			cHead.transform.localPosition 	= new Vector3( 0.0f, 0.30f, 0.0f );
			cBody.transform.localPosition 	= new Vector3( 0.0f, -0.30f, 0.0f );
			cLeft.transform.localPosition 	= new Vector3( -1.2f, 0.0f, 0.0f );
			cRight.transform.localPosition 	= new Vector3( 1.2f, 0.0f, 0.0f );
			
			cHead.transform.localScale 		= new Vector3( 0.001f, 0.001f, 0.001f );
			cBody.transform.localScale 		= new Vector3( 0.001f, 0.001f, 0.001f );
			cLeft.transform.localScale 		= new Vector3( 0.001f, 0.001f, 0.001f );
			cRight.transform.localScale 	= new Vector3( 0.001f, 0.001f, 0.001f );
			
			
			// Dragon Look Points
			GameObject lookL 				= new GameObject( "DLookL" );
			GameObject lookR 				= new GameObject( "DLookR" );
			
			lookL.tag						= "DLookL";
			lookR.tag						= "DLookR";
			
			lookL.transform.parent 			= m_petDragon.transform;
			lookL.transform.position 		= new Vector3( 3.50f, 2.00f, -1.00f );
			
			lookR.transform.parent 			= m_petDragon.transform;
			lookR.transform.position 		= new Vector3( -3.50f, 2.00f, -1.00f );
			
			// init ui
			m_uiRefs						= new Hashtable();
			m_carePlayUI 					= GameObject.Find("CareAndPlayHUD") as GameObject;
			m_carePlayUI.GetComponent<CareAndPlayHUD>();
			m_carePlayUI.GetComponent<GamePauseManager>().PauseCallBack = this.PauseGame;
			
			m_carePlayTab					= GameObject.Find("MainTab") as GameObject;
			
			m_uiManager						= m_carePlayUI.AddComponent<CarePlayUIManager>();
			m_uiManager.Initialize();
			m_uiManager.SetCB_Call(this.OnDragonCall);
			m_uiManager.SetCB_Book(this.OnBookOfDragon);
			m_uiManager.SetCB_Inventory(this.OnInventory);
			m_uiManager.SetCB_Inventory_Close(this.OnInventoryClose);
			m_uiManager.SetCB_Map(this.OnMap);
			m_uiManager.SetCB_Profile(this.OnProfile);
			m_uiManager.SetCB_Profile_Close(this.OnProfileClose);
			
			m_tabManager					= m_carePlayTab.GetComponent<CarePlayTabManager>();
			
			m_energyGauge 					= GameObject.Find("EnergyProgress") as GameObject;
			m_bondGauge						= GameObject.Find("BondProgress") as GameObject;
			m_energyBondUI					= GameObject.Find("EnergyBond") as GameObject;
			m_pauseUI						= GameObject.Find("PauseBTN") as GameObject;
			
			StatsManager.Instance.EnergyGauge	= m_energyGauge.GetComponent<EnergyProgress>();
			StatsManager.Instance.BondGauge		= m_bondGauge.GetComponent<BondProgress>();
			
			// UI finger tap
			m_uiFingerSingleTap				= GameObject.Find("SingleTapFinger") as GameObject;
			m_uiFingerDoubleTap 			= GameObject.Find("DoubleTapFinger") as GameObject;
			m_uiFingerHold					= GameObject.Find("HoldFinger") as GameObject;
			
			m_uiFingerSingleTap.transform.parent.localPosition = new Vector3( -6.043911e-05f, 319.0215f, 0f );
			
			m_uiFingerSingleTap.SetActive(false);
			m_uiFingerDoubleTap.SetActive(false);
			m_uiFingerHold.SetActive(false);
			
			// set inventory callbacks
			InventoryUIHandler.OnFeedingItemCallback 	= this.OnFeedingItem;
  			InventoryUIHandler.OnFetchingItemCallback 	= this.OnFetchingItem;
			InventoryUIHandler.OnCarePlayItemCallback 	= this.OnCarePlayItem;
			
			// set Hud Callback to enable gestures
			//m_carePlayUI.GetComponent<CareAndPlayHUD>().SetDialogCloseCB(m_gestureManager.EnableGestures);
			
			// init dragon animation controller
			m_dragonAnimScript = m_petDragon.gameObject.GetComponent<DragonScript>();
			m_dragonAnimScript.SetPettingDelegate(this);
			
			// init dragon animation
			m_dragonAnimation = m_petDragon.gameObject.GetComponent<DragonAnimation>();
			m_dragonAnimation.SetPettingDelegate(this);
			
			m_petDragon.GetComponent<JawCruncherStateController>().SetPettingDelegate( this );
			
			// init dragon animation tool
			DragonAnimationQueue.getInstance().SetDAnim(m_petDragon.gameObject.GetComponent<DragonAnimation>());
			DragonAnimationQueue.getInstance().SetPettingDelegate(this);
			
			// initialize DragonStateMachine
			m_dragonStateMachine = m_petDragon.gameObject.GetComponent<Dragon>();
			m_dragonStateMachine.SetPettingDelegate(this);
			m_dragonStateMachine.Initialize();
			
			// initialize DragonScript
			m_dragonAnimScript.Initialize();
			
			// Setup Pausables
			m_petDragon.AddComponent<Pausable>();
			m_mainCamera.AddComponent<Pausable>();
			
			// reset values
			StatsManager.Instance.DragonMode = StatsManager.MODE_NORMAL; // - LA
			//StatsManager.Instance.DragonMode = StatsManager.MODE_ACT1; // - LA
			
			// on start of touchplay, always set interactive to true
			m_dragonStateMachine.SetIsInteractible(true);
			
			// Swipe UI
			m_swipeUI				= GameObject.Find("SwipeUI");
			m_swipeHolder			= GameObject.Find("SwipeHolder");
			m_swipeFingerGO			= GameObject.Find("SwipeFinger");
			m_swipeFinger			= m_swipeFingerGO.GetComponent<TweenPosition>();
		    m_swipeUITexture		= m_swipeUI.GetComponent<UITexture>();
			m_swipeUITexture.alpha	= 0.3f;
			m_swipeHolder.SetActive(false);
			
			m_swipeUI.GetComponent<UITexture>().alpha 		= 0.5f;
			m_swipeFingerGO.GetComponent<UITexture>().alpha = 0.75f;
		
			
			//Double Tap UI
			m_doubleTapTweenPos 	= GameObject.Find("DoubleTapUI").GetComponent<TweenPosition>();
			
			GameObject successIcon	= GameObject.Find("SuccesIcon");
			UISprite spriteSucccessIcon = successIcon.GetComponent<UISprite>();
			spriteSucccessIcon.alpha = 0.0f;
			m_successIconTweenAlpha = successIcon.GetComponent<TweenAlpha>();
			m_successIconTweenAlpha.onFinished=ResetSuccesAnimation;
			
			// hide UIs
			this.HideUI("DCall");
			
			// Handle Timer for Usage Tracker
			m_usageTrackerScheduler = S6Scheduler.ScheduleAction( this, StatsManager.Instance.UpdateTrackers, 0.5f, S6Scheduler.INFINITE, false);
			
			// update dragon mood for Animation Controllers
			StatsManager.Instance.UpdateMood();
			
			// load objects
			// fish
			m_fishItems.Add(this.CreateObject( "Fish", 							"Prefabs/Feeding/FeedingFish", 1.0f));
			
			// chicken
			m_chickenItems.Add(this.CreateObject( "ChickenLeg", 				"Prefabs/Feeding/FeedingChickenLeg", 1.0f));
			m_chickenItems.Add(this.CreateObject( "ChickenThigh", 				"Prefabs/Feeding/FeedingChickenThigh", 1.0f));
			m_chickenItems.Add(this.CreateObject( "ChickenWing", 				"Prefabs/Feeding/FeedingChickenWing", 1.0f));
			
			// seafood
			m_seafoodItems.Add(this.CreateObject( "Crab", 						"Prefabs/Feeding/FeedingCrab", 1.0f));
			m_seafoodItems.Add(this.CreateObject( "Lobster", 					"Prefabs/Feeding/FeedingLobster", 1.0f));
			m_seafoodItems.Add(this.CreateObject( "Shrimp", 					"Prefabs/Feeding/FeedingShrimp", 1.0f));
			
			// fruits & veggies
			m_fruitsAndVeggiesItems.Add(this.CreateObject( "FruitsAndVeggies", 	"Prefabs/Feeding/FeedingOrange", 1.0f));
			m_fruitsAndVeggiesItems.Add(this.CreateObject( "FruitsAndVeggies", 	"Prefabs/Feeding/FeedingPeach", 1.0f));
			m_fruitsAndVeggiesItems.Add(this.CreateObject( "FruitsAndVeggies", 	"Prefabs/Feeding/FeedingTurnip", 1.0f));
			
			// fetching items
			m_fetchingItems.Add(this.CreateObject( "Boomerang", 				"Prefabs/Fetching/Boomerang", 1.0f));
			
			// adjust boomerang transform
			m_fetchingItems[0].transform.parent			= Camera.main.transform;
			m_fetchingItems[0].transform.localPosition 	= new Vector3( -0.05178565f, -0.51644f, 1.536007f );
			m_fetchingItems[0].transform.localRotation 	= Quaternion.Euler(new Vector3( 0.0f, 283.0f, 69.99999f ));
			
			// Handle disable/enable of gestures during hide/show of overlays
			// These methods are called during the initial add & remove of overlays.
			ScreenManager.Instance.OnDisplayOverlay = () => 
			{
				switch( TutorialController.GTutState )
				{
					case TutorialController.ETutorialState.BondMeterIntro:
					case TutorialController.ETutorialState.PlayerProfileIntro:
						// don't update gestures during this state of tutorial
					break;
					default:
						m_gestureManager.DisableGestures();
					break;
				}
			};
			
			ScreenManager.Instance.OnHideOverlay = () => 
			{
				switch( TutorialController.GTutState )
				{
					case TutorialController.ETutorialState.BondMeterIntro:
					case TutorialController.ETutorialState.PlayerProfileIntro:
						// don't update gestures during this state of tutorial
					break;
					default:
						m_gestureManager.EnableGestures();
					break;
				}
			};
			
			// Check Scenes
			this.HandleScenes();
		}
		
		public void AnimateOverlayForPostFeed()
		{
			GameObject _open = GameObject.Find("OpenArrw");
			VirtualButtonClicker _openActivate = _open.GetComponent<VirtualButtonClicker>();
								 _openActivate.ClickButton();
			
			Utility.SetIsButtonEnabled( "OpenArrw", false );
			Utility.SetIsButtonEnabled( "CloseArrw", false );
			Utility.SetIsButtonEnabled( "BodBtn", false );
			Utility.SetIsButtonEnabled( "MapBtn", false );
			Utility.SetIsButtonEnabled( "InventoryBtn", true );
			Utility.SetIsButtonEnabled( "ProfileBtn", false );
		}
		
		public void CarePlayHudReset()
		{
			GameObject _close = GameObject.Find("CloseArrw");
					   _close.collider.enabled = true;
			
			VirtualButtonClicker _closeActivate = _close.GetComponent<VirtualButtonClicker>();
								 _closeActivate.ClickButton();
			
			Utility.SetIsButtonEnabled( "OpenArrw", true );
			Utility.SetIsButtonEnabled( "CloseArrw", true );
			Utility.SetIsButtonEnabled( "BodBtn", true );
			Utility.SetIsButtonEnabled( "MapBtn", true );
			Utility.SetIsButtonEnabled( "InventoryBtn", true );
			Utility.SetIsButtonEnabled( "ProfileBtn", true );
		}
		
		private GameObject CreateObject (
			string p_name,
			string p_path,
			float  p_scale
		){
			//Debug.Log("PettingMaiDragonStateMachinen::CreateObject p_name:"+p_name+" p_path:"+p_path);
			GameObject obj = Instantiate(Resources.Load( p_path, typeof(GameObject) )) as GameObject;
					   obj.name = p_name;
			
			Rigidbody rigid = obj.GetComponent<Rigidbody>();
			if( rigid != null ) {
				rigid.useGravity = false;
				rigid.isKinematic = true;
			}
			
			// Destroy unwanted component
			Destroy(obj.GetComponent<FeedObjectBezier>());
			
			obj.transform.position = new Vector3(Camera.main.transform.position.x, Camera.main.nearClipPlane + 0.5f, Camera.main.transform.position.z + 1.75f);
			obj.transform.parent = Camera.main.transform;
			//obj.GetComponent<FeedObjectBezier>().enabled = false;
			//obj.GetComponent<ConstantForce>().enabled = false;
			obj.rigidbody.Sleep();
			
			obj.SetActive(false);
			obj.AddComponent<Pausable>().Pause();
			
			return obj;
		}
		
		public void ShowSwitchingItem ()
		{
			if( m_currItems == null ) { return; }
			
			if( m_currItems.Count <= 0 ) { return; }
			
			if( StatsManager.Instance.DragonPlace != "Middleground" ) { return; }
			
			m_currItem = m_currItems[new System.Random(Utility.seed).Next(m_currItems.Count)];
			
			this.HideUI( "InventoryPanel" );
			
			// Switch to Feeding/Fetching
			if ( m_currItem != null ) {
				
				S6Scheduler.ScheduleAction( this, () => 
				{
					m_currItem.SetActive( true );
					iTween.MoveFrom( m_currItem, new Vector3( 0.0f, -2.0f, 1.830233f ), 1.0f );
				}, 1.0f, 1, false );
			}
			
		}
		
		private void HandleScenes ()
		{
			DragonAnimationQueue.getInstance().ClearAll();
			
#if DEBUG_FLYIN_TEST
			SceneTracker.Instance().CurrentScene = EDCScenes.SC_Flight;
#endif
			
			if ( SceneTracker.Instance().CurrentScene == EDCScenes.SC_Invalid ) {
				
				// From Non CarePlay Scenes
				if ( !TutorialController.Instance.ContinueFromSavedTutorial() ) {
					//TODO move to separate scene type & check for first time entry
					S6Scheduler.ScheduleAction( this, this.DoIntroSequence, 0.0f, 1, false );
				}
				
			} else if ( SceneTracker.Instance().CurrentScene == EDCScenes.SC_Fetching ) {
				
				S6Scheduler.ScheduleAction(this, m_gestureManager.EnableGestures, 1.0f, 1, false);
				TutorialStatus = false;
				// +KJ:10252013 remove this trigger
				//m_petDragon.transform.position = new Vector3( -10.00f, 0.00f, 5.00f );
				//m_petDragon.transform.rotation = Quaternion.Euler( new Vector3( 0.00f, 40.00f, 0.00f ) );
				//this.DragonStateMachine.Reaction("Type_Transitions", "Event_Fetching->Petting");
				this.DragonStateMachine.RestartEventTimer();
				this.DragonStateMachine.ResumeEventTimer();
				
				m_dragonAnimScript.LookAtObject("MainCamera", true);
				m_dragonAnimScript.LookAtObject("CamPlayerHead", false);
				TutorialController.Instance.UpdateTutorial();
				
			} else if ( SceneTracker.Instance().CurrentScene == EDCScenes.SC_Feeding ) {
				
				S6Scheduler.ScheduleAction(this, m_gestureManager.EnableGestures, 1.0f, 1, false);
				TutorialStatus = false;
				this.DragonStateMachine.RestartEventTimer();
				this.DragonStateMachine.ResumeEventTimer();
				
				if ( TutorialController.GTutState == TutorialController.ETutorialState.PostFeeding ) {
					
					//m_carePlayUI.SetActive(false);
					//m_petDragon.transform.position = new Vector3( 2.919437f, 0, 20.26693f );
					//m_petDragon.transform.rotation = Quaternion.Euler( 0, 200, 0 );
					
					//Camera.main.transform.position = new Vector3( 3.887726f, 1.522887f, 15.81641f );
					m_gestureManager.DisableGestures();
					
					StatsManager.Instance.DragonMode = StatsManager.MODE_ACT1;
					
				    this.HideUI("PostFeedingUI");
					ResponseManager.Instance.DisplayIcon( "Response_Happy" );
					m_dragonAnimScript.LookAtObject("MainCamera", true);
					m_dragonAnimScript.LookAtObject("CamPlayerHead", false);
				 	TutorialDialogueController.Instance.ActivateTutorial("Reinforce_Feed_Tutorial", "CareAndPlayHUD");
				
				} else if ( TutorialController.GTutState == TutorialController.ETutorialState.PostTiredCue ) {
					TutorialController.Instance.UpdateTutorial();
				}
				
			} else if ( SceneTracker.Instance().CurrentScene == EDCScenes.SC_Flight ) {
				
				TutorialStatus = false;
				
				this.m_gestureManager.DisableGestures();
				this.DragonStateMachine.PauseEventTimer();
				
				// + LA 102313: Force head and eye look off
				PettingMain.Instance.DragonScript.DisableLookAt(true);
				PettingMain.Instance.DragonScript.DisableLookAt(false);
				PettingMain.Instance.DragonScript.LookAtObject("", true);
				PettingMain.Instance.DragonScript.LookAtObject("", false);
				// - LA
				
				// + LA 102313: Parent the camera to the camera controller
				PettingMain.Instance.CP_CameraController.AttachCameraAndReset( Camera.main.transform );
				// - LA
				
				// + LA 102313: Play the fly in animation
				CarePlayNFController.GetPetAnimController().SetBodyAnimStateTo( ECP_BodyAnim.FlyIntoTheCove );
				CarePlayNFController.GetPetAnimController().SetHeadAnimStateTo( ECP_HeadAnim.FlyIntoTheCove );
				// _ LA
				
				S6Scheduler.ScheduleAction(this, OnCompleteFlyIn, 11.3f, 1, false); // + LA 102313: The delay is in sync with fly in animation
				
			} else {
				
				S6Scheduler.ScheduleAction(this, m_gestureManager.EnableGestures, 1.0f, 1, false);
				TutorialStatus = false;
				this.DragonStateMachine.RestartEventTimer();
				this.DragonStateMachine.ResumeEventTimer();
				
			}
			
			SceneTracker.Instance().CurrentScene = EDCScenes.SC_Petting;
		}
		
		#region Intro sequence
		private void DoIntroSequence ()
		{
			if ( !m_bWillTriggerIntro )  {
				this.ContinueFromIntro();
				TutorialStatus = false;
				return;
			}
				
			//TutorialStatus = true; // - LA
			TutorialStatus = false;
			TutorialController.Instance.UpdateTutorial();
			
			m_introRock.SetActive( true );
			
			StatsManager.Instance.DragonMode = "Act1";
			
			//Disable stuff
			this.GestureManager.DisableGestures();
			this.DragonStateMachine.PauseEventTimer();
			m_dragonStateMachine.EventTimer.PauseScheduler();
			m_dragonStateMachine.CoveTimer.IsPaused();
			m_dragonStateMachine.StatusUpdater.PauseScheduler();
			m_carePlayUI.gameObject.SetActive( false );
			
			// Force off Head & Eye look
			m_dragonAnimScript.DisableLookAt(true);
			m_dragonAnimScript.DisableLookAt(false);
			m_dragonAnimScript.LookAtObject("", true);
			m_dragonAnimScript.LookAtObject("", false);
				
			m_petDragon.transform.position = Vector3.zero;
			m_petDragon.transform.rotation = Quaternion.identity;
			Camera.main.nearClipPlane = 0.1f;
			PettingMain.Instance.CP_CameraController.AttachCameraAndReset( Camera.main.transform );
			CarePlayNFController.GetPetAnimController().SetBodyAnimStateTo( ECP_BodyAnim.Act1_01_opening );
			CarePlayNFController.GetPetAnimController().SetHeadAnimStateTo( ECP_HeadAnim.Act1_01_opening );
			
			S6Scheduler.ScheduleAction( this, OnCompleteCinematic, 35.0f, 1 );
		}
		
		private void OnCompleteCinematic ()
		{
			//InstructionsManager.Instance.DisplayInstruction( "Single tap on the ground to move forward" );
			ShowTapFingerGuide(1);
			m_gestureManager.EnableGestures ();
		}
		
		private void OnCompleteFlyIn ()
		{
			PettingMain.Instance.DragonScript.LookAtObject("CamPlayerHead", true);
			PettingMain.Instance.DragonScript.LookAtObject("MainCamera", false);
			
			CarePlayNFController.GetPetAnimController().SetBodyAnimStateTo( ECP_BodyAnim.IdleLookAround );
			CarePlayNFController.GetPetAnimController().SetHeadAnimStateTo( ECP_HeadAnim.Default );
			
			this.DragonStateMachine.RestartEventTimer();
			this.DragonStateMachine.ResumeEventTimer();
			
			m_gestureManager.EnableGestures();
		}
		
		public void DelayIntroTutorial ()
		{
			S6Scheduler.ScheduleAction( this, this.ShowIntroTutorial, 13.0f, 1 );
		}
		
		public void ShowIntroTutorial ()
		{
			// Look at Camera
			TutorialController.Instance.UpdateTutorial();
			
			m_dragonAnimScript.LookAtPos(Camera.main.transform.position, true);
			m_dragonAnimScript.LookAtPos(Camera.main.transform.position, false);
			
			this.ContinueFromIntro();
			
			if ( TutorialStatus ) {
				this.TutorialOnShow("Tutorial_DragonCall_Intro");
			}
			
			// Check tutorials at the end of current frame
			S6Scheduler.ScheduleAction(this,
			() => {
				TutorialDialogueController.Instance.ReadTutorialMeta();
			}, 0.0f, 1, false);
			
			// manual position dragon near rocks
			
			//DEBUG -- DELETE AFTER ALL TUTORIAL ARE DONE 
			
			m_petDragon.transform.position = new Vector3( 2.919437f, 0.0f, 20.26693f );
		}	
		
		private void ContinueFromIntro ()
		{
			CarePlayNFController.GetPetAnimController().SetBodyAnimStateTo( ECP_BodyAnim.IdleLookAround );
			CarePlayNFController.GetPetAnimController().SetHeadAnimStateTo( ECP_HeadAnim.Default );

			Camera.main.transform.parent = null;
			Camera.main.nearClipPlane = 0.3f;
			m_gestureManager.EnableGestures();
			m_carePlayUI.gameObject.SetActive( true );
			this.DragonStateMachine.RestartEventTimer();
			this.DragonStateMachine.ResumeEventTimer();
			
			// Default Position and Rotation
			m_petDragon.transform.position = MIDDLE_GROUND_POS;
			m_petDragon.transform.rotation = Quaternion.Euler(MIDDLE_GROUND_ROT);
			
			// Look at Camera
			m_dragonAnimScript.DisableLookAt(true);
			m_dragonAnimScript.DisableLookAt(false);
			m_dragonAnimScript.LookAtObject("MainCamera", true);
			m_dragonAnimScript.LookAtObject("MainCamera", false);
		}
		#endregion
		
		public void OnDestroy ()
		{
			StatsManager.Instance.ResetDragonStatus();
			m_instance = null;
			m_usageTrackerScheduler.StopScheduler();
			m_usageTrackerScheduler.StopCoroutine();
			m_usageTrackerScheduler = null;
			Pausable.Reset();
		}
		
		#region G E T T E R | S E T T E R
		public GestureManager GestureManager
		{
			get { return m_gestureManager; }
		}
		
		public Dragon DragonStateMachine
		{
			get { return m_dragonStateMachine; }	
		}
		
		public DragonScript DragonScript
		{
		 	get { return m_dragonAnimScript; }
		}
		
		public DragonAnimation DragonAnimation
		{
			get { return m_dragonAnimation; }
		}
		
		public CarePlayUIManager UIManager
		{
			get { return m_uiManager; }
		}
		
		public CarePlayTabManager TabManager
		{
			get { return m_tabManager; }
		}
		
		public CameraRigController CP_CameraController
		{
			get { return m_cameraController; }
		}
		
		public GameObject DragonGameObject
		{
			get { return m_petDragon; }
		}
		
		public GameObject HeadSoundContainer
		{
			get { return m_headSoundContainer; }
		}
		
		public GameObject BodySoundContainer
		{
			get { return m_bodySoundContainer; }
		}
		public GameObject HeadAndBodySoundContainer
		{
			get { return m_headAndBodySoundContainer; }
		}
		
		public SheepManager SheepManager
		{
			get { return m_sheepManager; }
		}
		
		public GameObject DragonHeadObject 
		{
			get { return m_dragonHeadCollider; }
		}
		
		public bool TutorialStatus
		{
			set { m_bIsTutorialOn = value; }
			get { return m_bIsTutorialOn; }
		}
		
		public bool IsTabOpen
		{
			get { return m_tabManager.IsTabOpen; }
		}	
		#endregion
		
		public void ShowUI ( string p_uiTag )
		{
			// Show Main UI
			if ( p_uiTag == "MainUI" ) {
				m_tabManager.ToggleTab( true );
				return;
			}
			
			// Show EnergyBondUI
			if ( p_uiTag == "EnergyBond" ) {
				m_energyBondUI.SetActive( true );
			}
			
			// Show PauseUI
			if ( p_uiTag == "PauseBTN" ) {
				m_pauseUI.SetActive( true );
			}
			
			GameObject obj = m_uiRefs[p_uiTag] as GameObject;
			
			if ( obj == null ) { return; }
			
			obj.SetActive(true);
			m_uiRefs.Remove(obj);
		}
		
		public void HideUI ( string p_uiTag )
		{
			//Debug.LogError("HideUI:"+p_uiTag);
			
			// Hide Whole UI with Animation
			if ( p_uiTag == "InventoryPanel" ) {
				GameObject inventoryPanel = GameObject.Find("InventoryPanel");
				
				if ( inventoryPanel == null ) { return; }
				
				InventoryUIHandler inventory = inventoryPanel.GetComponent<InventoryUIHandler>();
				inventory.OnHideHUD();

				return;
			}
			
			// Hide EnergyBondUI
			if ( p_uiTag == "EnergyBond" ) {
				m_energyBondUI.SetActive( false );

			}
			
			// Hide PauseUI
			if ( p_uiTag == "PauseBTN" ) {
				m_pauseUI.SetActive( false );
			}
			
			// Hide Main UI
			if ( p_uiTag == "MainUI" ) {
				m_tabManager.ToggleTab( false );
				return;
			}
			
			if ( p_uiTag == "PostFeedingUI" ) {
				DisableUIForTutorialPostFeeding();
			} else {
				GameObject obj = GameObject.FindGameObjectWithTag(p_uiTag);
						   obj.SetActive(false);
				m_uiRefs[p_uiTag] = obj;

			}
		}

		public void FillWater ()
		{
			OutBucket[] buckets = GameObject.FindObjectsOfType(typeof(OutBucket)) as OutBucket[];
			foreach( OutBucket bucket in buckets )
			{
				GameObject waterOnBucket = GameObject.Find("WaterTexture").gameObject;
				GameObject splashOnBucket = GameObject.Find("Burst").gameObject;

				if ( waterOnBucket ){
					iTween.ScaleAdd ( waterOnBucket , iTween.Hash ( "y", 0.175f ) );
					splashOnBucket.GetComponent<ParticleSystem>().Play();
				}
			}
		}
		
		private void DisableUIForTutorialPostFeeding ()
		{
			GameObject _energyBond = GameObject.Find("EnergyBond");
			GameObject _pause	   = GameObject.Find("PauseBTN");
			
			_energyBond.SetActive(false);
			_pause.SetActive(false);

		}
		
		// update
		public void Update ()
		{
			// +KJ:10072013 Test Raycasting
			//RaycastHit hit;
	        //if (!Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), out hit))
	        //    return;
	        /*
	        MeshCollider meshCollider = hit.collider as MeshCollider;
	        if (meshCollider == null || meshCollider.sharedMesh == null)
	            return;
	        
	        Mesh mesh = meshCollider.sharedMesh;
	        Vector3[] normals = mesh.normals;
	        int[] triangles = mesh.triangles;
	        Vector3 n0 = normals[triangles[hit.triangleIndex * 3 + 0]];
	        Vector3 n1 = normals[triangles[hit.triangleIndex * 3 + 1]];
	        Vector3 n2 = normals[triangles[hit.triangleIndex * 3 + 2]];
	        Vector3 baryCenter = hit.barycentricCoordinate;
	        Vector3 interpolatedNormal = n0 * baryCenter.x + n1 * baryCenter.y + n2 * baryCenter.z;
	      			interpolatedNormal = interpolatedNormal.normalized;
	        Transform hitTransform = hit.collider.transform;
	        interpolatedNormal = hitTransform.TransformDirection(interpolatedNormal);
			
	        Debug.DrawRay(hit.point, interpolatedNormal);
			//*/
			
			//Debug.LogError("Point:"+hit.point+" IntNormal:"+interpolatedNormal);
			//Debug.LogError("Point:"+hit.point);
			
			//if( hit.point.x >= -0.9 )
			//	Debug.LogError("0");
			//else
			//	Debug.LogError("1");
		}
		
		/****************************************************
		 * UI Callbacks
		 **/
		public void PauseGame ( bool p_bIsPaused )
		{
			//Debug.LogError("PettingMain::PauseGame "+p_bIsPaused+"\n");
			
			m_sheepManager.SheepIsPaused(p_bIsPaused);
			m_dragonStateMachine.PauseGame(p_bIsPaused);
			//CarePlayNFController.GetPetAnimController().SetIsPaused(p_bIsPaused);
			CarePlayNFController.GetFetchAnimController().SetIsPaused(p_bIsPaused);
			CarePlayNFController.GetFeedAnimController().SetIsPaused(p_bIsPaused);
			//CarePlayNFController.GetJCAnimController().SetIsPaused(p_bIsPaused);
			CarePlayNFController.GetEyeLookController().SetIsPaused(p_bIsPaused);
			CarePlayNFController.GetDrivableController().SetIsPaused(p_bIsPaused);
			// pause these objests. (m_gameObjectRefs)
			
			// Pause all pausables & cues
			if ( p_bIsPaused ) {
				DragonAnimationQueue.getInstance().PauseActionCues();
				Pausable.PauseAll();
			} else {
				DragonAnimationQueue.getInstance().ResumeActionCues();
				Pausable.ResumeAll();
			}
			
			bIsPaused = p_bIsPaused;
		}
		
		public void PauseTimer ( S6Scheduler.ScheduleController p_controller )
		{
			if ( p_controller == null ) { return; }
			
			if ( ! p_controller.IsPaused() ) {
				m_pausedTimers.Add( p_controller );
				p_controller.PauseScheduler();
			}
		}
		
		public void ResumeTimers ()
		{
			foreach ( S6Scheduler.ScheduleController c in m_pausedTimers ) {
				c.ResumeScheduler();
			}
			m_pausedTimers.Clear();
		}
		
		public void UpdateGauge ( Gauge p_gauge, float p_val )
		{
			switch(p_gauge)
			{
				case Gauge.Bond:
					//m_bondGauge.GetComponent<BondProgress>().addValue(p_val);
				break;
				case Gauge.Energy:
					m_energyGauge.GetComponent<EnergyProgress>().addValue(p_val);
				break;
			}	
		}
		
		public EnergyProgress GetGauge 
		{
			get { return m_energyGauge.GetComponent<EnergyProgress>(); }
		}
		
		// +KJ:10072013 TODO: Please apply our current tutorial system here
		public void TutorialOnShow ( string p_tutorialId )
		{
		}
		
		// +KJ:10072013 TODO: Please remove this
		public void TutorialOnClose ()
		{
		}
		
		public void OnDragonCall ()
		{
			string name = "";
			if ( UserPrefs.Instance.GetDragonCallRecorded() == 1 ) {
				name = "DragonCall";
			} else {
				name = "DragonCallDefault";
			}
			
			SoundManager.Instance.PlayDragonCall( Camera.mainCamera.gameObject, name, "Buttons", UserPrefs.Instance.GetDragonCallRecorded() );
			m_dragonStateMachine.DragonCallDone();
		}
		
		private void OnBookOfDragon ()
		{
			TutorialDialogueController.Instance.Trigger( ETutorialEvents.Evt_BOD );
		}
		
		private void OnInventory ()
		{
			TutorialDialogueController.Instance.Trigger( ETutorialEvents.Evt_Inventory );
			this.PauseTimer( m_dragonStateMachine.EventTimer );
			this.PauseTimer( m_dragonStateMachine.CoveTimer );
			this.PauseTimer( m_dragonStateMachine.TutorialTimer );
			this.PauseTimer( m_dragonStateMachine.StatusUpdater );
			this.PauseTimer( m_dragonStateMachine.Sprint9DemoTimer );
			this.PauseTimer( m_dragonStateMachine.IdleTimer );
			this.PauseGame( true );
		}
		
		private void OnInventoryClose ()
		{
			this.ResumeTimers();
			this.PauseGame( false );
		}
		
		private void OnMap ()
		{
			TutorialDialogueController.Instance.Trigger( ETutorialEvents.Evt_Map );
		}
		
		private void OnProfile ()
		{
			PettingMain.Instance.DragonStateMachine.PauseEventTimer();
			PettingMain.Instance.bIsUIPresent = true;
			TutorialDialogueController.Instance.Trigger( ETutorialEvents.Evt_Player_Profile );
			GameObject dbox = GameObject.Find( "DialogBox" ) as GameObject;
			if ( dbox != null ) {
				dbox.GetComponent<Pausable>().enabled = false;
				//GameObject.Destroy(dbox.GetComponent<Pausable>());
			}
			this.PauseGame(true);
		}
		
		private void OnProfileClose ()
		{
			//Debug.LogError("PettingMain::OnProfileClose");
			
			if( TutorialController.GTutState == TutorialController.ETutorialState.PlayerProfileIntro ) {
				TutorialController.Instance.UpdateTutorial();
			}
			
			Utility.SetIsButtonEnabled("ProfileBtn",true);
			
			PettingMain.Instance.DragonStateMachine.ResumeEventTimer();
			PettingMain.Instance.bIsUIPresent = false;
			this.PauseGame(false);
		}
		
		public void OnFeedingItem ( EFoodItems p_itemId )
		{
			// always reset the current feeding/fetching item
			m_currItem = null;
			m_currItems = null;
			
			DragonDNA.Instance.currToy = EToyItems.Invalid;
			DragonDNA.Instance.currFood = p_itemId;
			DragonDNA.Instance.currCarePlay = ECarePlayItems.Invalid;
			
			// Resume timers
			this.OnInventoryClose();
			
			// sanity checking during JC Mode
			if ( StatsManager.Instance.DragonMode != StatsManager.MODE_NORMAL 
				 && StatsManager.Instance.DragonMode != StatsManager.MODE_ACT1
			){ return; }
			
			// Set Random Seed
			Utility.SetSeed(Utility.RANDOM_INT( 0, 1000 ));
			
			switch( p_itemId )
			{
				// Normal Switch Screen
				case EFoodItems.FruitsAndVeggies:	
					m_currItems = m_fruitsAndVeggiesItems;
					this.SwitchTo( () => m_dragonStateMachine.Reaction("Type_From_Inventory", "Event_Switch_To_Feeding"), 2.0f );
					
					// TODO: Call this method thru tables
					//  Temp: Hard coded refusal checking
					if ( AccountManager.Instance.EnergyMeter < 100.0f ) {
						this.ShowSwitchingItem();
					}
				break;
				case EFoodItems.Seafood:	
					m_currItems = m_seafoodItems;
					this.SwitchTo( () => m_dragonStateMachine.Reaction("Type_From_Inventory", "Event_Switch_To_Feeding"), 2.0f );
					
					// TODO: Call this method thru tables
					//  Temp: Hard coded refusal checking
					if ( AccountManager.Instance.EnergyMeter < 100.0f ) {
						this.ShowSwitchingItem();
					}
				break;
				case EFoodItems.Fish:				
					m_currItems = m_fishItems;
					this.SwitchTo( () => m_dragonStateMachine.Reaction("Type_From_Inventory", "Event_Switch_To_Feeding"), 2.0f );
					
					// TODO: Call this method thru tables
					//  Temp: Hard coded refusal checking
					if ( AccountManager.Instance.EnergyMeter < 100.0f ) {
						this.ShowSwitchingItem();
					}
				break;
				
				//KJ + Added For Tutorial
				case EFoodItems.TutorialFish:				
					m_currItems = m_fishItems;
					this.SwitchTo( () => m_dragonStateMachine.Reaction("Type_From_Inventory", "Event_Switch_To_Feeding_Tutorial"), 2.0f );
					
					// TODO: Call this method thru tables
					if ( AccountManager.Instance.EnergyMeter < 100.0f ) {
						this.ShowSwitchingItem();
					}
				break;
				
				case EFoodItems.Chicken:			
					m_currItems = m_chickenItems;
					this.SwitchTo( () => m_dragonStateMachine.Reaction("Type_From_Inventory", "Event_Switch_To_Feeding"), 2.0f );
					
					// TODO: Call this method thru tables
					//  Temp: Hard coded refusal checking
					if ( AccountManager.Instance.EnergyMeter < 100.0f ) {
						this.ShowSwitchingItem();
					}
				break;
				
				// Canned
				case EFoodItems.DriedTreats:
					m_dragonStateMachine.Reaction("Type_From_Inventory", "Event_Canned_Dried_Insect");
				break;
				case EFoodItems.Eel:
					m_dragonStateMachine.Reaction("Type_From_Inventory", "Event_Canned_Eel");
				break;
				case EFoodItems.Water:
					GameObject  waterBucket=GameObject.Find("waterbucket");
				
					if ( waterBucket == null ) {	
					
						m_dragonStateMachine.Reaction("Type_From_Inventory", "Event_Canned_Water");
					
					} else {
					
						if( StatsManager.Instance.DragonMode == "WaterBucket"
							&& m_dragonStateMachine.IsInteractible()
						){
							string strEvent 			= "Event_Water_Bucket";
							string strEventType 		= "Type_From_Inventory";
							m_dragonStateMachine.GestureReaction(strEventType, strEvent, null);
						}
					}
				break;
				//case EFoodItems.ScaleOil:
				//	m_dragonStateMachine.Reaction("Type_From_Inventory", "Event_Canned_Scale_Oil");
				//break;
			}
		}
		
		public void OnFetchingItem ( EToyItems p_itemId )
		{
			// always reset the current feeding/fetching item
			m_currItem = null;
			m_currItems = null;
			
			DragonDNA.Instance.currToy = p_itemId;
			DragonDNA.Instance.currFood = EFoodItems.Invalid;
			DragonDNA.Instance.currCarePlay = ECarePlayItems.Invalid;
			
			// Resume timers
			this.OnInventoryClose();
			
			// sanity checking during JC Mode
			if( StatsManager.Instance.DragonMode != StatsManager.MODE_NORMAL ) { return; }
			
			switch( p_itemId )
			{
				// Normal Switch Screen
				case EToyItems.Boomerang:
					m_currItems = m_fetchingItems;
					this.SwitchTo( () => m_dragonStateMachine.Reaction("Type_From_Inventory", "Event_Switch_To_Fetching"), 2.0f );
				
					// Set Random Seed
					Utility.seed = Utility.RANDOM_IN_TWO_INT( 0, 1000 );
				
					// TODO: Call this method thru tables
					//  Temp: Hard coded refusal checking
					if ( !(AccountManager.Instance.EnergyMeter <= 0.0f) ) {
						this.ShowSwitchingItem();
					}
				break;
				// IUC Jaw Cruncher
				case EToyItems.JawCruncher:
					
					m_dragonStateMachine.GestureReaction( "Type_Touch", "Event_Hold", null );
					m_jcController.bIsFromInventory = true;	

				break;
			}
		}
		
		public void OnCarePlayItem ( ECarePlayItems p_itemId )
		{
			string evtType = string.Empty;
			string evt = string.Empty;
			
			DragonDNA.Instance.currToy = EToyItems.Invalid;
			DragonDNA.Instance.currFood = EFoodItems.Invalid;
			DragonDNA.Instance.currCarePlay = p_itemId;
			
			// Resume timers
			this.OnInventoryClose();
			
			switch( p_itemId )
			{
				case ECarePlayItems.DentalKit:
				
					evtType = "Type_From_Inventory";
					evt = "Event_Dental_Kit_Ready";
					
					// +KJ:10182013 Gesture Blocking
					if ( !TutorialController.Instance.IsValidGestureForTutorial( evtType, evt, null ) ) { return; }
					
					m_dragonStateMachine.SetMouthCollidersActive( true );
					m_dragonStateMachine.AddDirtyMaterial();
					m_dragonStateMachine.Reaction( evtType, evt );

					PettingMain.Instance.HideUI("MainUI");
				break;
				
				case ECarePlayItems.DragonSalve:
						
					evtType = "Type_From_Inventory";
					evt = "Event_Dragon_Salve_Ready";
				
					// +KJ:10182013 Gesture Blocking
					if ( !TutorialController.Instance.IsValidGestureForTutorial( evtType, evt, null ) ) { return; }
						
					m_dragonStateMachine.Reaction( evtType, evt );
					
					PettingMain.Instance.HideUI("MainUI");
				break;
				
				case ECarePlayItems.SoapAndBrush:
				
					evtType = "Type_From_Inventory";
					evt = "Event_Bath_Ready";
				
					if ( !TutorialController.Instance.IsValidGestureForTutorial( evtType, evt, null ) ) { return; }
				
					m_dragonStateMachine.Reaction( evtType, evt );

					PettingMain.Instance.HideUI("MainUI");
				break;
			}
			
		}
		
		public void SwitchTo ( 
			Action p_action, 
			float p_delay
		){
			this.GestureManager.DisableGestures();
			m_dragonStateMachine.SetIsInteractible( false );
			S6Scheduler.ScheduleAction( this, () =>
			{
				m_dragonStateMachine.SetIsInteractible( true );
				p_action();
			},
			p_delay,
			1,
			false);
		}
		
		public void OnExitPetting ( string p_scene )
		{
			StatsManager.Instance.ResetDragonStatus();
			StatsManager.Instance.ToothlessLastPosition 	= m_petDragon.transform.position;
			StatsManager.Instance.ToothlessLastRotation 	= m_petDragon.transform.rotation;
			GameObject cfController = GameObject.Find("CAREPLAY_NF_Controller") as GameObject;
			StatsManager.Instance.ToothlessLastHeadPosition = cfController.GetHeadLookController().HeadLookTransform.position;
			StatsManager.Instance.ToothlessLastHeadRotation = cfController.GetHeadLookController().HeadLookTransform.rotation;
			StatsManager.Instance.CameraLastPosition 		= Camera.mainCamera.transform.position;
			StatsManager.Instance.CameraLastRotation 		= Camera.mainCamera.transform.rotation;
			StatsManager.Instance.CameraLastFOV 			= Camera.mainCamera.fieldOfView;
			PET_NF_AnimController pettingController = cfController.GetPetAnimController();
			pettingController.SetBodyAnimStateTo( ECP_BodyAnim.IdleLookAround );
			pettingController.SetHeadAnimStateTo( ECP_HeadAnim.Default );
			StatsManager.Instance.ToothlessLastHeadTongueBodyNormTime = new Vector3( 
				pettingController.GetCurAnimNormTime( ERigType.Head ),
				pettingController.GetCurAnimNormTime( ERigType.Tongue ),
				pettingController.GetCurAnimNormTime( ERigType.Body ) );
			
			AutoFade.LoadLevel(p_scene,1f,0.5f,Color.black , (int)Constants.ScreenTransitionType.seemlessLike);
		}
		
		/****************************************************
		 * Gesture Listeners
		 **/
		public void GestureListener ( GestureObject p_gesture )
		{
			m_dragonStateMachine.Trigger(p_gesture);
		}
		
		/****************************************************
		 * Check To Open Inventory
		 **/
		public void CheckInventoryEvents ( 
			string p_evtType,
			string p_evt,
			string p_touched
		){
			if ( p_evtType == "Type_Touch" && p_evt == "Event_Tap" ) {
				
				if ( p_touched == "FishBone" ) {
					
					m_uiManager.OnInventory();
					
				} else if ( p_touched == "Boomerang" ) {
					
					m_currItems = m_fetchingItems;
					this.ShowSwitchingItem();
					this.OnExitPetting("Fetching");
					
				}
				// Fetching Boomerang
				// FeedingModule
			}
		}
		
		/****************************************************
		 * Instance Ref
		 **/
		public static PettingMain Instance
		{
			get{ return m_instance; }
		}
		
		public void AnimateDoubleTap ()
		{
			m_doubleTapTweenPos.Play( true );
		}
		
		public void AnimateSuccessAnimation ()
		{
			m_successIconTweenAlpha.Play( true );
		}
		
		public void ResetSuccesAnimation ( UITweener p_tween )
		{
			m_successIconTweenAlpha.Play( false );
		}  
		
#region Swipe positions
		private Vector3 m_swipeStartPos;
		private Vector3 m_swipeFrom;
		private Vector3 m_swipeTo;
#endregion
		
		public void ShowSwipeUI ( FingerGestures.SwipeDirection p_swipeDirection )
		{
			switch ( p_swipeDirection ) 
			{
				case FingerGestures.SwipeDirection.Up:
					
					m_swipeHolder.SetActive(true);
					m_swipeFingerGO.SetActive(true);
					m_swipeFinger.enabled = true;
				
					m_swipeUI.transform.localScale	= new Vector3(84.0f,266.0f,0.2f);
					m_swipeUI.transform.eulerAngles	= Vector3.zero;
					m_swipeFingerGO.transform.eulerAngles = new Vector3(0, 0, 37);
					m_swipeStartPos =					new Vector3(90, -200, 0);
					m_swipeFingerGO.transform.position = m_swipeStartPos;
					m_swipeFrom = 						new Vector3(90, -200, 0);
					m_swipeFinger.from = 				m_swipeFrom;
					m_swipeTo = 						new Vector3(90, 50, 0);
					m_swipeFinger.to = 					m_swipeTo;
					m_swipeFinger.Toggle();
						
				break;
				
				case FingerGestures.SwipeDirection.Down:  
				case FingerGestures.SwipeDirection.Right: 
				case FingerGestures.SwipeDirection.Left:
				
					m_swipeFinger.enabled = true;
					iTween.ValueTo(this.gameObject, iTween.Hash("from", 1.0f, 
																"to", 0, 
																"onUpdate", "SwipeUIFadeOut", 
																"oncomplete", "UpdateSwipeUI", 
																"oncompleteparams", p_swipeDirection, 
																"oncompletetarget", this.gameObject ));
					
				break;
			}
			
		}
		
		public void HideSwipeUI ()
		{
			iTween.ValueTo(this.gameObject, iTween.Hash("from", 1.0f, 
														"to", 0, 
														"onUpdate", "SwipeUIFadeOut", 
														"oncomplete", "DisableSwipeUI", 
														"oncompletetarget", this.gameObject));
		}
		
		void SwipeUIFadeOut ( float p_alpha )
		{
			float arrowAlp = p_alpha * 0.5f;
			float handAlp  = p_alpha * 0.75f;
			
			m_swipeUI.GetComponent<UITexture>().alpha = arrowAlp;
			m_swipeFingerGO.GetComponent<UITexture>().alpha = handAlp;
		}	
		
		void UpdateSwipeUI ( FingerGestures.SwipeDirection p_swipeDirection )
		{
			m_swipeFinger.enabled = false;
			m_swipeFinger.Reset();
			
			m_swipeUI.GetComponent<UITexture>().alpha = 0.5f;
			m_swipeFingerGO.GetComponent<UITexture>().alpha = 0.75f;
		
			switch ( p_swipeDirection )
			{
				case FingerGestures.SwipeDirection.Down:
				
					m_swipeUI.transform.localScale	= new Vector3(84.0f,-266.0f,0.2f);
					m_swipeUI.transform.eulerAngles	= Vector3.zero;
					m_swipeFingerGO.transform.eulerAngles = new Vector3(0, 0, 37);
					m_swipeStartPos =					new Vector3(90, 0, 0);
					m_swipeFingerGO.transform.position = m_swipeStartPos;
					m_swipeFrom = 						new Vector3(90, 0, 0);
					m_swipeFinger.from = 				m_swipeFrom;
					m_swipeTo = 						new Vector3(90, -220, 0);
					m_swipeFinger.to = 					m_swipeTo;
					m_swipeFinger.enabled = true;
				
				break;

				case FingerGestures.SwipeDirection.Left:
				
				    Debug.Log("SWIPE LEFT");
					m_swipeUI.transform.localScale =  new Vector3(84.0f, 266.0f,0.2f);
					m_swipeUI.transform.eulerAngles=new Vector3(0, 0, 90);
					m_swipeFingerGO.transform.eulerAngles = new Vector3(0, 0, 37);
					m_swipeStartPos =					new Vector3(200, -100, 0);
					m_swipeFingerGO.transform.position = m_swipeStartPos;
					m_swipeFrom = 						new Vector3(200, -100, 0);
					m_swipeFinger.from = 				m_swipeFrom;
					m_swipeTo = 						new Vector3(-50, -100, 0);
					m_swipeFinger.to = 					m_swipeTo;
					m_swipeFinger.enabled = true;
					
				break;
				
				case FingerGestures.SwipeDirection.Right:
					
					Debug.Log("SWIPE RIGHT");
					m_swipeUI.transform.localScale = new Vector3(84.0f, -266.0f,0.2f);
					m_swipeUI.transform.eulerAngles=new Vector3(0, 0, 90);
					m_swipeFingerGO.transform.eulerAngles = new Vector3(0, 0, 37);
					m_swipeStartPos =					new Vector3(0, -100, 0);
					m_swipeFingerGO.transform.position = m_swipeStartPos;
					m_swipeFrom = 						new Vector3(0, -100, 0);
					m_swipeFinger.from = 				m_swipeFrom;
					m_swipeTo = 						new Vector3(230, -100, 0);
					m_swipeFinger.to = 					m_swipeTo;
					m_swipeFinger.enabled = true;
					
				break;
				
				m_swipeFinger.Toggle();
			}
			
			m_swipeHolder.SetActive(true);
			m_swipeFingerGO.SetActive(true);
		}
		
		public void ResetSwipeUI ()
		{
			S6Scheduler.ScheduleAction(this, () => {
				m_swipeFinger.Reset();
				m_swipeFingerGO.transform.position = m_swipeStartPos;
				m_swipeFinger.from = m_swipeFrom;
				m_swipeFinger.to = m_swipeTo;
				m_swipeFinger.Toggle();
			},
			1.0f,
			1);
		}
		
		public void DisableSwipeUI ()
		{
			m_swipeHolder.SetActive(false);
			m_swipeFingerGO.SetActive(false);
		}
		
		#region TAP FINGER GUIDE
		public void ShowTapFingerGuide ( int p_status )
		{
			//-0 hide all
			//-1 single tap
			//-2 double tap
			
			if ( m_uiFingerSingleTap.activeSelf == true ) { m_uiFingerSingleTap.SetActive(false); }
			if ( m_uiFingerDoubleTap.activeSelf == true ) { m_uiFingerDoubleTap.SetActive(false); }
			if ( m_uiFingerHold.activeSelf == true ) 	 { m_uiFingerHold.SetActive(false); }
			
			if ( p_status  == 1 ) {
				
				m_uiFingerSingleTap.SetActive(true);
				
			} else if ( p_status == 2 ) {
				
				m_uiFingerDoubleTap.SetActive(true);
				
			} else if ( p_status == 3 ) {
				
				m_uiFingerHold.SetActive(true);
				
			} else {
				
				m_uiFingerSingleTap.SetActive(false);
				m_uiFingerDoubleTap.SetActive(false);
				m_uiFingerHold.SetActive(false);
				
			}
		}
		#endregion
		
		private float m_sliderPosition = 0;
		private bool uitabFlag = false;
		private void OnGUI ()
		{
			if ( Input.GetKeyUp( KeyCode.X ) )
			{
				AutoFade.LoadLevel("PettingDebug" ,0.5f,0.5f,Color.black, (int)Constants.ScreenTransitionType.loadingScreen);
				//m_dragonStateMachine.PlayActionSequence("Cue_ItchyAche_GoodMood");
			}
				
			if ( Input.GetKeyUp( KeyCode.Z ) )
			{
				m_dragonStateMachine.PlayActionSequence("Cue_Thirsty_NeutralMood");
			}
			
			if ( Utility.CreateEnableButton("toggle ui", 100, 300, 100, 25) ) {
				
				Utility.OverAllUIFlag = !Utility.OverAllUIFlag;
		
			}
			
			/*
			if( Utility.CreateEnableButton("close ui", 100, 300, 100, 25) )
			{
				//Utility.OverAllUIFlag = !Utility.OverAllUIFlag;
				uitabFlag = false;
				this.TogglePettingTab( uitabFlag );
			}
			
			if( Utility.CreateEnableButton("open ui", 200, 300, 100, 25) )
			{
				//Utility.OverAllUIFlag = !Utility.OverAllUIFlag;
				uitabFlag = true;
				this.TogglePettingTab( uitabFlag );
			}
			//*/
			
			if ( ! Utility.OverAllUIFlag ) { return; }
			
#if DEBUG_ENERGY_TEST
			/*if( Utility.CreateEnableButton("[-] Bond", 100, 200, 100, 50) )
			{
				 AccountManager.Instance.BondMeter-=5.0f;
			}
			
			if( Utility.CreateEnableButton("[+] Bond", 100, 250, 100, 50) )
			{
				Vector3 pos      		  = m_petDragon.transform.position;
				pos.y                    += 0.7f;
				StatusMeterParticleManager.Instance.DisplayParticle( StatusMeterParticleManager.PARTICLETYPE.BOND, pos, 2,Debug.Break );
				AccountManager.Instance.BondMeter+=5.0f;
			}
			
			
			if( Utility.CreateEnableButton("[Set_Energy_0]", 100, 150, 100, 50) )
			{
				StatsManager.Instance.SetDragonData(StatsManager.KEY_ENERGY, 0.0f);
			}
			//*/
#endif
			
#if DEBUG_STATEMACHINE_VALUES
			// + LA 090413: Temporary debug
			/*
			if( Utility.CreateEnableButton("Dragon Salve", 100, 50, 100, 50) )
			{
				// pass salve id here
				this.OnCarePlayItem(ECarePlayItems.DragonSalve);
			}
			//*/// - LA
			
			/*
			if( Utility.CreateEnableButton("[Set_LowMood]", 100, 150, 100, 50) )
			{
				//Utility.bDebugActions = !Utility.bDebugActions;
			 	StatsManager.Instance.DragonMood = 0;
			}
			//*/
			
			//*
			if ( Utility.CreateEnableButton("Action", 100, 150, 100, 50) ) {
				Utility.bDebugActions = !Utility.bDebugActions;
			 	//StatsManager.Instance.DragonMood = 0;
			}
			//*/
			
			if ( Utility.CreateEnableButton("StateMachine", 100, 100, 100, 50) ) {
				Utility.bEnabledUI = !Utility.bEnabledUI;
			}
			
			//*
			if ( Utility.CreateEnableButton("Toggle Debug", 100, 50, 100, 50) ) {
				Utility.bIsDebugEnabled = !Utility.bIsDebugEnabled;
			}
			//*/
#endif
			
#if DEBUG_EVENTS
			if( Utility.CreateEnableButton("CameraPan", 100, Screen.height-150, m_initSize.x, m_initSize.y) )
			{
				m_dragonStateMachine.Reaction("Type_Cove_Sequence_Opening", "Event_Camera_Pan");
			}
			if( Utility.CreateEnableButton("PettingCue", 100, Screen.height-200, m_initSize.x, m_initSize.y) )
			{
				m_dragonStateMachine.Reaction("Type_Cove_Sequence_Opening", "Event_Play_More");
			}
			if( Utility.CreateEnableButton("PlayMore", 100, Screen.height-250, m_initSize.x, m_initSize.y) )
			{
				m_dragonStateMachine.Reaction("Type_Cove_Sequence_Opening", "Event_Petting_Cue");
			}
			if( Utility.CreateEnableButton("Roam", 100, Screen.height-300, m_initSize.x, m_initSize.y) )
			{
				m_dragonStateMachine.Reaction("Type_Roam", "Event_Roam");
			}
#endif
			
#if DEBUG_USAGE_TRACKING
			ListTracker[] lists = StatsManager.Instance.UsageTracker.GetListTracker;
			
			const int LIMIT_WIDTH 					= 300;
			const int LIMIT_HEIGHT 					= 600;
			const int LIMIT_BARHEIGHT				= 50;
			
			int i = 0;
			int totalLength = 0;
			
			GUI.BeginGroup(new Rect(0, Screen.height - LIMIT_HEIGHT, LIMIT_WIDTH, LIMIT_HEIGHT));
			
			EFreqTrackList listName = (EFreqTrackList)0; //to first item
			foreach(Dictionary<string, UsageFreqTracker> listTrack in lists){
				
				//Debug.Log("==== Trackers in \""+listName.ToString()+"\" list:\n");
				
				int ctr = 0;
				foreach(var item in listTrack){
					ctr++;
					
					string name = item.Key;
					UsageFreqTracker tracker = item.Value;
					string usageData = name+" "+tracker.NumConsecutiveUsage+" "+tracker.NumRepeatOveruse;
					
					if(GUI.Button(new Rect(0, i * LIMIT_BARHEIGHT - m_sliderPosition, LIMIT_WIDTH, LIMIT_BARHEIGHT), usageData))
					{
						Debug.Log("         "+ctr+".) "+name+": ["+
								"Rep("+ tracker.NumUsage            + ", "  +
									  + tracker.NumConsecutiveUsage + ", "  +
										tracker.NumRepeatOveruse + "), " +
							 "Forget("+ tracker.ForgetTime          + ", "  +
										tracker.CurForgetTime       + "), " +
							  "Usage("+ //tracker.GetUsageFactor()    + ", " +
									    tracker.GetUsageTestValue() + "), " +
						  "UsageFreq("+ tracker.BaseFrequency       + ", "  +
										tracker.GetUsageFrequency() + ") "  +
						       "Time("+ tracker.BaseTime            + ", "  +
							            tracker.TotalTime           + ") "  +
							"]\n");
					}
					
					i++;
					totalLength = LIMIT_BARHEIGHT + (i * LIMIT_BARHEIGHT);
				}
				
				listName++;
			}
			
			GUI.EndGroup();
			m_sliderPosition = GUI.VerticalSlider(new Rect(LIMIT_WIDTH, Screen.height - LIMIT_HEIGHT, 200, LIMIT_HEIGHT), m_sliderPosition, 0.0f, totalLength - (LIMIT_HEIGHT + LIMIT_BARHEIGHT));
#endif
		}
	}
}
