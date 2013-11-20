// +KJ:06282013 Debug Utilities
//#define DEBUG_SPECIFIC_TRIGGER
//#define DEBUG_OFF_IDLE_ON_MIDGROUND
#define DEBUG_ACTION_SEQUENCE
//#define DEBUG_CUEABLES
//#define DEBUG_EVENTS
//#define DEBUG_DRAGON_CALL_ACTI0N
#define DEBUG_TIMERS

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using DragonCatcher.Common;
using DragonCatcher.AnimationTool;
using DragonCatcher.CarePlay;
using DragonCatcher.CarePlay.Petting;
using DragonCatcher.CarePlay.TouchPlay;
using DragonCatcher.CarePlay.UsageFrequency;

namespace DragonCatcher.CarePlay.TouchPlay
{
#if DEBUG_ACTION_SEQUENCE
		//CCC: Start: Editor mode code
		enum ETestActionOverride {
			FALSE,
			TRUE,
			CONTINUE,
		}
#endif
	
	public class Dragon : MonoBehaviour 
	{
		//---CONSTANTS
		private const int DEFAULT_REQ_PRIO = 0;
		private const float DEFAULT_REQ_CHANCE = 1.0f;
		private const string REQ_STRKEY_PRIO = "Prio";
		private const string REQ_STRKEY_CHANCE = "Chance";
		private const string REQ_STRKEY_QUEUEABLE = "Queueable";
		
		public enum EUsageQueryKey {
			CUR_ITEM,
			CUR_ACTION,
			ITEM_USAGE,
			ACT_USAGE
		};
		
		private const string REQ_STRKEY_CUR_ITEM = "CurItem";
		private const string REQ_STRKEY_CUR_ACTION = "CurAction";
		private const string REQ_STRKEY_ITEM_USAGE = "ItemUsage";
		private const string REQ_STRKEY_ACT_USAGE = "ActUsage";	
		
		public static Dictionary<string, EUsageQueryKey> usageQueryKey_to_enum = new Dictionary<string, EUsageQueryKey>{
				{ REQ_STRKEY_CUR_ITEM,   EUsageQueryKey.CUR_ITEM   },
				{ REQ_STRKEY_CUR_ACTION, EUsageQueryKey.CUR_ACTION },
				{ REQ_STRKEY_ITEM_USAGE, EUsageQueryKey.ITEM_USAGE },
				{ REQ_STRKEY_ACT_USAGE,  EUsageQueryKey.ACT_USAGE  },
		};
		
		/** Properties ******************************************/
		private PettingMain m_delegate;
		private AnimatorStateInfo stateInfo;
		private bool m_isInteractible;
		private float m_curRubLength;
		[SerializeField] 
		private List<List<string>> m_events;
		[SerializeField] 
		private List<List<string>> m_gestures;
		// +KJ:08122013 
		/// NOTE: This must be adjusted.. this could cause problems that is hard to debug. 
		/// TODO: Adjust this on the Event Tables
		private GestureObject m_triggeredGesture;
		
		// +KJ:08122013 
		/// <summary>
		/// Flags to determine dragon reacts on triggered gesture
		/// </summary>
		/// <returns>
		/// bool
		/// </returns>
		public bool bSatisfiedGesture;
		
		// timebased
		private S6Scheduler.ScheduleController m_eventTimer;
		private S6Scheduler.ScheduleController m_timeOnCove;
		private S6Scheduler.ScheduleController m_tutorialTimer;
		private S6Scheduler.ScheduleController m_statusUpdater;
		[SerializeField] 
		private List<List<string>> m_timedEvents;
		
		private float m_touchMomentTimer = 0.0f; 
		public bool m_bIsToothlessWaiting = false;
		
		// Sprint 9 Demo Scheduler
		private int m_sprint9DemoCounter = 1;
		private S6Scheduler.ScheduleController m_sprint9Demo;
		private S6Scheduler.ScheduleController m_idleTimer;
		
		// gesture particles
		private GameObject m_rubParticle;
		private ParticleSystem m_tapParticle;
		private ParticleSystem m_doubleTapParticle;
		private GameObject m_soapParticle;
		private RubMeter m_rubMeter;
		private ParticleSystem m_holdParticle;
		public Vector2 m_endPoint;
		public Vector2 m_startPoint;
		
		//STATICS
		public static ParticleSystem m_successParticle;
		public static GameObject m_successBurst;
		public static GameObject m_feedbackCamera;
		
		private GameObject m_head			= null;
		private GameObject m_tongue			= null;
		private Material m_dirtyMaterial	= null;
		private GameObject m_headJoint		= null;
		private GameObject m_mouthJoint		= null;
		
		private bool m_bwillDoubleTap 		= false;
		private bool m_bwillRefuse	  		= false;
		public TouchHitTester m_hitCollider	= null;
			
		private bool debugVar_isAniSeq = false;  //xxx: This variable is for debugging only.
		
		private GameObject m_lastTouchOBject = null;
		
		private bool m_bEventIsCueable 	= false;
		private List<string> m_cuedActions;
		
#if DEBUG_ACTION_SEQUENCE
		public string m_debugReaction = string.Empty;
		public int m_debugActionSequenceIndex = -1;
		public string m_debugOverrideActionSequence = string.Empty;
#endif
		
		//---Usage Tracking member variables
		//   -Note: These holds the keyword to access usage tracking data.
		private string m_testCurItemUsed = null;   //name of the current item presented
		private string m_testCurActionUsed = null; //name of the current action
		
		#region Rubbing Direction
		private Vector2 m_rubbingDirection = new Vector2(0, 0);
		private Vector2 m_rubbingTargetDirection = new Vector2(0, 0);
		private Queue<Vector2> m_rubbingObjectIndex = new Queue<Vector2>();
		private List<GameObject> m_rubbingColliders	= new List<GameObject>();
		
		private float m_rubDirectionX = 0.0f;
		private float m_rubTargetDirectionX = 0.0f;
		#endregion
		
		#region Timer Vars
		int m_batch2Counter;
		List<string> m_batch2Pattern;
		int m_roamBgCounter;
		float m_lowMeterLimit = 60.0f;
		#endregion
		
		/** Initialization **********************************/
		public void Awake ()
		{
			this.StopAllCoroutines();
			
			m_events 						= new List<List<string>>();
			m_timedEvents					= new List<List<string>>();
			m_gestures						= new List<List<string>>();
			m_cuedActions					= new List<string>();
			
			GameObject rubParticle		 	= GameObject.Instantiate((GameObject)Resources.Load("Prefabs/Petting/Particles/RubParticles", typeof(GameObject))) as GameObject;
			GameObject tapParticle		 	= GameObject.Instantiate((GameObject)Resources.Load("Prefabs/Petting/Particles/TapParticle", typeof(GameObject))) as GameObject;
			GameObject touchParticle	 	= GameObject.Instantiate((GameObject)Resources.Load("Prefabs/Petting/TouchTrail", typeof(GameObject))) as GameObject;
			GameObject holdParticle		 	= GameObject.Instantiate((GameObject)Resources.Load("Prefabs/Petting/Particles/HoldParticle", typeof(GameObject))) as GameObject;
			GameObject rubMeter			 	= GameObject.Instantiate((GameObject)Resources.Load("Prefabs/Petting/RubMeterParticle", typeof(GameObject))) as GameObject; // + LA 080213
			GameObject doubleTapParticle 	= GameObject.Instantiate((GameObject)Resources.Load("Prefabs/Petting/Particles/DoubleTap", typeof(GameObject))) as GameObject;
			GameObject soapParticle			= GameObject.Instantiate((GameObject)Resources.Load("Prefabs/Petting/Particles/SoapParticles", typeof(GameObject))) as GameObject;
		    m_successBurst					= GameObject.Instantiate((GameObject)Resources.Load("Prefabs/Petting/Burst", typeof(GameObject))) as GameObject;
			
			m_headJoint						= GameObject.Find( "joint_Head" );
			m_head 							= GameObject.Find( "Head" );
			m_tongue						= GameObject.Find( "Tongue" );
			m_dirtyMaterial 				= Resources.Load( "Materials/toothless_head_color_dirty" ) as Material;
			
			m_mouthJoint 					= Instantiate( Resources.Load( "Prefabs/Petting/MouthJoint" ) ) as GameObject;
			m_mouthJoint.name 				= "MouthJoint";
			m_mouthJoint.transform.parent 	= m_headJoint.transform;
			
			m_mouthJoint.transform.localPosition = new Vector3( 0.0f, -0.23f, 0.42f );
			m_mouthJoint.transform.localRotation = Quaternion.Euler(new Vector3( 13.0f, 0.0f, 0.0f ));
			m_mouthJoint.transform.localScale = new Vector3( 0.4f, 0.4f, 0.4f );
			
			DentalParticles.Instance.Initialize( 10 );
			
			this.SetMouthCollidersActive( false );

			m_feedbackCamera			 	= GameObject.Find("TouchFeedbackCamera");
			
			rubParticle.AddComponent<FollowComponent>();
 			tapParticle.AddComponent<FollowComponent>();
			holdParticle.AddComponent<FollowComponent>();
			rubParticle.transform.localScale = new Vector3( 0.25f, 0.25f, 0.25f );
			tapParticle.transform.localScale = new Vector3( 0.25f, 0.25f, 0.25f );
			m_rubParticle 					= rubParticle;
			m_tapParticle 					= tapParticle.GetComponent<ParticleSystem>();
			m_holdParticle					= holdParticle.GetComponent<ParticleSystem>();
			m_doubleTapParticle				= doubleTapParticle.GetComponent<ParticleSystem>();
			m_soapParticle					= soapParticle;
			m_rubMeter						= rubMeter.GetComponent<RubMeter>();
			m_successParticle				= m_successBurst.GetComponent<ParticleSystem>();
			m_rubMeter.Disable();
			m_holdParticle.Stop();
			m_rubParticle.SetActive(false);
			m_soapParticle.SetActive(false);
			
			m_hitCollider 	= GameObject.FindObjectOfType( typeof( TouchHitTester ) ) as TouchHitTester;
			
			this.FindColliders();
			
			m_rubbingColliders.Add(GameObject.Find("RubTopCollider").gameObject);
			m_rubbingColliders.Add(GameObject.Find("RubBottomCollider").gameObject);
			m_rubbingColliders.Add(GameObject.Find("RubLeftCollider").gameObject);
			m_rubbingColliders.Add(GameObject.Find("RubRightCollider").gameObject);
			for(int i = 0; i<m_rubbingColliders.Count; i++)	m_rubbingColliders[i].SetActive(false);
		}
		
		public void Initialize ()
		{
			this.SetIsInteractible(true);
			
#if ( DEBUG_OFF_IDLE_ON_MIDGROUND )
			S6Scheduler.ScheduleAction(
			this,
			() =>
			{
				//this.Reaction("Type_Events", "Event_Idle_Midground");
				//this.Reaction("Type_Cove_Sequence_Opening", "Event_Camera_Pan");
				//this.Reaction("Type_Cove_Sequence_Opening", "Event_Petting_Cue");
				//this.Reaction("Type_Cove_Sequence_Opening", "Event_Play_More");
				//this.Reaction("Type_Cove_Sequence_Opening", "Event":"Event_Petting_Cue");
				//this.Reaction("Type_Roam", "Event_Roam");
			},
			1.0f,
			1,
			false);
#endif
			
			// Game Timer
			m_timeOnCove = S6Scheduler.ScheduleAction(this,
			() => {
				
				// sanity check.. dont resume if on tutorial mode
				if( TutorialController.IsInTutorialMode ) { return; }
				
				// Temporary commented for Sprint 9 Demove
				StatsManager.Instance.coveTime += 1.0f;
				
				//this.TimedReaction("Type_Anim_Cue_Events", "Event_Animation_Cues");
				this.TimedReaction( "Type_CoveTime_Based", "Event_Idle_Batch3_To_5_Distant" );
				this.TimedReaction( "Type_CoveTime_Based", "Event_Idle_Batch3_To_5_Friendly" );
				this.TimedReaction( "Type_CoveTime_Based", "Event_Idle_Batch3_To_5_Close" );
				this.TimedReaction( "Type_CoveTime_Based", "Event_Idle_Batch3_To_5_Deep" );
			},
			1.0f,
			S6Scheduler.INFINITE,
			false);
			
			// Tutorial Timer
			m_tutorialTimer = S6Scheduler.ScheduleAction(this, 
			() => {
				
				if ( m_tutorialTimer != null )
				{
					if ( TutorialController.GTutState != TutorialController.ETutorialState.PostPetting ) { return; }

					if ( CarePlayNFController.GetPetAnimController().IsCurAnimStateOnSub(ERigType.Body, ECP_SubState.TouchMoment, "TouchMomentWaitingIdle" ) ) {
						StatsManager.Instance.WaitingTime += 1.0f;
						StatsManager.Instance.TouchMomentTime += 1.0f;
						
						this.TimedReaction( "Type_Time_Based", "Event_Ignoring_Toothless" );
						
						if( StatsManager.Instance.TouchMomentTime == 15.0f ) {
							PettingMain.Instance.ShowTapFingerGuide(3);
						}
					}
				}
			},
			1.0f,
			S6Scheduler.INFINITE,
			false);
			
			// Event Timer
			m_eventTimer = S6Scheduler.ScheduleAction(this,
			() => {
				
				// sanity check.. dont resume if on tutorial mode
				if ( TutorialController.IsInTutorialMode ) { return; }
				
				// Temporary commented for Sprint 9 Demove
				StatsManager.Instance.eventTime += 1.0f;
				StatsManager.Instance.bucketTime += 1.0f;
				
				
				this.TimedReaction("Type_Time_Based", "Event_No_User_Interaction");
				this.TimedReaction("Type_Time_Based", "Event_Back_To_Stand");
				/*
				//this.TimedReaction("Type_Time_Based", "Event_Idle_Stand");
				//this.TimedReaction("Type_Time_Based", "Event_Idle_Sit");
				//*/
				
				/** Scott Demo Mode
				this.TimedReaction("Type_Sprint_8_Demo", "Event_Idle_Sit_1");
				this.TimedReaction("Type_Sprint_8_Demo", "Event_Idle_Look_Around");
				this.TimedReaction("Type_Sprint_8_Demo", "Event_Idle_Sit_2");
				this.TimedReaction("Type_Sprint_8_Demo", "Event_Sratch");
				this.TimedReaction("Type_Sprint_8_Demo", "Event_Idle_Sit_3");
				this.TimedReaction("Type_Sprint_8_Demo", "Event_Chase_tail");
				this.TimedReaction("Type_Sprint_8_Demo", "Event_Idle_Sit_4");
				this.TimedReaction("Type_Sprint_8_Demo", "Event_Walk_Batch_3");
				//*/
			},
			1.0f,
			S6Scheduler.INFINITE,
			false);
			
			// Meter Update every 2 sec
			m_statusUpdater = S6Scheduler.ScheduleAction(this,
			() => {
				
				// sanity check.. dont resume if on tutorial mode
				if ( TutorialController.IsInTutorialMode ) { return; }
				
				/*
				StatsManager.Instance.CheckAffectedMetersByMeter("Thirsty");
				StatsManager.Instance.CheckAffectedMetersByMeter("Sick");
				StatsManager.Instance.CheckAffectedMetersByMeter("Dental");
				StatsManager.Instance.CheckAffectedMetersByMeter("Dirty");
				StatsManager.Instance.CheckAffectedMetersByMeter("Energy");
				//*/
			},
			2.0f,
			S6Scheduler.INFINITE,
			false);
			
#if DEBUG_DRAGON_CALL_ACTI0N
			m_sprint9DemoCounter = 3;
			m_roamBgCounter = 10;
#endif

			m_idleTimer = S6Scheduler.ScheduleAction( this,
			() => {
				// if not in tutorial state
				if( TutorialController.GTutState == TutorialController.ETutorialState.Invalid
					|| TutorialController.GTutState == TutorialController.ETutorialState.Done 
					|| TutorialController.GTutState == TutorialController.ETutorialState.Cinematic
					|| TutorialController.GTutState == TutorialController.ETutorialState.TutorialCPart2
					&&  !( AccountManager.Instance.EnergyMeter 	<= m_lowMeterLimit
						|| AccountManager.Instance.ThirstMeter 	<= m_lowMeterLimit
						|| AccountManager.Instance.DirtyMeter 	<= m_lowMeterLimit
						|| AccountManager.Instance.BoredMeter	<= m_lowMeterLimit
						|| AccountManager.Instance.SickMeter 	<= m_lowMeterLimit
						|| AccountManager.Instance.DentalMeter 	<= m_lowMeterLimit )
					&& StatsManager.Instance.DragonPlace == "Middleground" 
				) {
					//this.TimedReaction("Type_Sprint_9_Demo", "Event_Custom_Batch_3");
				}
			},
			30.0f,
			S6Scheduler.INFINITE,
			false);
			
			// +KJ:09192013 Temp. off the update of meters by time.
			//m_statusUpdater.PauseScheduler();
			m_batch2Counter = 0;
			m_batch2Pattern = new List<string>();
			m_batch2Pattern.Add("Event_Custom_Batch_2_LookAround");
			m_batch2Pattern.Add("Event_Custom_Batch_2_Scratching");
			m_batch2Pattern.Add("Event_Custom_Batch_2_ChaseTail");
			m_roamBgCounter = 0;
			
			m_sprint9Demo = S6Scheduler.ScheduleAction(this,
			() => {
				
				// sanity check.. dont resume if on tutorial mode
				if ( TutorialController.IsInTutorialMode ) { return; }
				
				//Debug.Log("Timer:"+m_sprint9DemoCounter);
				
				//if( !m_isInteractible )
				//	return;
				
				m_roamBgCounter++;
				
				if ( m_sprint9DemoCounter <= 3 ) {
					this.TimedReaction("Type_Sprint_9_Demo", m_batch2Pattern[m_batch2Counter]);
					m_batch2Counter++;
					m_sprint9DemoCounter++;
					if ( m_batch2Counter > 2 ) {
						m_batch2Counter = 0;
						//Utility.ShuffleList<string>(batch2Pattern); // + LA
					}
				} else if ( m_sprint9DemoCounter > 3 ) {
					// Energy, Thirsty, Dirty, Bored, Sick, Dental
					if( AccountManager.Instance.EnergyMeter 	<= m_lowMeterLimit
						|| AccountManager.Instance.ThirstMeter 	<= m_lowMeterLimit
						|| AccountManager.Instance.DirtyMeter 	<= m_lowMeterLimit
						|| AccountManager.Instance.BoredMeter	<= m_lowMeterLimit
						|| AccountManager.Instance.SickMeter 	<= m_lowMeterLimit
						|| AccountManager.Instance.DentalMeter 	<= m_lowMeterLimit
					) {
						
						Debug.Log("~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~ \n");
						Debug.Log("- PLAYING ANIMATION CUE - Type_Sprint_9_Demo::Event_Custom_Cues \n");
						Debug.Log("--------------------------------------------- \n");
						//Debug.Break();
						
						this.TimedReaction("Type_Sprint_9_Demo", "Event_Custom_Cues");
						m_batch2Counter = 0;
						m_sprint9DemoCounter = 0;
					} else {
						// New Integration of Idle Animations
						this.TimedReaction("Type_Sprint_9_Demo", "Event_Custom_Idle");
						m_sprint9DemoCounter = 0;
						m_roamBgCounter = 0;
					}
					
					m_sprint9DemoCounter = 0;
					m_batch2Counter = 0;
				}
			},
			2.5f,
			S6Scheduler.INFINITE,
			false);
		}
		
		public void FindColliders ()
		{
			GameObject rubTop	 				 = Instantiate(Resources.Load("Prefabs/Petting/DragonColliders/RubTopCollider"), transform.position, Quaternion.identity) as GameObject;
			rubTop.name							 = "RubTopCollider";
			rubTop.transform.parent 	       	 = m_headJoint.transform;
			rubTop.transform.localPosition 		 = new Vector3(0.006363988f, 0.07254031f,0.3909606f);
			rubTop.transform.localEulerAngles 	 = new Vector3(321.2937f, 173.6868f, 1.108124f);
			
		    GameObject rubRight					 = Instantiate(Resources.Load("Prefabs/Petting/DragonColliders/RubRightCollider"), transform.position, Quaternion.identity) as GameObject;
			rubRight.name						 = "RubRightCollider";
			rubRight.transform.parent			 = m_headJoint.transform;
			rubRight.transform.localPosition	 = new Vector3(-0.2537453f, -0.2903501f,0.280512f);
			rubRight.transform.localEulerAngles	 = new Vector3(323.8072f,113.1607f,56.11443f);
			
		
			GameObject rubLeft					 = Instantiate(Resources.Load("Prefabs/Petting/DragonColliders/RubLeftCollider"), transform.position, Quaternion.identity) as GameObject;
			rubLeft.name						 = "RubLeftCollider";
			rubLeft.transform.parent			 = m_headJoint.transform;
			rubLeft.transform.localPosition		 = new Vector3(0.3512354f, -0.2992908f,0.1996863f);
			rubLeft.transform.localEulerAngles	 = new Vector3(323.8071f,113.1606f,46.68074f);
			
			GameObject rubBot					 = Instantiate(Resources.Load("Prefabs/Petting/DragonColliders/RubBottomCollider"), transform.position, Quaternion.identity) as GameObject;
			rubBot.name						 	 = "RubBottomCollider";
			rubBot.transform.parent			 	 = m_headJoint.transform;
			rubBot.transform.localPosition		 = new Vector3(0.04385789f, -0.5225507f,-0.121626f);
			rubBot.transform.localEulerAngles	 = new Vector3(324.0195f,181.3491f,350.326f);
		}
		
		public void SetMouthCollidersActive( bool p_bIsOn )
		{
			foreach( Transform child in m_mouthJoint.transform )
			{
				child.gameObject.SetActive( p_bIsOn );
			}
		}
		
		public void AddDirtyMaterial()
		{
			Material[] headMaterials 	= m_head.renderer.materials;
			Material[] tongueMaterials	= m_tongue.renderer.materials;
			
			if( m_dirtyMaterial != null )
			{
				headMaterials[1] 	= m_dirtyMaterial;
				tongueMaterials[1] 	= m_dirtyMaterial;
				
				m_head.renderer.materials 	= headMaterials;
				m_tongue.renderer.materials	= tongueMaterials;
			}
		}
		 
		public void Log ( System.Object p_log )
		{
			D.Log(""+p_log+"\n");
		}
		
		// + LA 101013: Property for timers
		public S6Scheduler.ScheduleController EventTimer
		{
			get { return m_eventTimer; }
			set { m_eventTimer = value; }
		}
		
		public S6Scheduler.ScheduleController CoveTimer
		{
			get { return m_timeOnCove; }
			set { m_timeOnCove = value; }
		}
		
		public S6Scheduler.ScheduleController TutorialTimer
		{
			get { return m_tutorialTimer; }
			set { m_tutorialTimer = value; }
		}
		
		public S6Scheduler.ScheduleController StatusUpdater
		{
			get { return m_statusUpdater; }
			set { m_statusUpdater = value; }
		}
		
		public S6Scheduler.ScheduleController Sprint9DemoTimer
		{
			get { return m_sprint9Demo; }
		}
		
		public S6Scheduler.ScheduleController IdleTimer
		{
			get { return m_idleTimer; }
		}
		
		// +KJ:10092013 Pause Timers
		public void PauseGame ( bool p_bIsPaused )
		{
			if( p_bIsPaused )
			{
				m_timeOnCove.PauseScheduler();
				m_statusUpdater.PauseScheduler();
				m_eventTimer.PauseScheduler();
				m_sprint9Demo.PauseScheduler();
			}
			else
			{
				// sanity check.. dont resume if on tutorial mode
				if( TutorialController.IsInTutorialMode )
					return;
				
				m_timeOnCove.ResumeScheduler();
				m_statusUpdater.ResumeScheduler();
				m_eventTimer.ResumeScheduler();
				m_sprint9Demo.ResumeScheduler();
			}
		}
		
		public void RestartEventTimer ()
		{
			StatsManager.Instance.eventTime = 0.0f;
			m_eventTimer.SchedulerRestarted();
		}
		
		public void PauseEventTimer ()
		{
			m_eventTimer.PauseScheduler();
			m_sprint9Demo.PauseScheduler();
		}
		
		public void ResumeEventTimer ()
		{
			if( TutorialDialogueController.Instance.IsActive ) return;
			if( PettingMain.Instance.bIsUIPresent ) return;
			m_eventTimer.ResumeScheduler();
			m_sprint9Demo.ResumeScheduler();
		}
		
		public void StartIdleTimer ()
		{
			m_idleTimer.RestartScheduler();
			m_idleTimer.ResumeScheduler();
			
			StatsManager.Instance.coveTime = 0;
			m_timeOnCove.RestartScheduler();
			m_timeOnCove.ResumeScheduler();
		}
		
		public void StopIdleTimer ()
		{
			m_timeOnCove.StopScheduler();
			m_idleTimer.StopScheduler();
		}
		
		public void ResumeIdleTimer ()
		{
			m_timeOnCove.ResumeScheduler();
			m_idleTimer.ResumeScheduler();
		}
		
		public void PauseIdleTimer ()
		{
			m_timeOnCove.PauseScheduler();
			m_idleTimer.PauseScheduler();
		}
		
		public void ActionOnComplete ()
		{ 
#if DEBUG_CUEABLES
			Debug.Log("Dragon::ActionOnComplete");
#else
			Log("Dragon::ActionOnComplete");
#endif
			
			// Start Idle Timer after every actions except for
			if( StatsManager.Instance.currentReaction != "Batch_3_To_5" )
			{
				//this.StartIdleTimer();
				this.ResumeIdleTimer();
			}
			
			// Hardcoded Checking if in JawCruncher State
			if( StatsManager.Instance.currentReaction == "Fake_Throw_Jaw_Cruncher_Short" 
				&& !JawCruncher.HasJC()
				&& StatsManager.Instance.DragonMode == StatsManager.MODE_IUC 
			){ 
				//StatsManager.Instance.DragonMode = StatsManager.MODE_NORMAL;
				//Debug.Break();
			}
			
			if( m_cuedActions.Count > 0 )
			{
				string cuedAction = m_cuedActions[0];
				m_cuedActions.Remove(cuedAction);
				ArrayList playCuedAction = new ArrayList(1);
						  playCuedAction.Add(cuedAction);
				this.PlayActionSequence(playCuedAction);

#if DEBUG_CUEABLES
				Debug.Log("Dragon::ActionOnComplete Playing Cued Action:"+cuedAction);
#else
				Log("Dragon::ActionOnComplete Playing Cued Action:"+cuedAction);
#endif
				
			  
				return;
			}
		
			if( StatsManager.Instance.currentReaction != "Batch_3_To_5" )
			{
				this.ResumeEventTimer();
			}
			
			// Check OnComplete Actions for Tutorial
			TutorialController.Instance.ActionOnComplete();
		}
		
		void OnGUI()
		{
			if ( ! Utility.OverAllUIFlag ) { return; }
			
			//GUI.Label( new Rect(0, 150, 700, 50), "IdleCounter: " + m_sprint9DemoCounter + " RoamCTR: " + m_roamBgCounter );
			//GUI.Label(new Rect(0, 50, 100, 50), "BucketTime: "  + StatsManager.Instance.bucketTime);
#if DEBUG_TIMERS
			//GUI.Label(new Rect(0, 200,  700, 50), "EventTimer: " 		+ m_eventTimer.IsPaused());
			//GUI.Label(new Rect(0, 25, 500, 50), "CoveTimer: " 		+ m_timeOnCove.IsPaused());
			//GUI.Label(new Rect(0, 50, 500, 50), "StatusTimer: " 	+ m_statusUpdater.IsPaused());
			//GUI.Label(new Rect(0, 75, 500, 50), "Sprint9Timer: " 	+ m_sprint9Demo.IsPaused());
#endif
			//if( m_idleTimer != null )
			//{
			//	Utility.CreateEnableButton(" Idle " + m_idleTimer.timer, 100, 325, 100, 25);
			//}
		}
		
		private void GestureEnablingMethod ( GestureObject p_gesture, List<List<string>> p_events )
		{
			if( p_gesture == null )
				return;
			
			if( p_events != m_gestures )
				return;
			
			// Sanity checking if the state is not interactible
			if( !this.IsInteractible() )
				return;
			
			// Sanity Checking for Water Splash Animations to avoid spam actions on Water Bucket
			if( StatsManager.Instance.currentReaction == "Dragon_Splash_Upward"
				|| StatsManager.Instance.currentReaction == "Dragon_Splash_Front"
				|| StatsManager.Instance.currentReaction == "Dragon_Splash_Front_To_Fetching"
				|| StatsManager.Instance.currentReaction == "Dragon_Splash_Front_To_Feeding"
				|| StatsManager.Instance.currentReaction == "Dragon_Splash_To_Dragon_Salve"
			){
				return;
			}
			
			if( ( p_gesture.type == GestureManager.GestureType.Swipe || ( p_gesture.type == GestureManager.GestureType.Tap && p_gesture.tapCount == 2 ) )
			){
				//PettingMain.Instance.AnimateSuccessAnimation();
				Vector3 pos = m_feedbackCamera.camera.ScreenToWorldPoint( new Vector3(m_endPoint.x,m_endPoint.y,2));
				m_successBurst.transform.position=pos;
				m_successParticle.Play();
				
				m_doubleTapParticle.GetComponent<FollowComponent>().Point(m_endPoint);
				m_doubleTapParticle.Play();
			}
		}
		
		/** Schedulers ******************************************/
		public void DragonMoodTicker ()
		{
			StatsManager.Instance.CheckMoodComputations("Bored");
		}
			
		/** Setters *********************************************/
		public void SetPettingDelegate (PettingMain p_Delegate)
		{
			m_delegate = p_Delegate;
			StatsManager.Instance.PettingDelegate(p_Delegate);
		}
		
		public void SetIsInteractible ( bool p_isInteractible )
		{
			m_isInteractible = p_isInteractible;
			StatsManager.Instance.isInteractible = ( p_isInteractible == true ? 1 : 0 );
			
			if( !p_isInteractible )
			{
				m_rubParticle.SetActive(false);
				m_tapParticle.Stop();
				m_doubleTapParticle.Stop();
			}
		}
		
		public void DisableParticles ()
		{
			if ( m_tapParticle 	     != null ) m_tapParticle.Stop();
			if ( m_doubleTapParticle != null ) m_doubleTapParticle.Stop();
			if ( m_holdParticle		 != null ) m_holdParticle.Stop();
			if ( m_rubParticle 	     != null ) m_rubParticle.SetActive(false);
		}
		
		public void AnimateTap(Vector2 p_point)
		{
			m_tapParticle.GetComponent<FollowComponent>().Point(p_point);
			m_tapParticle.Play();		
		}
		
		/** Getters *********************************************/
		public bool IsInteractible ()
		{
			return m_isInteractible;
		}
		
		public bool IsDragonInteractable ()
		{
			stateInfo = CarePlayNFController.GetPetAnimController().BodyAnimator.GetCurrentAnimatorStateInfo(0);	
			
			if ( stateInfo.IsName("Base Layer.IdleLookAround")
			||	 stateInfo.IsName("Base Layer.UprightSitLoop")
			||	 stateInfo.IsName("Base Layer.IdleSitLoop")
			){
				return true;	
			}
			
			return false;
		}
		
		// TODO: Apply this as requirement parameters of event for IUC
		private float m_fastSwipeCounter 		= 0.0f;
		private const float FAST_SWIPE_CAP		= 10.0f;
		
		// + LA 092713: Refactored Trigger method
		public void Trigger ( GestureObject p_gesture )
		{
			bool _isHitinInteractiveArea			= m_hitCollider.GetIsHitAt(p_gesture.position,true);
			bool _isSwipeValid						= m_hitCollider.GetIsHitAt(m_startPoint,true);
			
			GameObject TouchObject 				  	= null;
			StatsManager.Instance.DragonTouchPart 	= "";
			
			if( p_gesture.hit.collider != null )
			{
				StatsManager.Instance.DragonTouchPart = p_gesture.hit.collider.gameObject.tag;
				TouchObject 					      = p_gesture.hit.collider.gameObject;
			}
			
			// Event Params
			string strEvent = "";
			string strEventType = "";
			
			# region Gesture Up
			if( p_gesture.type == GestureManager.GestureType.Up )
			{
				strEvent = "Event_Up";
				strEventType = "Type_Touch";
				
				m_rubMeter.Disable();
				//m_soapParticle.SetActive( false );
				
				#region Rubbing temp fix on Touch Up 
				if(StatsManager.Instance.DragonState == "Rubbing" && this.IsInteractible())
				{	
					m_rubbingTargetDirection = Vector2.zero;
					m_rubbingObjectIndex.Clear();
					UpdateRubbingPosition(0.8f);
				}
				#endregion
				
				
				
				bSatisfiedGesture = false;
			}
			#endregion
			#region Gesture Down
			else if( p_gesture.type == GestureManager.GestureType.Down )
			{
//				strEvent = "Event_Down";
//				strEventType = "Type_Touch";
				
				switch( StatsManager.Instance.DragonMode )
				{
				case StatsManager.MODE_NORMAL:
					
					strEvent = "Event_Down";
					strEventType = "Type_Touch";
					
					if( StatsManager.Instance.DragonState == "Rubbing" && this.IsInteractible() )
					{
						if(m_rubbingTargetDirection == m_rubbingDirection &&  TouchObject != null)
						{
							m_rubbingTargetDirection = Vector2.zero;
							m_rubbingDirection = Vector2.zero;
							switch(p_gesture.hit.collider.gameObject.name)
							{
							case "RubTopCollider":
								m_rubbingTargetDirection = new Vector2(0,1);
								break;
							case "RubBottomCollider":
								m_rubbingTargetDirection = new Vector2(0,-1);
								break;
							case "RubLeftCollider":
								m_rubbingTargetDirection = new Vector2(-1,0);
								break;
							case "RubRightCollider":
								m_rubbingTargetDirection = new Vector2(1,0);
								break;
							}
							UpdateRubbingPosition(0.5f);
						}
					}
					
					break;
				case StatsManager.MODE_IUC:
					
					strEvent = "Event_Down";
					strEventType = "Type_Touch";
					
					if( !JawCruncher.IsThrownJC() )
					{
						PettingMain.Instance.GestureManager.EnableHold();
						StatsManager.Instance.touchPosition = p_gesture.position;
						m_fastSwipeCounter = 0.0f;
					}
					
					break;
					
				case StatsManager.MODE_ACT1:
					strEvent = "Event_Down";
					strEventType = "Type_Touch";
					
					break;
					
				case StatsManager.MODE_DRAGONSALVE:
					strEvent = "Event_Down";
					strEventType = "Type_Touch";
					
					break;
				}
			}
			#endregion
			#region Gesture Tap / Double Tap
			else if( p_gesture.type == GestureManager.GestureType.Tap )
			{
				if( p_gesture.tapCount == 2 )
				{
					switch( StatsManager.Instance.DragonMode )
					{
						case StatsManager.MODE_NORMAL:
							if( _isHitinInteractiveArea )
							{
								strEventType = "Type_Touch";
								strEvent = "Event_DoubleTap";
								StatsManager.Instance.ResetRubCount();
								// + ET  091913 TEMP ON/OFF OF RUBBING COLLIDERS
								StopCoroutine("SetColliderVisible");
								StartCoroutine(SetColliderVisible(0.5f));
								// - ET 
							}
						break;
						
						case StatsManager.MODE_IUC:
						break;
						
						case StatsManager.MODE_WATERBUCKET:
							/*
							if( TouchObject.transform.parent.name.Contains("WaterBucket") )
							{
								strEventType = "Type_From_Inventory";
								strEvent = "Event_Water_Bucket";	
							}
							// + LA 092713: This is to not conflict with the other double tap event
							else
							{
								strEventType = "Type_Touch";
								strEvent = "Event_DoubleTap";
							}
							//*/
						break;
						
						case StatsManager.MODE_TOUCHMOMENT:
						break;
						
						case StatsManager.MODE_ACT1:
						
						
						if( TutorialController.GTutState == TutorialController.ETutorialState.PostPetting )
						{
							if( _isHitinInteractiveArea )
							{
								strEventType = "Type_Touch";
								strEvent = "Event_DoubleTap";
							}
						}
							
						if( TutorialController.GTutState == TutorialController.ETutorialState.Petting )
						{
							if( _isHitinInteractiveArea )
							{
								strEventType = "Type_Touch";
								strEvent = "Event_DoubleTap";
								
								// + ET 110513 SUCCESS BURST
								Vector3 pos = m_feedbackCamera.camera.ScreenToWorldPoint( new Vector3( p_gesture.position.x, p_gesture.position.y, 2 ));
								m_successBurst.transform.position=pos;
								m_successParticle.Play();
								// - ET
								
								StatsManager.Instance.ResetRubCount();
								// + ET  091913 TEMP ON/OFF OF RUBBING COLLIDERS
								StopCoroutine("SetColliderVisible");
								StartCoroutine(SetColliderVisible(0.5f));
								// - ET 
							}
						}
						
						if( StatsManager.Instance.DragonState == "Analyzing_Near" )
						{
							if( TouchObject != null && TouchObject.transform.name.Contains("Floor") )
							{
								strEventType = "Type_Touch";
								strEvent = "Event_DoubleTap";
							}
						}
						
						break;
					}
					
					m_bwillDoubleTap 	= true;
				}
				else if( p_gesture.tapCount == 1 )
				{
//					strEventType = "Type_Touch";
//					strEvent = "Event_Tap";
					switch( StatsManager.Instance.DragonMode ) 
					{
						case StatsManager.MODE_IUC:
							
							strEventType = "Type_Touch";
							strEvent = "Event_Tap";
							
							JawCruncherStateController controller = PettingMain.Instance.DragonGameObject.GetComponent< JawCruncherStateController >();
								
							if( controller.SetGetState == JawCruncherStateController.JC_STATE.FixNF  
							&&  controller != null
							&&  controller.m_bIsEnableToTap )
							{
								Vector3 pos = m_feedbackCamera.camera.ScreenToWorldPoint( new Vector3( p_gesture.position.x, p_gesture.position.y, 2 ));
								m_successBurst.transform.position=pos;
								m_successParticle.Play();
								controller.SetGetState = JawCruncherStateController.JC_STATE.Drop;
							}
						break;
						
						case StatsManager.MODE_WATERBUCKET:
							if( TouchObject != null && TouchObject.transform.parent.name.Contains("WaterBucket") )
							{
								strEventType = "Type_From_Inventory";
								strEvent = "Event_Water_Bucket";
								Vector3 pos = m_feedbackCamera.camera.ScreenToWorldPoint( new Vector3( p_gesture.position.x, p_gesture.position.y, 2 ));
								m_successBurst.transform.position=pos;
								m_successParticle.Play();
							}
								
							else
							{
								strEventType = "Type_From_Inventory";
								strEvent = "Event_Outcome_Blast";
							}
						break;
						
						case StatsManager.MODE_ACT1:
							if( TouchObject != null && TouchObject.transform.name.Contains("Floor") )
							{
								strEventType = "Type_Touch";
								strEvent = "Event_Tap";
							}
						break;
						
						case StatsManager.MODE_NORMAL:
							strEventType = "Type_Touch";
							strEvent = "Event_Tap";
						break;
					}
				}
			}
			#endregion
			#region Gesture Move
			else if( p_gesture.type == GestureManager.GestureType.Move )
			{
				strEvent = "Event_Move";
				strEventType = "Type_Touch";
				
				if( StatsManager.Instance.DragonState == "Rubbing" && this.IsInteractible() )
				{
					bool bIsRubbingTrigger = true;
					
					if(TouchObject != null)
					{
						if( m_rubbingDirection == m_rubbingTargetDirection )
						{
							switch( StatsManager.Instance.DragonMode )
							{
							case StatsManager.MODE_ACT1:
							case StatsManager.MODE_NORMAL:
								switch(TouchObject.name)
								{
								case "RubTopCollider":
									m_rubbingObjectIndex.Enqueue(new Vector2(0,1));
									break;
								case "RubBottomCollider":
									m_rubbingObjectIndex.Enqueue(new Vector2(0,-1));
									break;
								case "RubLeftCollider":
									m_rubbingObjectIndex.Enqueue(new Vector2(-1,0));
									break;
								case "RubRightCollider":
									m_rubbingObjectIndex.Enqueue(new Vector2(1,0));
									break;
								default :
									m_rubbingObjectIndex.Enqueue(new Vector2(0,0));
									bIsRubbingTrigger = false;
									break;
								}
								break;
								
							case StatsManager.MODE_DRAGONSALVE:
								switch(TouchObject.name)
								{
								case "LeftCollider":
									m_rubbingObjectIndex.Enqueue(new Vector2(-1,0));
									break;
								case "RightCollider":
									m_rubbingObjectIndex.Enqueue(new Vector2(1,0));
									break;
								default :
									m_rubbingObjectIndex.Enqueue(new Vector2(0,0));
									bIsRubbingTrigger = false;
									break;
								}
								break;
								
							case StatsManager.MODE_BATH:
								switch(TouchObject.name)
								{
								case "LeftCollider":
									if( p_gesture.hit.point.x > 0.5f )
										m_rubbingObjectIndex.Enqueue(new Vector2(-1,0)); 
									else 
										m_rubbingObjectIndex.Enqueue(new Vector2(-1,1));
									if( p_gesture.hit.point.x < 0.7f )
										DentalParticles.Instance.Apply( p_gesture.hit.point, TouchObject.transform, 0.1f, 0.2f );
									break;
								case "RightCollider":
									if( p_gesture.hit.point.x < 0.5f )
										m_rubbingObjectIndex.Enqueue(new Vector2(1,0));
									else
										m_rubbingObjectIndex.Enqueue(new Vector2(1,1));
									if( p_gesture.hit.point.x < 0.27f )
										DentalParticles.Instance.Apply( p_gesture.hit.point, TouchObject.transform, 0.1f, 0.2f );
									break;
								default :
									m_rubbingObjectIndex.Enqueue(new Vector2(0,0));
									bIsRubbingTrigger = false;
									break;
								}
								break;
								
							case StatsManager.MODE_DENTALKIT:
								switch(TouchObject.name)
								{
								case "Teeth":
									DentalParticles.Instance.Apply( p_gesture.hit.point, TouchObject.transform, 0.01f, 0.02f );
									break;
								}
								
								break;
							}
							
							while(m_rubbingObjectIndex.Count>5)
							{
								m_rubbingObjectIndex.Dequeue();
							}
								
							Vector2 sum = Vector2.zero;
							foreach( Vector2 index in m_rubbingObjectIndex ) 
							{
								sum += index;
							}
							sum.x = sum.x / m_rubbingObjectIndex.Count;
							sum.y = sum.y / m_rubbingObjectIndex.Count;
							m_rubbingTargetDirection = sum.normalized;
							UpdateRubbingPosition(0.5f);
						}
					}
					else
					{
						if(m_rubbingTargetDirection != Vector2.zero)
						{
							m_rubbingObjectIndex.Clear();
							m_rubbingTargetDirection = Vector2.zero;
							UpdateRubbingPosition(0.5f);	
						}
						bIsRubbingTrigger = false;
					}
						
					Vector3 tempCenterPos 	    = Camera.main.ScreenToViewportPoint(new Vector3(p_gesture.position.x, p_gesture.position.y, 0));
					const float CENTER_VIEWPORT = 0.50f;
					const float TEMP_THRESHOLD  = 0.25f;
						 
					/*
					*	Immediate fix to center the rubbing position 
					*	Also to make the "RUBBING PARTICLE" centered
					
					if(	( CENTER_VIEWPORT + TEMP_THRESHOLD ) >= tempCenterPos.x
					&&  ( CENTER_VIEWPORT - TEMP_THRESHOLD ) <= tempCenterPos.x
					&&  ( CENTER_VIEWPORT + TEMP_THRESHOLD ) >= tempCenterPos.y
					&&  ( CENTER_VIEWPORT - TEMP_THRESHOLD ) <= tempCenterPos.y	)*/
					if ( bIsRubbingTrigger )
					{
						m_eventTimer.RestartScheduler();
	                    m_eventTimer.PauseScheduler();
						m_sprint9Demo.PauseScheduler();
							
						if (m_lastTouchOBject == null
						||	( TouchObject != null && m_lastTouchOBject != TouchObject ) )
						{
							m_lastTouchOBject = TouchObject;
							m_rubMeter.UpdatePosition(m_lastTouchOBject.transform.position);
						}
							
						m_rubMeter.Enable();
							
						if(m_curRubLength <= 250.0f)
						{
							m_curRubLength += p_gesture.velocity;
								
							if( Utility.RANDOM_INT(0,100) < 10 && m_curRubLength <= 200 )
							{
								// +KJ:08022013 
								//	TODO: Adjust this one and encode all at the tables
								// 	NOTE: Put all the triggers on the States listed below.
								//  The poses under rubbing state are:
								//	1) Pose_PettingHead
								// 	2) Pose_DragonSalve
								//	3) Pose_ToothBrush
								//	4) Pose_SoapAndBrush
									
								#region Dragon Poses
								if( StatsManager.Instance.DragonPose == "Pose_PettingHead" )
								{
										
								}
								else if( StatsManager.Instance.DragonPose == "Pose_DragonSalve" )
								{
										
								}
								else if( StatsManager.Instance.DragonPose == "Pose_DentalKit" )
								{
										
								}
								else if( StatsManager.Instance.DragonPose == "Pose_Bath" )
								{
										
								}
								#endregion
							}
						}
						else 
						{
							float completionPercentage = ((float)StatsManager.Instance.CurrentRubCount / (float)StatsManager.Instance.MaxRubCount);
							completionPercentage = Mathf.Clamp01(completionPercentage);
							
							if( StatsManager.Instance.DragonMode == StatsManager.MODE_DENTALKIT )
							{
								Color headColor 		= m_head.renderer.materials[1].color;
								Color tongueColor 		= m_tongue.renderer.materials[1].color;
								float alphaValue 		= 1.0f - completionPercentage;
								
								headColor.a 			= alphaValue;
								tongueColor.a 			= alphaValue;
							
								m_head.renderer.materials[1].color = headColor;
								m_tongue.renderer.materials[1].color = tongueColor;
							}
							
							m_rubMeter.UpdateCompletion(completionPercentage);
								
							m_curRubLength = 0.0f;
							StatsManager.Instance.UpdateRubCount();
								
							// + LA 091013: This is where we'll put reaction after the rubbing phase
								
							if( StatsManager.Instance.CurrentRubCount > StatsManager.Instance.MaxRubCount )
							{
								if( StatsManager.Instance.DragonPose == "Pose_PettingHead" )
								{
									// + LA 091013: This is to be replaced with a new reaction
									// NOTE: This has two variations
									// 1) Normal Pet Done (When the petting is completed in the parts other than the lower neck)
									// 2) Play dead like action (When the petting is completed in the lower neck)
									// - LA
									for(int i = 0; i<m_rubbingColliders.Count; i++)	m_rubbingColliders[i].SetActive(false);
									bool bIsSweetSpotHit = false;
									if( m_rubbingDirection.x > 0f && m_rubbingDirection.y >= 0f)	
										bIsSweetSpotHit = true;	
									
									m_rubbingTargetDirection = Vector2.zero;
									UpdateRubbingPosition(0.3f);
									
									if( TutorialController.GTutState == TutorialController.ETutorialState.Petting )
									{
										this.Reaction("Type_Petting", "Event_Petting_Done");

									}
									else
									{
										if(bIsSweetSpotHit)	this.Reaction("Type_Petting", "Event_Petting_Done_Sweet_Spot");
										else 				this.Reaction("Type_Petting", "Event_Petting_Done");
									}
									PettingMain.Instance.ShowUI("InventoryPanel");
									PettingMain.Instance.ShowUI("MainUI");
									
								}
								else if( StatsManager.Instance.DragonPose == "Pose_DragonSalve" )
								{
									this.Reaction("Type_Rubbing", "Event_Dragon_Salve_Done"); // aries:10182013 done dragon salve. 
									PettingMain.Instance.ShowUI("InventoryPanel");
									PettingMain.Instance.ShowUI("MainUI");
								}
								else if( StatsManager.Instance.DragonPose == "Pose_DentalKit" )
								{
									this.Reaction( "Type_Rubbing", "Event_Dental_Kit_Done" );
									this.SetMouthCollidersActive( false );
									DentalParticles.Instance.Reset();
									PettingMain.Instance.ShowUI("InventoryPanel");
									PettingMain.Instance.ShowUI("MainUI");
								}
								else if( StatsManager.Instance.DragonPose == "Pose_Bath" )
								{	
									Debug.Log( "Done" );
									this.Reaction("Type_Rubbing", "Event_Bath_Done");
									DentalParticles.Instance.Reset();
									PettingMain.Instance.ShowUI("InventoryPanel");
									PettingMain.Instance.ShowUI("MainUI");
								}
							
								StatsManager.Instance.ResetRubCount();
							}
						}
						return;
					}	
					else
					{
						m_rubMeter.Disable();
					}
				}
			}
			#endregion
			#region Gesture SwipeUp
			else if( p_gesture.type == GestureManager.GestureType.Swipe 
				&& p_gesture.direction == FingerGestures.SwipeDirection.Up 
				&& _isSwipeValid )
			{
				strEvent = "Event_SwipeUp";
				strEventType = "Type_Touch";
				bSatisfiedGesture = false;
			}
			#endregion
			#region Gesture SwipeDown
			else if( p_gesture.type == GestureManager.GestureType.Swipe 
				&& p_gesture.direction == FingerGestures.SwipeDirection.Down 
				&& _isSwipeValid)
			{
				strEvent = "Event_SwipeDown";
				strEventType = "Type_Touch";
				//bSatisfiedGesture = false;
			}
			#endregion
			#region Gesture SwipeLeft
			else if( p_gesture.type == GestureManager.GestureType.Swipe 
				&& p_gesture.direction == FingerGestures.SwipeDirection.Left 
				&& _isSwipeValid)
			{
				strEvent = "Event_SwipeLeft";
				strEventType = "Type_Touch";
				
				//bSatisfiedGesture = false;
			}
			#endregion
			#region Gesture SwipeRight
			else if( p_gesture.type == GestureManager.GestureType.Swipe 
				&& p_gesture.direction == FingerGestures.SwipeDirection.Right 
				&& _isSwipeValid)
			{
				strEvent = "Event_SwipeRight";
				strEventType = "Type_Touch";
				
				//bSatisfiedGesture = false;
			}
			#endregion
			#region Gesture Swipe
			else if( p_gesture.type == GestureManager.GestureType.Swipe )
			{
				strEvent = "Event_Swipe";
				strEventType = "Type_Touch";
				
				bSatisfiedGesture = false;
			}
			#endregion
			#region Gesture Hold
			else if( p_gesture.type == GestureManager.GestureType.Hold )
			{
				strEvent = "Event_Hold";
				strEventType = "Type_Touch";
				
				switch( StatsManager.Instance.DragonMode )
				{
				case StatsManager.MODE_NORMAL:
					
					break;
					
				case StatsManager.MODE_IUC:
					
					if( !JawCruncher.IsThrownJC() )
					{
						StatsManager.Instance.touchPosition = p_gesture.position;
						m_fastSwipeCounter = 0.0f;
						//m_holdParticle.gameObject.GetComponent<FollowComponent>().SeekPoint(p_gesture.position);
						//m_holdParticle.gameObject.GetComponent<FollowComponent>().lerpSpeed = 1.0f;
						//m_holdParticle.Play();
						JawCruncher.EnableFollowFingerJC();
					}
					
					break;
					
				case StatsManager.MODE_DRAGONSALVE:
					
					break;
				}
			}
			#endregion
			#region Gesture Hold_Move
			else if( p_gesture.type == GestureManager.GestureType.Hold_Move )
			{
				strEvent = "Event_Hold_Move";
				strEventType = "Type_Touch";
				
				switch( StatsManager.Instance.DragonMode )
				{
				case StatsManager.MODE_NORMAL:
					break;
				case StatsManager.MODE_IUC:
					if( !JawCruncher.IsThrownJC() )
					{
						StatsManager.Instance.touchPosition = p_gesture.position;
						JawCruncher.EnableFollowFingerJC();
					}
					break;	
				case StatsManager.MODE_DRAGONSALVE:
					break;
				}
			}
			#endregion
			#region Gesture Hold_Move_Fast
			else if( p_gesture.type == GestureManager.GestureType.Hold_Move_Fast )
			{
				strEvent = "Event_Hold_Move_Fast";
				strEventType = "Type_Touch";
				
				switch( StatsManager.Instance.DragonMode )
				{
				case StatsManager.MODE_NORMAL:
					
					break;
					
				case StatsManager.MODE_IUC:
					
					if( !JawCruncher.IsThrownJC() )
					{
						
						StatsManager.Instance.touchPosition = p_gesture.position;
						JawCruncher.EnableFollowFingerJC();
					}
					
					break;
					
				case StatsManager.MODE_DRAGONSALVE:
					
					break;
				}
			}
			#endregion
			#region Gesture Hold_Up
			else if( p_gesture.type == GestureManager.GestureType.Hold_Up )
			{
//				strEvent = "Event_Hold_Up";
//				strEventType = "Type_Touch";
				
				switch( StatsManager.Instance.DragonMode )
				{
				case StatsManager.MODE_NORMAL:
					
					break;
					
				case StatsManager.MODE_IUC:
					
					strEvent = "Event_Hold_Up";
					strEventType = "Type_Touch";
					
					if( !JawCruncher.IsThrownJC() )
					{
						StatsManager.Instance.touchPosition = p_gesture.position;
						JawCruncher.DisableFollowFingerJC();
						JawCruncher.DropJC();
						m_fastSwipeCounter = 0.0f;
					}
					
					break;
					
				case StatsManager.MODE_DRAGONSALVE:
					
					break;
					
				case StatsManager.MODE_DENTALKIT:
					//m_soapParticle.SetActive( false );
					break;
					
				case StatsManager.MODE_ACT1:
					
					if( CarePlayNFController.GetPetAnimController().IsCurAnimStateOnSub( ERigType.Body, "TouchMoment", "TouchMomentWaitingIdle" ) )
					{
						strEvent = "Event_Hold_Up";
						strEventType = "Type_Touch";
					}
					
					if( CarePlayNFController.GetPetAnimController().IsCurAnimStateOnSub( ERigType.Body, "TouchMoment", "TouchMomentToMidground" ) )
					{
						float currentAnimTime = CarePlayNFController.GetPetAnimController().GetCurAnimNormTime( ERigType.Body );
						
						if( currentAnimTime <= 0.4f )
						{
							strEvent = "Event_Hold_Up";
							strEventType = "Type_Touch";
						}
					}
					
					break;
				}
				
				bSatisfiedGesture = false;
			}
			#endregion
			#region Gesture Hold_Swipe
			else if( p_gesture.type == GestureManager.GestureType.Hold_Swipe )
			{
				strEvent = "Event_Hold_Swipe";
				strEventType = "Type_Touch";
				
				switch( StatsManager.Instance.DragonMode )
				{
				case StatsManager.MODE_NORMAL:
					
					break;
					
				case StatsManager.MODE_IUC:
					
					if( !JawCruncher.IsThrownJC() )
					{
						StatsManager.Instance.touchPosition = p_gesture.position;
						JawCruncher.ThrowJC(p_gesture.screenHead, p_gesture.screenTail, p_gesture.velocity);
					}
					
					break;
					
				case StatsManager.MODE_DRAGONSALVE:
					
					break;
				}
			}
			#endregion
			#region Gesture Hold_Fake_Swipe
			else if( p_gesture.type == GestureManager.GestureType.Hold_Fake_Swipe )
			{
				strEvent = "Event_Hold_Fake_Swipe";
				strEventType = "Type_Touch";
				
				switch( StatsManager.Instance.DragonMode )
				{
				case StatsManager.MODE_NORMAL:
					
					break;
					
				case StatsManager.MODE_IUC:
					
					if( !JawCruncher.IsThrownJC() )
					{
						GestureManager.SwipeWorldPos( p_gesture.screenHead, p_gesture.screenTail );
						StatsManager.Instance.touchPosition = p_gesture.position;
						JawCruncher.EnableFollowFingerJC();
					}
					
					break;
					
				case StatsManager.MODE_DRAGONSALVE:
					
					break;
				}
				
				bSatisfiedGesture = false;
			}
			#endregion
			else
			{
				m_holdParticle.Stop();
			}
			
			// + LA 103013: This is for any triggered reaction
			switch( StatsManager.Instance.DragonMode )
			{
			case StatsManager.MODE_ACT1:
				
				if( StatsManager.Instance.DragonState == "Coming_Down" )
				{
					strEventType = "Type_Touch";
					strEvent = "Event_AnyInteraction";
				}
				
				if( StatsManager.Instance.DragonState == "TouchMoment_Foreground" )
				{
					Debug.Log ( "Event: " + strEvent );
					if( strEvent != "Event_Hold" && 
						strEvent != "Event_Down" &&
						strEvent != "Event_Up" &&
						strEvent != "Event_Move" )
					{
						strEventType = "Type_Touch";
						strEvent = "Event_AnyInteraction";
					}
				}
				
				break;
			}
			
			if(p_gesture.type != GestureManager.GestureType.Move)
			{
				m_curRubLength = 0.0f;
			}
			
			StatsManager.Instance.currentEventType = strEventType;
			StatsManager.Instance.currentEvent = strEvent;
			
			// trigger gestures for Tutorial
			if( !TutorialDialogueController.Instance.TriggerGesture(strEvent) )
			{
				D.Log("Tutorial is Active:"+TutorialDialogueController.Instance.IsActive+" Cannot Play Action with Event:"+strEvent+" \n");
				return;
			}
			
			// block gestures. for Tutorial
			if( !TutorialController.Instance.IsValidGestureForTutorial( strEventType, strEvent, p_gesture ) )
			{
				D.Log("Dragon::Trigger Gesture:"+strEvent+" is not valid during TutorialState:"+TutorialController.GTutState+" \n");
				return;
			}
			
			// trigger gestures
			this.GestureReaction(strEventType, strEvent, p_gesture);
			m_delegate.CheckInventoryEvents( strEventType, strEvent, StatsManager.Instance.DragonTouchPart );
		}	
		// - LA
			
		private IEnumerator SetColliderVisible (float p_delay)
		{
			yield return new WaitForSeconds(p_delay);
			bool bIsRubbing = false;
			if(StatsManager.Instance.DragonState == "Rubbing")	bIsRubbing = true;

			if( bIsRubbing )
			{
				PettingMain.Instance.HideUI("InventoryPanel");
				PettingMain.Instance.HideUI("MainUI");
			}
			else
			{
				PettingMain.Instance.ShowUI("InventoryPanel");
				PettingMain.Instance.ShowUI("MainUI");
			}

			for(int i=0; i<m_rubbingColliders.Count; i++)	m_rubbingColliders[i].SetActive(bIsRubbing);
		}
		
		#region Tutorial Green Circle Burst
		public static void BurstForBoomAndFish( GestureObject p_gesture )
		{
			Vector3 pos = m_feedbackCamera.camera.ScreenToWorldPoint( new Vector3( p_gesture.position.x, p_gesture.position.y, 2 ));
			
			m_successBurst.transform.position=pos;
			m_successParticle.Play();
		}
		#endregion
		
		#region Rubbbing section iTween SetValue as Lerp
		private void SetRubbingDirection (Vector2 p_value)
		{
			m_rubbingDirection = p_value;
			CarePlayNFController.GetPetAnimController().SetPettingDirection(m_rubbingDirection);
		}
		private void UpdateRubbingPosition (float p_time)
		{
			Utility.StopiTweenByName("rubLerp");
			iTween.ValueTo(gameObject, iTween.Hash( "name", "rubLerp",
													"from", m_rubbingDirection,
													"to", m_rubbingTargetDirection,
													"onupdate", "SetRubbingDirection",
													"time", p_time,
													"oncompletetarget", gameObject));
		}
		#endregion
		
		// +KJ:05282013 this is trigger after every animations queue played
		public void TriggerAncilliary ()
		{
			this.Reaction("Type_Events", "Event_Idle_Midground");
		}
		
		// trigger for item usage on pet dragon
		public void TriggerItemUse (string p_itemId)
		{
			if( !this.IsInteractible() )
				return;
			
			this.Reaction();
		}
		
		// trigger for DragonCall and other ui button events
		public void TriggerButton (string p_buttonId)
		{
			if( !this.IsInteractible() )
				return;
			
			this.Reaction();
		}
		
		public enum ETestResult {
			//NOTE: This should be in the right sequence
			FAILED,     // 0 = false
			PASSED,     // 1 = true;
			IGNORED     // 2 = unused in conversion from boolean
		}
		
		public ETestResult TestUsageInfo (string p_reqKey, object p_data){
			
			//---check if parameter values are valid
			if(!usageQueryKey_to_enum.ContainsKey(p_reqKey)) 
			{
				Log("====Not UsageInfo \""+p_reqKey+"\" \n");
				return ETestResult.IGNORED;
			}
			
			Log("=======Testing UsageInfo \""+p_reqKey+"\": Value:"+p_data+"\n");
			
			ArrayList usageData = p_data as ArrayList;
			if(usageData == null) 
			{
				Log("====Testing FAILED \n");
				return ETestResult.FAILED;
			}
			
			//---init
			ETestResult eResult = ETestResult.IGNORED;
			UsageFreqManager usageMgr = StatsManager.Instance.UsageTracker;
			
			EUsageQueryKey eKey = usageQueryKey_to_enum[p_reqKey];
			
			bool isCurAction = (eKey == EUsageQueryKey.CUR_ACTION);
			
			Log("EnumTable:"+MiniJSON.jsonEncode(usageQueryKey_to_enum)+" eKey:*"+eKey+"* m_testCurActionUsed:*"+m_testCurActionUsed+"* isCurAction:*"+isCurAction+"*");
			
			//---if usage of current action is trackable.
			if(m_testCurActionUsed != null && (int)eKey == (int)EUsageQueryKey.CUR_ACTION){
				
				bool isPassed = usageMgr.TestUsage_OfCurrent(m_testCurActionUsed, ETrackedObj.ACTION, usageData);
				eResult = (isPassed ? ETestResult.PASSED : ETestResult.FAILED);
				Log("====Testing CUR_ACTION result:\""+eResult+"\" isPassed:\""+isPassed+"\" \n");
			}
			else if(m_testCurItemUsed != null && eKey == EUsageQueryKey.CUR_ITEM){
				
				bool isPassed = usageMgr.TestUsage_OfCurrent(m_testCurActionUsed, ETrackedObj.ITEM, usageData);
				eResult = (isPassed ? ETestResult.PASSED : ETestResult.FAILED);
				Log("====Testing CUR_ITEM result:\""+eResult+"\" isPassed:\""+isPassed+"\" \n");
			}
			else if(eKey == EUsageQueryKey.ITEM_USAGE){
				bool isPassed = usageMgr.TestUsage(ETrackedObj.ITEM, usageData);
				eResult = (isPassed ? ETestResult.PASSED : ETestResult.FAILED);
				Log("====Testing ITEM_USAGE result:\""+eResult+"\" isPassed:\""+isPassed+"\" \n");
			}
			else if(eKey == EUsageQueryKey.ACT_USAGE){
				//Debug.LogWarning("TestUsageInfo(): ----- (eKey == EUsageQueryKey.ACT_USAGE)\n");
				bool isPassed = usageMgr.TestUsage(ETrackedObj.ACTION, usageData);
				eResult = (isPassed ? ETestResult.PASSED : ETestResult.FAILED);
				Log("====Testing ACT_USAGE result:\""+eResult+"\" isPassed:\""+isPassed+"\" \n");
			}
			else
			{
				Log("====Testing USAGE_UNIDENTIFIED eKey:\""+eKey+"\" result:\""+eResult+"\" \n");
			}
			
			//Debug.Break();
			
			return eResult;
			
		}
		
		/// <summary>
		/// Test if the passed requirement is satisfied
		///   
		/// </summary>
		public bool TestRequirements (Hashtable p_Reqs){
			
			Log("=====Testing conditions:\n");
			
			bool bResult = true;
			
			// Dragon DNA Requirement checking
			if ( ! DragonDNA.Instance.CheckDNARequirement( p_Reqs ) ) {
				bResult = false;
			}
			
			//TODO: refactor the loop of HashTable 
			foreach(string condKey in p_Reqs.Keys){
				
				//Skip "Prio", "Chance" and  REQ_STRKEY_QUEUEABLE values. This will be check later.
				if( condKey == REQ_STRKEY_PRIO 
					|| condKey == REQ_STRKEY_CHANCE
					|| condKey == REQ_STRKEY_QUEUEABLE
				){
					Log("=======Skip Testing \""+condKey+"\": Value:"+StatsManager.Instance.GetDragonData(condKey)+"\n");
					continue;
				}
				else
				{
					Log("=======Testing \""+condKey+"\": Value:"+StatsManager.Instance.GetDragonData(condKey)+"\n");
				}
			
				object valueObj = p_Reqs[condKey]; // as object;
			
				//---Test current item's or action's usage information.
				ETestResult bUsageResult = TestUsageInfo(condKey, valueObj);
				     if(bUsageResult == ETestResult.PASSED){ Log("Usage Satisfied."); continue; }
				else if(bUsageResult == ETestResult.FAILED){ Log("Usage Failed."); bResult = false; break; }
				
				System.Object dragonVal = StatsManager.Instance.GetDragonData(condKey);
				if(dragonVal == null){
					Log("===========Key("+condKey+") not found in dragon's Data\n");
					bResult = false;
					break;
				}
				
				//---Check INTEGER value
				if(valueObj is Double){
					
					int intValue = (int)(double)valueObj;  //desc: unbox then convert to int
					Log("=========Condition(\""+condKey+"\") value is INTEGER. Value=\""+intValue.ToString()+"\"\n");
					
					// --check if key in dragon data exists.
					System.Object strVal = StatsManager.Instance.GetDragonData(condKey);
					
					if(strVal == null){
						Log("===========Key("+condKey+") not found in dragon's Data\n");
						//continue;
						bResult = false;
						break;
					}
					
					// --check if condition is satisfied.
					//int dragonValue = int.Parse(strVal);
					int dragonValue = (int)strVal;
					
					if( intValue != dragonValue ){
						Log("===========FAILED condition\n");
						bResult = false; 
						break; 
					}
					Log("===========Condition SATISFIED!!!\n");
					
				}
				//---Check STRING value
				else if(valueObj is string){
					
					string strValue = valueObj as string;
					Log("=========Condition \""+condKey+"\" value is STRING. Value=\""+strValue+"\"\n");
					
					// --check if key in dragon data exists.
					string dragonValue = StatsManager.Instance.GetDragonData(condKey) as string;
					if(dragonValue == null){
						Log("===========Key("+condKey+") not found in dragon's Data\n");
						//continue;
						bResult = false;
						break;
					}
					
					// --check if condition is satisfied.
					Log("=========Comparing the dragonValue(\""+dragonValue+"\") to \""+strValue+"\"\n");
					if(strValue != dragonValue){
						Log("===========FAILED condition\n");
						bResult = false;
						break;
					}
					
					Log("===========Condition SATISFIED!!!\n");
					
				}
				//---Check ARRAY value with ARRAY on DragonData
				else if(valueObj is ArrayList && dragonVal is ArrayList ){
					
					Log("=========Condition \""+condKey+"\" value is ARRAYLIST with DragonValue ARRAYLIST.\n");
					
					ArrayList arrValues = valueObj as ArrayList;
					if(arrValues.Count == 0) continue;
					
					//check value type in the list
					object item0 = arrValues[0];
					object item1 = arrValues[1];
					
					//---if items in the list are STRINGS for Even indeces and ArrayList for Odd indeces
					if(item0 is string && (item1 is ArrayList)){
						
						ArrayList dragonValue = StatsManager.Instance.GetDragonData(condKey) as ArrayList;
						if(dragonValue == null){
							Log("===========Key("+condKey+") not found in dragon's Data\n");
							//continue;
							bResult = false;
							break;
						}
						
						Log("===========List of [0]STRINGS [1]ARRAYLIST\n");
						
						bool bFound = false;
						for( int i = 0; i < arrValues.Count; i += 2 )
						{
							string key = arrValues[0] as string;
							ArrayList values = arrValues[1] as ArrayList;
							
							Log("==============Is Key [\""+key+"\"] With Values \""+MiniJSON.jsonEncode(values)+"\"\n");
							
							for( int j = 0; j < dragonValue.Count; j += 2 )
							{
								string keyToCompare 		= dragonValue[0] as string;
								ArrayList valuesToCompare 	= dragonValue[1] as ArrayList;
								
								Log("==================Is KeyToCompare [\""+keyToCompare+"\"] With ValuesToCompare \""+MiniJSON.jsonEncode(valuesToCompare)+"\"\n");
								
								if( key == keyToCompare )
								{
									for( int k = 0; k < values.Count; k++ )
									{
										string valueToCompare = values[k] as string;
										
										if( valuesToCompare.Contains(valueToCompare) )
										{
											bFound = true;
											Log("======================DragonData contains Key[\""+key+"\"] With \""+valueToCompare+" Value\"\n");
											break;
										}
										else
										{
											Log("======================DragonData don't have Key[\""+key+"\"] With \""+valueToCompare+" Value\"\n");
										}
									}
								}
							}
						}
						
						if(!bFound){
							Log("===========FAILED condition\n");
							bResult = false;
							break;
						}
						
						Log("===========Condition SATISFIED!!!\n");
						
					}
					else {
						Log("=========Condition \""+condKey+"\" contains value of unknown type.\n");
						Log("=========Object type: "+valueObj.GetType().ToString()+"\n");
						bResult = false;
						break;
					}
					
				}
				//---Check ARRAY value
				else if(valueObj is ArrayList && ( dragonVal is string || dragonVal is float || dragonVal is int ) ){
					
					Log("=========Condition \""+condKey+"\" value is ARRAYLIST.\n");
					
					//string strDragonVal = StatsManager.Instance.GetDragonData(condKey) as string;
					// --check if key in dragon data exists.
					if(dragonVal == null){
						Log("===========Key \""+condKey+"\" not found in dragon's Data.\n");
						//continue;
						bResult = false;
						break;
					}
					
					ArrayList arrValues = valueObj as ArrayList;
					if(arrValues.Count == 0) continue;
					
					//check value type in the list
					object item0 = arrValues[0];
					object item1 = arrValues[1];
					
					//---if items in the list are STRINGS
					if(item0 is string && !(item1 is ArrayList)){
						
						string dragonValue = (string)dragonVal;
						
						Log("===========List of STRINGS\n");
						
						bool bFound = false;
						foreach(string item in arrValues){
							Log("==============Is Value [\""+dragonValue+"\"] equal to \""+item+"\"\n");
							if(item == dragonValue){ 
								Log("===============Match found!\n");
								bFound = true;
								break;
							}
						}
						
						if(!bFound){
							Log("===========FAILED condition\n");
							bResult = false;
							break;
						}
						
						Log("===========Condition SATISFIED!!!\n");
						
					}
					//---if items in the list are INTEGERS
					else if(item0 is Double){
						
						float dragonValue = Utility.ParseToFloat(dragonVal);
						
						Log("===========List of INTEGERS\n");
						
						//???: ASSUME: ...that the list of integer is always a range values.
						if(arrValues.Count >= 2){
							float minVal = (float)(double)arrValues[0];   //unbox then convert to float.
							float maxVal = (float)(double)arrValues[1];
							
							Log("==============Testing dragon value("+dragonValue+") between range ("+minVal+", "+maxVal+")\n");
							
							//if not within the range, failed.
							if(!(minVal <= dragonValue && (dragonValue <= maxVal || maxVal < 0))){
							//if(dragonValue < minVal || dragonValue > maxVal){
								Log("===========FAILED condition\n");
								bResult = false;
								break;
							}
							
							Log("===========Condition SATISFIED!!!\n");
							
						}
						else {
							Log("=========FAILED condition. Insufficient integer item in the list.\n");
							bResult = false;
							break;
						}
						
					}
					else {
						Log("===========List of UNSUPPORTED type.\n");
						Log("===========Object type: "+item0.GetType().ToString()+"\n");
						bResult = false;
						break;
					};
				}
				else {
					Log("=========Condition \""+condKey+"\" contains value of unknown type.\n");
					Log("=========Object type: "+valueObj.GetType().ToString()+"\n");
					Log("=========Object Value: "+MiniJSON.jsonEncode(valueObj as ArrayList)+"\n");
					bResult = false;
					break;
				}
				
			}
			
			return bResult;
		}
		
		/// <summary>
		///   Chooses reaction data by "Prio" & "Chance".
		/// </summary>
		/// <note>
		///    The format of each item in p_reactins list should be:
		///       Array[
		///           anyObject,
		///           Hashtable:{
		///              //hashtable contents
		///           }
		///       ]
		/// </note>
		/// <returns>
		///    The chosen item in p_reactions array.
		/// </returns>
		ArrayList ChooseByPrioChance (ArrayList p_reactions, int p_condIndex, bool p_isEvent = false )
		{
			//NOTE: This function is not yet tested completely. 
			//TODO: Debug this further if working in all situations. 
			
			//---Desc: Get the reaction data with HIGHEST PRIORITY value.
			const string keyPrio   		= REQ_STRKEY_PRIO;
			const string keyChance 		= REQ_STRKEY_CHANCE;
			const string keyQueueable	= REQ_STRKEY_QUEUEABLE;
			const float  defChance = 1.0f; //default chance value
			float totalChanceVal = 0.0f;
			int maxPrioVal    = int.MinValue;
			int maxPrioCount  = 0;
			ArrayList listPrioReact = new ArrayList();                  //will contain reactions with highest Prio value together...
                                                                        //   ...with the previous highest Prio value.
			List<bool> listCueableReact = new List<bool>();				//will contain reaction's cueable status. 
																		//	 ...indices of listPrioReact & listCueableReact are mirrored with each other.
			
			Log("=== Choosing by PRIO Value\n");
			foreach(ArrayList reactItem in p_reactions){
				
				DLogItem("===== Checking Prio of [-?-]: \""+ActDesc(reactItem)+"\" \n");  //xxx: Debug code
				
				if(reactItem.Count < p_condIndex + 1) continue;         //optional sanity check
				
				Hashtable req = reactItem[p_condIndex] as Hashtable;    //NOTE: reactItem[0] is the string key of reaction data.
				
				if(req == null) continue;                               //optional sanity check
				
				int valPrio = 0;                                        //default to zero if "Prio" does not exist.
				
				if(req.ContainsKey(keyPrio) && req[keyPrio] is Double){ //NOTE: verify if short circuiting is supported.
					
					if( req[keyPrio] is Double )	valPrio = (int)(double)req[keyPrio];
					else 							valPrio = int.Parse(req[keyPrio].ToString());
					
					Log("======= Has \"Prio\" Condition \n");  //xxx: Debug code
					
				}
				
				Log("===== Display Prio Value: "+valPrio+" \n");  //xxx: Debug code
				
				if(valPrio > maxPrioVal){
					Log("======= NEW HEIGHEST Prio Val: "+valPrio+", Prev Max Prio: "+maxPrioVal+" \n");
					maxPrioVal = valPrio;
					maxPrioCount = 0;                                   //reset number of reactItem with highest Prio
					totalChanceVal = 0.0f;
				}
				else if(valPrio < maxPrioVal){
					Log("======= NOT ENOUGH Prio Val: "+valPrio+", Prev Max Prio: "+maxPrioVal+" \n");
					continue;
				}
					
				listPrioReact.Add(reactItem);
				maxPrioCount++;
				
				//  --update totalChance for current highest priority reaction data.
				float valChance = defChance;                                 //default value if "Chance" does not exist.
				
				if(req.ContainsKey(keyChance) && req[keyChance] is Double){
					valChance = (float)(double)req[keyChance];
					Log("======= Has \"Chance\" Condition \n");
				}
				
				totalChanceVal += valChance;
				Log("======= CHANCE Value: "+valChance+"; Total Chance = "+totalChanceVal+
					" (for MaxPrio = "+ maxPrioVal +"), MaxPrioCount = "+maxPrioCount+"\n");
				
				//  --IsQueueable checking
				if( p_isEvent )
				{
					int isQueueable = 0;
					string actDesc = ActDesc(reactItem);    
					
					if(req.ContainsKey(keyQueueable) && req[keyQueueable] is Double){
						isQueueable = (int)(double)req[keyQueueable];
						
						// TODO: apply enable/disable of queueable flag here for a specific reaction
#if DEBUG_CUEABLES
						Debug.LogError("======= "+actDesc+" Has \"Cueable/Queueable\" Condition and its "+isQueueable+"\n");
#else
						Log("======= Has \"Cueable/Queueable\" Condition and its "+isQueueable+"\n");
#endif
					}
					
					// +KJ:08232013 Record here if reaction is cueable or not
					// -- optimized
					//listCueableReact.Add(( isQueueable == 1 ? true : false ));
					// -- debugable
					if( isQueueable != 0 )  listCueableReact.Add(true);
					else 					listCueableReact.Add(false);
				}
			}
			
			//---Desc: Choose reaction data by CHANCE value
			
			ArrayList chosenReact    = null;
			float luckyRange = 0.0f;
			float luckyPick = Utility.RANDOM_FLOAT(0, totalChanceVal);
			
			Log("===Choosing by CHANCE Value. Lucky Pick = "+luckyPick+"\n");
			Log("===== listPrioReact.Count = "+listPrioReact.Count +"; maxPrioCount = "+maxPrioCount+"\n");
			
			IEnumerator enumReact = listPrioReact.GetEnumerator(listPrioReact.Count - maxPrioCount, maxPrioCount);
			while(enumReact.MoveNext()){                                 //note: This is just like foreach() but for a specific range in an ArrayList.
				
				ArrayList reactItem = enumReact.Current as ArrayList;
				if(reactItem == null) continue;
				
				string actDesc = ActDesc(reactItem);                            //xxx: Debug code
				DLogItem("=====Checking Chance of [-?-]: \""+actDesc+"\" \n");  //xxx: Debug code
				
				
				//NOTE: At this point, each reactItem will surely be a 2 item array where index 1 is in the requirement format.
				//if(reactItem.Count < 2) continue;   //No need to sanity check
				
				Hashtable req = reactItem[p_condIndex] as Hashtable; 
				
				float valChance = defChance;
				
				if(req.ContainsKey(keyChance) && req[keyChance] is Double){
					valChance = (float)(double)req[keyChance];
				}
				
				luckyRange += valChance;
				
				if(luckyPick <= luckyRange){
					//Log("===== It's CHOSEN!!! \""+actDesc+"\" \n");
					chosenReact = reactItem;
					
					// +KJ:08232013 update the cueable flag on this chosen reaction.
					if( p_isEvent )
					{
						int selectedReactionIndex = listPrioReact.IndexOf(reactItem);
						m_bEventIsCueable = listCueableReact[selectedReactionIndex];
						
#if DEBUG_CUEABLES
						Debug.LogError("===== It's CHOSEN!!! \""+actDesc+"\" and cueable:"+m_bEventIsCueable+" \n");
#else
						Log("===== It's CHOSEN!!! \""+actDesc+"\" and cueable:"+m_bEventIsCueable+" \n");
#endif
					}
					else
					{
						Log("===== It's CHOSEN!!! \""+actDesc+"\"\n");
					}
					break;
				}
			}
			
			//???: test this if working
			//foreach(ArrayList reactItem in listPrioReact.GetEnumerator(listPrioReact.Count - maxPrioCount, maxPrioCount)){
			//}
			
			return chosenReact;
		}
		
		ArrayList ChooseRandomly(ArrayList p_items){
			int pick = Utility.RANDOM_INT(p_items.Count);
			return p_items[pick] as ArrayList;
		}
		
		//xxx: Start: Debug function:
		private string printListString(ArrayList p_listString){
			string strActions = "";
			foreach(string action in p_listString){
				strActions += "\""+action+"\", ";
			}
			return strActions;
		}
		
		private string ActDesc(ArrayList p_data){
			if(!D.On) return null;   //avoid expensive debug code if debugging is enabled
			
			string outStr = "[no name]";
			
			if(p_data == null || p_data.Count <= 0)
				return outStr;
			
			object desc = p_data[0];
			
			if(desc is string){
				outStr = desc as string;
			}
			else if(desc is ArrayList){
				ArrayList list = desc as ArrayList;
				outStr = "[ ";
				foreach(object item in list){
					if(item is Double)		   outStr += ((double)item).ToString()+", ";
					else if(item is string)    outStr += "\""+(item as string)+"\", ";
					else if(item is ArrayList) outStr += "[array], ";
					else if(item is Hashtable) outStr += "{hash}, ";
					else                       outStr += "?, ";
				}
				outStr += " ]";
			}
			
			return outStr;
		}
		
		private void DLogItem(string p_str){
			if(!D.On) return; //avoid expensive debug code if debugging is enabled
			string itemType;
			if(this.debugVar_isAniSeq)
				itemType = "Act-Seq";
			else 
				itemType = "Reaction";
		
			Log(p_str.Replace("[-?-]",itemType));
		}
		
		//xxx: End: Debug function:
		
		public class PrioList {
			public int prioVal = DEFAULT_REQ_PRIO;   //priority value
			public ArrayList list;
			public PrioList(int p_prio, ArrayList p_list){
				prioVal = p_prio;
				list = p_list;
			}
		}
		
		//TODO: optimize this
		private List<PrioList> SortedListByPrio(ArrayList p_items, int p_condIndex){
			//List<int> prioNumList = new List<int>();
			Dictionary<int, ArrayList> listByPrio = new Dictionary<int, ArrayList>();
			
			Log("-----Start SortedListByPrio()\n");
			Log("-----Looping each item in the list\n");
			
			foreach(ArrayList itemData in p_items){
				Log("-------Next Item\n");
				int prioVal = DEFAULT_REQ_PRIO;
				
				if(p_condIndex < itemData.Count) {
					Hashtable reactConditions = itemData[p_condIndex] as Hashtable;
					prioVal = GetPrioValue(reactConditions);
				}
				else {
					Log("---------Item has NO \"Requirements\" data.\n");
				}
				
				Log("-------PrioValue = "+prioVal+"\n");
				listByPrio[prioVal].Add(itemData);
			}
			
			//TODO: optimize this
			List<int> list = new List<int>();
			foreach(int iKey in listByPrio.Keys) list.Add(iKey);
			list.Sort();
			list.Reverse();
			
			
			Log("-----Creating sorted list of list of items grouped by priority\n");
			
			List<PrioList> outList = new List<PrioList>();
			foreach(int prioVal in list){
				PrioList pList = new PrioList(prioVal, listByPrio[prioVal]);
				outList.Add(pList);
				Log("-------Adding list with Prio = " + prioVal + "\n");
			}
			
			Log("-----Finish getting SortedListByPrio()\n");
			return outList;
		}
		
		private int GetPrioValue(Hashtable p_Reqs){
			
			int val = DEFAULT_REQ_PRIO;  
			
			if(p_Reqs.ContainsKey(REQ_STRKEY_PRIO)){
				
				object prio = p_Reqs[REQ_STRKEY_PRIO];
				
				if(prio is Double){
					val = (int)(double)prio;
				}
				//else if(prio is string){
				//	val = int.Parse(prio);
				//}
			}

			return val;
		}
		
		// +KJ:07032013
		//Generic function for Choosing Events based on their requirements
		/// <summary>
		///    Choose a qualified event in the given list based 'Chance', 'Prio' & 'Queueable'.
		///    These are the suggested params to be checked. but basically we can use all the params used by the others.
		/// </summary>
		/// <returns>
		///    The qualified event.
		/// </returns>
		/// <param name='p_events'>
		///    -List of events to be checked. Should be in the right format. 
		///    -For the format of each item, 
		/// 		- index n is an array with..
		/// 			- index 0 should contain the reactions (Array) for the target event while the.. (required)
		/// 			- index 1 'can' contain a hashtable of requirements. (optional)
		/// </param>
		ArrayList ChooseQualifiedEvents (ArrayList p_events)
		{
			// choose by requirements
			ArrayList qualifiedEvents 	= new ArrayList();
			ArrayList defaultEvents 	= new ArrayList();
			
			foreach(ArrayList evt in p_events)
			{
				//ArrayList reactions 	= evt[0] as ArrayList;
				Hashtable requirements	= null; //evt[1] as Hashtable;
				
				if ( evt.Count > 1 )
				{
					requirements = evt[1] as Hashtable;
					
					if( TestRequirements( requirements ) ) {
						qualifiedEvents.Add(evt);
					}

				} else {
					defaultEvents.Add( evt );
				}
			}
			
			// choose reaction by prio & chance
			if( qualifiedEvents.Count > 0 )
				qualifiedEvents = ChooseByPrioChance(qualifiedEvents, 1, true);
			// choose default reactions randomly
			else if( defaultEvents.Count > 0 )
				qualifiedEvents = ChooseRandomly(defaultEvents);
			else
				return null;
			
			// chosen reactions
#if DEBUG_CUEABLES
			Debug.LogError(">>>> ChosenReaction:"+MiniJSON.jsonEncode(qualifiedEvents));
#else
			Log(">>>> ChosenReaction:"+MiniJSON.jsonEncode(qualifiedEvents));
#endif
			
			if( qualifiedEvents == null ) 
			{
#if DEBUG_CUEABLES
				Debug.LogError("--------------- No Satisfied Event ---------------");
#else
				D.Warn("--------------- No Satisfied Event ---------------");
#endif
				return null;
			}
			
			if( qualifiedEvents.Count < 1 )
			{
#if DEBUG_CUEABLES
				Debug.LogError("--------------- No Satisfied Event ---------------");
#else
				D.Warn("--------------- No Satisfied Event ---------------");
#endif
				return null;
			}
			
			// return only the reactions of the target event and trim down the requirements of the current event
			return qualifiedEvents[0] as ArrayList;
		}
		
		
		/*
		ArrayList _ChooseQualifiedItem(ArrayList p_items, int p_condIndex){
			
			List<PrioList> listByPrio = SortedListByPrio(p_items, p_condIndex);
			
			//foreach(ArrayList itemList in listByPrio){
				
			//}
			
			return null;
			
		}
		
		ArrayList _ChooseQualifiedFromList(ArrayList p_items, int p_condIndex){
			
			return null;
			
		}
		*/
		
		//Generic function for ChooseQualified_ActSequence() and ChooseQualified_Reaction()
		/// <summary>
		///    Choose a qualified item in the given list based on Dragon values.
		/// </summary>
		/// <returns>
		///    The qualified item.
		/// </returns>
		/// <param name='p_items'>
		///    -List of items to be checked. Should be in the right format. 
		///    -For the format of each item, index p_condIndex should contain a hashtable.
		/// </param>
		/// <param name='p_condIndex'>
		///    -Index where the condition is located in the array
		/// </param>
		ArrayList ChooseQualifiedItem(ArrayList p_items, int p_condIndex){
			
			ArrayList listSatisfied = new ArrayList();
			ArrayList listDefaults  = new ArrayList();
			
			DLogItem("======== Checking valid [-?-]:========\n");  //xxx: Debug code.

			foreach(ArrayList itemData in p_items){
				
				DLogItem("=--- Loop [-?-] items\n");  //xxx: Debug code.
		
				if(itemData.Count < p_condIndex + 1) {
				
					DLogItem("----- [-?-] \""+ActDesc(itemData)+"\" has no requirements. Added to DEFAULT [-?-] list.\n"); //xxx: Debug Code
					
					listDefaults.Add(itemData);
					continue;
				}
				
				
				string strActDesc = ActDesc(itemData);                   //xxx: Debug code
				DLogItem("---Checking [-?-]: \"" + strActDesc + "\"\n"); //xxx: Debug code
				
				Hashtable reactConditions = itemData[p_condIndex] as Hashtable;
				
				if ( TestRequirements( reactConditions ) ) {
					listSatisfied.Add(itemData);
					DLogItem("-----ADDED SATISFIED [-?-]: " + strActDesc + "\n");  //xxx: Debug code
				}
			}
			
			//xxx: Start: Debug code
			if(D.On){
				DLogItem("********** List of SATISFIED [-?-] Data: Total Count: "+listSatisfied.Count+" **********\n");
				foreach( ArrayList item in listSatisfied ) Log("*** \""+ActDesc(item)+"\"\n");
				
				DLogItem("********** List of DEFAULT [-?-] Data: Total Count: "+listDefaults.Count+" **********\n");
				foreach( ArrayList item in listDefaults ) Log("*** \""+ActDesc(item)+"\"\n");
			}
			//xxx: End: 
			
			
			ArrayList chosenItem = null;
			
			//---Desc: Choose one in satisfied reaction data if any.
			if(listSatisfied.Count > 0){
				
				Log("---Choose one from SATISFIED items by Prio and Chance:\n");              //xxx: Debug code
				
				chosenItem = ChooseByPrioChance(listSatisfied, p_condIndex);
				
			}
			//---Desc: Choose one in default reactions
			else if(listDefaults.Count > 0){
				
				Log("---Choose one from items with NO-REQUIREMENTS:\n");   //xxx: Debug code
				
				chosenItem = ChooseRandomly(listDefaults);
				
			}
			
			DLogItem("::::::::Chosen [-?-]: \"" + ((chosenItem != null) ? ActDesc(chosenItem):"[NONE]")+ "\" ::::::\n");  //xxx: Debug code
			
			return chosenItem;
			
		}
		
		
		// check chances of sequence
		// +KJ:05232013 Notes: 
		//- at 'ReactionTable'
		//		* ReactionTable[Act_Action] 	= is an Array of ActionData
		//		* ReactionTable[Act_Action][0]	= is a Dictionary of ActionRequirements
		//		* ReactionTable[Act_Action][1]	= is an Array of Action Queue
		//			* ReactionTable[Act_Action][1][0] = is an Array of 'Default Action Queue'
		//			* ReactionTable[Act_Action][1][1] = is an Array of 'Conditional Action Queue' where in..
		//				* ReactionTable[Act_Action][1][1][0] = is the Array of 'Action Queue' and
		//				* ReactionTable[Act_Action][1][1][1] = is the Dictionary of 'Action Condition'
		//				* ReactionTable[Act_Action][1][1][2] = is the Array of 'Action Chances' [min,max] range
		//
		
#if DEBUG_ACTION_SEQUENCE
		
		public int OverrideReaction(string p_actionSequence){
			
			if ( !string.IsNullOrEmpty( p_actionSequence ) )
			{
				try
				{
					//Hashtable table = SequenceReader.GetInstance().ReadAniSequence( m_debugOverrideActionSequence );
					Hashtable table = SequenceReader.GetInstance().ReadAniSequence( p_actionSequence );
					ArrayList sequence = new ArrayList();
					sequence.Add( p_actionSequence );
					PlayActionSequence( sequence );
					return  (int)ETestActionOverride.TRUE;
				}
				catch (UnityException ex )
				{
					D.Warn( "Unable to play sequence: " + p_actionSequence + " -> " + ex.Message );
					return  (int)ETestActionOverride.FALSE;
				}
				//return  ETestActionOverride.TRUE;
			}
			if ( !string.IsNullOrEmpty( m_debugReaction ) )
			{
				ArrayList debugSequence = ActionSequence( m_debugReaction );
				
				if ( debugSequence == null )
				{
					D.Warn( "Sequence not found for reaction: " + m_debugReaction );
					return  (int)ETestActionOverride.FALSE;
				}
				
				ArrayList chosenItem = null;
				if ( m_debugActionSequenceIndex >= debugSequence.Count )
				{
					D.Warn( "Index is out of range - max : " + ( debugSequence.Count - 1 ) + " [use '-1' to use conditions]" );
					return  (int)ETestActionOverride.FALSE;
				}
				else if ( m_debugActionSequenceIndex < 0 )
				{
					chosenItem = ChooseQualifiedItem( debugSequence, 1 );  //NOTE: requirements will be located in index 1.
				}
				else
				{
					chosenItem = debugSequence[ m_debugActionSequenceIndex ] as ArrayList;
				}

				if ( chosenItem == null )
				{
					D.Warn( "No action sequence for reaction");
					return  (int)ETestActionOverride.FALSE;
				}
				
				PlayActionSequence( chosenItem[0] as ArrayList );
				return  (int)ETestActionOverride.TRUE;
			}
			
			return  (int)ETestActionOverride.CONTINUE;
		}
		//CCC: End: Editor mode code
#endif
		void LateUpdate ()
		{
#if DEBUG_EVENTS
			int[] evtCount = new int[3]{ m_timedEvents.Count, m_gestures.Count, m_events.Count };
			//Debug.Log(">> "+evtCount[0]+"|"+evtCount[1]+"|"+evtCount[2]+" <<\n");
#endif
			// when the game is paused. this should never be checked.
			if( !m_delegate ) return;
			
			if( m_delegate.bIsPaused ) return;
			
			// check timebased reactions
			bool timedIsTriggered 	= this.ProcessTimedReaction();
			bool gestureEvents		= false;
			bool events				= false;
			
			if( !timedIsTriggered )
				gestureEvents = this.ProcessReactions(m_gestures);
			
			if( !gestureEvents )
				events = this.ProcessReactions(m_events);
			
			// +KJ:08132013 Note:
			// - Poller should never be restarted on 'Non' TimeBased Event Triggers and be 'Paused' on all kinds of triggers
			// - On Gesture Events, Gelo's method for gesture particle responces should be called out there.
			if( timedIsTriggered || gestureEvents || events )
			{
#if DEBUG_EVENTS
				Debug.Log("Count: "+evtCount[0]+"|"+evtCount[1]+"|"+evtCount[2]+"\n");
				Debug.Log("-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-"+"\n");
				Debug.Log(
					"Count: "+m_timedEvents.Count+"|"+m_gestures.Count+"|"+m_events.Count+"\n"+
					"Triggered: "+timedIsTriggered+"|"+gestureEvents+"|"+events+"\n"
				);
#endif
				
				bSatisfiedGesture = true;
				
				if( !timedIsTriggered )
				{
					this.RestartEventTimer();
				}
				
				if( gestureEvents ) //&& StatsManager.Instance.DragonMode != StatsManager.MODE_IUC )
				{
					m_sprint9DemoCounter = 0;
					// Start Idle Timer after a gesture is validated
					this.StartIdleTimer();
				}
				
				this.PauseEventTimer();
			}
		}

		/** Reaction ********************************************/
		public bool GestureReaction ( string p_eventType, string p_event, GestureObject p_gesture )
		{
			if( p_eventType == "" || p_event == "" )
				return false;
			
#if UNITY_EDITOR
			ETestActionOverride retVal = (ETestActionOverride)OverrideReaction(m_debugOverrideActionSequence);
			if(retVal != ETestActionOverride.CONTINUE)
				return retVal == ETestActionOverride.TRUE;
#endif
			List<string> evts = new List<string>();
			evts.Add(p_eventType);
			evts.Add(p_event);
			
			// test & special case for FakeSwipe
			if( p_eventType == "Type_Touch"
				&& p_event == "Event_Hold_Fake_Swipe" 
			){
				List<List<string>> tempGestureEvt = new List<List<string>>();
				tempGestureEvt.Add(evts);
				if( this.ProcessReactions(tempGestureEvt) )
				{
					bSatisfiedGesture = true;
					this.GestureEnablingMethod(p_gesture, m_gestures);
				}
				return true;
			}
			
			m_gestures.Add(evts);
			m_triggeredGesture = p_gesture;
			
			return true;
		}
		
		public void DragonCallDone ()
		{
			this.RestartEventTimer();
			this.Reaction("Type_Roam", "Event_Dragon_Call");
		}
		
		public bool Reaction ( string p_eventType = "", string p_event = "", bool p_forceInterrupt = false )
		{
			Log("----------------- Reading Reactions from: [Event_Type:\""+p_eventType+"\"][\"Event:"+p_event+"\"] ------------------------\n");
			
			if( p_eventType == "" || p_event == "" ){
			
				return false;
			}
			
#if UNITY_EDITOR
			ETestActionOverride retVal = (ETestActionOverride)OverrideReaction(m_debugOverrideActionSequence);
			if(retVal != ETestActionOverride.CONTINUE){
				//return (bool)retVal;
				return retVal == ETestActionOverride.TRUE;
			}
#endif
			
			//D.Warn("REACTION "+p_eventType+" "+p_event+" i:"+p_forceInterrupt+" \n");
			//Log("REACTION "+p_eventType+" "+p_event+" i:"+p_forceInterrupt+" \n");
			
			List<string> evts = new List<string>();
			evts.Add(p_eventType);
			evts.Add(p_event);
			m_events.Add(evts);
			
			// +KJ:06032013 this must be removed. because the checking is now moved to LateUpdate
			return true;
		}
		
		public bool TimedReaction ( string p_eventType, string p_event )
		{
			if( p_eventType == "" || p_event == "" )
				return false;
			
			List<string> evts = new List<string>();
			evts.Add(p_eventType);
			evts.Add(p_event);
			m_timedEvents.Add(evts);
			
			return true;
		}
		
		/** +KJ:09232013
		 * 
		 * Support this types on TimedReactions, Reactions.
		 * so that.. Gesture responses would know if the chosen Act seq is valid for the display of Gesture resp.
		 * 
		enum EEventTriggerType {
			INVALID,
			FAIL,
			TIME,
			GESTURE,
			EVENT,
			OVER_USAGE,
			MAX
		}
		 *
		 **/
		
		private bool ProcessTimedReaction ()
		{
			if( m_timedEvents.Count <= 0 ) 
				return false;
			
			ArrayList events = new ArrayList();
			
			foreach( List<string> evts in m_timedEvents )
			{
				string evtType 	= evts[0] as string;
				string evt		= evts[1] as string;
				
				// +KJ:07052013 Temp Remove usage of DragonMode
				ArrayList eventWithReactions = EventReader.GetInstance().ReadEvent(evtType, evt, StatsManager.Instance.DragonMode);
				
				if( eventWithReactions != null )
				{
					events.Add(eventWithReactions);
					Log("----------------- Valid TimedReactions from: [Event_Type:\""+evtType+"\"][\"Event:"+evt+"\"][\"Mode:"+StatsManager.Instance.DragonMode+"\"] ------------------------\n");
				}
				else
				{
					Log("----------------- Invalid TimedReactions from: [Event_Type:\""+evtType+"\"][\"Event:"+evt+"\"][\"Mode:"+StatsManager.Instance.DragonMode+"\"] ------------------------\n");
				}
			}
			
			m_timedEvents.Clear();
			
		//--------------------------
		// Choose Event
		//..........................
			
			if( events.Count <= 0 ) 
				return false;
			
			ArrayList qualifiedEventWithReactions = ChooseQualifiedEvents(events);
			
			if( qualifiedEventWithReactions == null
				|| qualifiedEventWithReactions.Count <= 0 ) return false;
			
		//--------------------------
		// Choose Reaction
		//..........................
		
			if( qualifiedEventWithReactions == null ){
				Debug.Log("Dragon::Reaction ********* Invalid Reaction list object *********");
				return false;
			}
			
			this.debugVar_isAniSeq = false;                              //xxx: Debug code
			ArrayList chosenReact  = ChooseQualifiedItem(qualifiedEventWithReactions, 2);  //NOTE: requirements will be located in index 2
			
			if(chosenReact == null) return false;   //if there is no qualified reaction, just continue the current.
			
		//--------------------------
		// Track Usage Attempt for
		//   the Reaction
		//..........................
			
			//---Record the usage attempt of current chosenReact.
			StatsManager.Instance.TrackReaction(chosenReact);
		
		
		//--------------------------
		// Check Usage of Action
		//..........................
			
			//---Check the usage record of the current chosen reaction
			//  -Desc: Before loading the action-sequence in current chosen reaction
			//     check any satisfied reaction in actionTracking event.
			do {
				m_testCurActionUsed = chosenReact[1] as string;
				
				Log("======== m_testCurActionUsed = \""+m_testCurActionUsed+"\"\n");
				
				if(m_testCurActionUsed == null) break;
				
				//---Choose satisfied reaction in ActionTracking Event.
				ArrayList eventData = EventReader.GetInstance().ReadEvent("Type_UsageTracking", "Event_Action_Tracking", StatsManager.Instance.DragonMode);
				if(eventData == null) break;
				
				ArrayList usageReactList = eventData[0] as ArrayList;
				if(usageReactList == null) break;
				
				ArrayList newReact = ChooseQualifiedItem( usageReactList, 2 );
				if(newReact == null) break;
				
				//Log("======= !!! new Reaction due to overusage of reaction.\n");
				//Debug.LogError("======= !!! new Reaction:"+(newReact[0] as string)+" due to overusage of reaction ActUsed:"+(chosenReact[1] as string)+".\n");
				//Debug.Break();
				
				chosenReact = newReact;
				m_bwillRefuse = true;
			
			} while(false);
			
			m_testCurActionUsed = null;
			
			
		//--------------------------
		// Choose Action-Sequence
		//..........................
			
			string keyActSeq     = chosenReact[0] as string;
			ArrayList listActSeq = ActionSequence(keyActSeq);            //Get the list of Action Sequence from specified Reaction.
			
			Log("=-==-==--=-===--=-=-= Choosing Action-Sequence from: [Reaction:\""+keyActSeq+"\"] -=-==-==-=-=-=-=\n");
			//Debug.Log("=-==-==--=-===--=-=-= Choosing Action-Sequence from: [Reaction:\""+keyActSeq+"\"] -=-==-==-=-=-=-=\n");
			
			this.debugVar_isAniSeq = true;
			ArrayList aniSeq       = ChooseQualifiedItem(listActSeq, 1);  //NOTE: requirements will be located in index 1.
		
			
			if(aniSeq == null){
				
				//TODO: play the 1st item in Action-Sequence list.
				
				return false;  //xxx: Temp code
			}
			else {
				this.PauseEventTimer();
				PlayActionSequence(aniSeq[0] as ArrayList);
			}
			
			return true;
		}
		
		private bool ProcessReactions ( List<List<string>> p_events )
		{
			if( p_events.Count <= 0 ) return false;
			
			ArrayList events = new ArrayList();
			
			foreach( List<string> evts in p_events )
			{
				string evtType 	= evts[0] as string;
				string evt		= evts[1] as string;
				
				// +KJ:07052013 Temp Remove usage of DragonMode
				ArrayList eventWithReactions = EventReader.GetInstance().ReadEvent(evtType, evt, StatsManager.Instance.DragonMode);
				
				if( eventWithReactions != null )
				{
					events.Add(eventWithReactions);
					Log("----------------- Valid Reactions from: [Event_Type:\""+evtType+"\"][\"Event:"+evt+"\"][\"Mode:"+StatsManager.Instance.DragonMode+"\"] ------------------------\n");
				}
				else
				{
					Log("----------------- Invalid Reactions from: [Event_Type:\""+evtType+"\"][\"Event:"+evt+"\"][\"Mode:"+StatsManager.Instance.DragonMode+"\"] ------------------------\n");
				}
			}
			
			p_events.Clear();
			
		//--------------------------
		// Choose Event
		//..........................
			
			if( events.Count <= 0 ) return false;
			
			ArrayList qualifiedEventWithReactions = ChooseQualifiedEvents(events);
			
			if( qualifiedEventWithReactions == null
				|| qualifiedEventWithReactions.Count <= 0 ) return false;
			
		//--------------------------
		// Choose Reaction
		//..........................
		
			if( qualifiedEventWithReactions == null ){
#if DEBUG_CUEABLES
				Debug.LogError("Dragon::Reaction ********* Invalid Reaction list object *********");
#else
				Log("Dragon::Reaction ********* Invalid Reaction list object *********");
#endif
				return false;
			}
			
			this.debugVar_isAniSeq = false;                              //xxx: Debug code
			ArrayList chosenReact  = ChooseQualifiedItem(qualifiedEventWithReactions, 2);  //NOTE: requirements will be located in index 2
			
			if(chosenReact == null) return false;   //if there is no qualified reaction, just continue the current.
			
		
			// + LA 101013: Temporarily commented just for this sprint
//			
//		//--------------------------
//		// Track Usage Attempt for
//		//   the Reaction
//		//..........................
//			
//			//---Record the usage attempt of current chosenReact.
//			StatsManager.Instance.TrackReaction(chosenReact);
//		
//		
//		//--------------------------
//		// Check Usage of Action
//		//..........................
//			
//			//---Check the usage record of the current chosen reaction
//			//  -Desc: Before loading the action-sequence in current chosen reaction
//			//     check any satisfied reaction in actionTracking event.
//			do {
//				m_testCurActionUsed = chosenReact[1] as string;
//				
//				Log("======== m_testCurActionUsed = \""+m_testCurActionUsed+"\"\n");
//				
//				if(m_testCurActionUsed == null) break;
//				
//				//---Choose satisfied reaction in ActionTracking Event.
//				ArrayList eventData = EventReader.GetInstance().ReadEvent("Type_UsageTracking", "Event_Action_Tracking", StatsManager.Instance.DragonMode);
//				if(eventData == null) break;
//				
//				ArrayList usageReactList = eventData[0] as ArrayList;
//				if(usageReactList == null) break;
//				
//				ArrayList newReact = ChooseQualifiedItem( usageReactList, 2 );
//				if(newReact == null) break;
//				
//				Log("======= !!! new Reaction due to overusage of reaction.\n");
//				//Debug.LogError("======= !!! new Reaction:"+(newReact[0] as string)+" due to overusage of reaction ActUsed:"+(chosenReact[1] as string)+".\n");
//				//Debug.Break();
//				
//				chosenReact = newReact;
//				m_bwillRefuse=true;
//				
//			} while(false);
//			
//			m_testCurActionUsed = null;
			
			
		//--------------------------
		// Choose Action-Sequence
		//..........................
			
			string keyActSeq     = chosenReact[0] as string;
			ArrayList listActSeq = ActionSequence(keyActSeq);            //Get the list of Action Sequence from specified Reaction.
			
			Log("=-==-==--=-===--=-=-= Choosing Action-Sequence from: [Reaction:\""+keyActSeq+"\"] -=-==-==-=-=-=-=\n");
			//Debug.Log("=-==-==--=-===--=-=-= Choosing Action-Sequence from: [Reaction:\""+keyActSeq+"\"] -=-==-==-=-=-=-=\n");
			
			
			this.debugVar_isAniSeq = true;
			ArrayList aniSeq       = ChooseQualifiedItem(listActSeq, 1);  //NOTE: requirements will be located in index 1.
		
			
			if(aniSeq == null){
				
				//TODO: play the 1st item in Action-Sequence list.
				
				return false;  //xxx: Temp code
				
			}
			else {
				
				this.GestureEnablingMethod( m_triggeredGesture, p_events );
				this.PlayActionSequence( aniSeq[0] as ArrayList );
			}
			
			return true;
		}
		
		/** Play Action Sequence ********************************/
		// +KJ:10082013 NOTE: This is for debug purposes only. 
		public void PlayActionSequence ( string p_actionSequence )
		{
			ArrayList actSeq = new ArrayList();
					  actSeq.Add(p_actionSequence);
			this.PlayActionSequence( actSeq );
		}
		
		protected void PlayActionSequence ( ArrayList p_actionSequence )
		{
			// check here if cueueble or action is uninteractibe.
			if( !this.IsInteractible() )
			{
				if( m_bEventIsCueable )
				{
					// Sanity check..
					//   1. Should not Add ActionSequence that is currently playing
					//	 2. Should not Add ActionSequence that is already in cue
					foreach( string action in p_actionSequence )
					{
						bool containsAction = false;
						
						foreach( string cuedAction in m_cuedActions )
						{
							if( action.Equals(cuedAction) )
								containsAction = true;
						}
						
						if( !containsAction ) { m_cuedActions.Add(action); }
						
#if DEBUG_CUEABLES
						if( containsAction ) Debug.LogError("Dragon::PlayActionSequence Dragon is not Interactible. adding "+action+" to cue.");
						else  				 Debug.LogError("Dragon::PlayActionSequence Dragon is not Interactible and already contains cueable "+action+" action.");
#else
						if( containsAction ) Log("Dragon::PlayActionSequence Dragon is not Interactible. adding "+action+" to cue.");
						else  				 Log("Dragon::PlayActionSequence Dragon is not Interactible and already contains cueable "+action+" action.");
#endif
					}
					return;
				}
				
#if DEBUG_CUEABLES
				Debug.LogError("Dragon::PlayActionSequence Cannot Play this Action because Action is not Interactibe.");
				//Debug.Break();
#else
				//Log("Dragon::PlayActionSequence Cannot Play this Action:"+MiniJSON.jsonEncode(p_actionSequence)+" because Action is not Interactibe.");
				Debug.LogWarning("Dragon::PlayActionSequence Cannot Play this Action:"+MiniJSON.jsonEncode(p_actionSequence)+" because Action is not Interactibe. \n");
#endif
				return;
			}
			
			// clear actions
			DragonAnimationQueue.getInstance().ClearAll();
			SoundManager.Instance.StopAnimationSound( PettingMain.Instance.HeadSoundContainer );
			SoundManager.Instance.StopAnimationSound( PettingMain.Instance.BodySoundContainer );
			SoundManager.Instance.StopAnimationSound( PettingMain.Instance.HeadAndBodySoundContainer );
			
			foreach( string action in p_actionSequence )
			{
				// Body Animations
				ArrayList bodyActionSequence = SequenceReader.GetInstance().ReadBodySequence(action);
				
				Log("----------- Playing Reaction:"+action+" -----------");
				
				//LogWarning(">>>>>>> Checking action: \""+action+"\"\n");
				if( bodyActionSequence != null )
				{
					// +KJ:06132013 Shared Parameter. this supports the same random value that a cue is sharing
					
					foreach( object actionData in bodyActionSequence )
					{
						ArrayList actionDataList = actionData as ArrayList;
						BodyData bodyData = new BodyData(); 
						
						bodyData.Action				= action;
						bodyData.Start 				= Utility.ParseToInt(actionDataList[0]);
						bodyData.ActionKey 			= actionDataList[1].ToString();
						bodyData.Duration			= float.Parse(actionDataList[2].ToString());
							
						if( actionDataList.Count > 3 )
						{
							bodyData.Param = actionDataList[3] as Hashtable;
						}
						
						bodyData.ExtractHashtableValues( bodyData.Param );
						bodyData.EventTrigger 	= this.FormulateEventTriggers( bodyData.Param );
						
						DragonAnimationQueue.getInstance().AddBodyCue( bodyData.GenerateHashtable() );
					}
				}
				
				// Head Animations
				ArrayList headActionSequence = SequenceReader.GetInstance().ReadHeadSequence(action);
				
				if( headActionSequence != null )
				{
					foreach( object actionData in headActionSequence )
					{
						ArrayList actionDataList = actionData as ArrayList;
						HeadData headData = new HeadData();
						
						headData.Action				= action;
						headData.Start 				= Utility.ParseToInt(actionDataList[0]);
						headData.ActionKey	 		= actionDataList[1].ToString();
						headData.Duration 			= float.Parse(actionDataList[2].ToString());
						
						if( actionDataList.Count > 3 )
						{
							headData.Param = actionDataList[3] as Hashtable;
						}
						
						headData.ExtractHashtableValues( headData.Param );
						headData.EventTrigger 		= this.FormulateEventTriggers( headData.Param );
						
						DragonAnimationQueue.getInstance().AddHeadCue( headData.GenerateHashtable() ) ;
					}
				}
				
				// Update Queue
				ArrayList updateActionQueue = SequenceReader.GetInstance().ReadUpdate(action);
				
				if( updateActionQueue != null )
				{
					foreach( object actionData in updateActionQueue )
					{
						ArrayList actionDataList = actionData as ArrayList;
						UpdateData updateData = new UpdateData();
						
						updateData.Action			= action;
						updateData.Start			= Utility.ParseToInt(actionDataList[0]);
						updateData.ActionKey 		= actionDataList[1].ToString();
						updateData.Duration 		= float.Parse(actionDataList[2].ToString());
						
						if( actionDataList.Count > 3 )
						{
                           updateData.Param = actionDataList[3] as Hashtable;
						}
						
						updateData.EventTrigger 	= this.FormulateEventTriggers( updateData.Param );
						
						DragonAnimationQueue.getInstance().AddUpdateCue( updateData.GenerateHashtable() );
					}
				}
				
				// Transition Queue
				ArrayList transitionQueue = SequenceReader.GetInstance().ReadTransform(action);
				
				if( transitionQueue != null )
				{
					foreach( object actionData in transitionQueue )
					{
						ArrayList actionDataList = actionData as ArrayList;
						TransformData transformData = new TransformData();
						
						transformData.Action		= action;
						transformData.Start 		= Utility.ParseToInt(actionDataList[0]);
						transformData.ActionKey 	= actionDataList[1].ToString();
						transformData.Duration 		= float.Parse(actionDataList[2].ToString());
										
						if( actionDataList.Count > 3 )
						{
							transformData.Param	= actionDataList[3] as Hashtable;
						}
						
						transformData.EventTrigger 	= this.FormulateEventTriggers( transformData.Param );
						
						DragonAnimationQueue.getInstance().AddTransformCue( transformData.GenerateHashtable() );
					}
				}
				
				ArrayList cameraQueue = SequenceReader.GetInstance().ReadCamera(action);
				
				if( cameraQueue != null )
				{
					foreach( object actionData in cameraQueue )
					{
						ArrayList actionDataList = actionData as ArrayList;
						CameraData cameraData = new CameraData();
						
						cameraData.Action			= action;
						cameraData.Start 			= Utility.ParseToInt(actionDataList[0]);
						cameraData.ActionKey 		= actionDataList[1].ToString();
						cameraData.Duration 		= float.Parse(actionDataList[2].ToString());
						
						if( actionDataList.Count > 3 )
						{
                            cameraData.Param = actionDataList[3] as Hashtable;
						}
						
						cameraData.EventTrigger = this.FormulateEventTriggers( cameraData.Param );
						
						DragonAnimationQueue.getInstance().AddCameraCue( cameraData.GenerateHashtable() );
					}
				}
				
				ArrayList eventQueue = SequenceReader.GetInstance().ReadEventTriggers(action);
				
				if( eventQueue != null )
				{
					foreach( object actionData in eventQueue )
					{
						ArrayList actionDataList = actionData as ArrayList;
						EventData eventData = new EventData();
						
						eventData.Action			= action;
						eventData.Start 			= Utility.ParseToInt(actionDataList[0]);
						eventData.ActionKey 		= actionDataList[1].ToString();
						eventData.Duration 			= float.Parse(actionDataList[2].ToString());
						
						if( actionDataList.Count > 3 )
						{
							eventData.Param = actionDataList[3] as Hashtable;
						}
						
						eventData.EventTrigger = this.FormulateEventTriggers( eventData.Param );
						
						DragonAnimationQueue.getInstance().AddEventCue( eventData.GenerateHashtable() );
					}
				}
				
				// + LA 072613
				ArrayList soundQueue = SequenceReader.GetInstance().ReadSounds(action);
				
				if( soundQueue != null )
				{
					foreach( object actionData in soundQueue )
					{
						ArrayList actionDataList = actionData as ArrayList;
						SoundData soundData = new SoundData();
						
						soundData.Action			= action;
						soundData.Start 			= Utility.ParseToInt(actionDataList[0]);
						soundData.ActionKey 		= actionDataList[1].ToString();
						soundData.Duration			= float.Parse(actionDataList[2].ToString());
						
						if( actionDataList.Count > 3 )
						{
							soundData.Param = actionDataList[3] as Hashtable;
						}
						
						soundData.EventTrigger = this.FormulateEventTriggers( soundData.Param );
						
						DragonAnimationQueue.getInstance().AddSoundCue( soundData.GenerateHashtable() );
					}
				}
				// - LA
			}
			
			DragonAnimationQueue.getInstance().PlayQueuedAnimations();
		}
		
		/** Formulating Event Triggers **************************/
		private Hashtable FormulateEventTriggers(Hashtable p_params)
		{
			if( p_params == null )
			{
				return null;
			}
			
			Hashtable dataHolder = null;
			
			if( p_params.ContainsKey(DragonAnimationQueue.KEY_TRIGGER) == false )
			{
				//if( p_params != null )
				//	D.Warn("Dragon::FormulateEventTriggers InvalidTrigger of a targeted param:"+MiniJSON.jsonEncode(p_params));
				//else
					D.Warn("Dragon::FormulateEventTriggers InvalidTrigger of a targeted param:"+p_params);
				return null;
			}
			
			dataHolder = p_params[DragonAnimationQueue.KEY_TRIGGER] as Hashtable;
			
			return dataHolder;
		}
		
		/** Sequence of Actions of Dragon ***********************/
		public void Actions(string p_reaction)
		{
			// check chances of sequence
		}
		
		/** Action sequence on event reaction *******************/
		protected ArrayList ActionSequence(string p_reaction)
		{
			ArrayList actions = ReactionReader.GetInstance().ReadAniSequence(p_reaction);
			return actions;
		}
		
		/** Utils. Range Checking *******************************
		 * Note: Parameter of this is an array with 2 length. [0]:min & [1]:max
		 **/
		protected bool InRange(ArrayList p_range, int p_toCompare)
		{
			int min = int.Parse(p_range[0].ToString());
			int max = int.Parse(p_range[1].ToString());
			
			if( p_toCompare >= min && p_toCompare <= max )
				return true;
			
			return false;
		}
		
		protected bool InRangeFloat(ArrayList p_range, float p_toCompare)
		{
			float min = float.Parse(p_range[0].ToString());
			float max = float.Parse(p_range[1].ToString());
			
			if( p_toCompare >= min && p_toCompare <= max )
				return true;
			
			return false;
		}
		
		//DISABLE ALL COLLIDERS AND ENABLE COLLIDER ON THIS PARAMETER
		public void EnableColliderOn(string p_colliderTag)
		{
			//D.Error("Dragon::EnableColliderOn Collider:"+p_colliderTag);
			
			//DISABLE ThIS COLLIDERS
			GameObject HeadCollider = GameObject.FindGameObjectWithTag("Head").gameObject;
			GameObject BodyCollider = GameObject.FindGameObjectWithTag("Body").gameObject;
			
			Dragon.EnableBoxCollider(HeadCollider, false);
			Dragon.EnableBoxCollider(BodyCollider, false);
			
			if( p_colliderTag.Length > 0 )
			{
				if( GameObject.FindGameObjectWithTag(p_colliderTag) )
				{
					GameObject SetThisColliderOn = GameObject.FindGameObjectWithTag(p_colliderTag).gameObject;
					Dragon.EnableBoxCollider(SetThisColliderOn, true);
				}
				else
				{
					Log("Dragon::EnableColliderOn colliderTag:"+p_colliderTag+" is not an Existing tag of a GameObject.");
				}
			}
			else
			{
				Log("Dragon::EnableColliderOn Empty p_colliderTag.. turning off all the colliders.");
			}
		}
		
		public static void EnableBoxCollider(
			GameObject p_gameObject,
			bool p_bIsEnabled
		){
			if( p_gameObject.collider )
			{
				//LogError(">>>> A ColliderOf:"+p_gameObject.name+" Enabled:"+p_bIsEnabled);
				p_gameObject.collider.enabled = p_bIsEnabled;
			}
			else
			{
				BoxCollider[] bodyCollidersB = p_gameObject.GetComponents<BoxCollider>();
				foreach( BoxCollider bc in bodyCollidersB )
				{
					//LogError(">>>> B ColliderOf:"+p_gameObject.name+" Enabled:"+p_bIsEnabled);
					bc.enabled = p_bIsEnabled;
				}
				
				SphereCollider[] bodyCollidersS = p_gameObject.GetComponents<SphereCollider>();
				foreach( SphereCollider sc in bodyCollidersS )
				{
					//LogError(">>>> C ColliderOf:"+p_gameObject.name+" Enabled:"+p_bIsEnabled);
					sc.enabled = p_bIsEnabled;
				}
			}
		}
		
		// utils
		private void ShowParticleOnPos(GameObject p_particle, Vector2 p_pos)
		{
			p_particle.GetComponent<FollowComponent>().SeekPoint(p_pos);
		}
		
		public void ClearAllEvents ()
		{
			this.PrintAndClear(m_timedEvents, "-TimeEvents-");
			this.PrintAndClear(m_gestures, "-GestureEvents-");
			this.PrintAndClear(m_timedEvents, "-Events-");
			m_cuedActions.Clear();
		}
		
		private void PrintAndClear ( List<List<string>> p_lists, string p_name )
		{
			if ( p_lists == null ) { return; }
			
			foreach ( List<string> ts in p_lists ) {
				foreach ( string tE in ts ) {
					Debug.LogError("PrintClear:"+p_name+" Event:"+tE+" \n");
				}
			}
			
			p_lists.Clear();
		}
		
		public void Destroy ()
		{
			this.StopAllCoroutines();
		}
		
	}
	
}//namespace DragonCatcher.CarePlay.TouchPlay
