//#define DEBUG_HEAD_EYE_LOOK

using UnityEngine;
using System;
using System.Linq;
using System.Collections;
using DragonCatcher.CarePlay.Petting;
using DragonCatcher.AnimationTool;
using DragonCatcher.Common;
using DragonCatcher.Common.StateMachine;
using DragonCatcher.CarePlay.TouchPlay;

namespace DragonCatcher.CarePlay.Petting
{
	public class DragonScript : MonoSingleton<DragonScript> 
	{	
		// Dragon Rig Animation Controllers
		private HeadLookController 			m_headLookController;
		private EyeLookController			m_eyeLookController;
		private PettingMain 				m_delegate;
		
		// Dragon animation controller / Queue
		DragonAnimation 					m_dragonAnim;
		DragonAnimationQueue 				m_animQueueController;
		
		// head look follow vars
		private Vector3 m_touchLookPos			= Vector3.zero;
		private bool m_bIsHeadLookActive		= false;
		private bool m_bIsEyeLookActive			= false;
		
#if DEBUG_HEAD_EYE_LOOK
		[SerializeField]
#endif
		private GameObject m_headLookAtObject	= null;
#if DEBUG_HEAD_EYE_LOOK
		[SerializeField]
#endif
		private GameObject m_eyeLookAtObject	= null;
		
		private PET_NF_AnimController m_petAnimController;
		private float m_lerpSpeed				= 0.0f;
		
		private const float m_eyeLerpSpeedAdjstmnt	= 1.0f;
		private const float m_headLerpSpdAdjstmnt	= 0.08f;
		private bool m_bIsComeDownAnimDone			= false;
		private GameObject m_touchMomentGlow		= null;
		
		// Reference
		private Transform SquirtPosRef;
		
		public void Initialize ()
		{
			Debug.Log("DragonScript::Initialize");
			
			//Controllers
			m_headLookController 	= GameObject.Find( "CAREPLAY_NF_Controller" ).gameObject.GetComponent<HeadLookController>();
			m_eyeLookController		= GameObject.Find( "CAREPLAY_NF_Controller" ).gameObject.GetComponent<EyeLookController>();
			m_dragonAnim 			= GameObject.Find( "toothless_Rig_Body" ).gameObject.GetComponent<DragonAnimation>();

			m_animQueueController = DragonAnimationQueue.getInstance();
			m_animQueueController.SetDAnim(m_delegate.DragonAnimation);
			
			GameObject headPos = GameObject.Find("joint_Head");
			
			GameObject squirtObject = GameObject.Instantiate((GameObject)Resources.Load("Prefabs/Petting/DragonColliders/SquirtRef")) as GameObject;
			SquirtPosRef = squirtObject.transform;
			SquirtPosRef.name="SquirtPosRef";
			SquirtPosRef.parent= headPos.transform;
			SquirtPosRef.localPosition=new Vector3(0.02617414f,-0.1928091f,0.9756898f);
			
			m_touchMomentGlow = Instantiate( Resources.Load( "Prefabs/Petting/TouchMomentParticleGlow" ) ) as GameObject;
			m_touchMomentGlow.transform.parent = headPos.transform;
			m_touchMomentGlow.transform.localPosition = new Vector3( -0.008107394f, 0.06249831f, -0.4102947f );
			m_touchMomentGlow.transform.localEulerAngles = new Vector3( 355.6342f, 359.9758f, -1.849756f );
			m_touchMomentGlow.SetActive( false );
			
			m_petAnimController = CarePlayNFController.GetPetAnimController();
		}
		
		public void Start ()
		{
			Debug.Log("DragonScript::Start");
		}
		
		/*
		public void OnBackPedalComplete()
		{
			Debug.LogError("back_pedal_complete");
			Debug.Break();
			m_petAnimController.SetBodyAnimStateTo((ECP_BodyAnim)0); // IdleLookAround
		}
		//*/
		
		void LateUpdate ()
		{
			if( m_delegate == null )
			{
				D.Warn("Warning! DragonScript::Update delegate must set first!");
				return;
			}
			
			float lerpSpeed = 0.05f;
			// lerp speed when iuc
			if( StatsManager.Instance.DragonMode == StatsManager.MODE_IUC )
				lerpSpeed = 1.0f;
			
			m_lerpSpeed += (lerpSpeed - m_lerpSpeed) * m_headLerpSpdAdjstmnt;
			
#if DEBUG_HEAD_EYE_LOOK
			if( m_headLookAtObject != null )
				m_headLookController.SetEnableControllerAndLerpWeight(true);
			
			if( m_eyeLookAtObject != null )
				m_eyeLookController.SetEnableControllerAndLerpEyeLookTarget(true);
#endif
			
			// + PC 060413: Rub Look
			if(
				StatsManager.Instance.DragonState == "Idle_Petting" 
				&& (
					StatsManager.Instance.DragonPose == "Pet_Head" 
					|| StatsManager.Instance.DragonPose == "Pet_LeftBody"
					|| StatsManager.Instance.DragonPose == "Pet_RightBody"
					// +KJ:08142013 BackBody should never be include here
					//|| StatsManager.Instance.DragonPose == "Pet_BackBody"
				)
			){
				Vector2 touchPos = new Vector2(Screen.width, Screen.height) * 0.5f;		
				
				if(Input.GetMouseButton(0))touchPos = Input.mousePosition;
	
				Vector2 normTouch = touchPos;
				normTouch.x /= Screen.width;
				normTouch.y /= Screen.height;			
				
				normTouch.x -= 0.5f;
				normTouch.y -= 0.5f;						
				
				normTouch =  Quaternion.Euler(0, 0, 45) * (Vector3)normTouch;
				
				Vector2 vRubLook = touchPos;
				
					 if(normTouch.x > 0 && normTouch.y > 0)vRubLook = new Vector2(-1, -1);
				else if(normTouch.x > 0 && normTouch.y < 0)vRubLook = new Vector2(-1, 1);
				else if(normTouch.x < 0 && normTouch.y > 0)vRubLook = new Vector2(1, -1);
				else if(normTouch.x < 0 && normTouch.y < 0)vRubLook = new Vector2(1, 1);
				else vRubLook = Vector2.zero;
				
				vRubLook = Quaternion.Euler(0, 0, -45) * (Vector3)vRubLook;
				
				vRubLook.x *= Screen.width * 0.5f;
				vRubLook.y *= Screen.height * 0.5f;			
				
				vRubLook.x += Screen.width * 0.5f;
				vRubLook.y += Screen.height * 0.5f;			
				
				Vector3 targetPos = Camera.mainCamera.ScreenPointToRay(vRubLook).origin;			
				Vector3 camPos = Camera.mainCamera.transform.position;	
				Vector3 vLook = (targetPos - camPos).normalized;
				Vector3 lookPos = camPos + vLook * 2f;
				m_headLookController.HeadLookTransform.position = Vector3.Lerp(m_headLookController.HeadLookTransform.position, lookPos, 0.1f);
			}
			// - PC			
			
			// +KJ:08062013 Touch, Drag Position
			if( m_bIsHeadLookActive || m_bIsEyeLookActive )
			{
				Vector2 touchPos = new Vector2(Screen.width, Screen.height) * 0.5f;		
				
				if(Input.GetMouseButton(0))touchPos = Input.mousePosition;
	
				Vector3 targetPos 	= Camera.mainCamera.ScreenPointToRay(touchPos).origin;			
				Vector3 camPos	 	= Camera.mainCamera.transform.position;	
				Vector3 vLook 		= (targetPos - camPos).normalized;
				m_touchLookPos 		= camPos + vLook * 2f;
			}
			
			// KJ:08062013 - Current Animation Check for water splash 
			if ( StatsManager.Instance.currentReaction == "Dragon_Splash_Upward")
			{
									
				if( m_petAnimController.IsCurAnimStateOnSub (ERigType.Body, "Thirsty", "OutcomeThirsty") )
				{
					float currentAnimTime = m_petAnimController.GetCurAnimNormTime(ERigType.Body);
					if ( currentAnimTime > 0.15f && currentAnimTime < 0.20f ) {
						Vector3 pos = SquirtPosRef.position;
						WaterCueParticles.Instance.Squirt(pos, 0.5f);	
						
					}
					else if ( currentAnimTime > 0.05f && currentAnimTime < 0.10f ) {
					
						OutBucket.DestroyItem();
						
					}
				}
			} 
			else if ( StatsManager.Instance.currentReaction == "Dragon_Splash_Front"
					  || StatsManager.Instance.currentReaction == "Dragon_Splash_Front_To_Fetching"
					  || StatsManager.Instance.currentReaction == "Dragon_Splash_Front_To_Feeding"
					  || StatsManager.Instance.currentReaction == "Dragon_Splash_To_Dragon_Salve"
			){
				if( m_petAnimController.IsCurAnimStateOnSub(ERigType.Body, "Thirsty", "OutcomeThirstySpray") )
				{
					float currentAnimTime = m_petAnimController.GetCurAnimNormTime(ERigType.Body);
					if ( currentAnimTime > 0.28f && currentAnimTime < 0.33f ) {
						Vector3 pos = SquirtPosRef.position;
						WaterCueParticles.Instance.Splat(pos, 0.5f);
						this.BucketSplash();
						
					}
					else if ( currentAnimTime > 0.35f && currentAnimTime < 0.40f ) {
					
						this.BucketSplash();
					}
				}
			} 
			else if ( StatsManager.Instance.currentReaction == "Dragon_Outcome_Blast" ) {
				
				if( m_petAnimController.IsCurAnimStateOnSub (ERigType.Body, "Thirsty", "OutcomeBlast") )
				{
					float currentAnimTime = m_petAnimController.GetCurAnimNormTime(ERigType.Body);
					if ( currentAnimTime > 0.5f && currentAnimTime < 0.53f ) {
						
						GameObject fireball = GameObject.Find("NFFireball");
						if(fireball != null)	return;
						string fireballPath = "Prefabs/TargetTraining/Attacks/NightFuryFlame";
						Vector3 pos 		= SquirtPosRef.position;
						pos.y			   -= 0.4f;
						fireball 			= Instantiate(Resources.Load(fireballPath), pos, Quaternion.identity) as GameObject;
						fireball.name 		= "NFFireball";
						Vector3 targetPos 	= new Vector3(-0.84f, -0.14f, -0.24f);
						this.BucketBlast(0.3f);
						iTween.MoveTo(fireball, iTween.Hash("position", targetPos, "time", 0.17f));
						Destroy(fireball, 0.18f);
					}
				}
			}
			
			if( m_petAnimController.IsCurAnimStateOnSub( ERigType.Body, "Act1", "Act1_05_ComeDown_RandomSwipe" ) ||
				m_petAnimController.IsCurAnimStateOnSub( ERigType.Body, "Act1", "Act1_05_ComeDown_02" ) )
			{
				float currentAnimTime = m_petAnimController.GetCurAnimNormTime(ERigType.Body);
				
				if( currentAnimTime >= 0.98f && m_bIsComeDownAnimDone == false)
				{
					PettingMain.Instance.ShowIntroTutorial();
					m_bIsComeDownAnimDone = true;
				}
			}
			
			if( m_petAnimController.IsCurAnimStateOnSub( ERigType.Body, "Thirsty", "InBetweenThirsty" ) )
			{
				float currentAnimTime = m_petAnimController.GetCurAnimNormTime(ERigType.Body);
				
				if( currentAnimTime >= 0.2f )
				{
					OutBucket[] buckets = GameObject.FindObjectsOfType(typeof(OutBucket)) as OutBucket[];
					foreach( OutBucket bucket in buckets )
					{
						bucket.DrainWaterOnBucket(15.0f);
					}

				}
			}
			
			if( m_petAnimController.IsCurAnimStateOnSub( ERigType.Body, "TouchMoment", "TouchMomentToMidground" ) )
			{
				float currentAnimTime = m_petAnimController.GetCurAnimNormTime( ERigType.Body );
				
				if( currentAnimTime >= 0.5f && currentAnimTime <= 0.7f )
				{
					m_touchMomentGlow.SetActive( true );
					PettingMain.Instance.DragonStateMachine.SetIsInteractible ( false );

				}
				else if( currentAnimTime >= 0.9f )
				{
					m_touchMomentGlow.SetActive( false );
				}
			}
			
			// +KJ:08062013 Head Follow
			if( m_bIsHeadLookActive )
				m_headLookController.HeadLookTransform.position = Vector3.Lerp(m_headLookController.HeadLookTransform.position, m_touchLookPos, 0.1f);
			
			// +KJ:08062013 Eye Follow
			if( m_bIsEyeLookActive )
				m_eyeLookController.EyeLookTransform.position = Vector3.Lerp(m_eyeLookController.EyeLookTransform.position, m_touchLookPos, 1.0f);
			
#if DEBUG_HEAD_EYE_LOOK
			Debug.LogError("---------------- \n");
			Debug.LogError("FingerLookIsActive Head:"+m_bIsHeadLookActive+" Eye:"+m_bIsEyeLookActive+" POS:"+m_touchLookPos+"\n");
#endif		
			
			// +KJ:08062013 Head Look
			if( m_headLookAtObject != null )
			{
				Vector3 targetPos = m_headLookAtObject.transform.position;
				Vector3 vLook = (targetPos - m_headLookController.HeadLookTransform.position);
				Vector3 lookPos = m_headLookController.HeadLookTransform.position + (vLook * 0.90f);
				//m_headLookController.HeadLookTransform.position = Vector3.Lerp(m_headLookController.HeadLookTransform.position, lookPos, 0.05f);
				m_headLookController.HeadLookTransform.position = Vector3.Lerp(m_headLookController.HeadLookTransform.position, lookPos, m_lerpSpeed);
#if DEBUG_HEAD_EYE_LOOK
				Debug.LogError("HeadLookAtObject "+m_headLookAtObject.name+" POS:"+targetPos+" _POS:"+m_headLookController.HeadLookTransform.position+"\n");
			}
			else
			{
				Debug.LogError("HeadLookAtObject False"+"\n");
#endif
			}
			
			// +KJ:08062013 Eye Look
			if( m_eyeLookAtObject != null )
			{
				Vector3 targetPos = m_eyeLookAtObject.transform.position;
				Vector3 vLook = (targetPos - m_eyeLookController.EyeLookTransform.position);
				Vector3 lookPos =  m_eyeLookController.EyeLookTransform.position + (vLook * 0.90f);
				//m_eyeLookController.EyeLookTransform.position = Vector3.Lerp(m_eyeLookController.EyeLookTransform.position, lookPos, 0.05f);
				m_eyeLookController.EyeLookTransform.position = Vector3.Lerp(m_eyeLookController.EyeLookTransform.position, lookPos, m_eyeLerpSpeedAdjstmnt);
#if DEBUG_HEAD_EYE_LOOK
				Debug.LogError("EyeLookAtObject "+m_eyeLookAtObject.name+" POS:"+targetPos+" _POS:"+m_eyeLookController.EyeLookTransform.position+"\n");
			}
			else
			{
				Debug.LogError("EyeLookAtObject False"+"\n");
#endif
			}

#if DEBUG_HEAD_EYE_LOOK
			Debug.LogError("~~~~~~~~~~~~~~~~"+"\n");
#endif
		}
		
		public void AllowFingerFollow ( bool p_b, bool p_bIsHead )
		{
			if( p_bIsHead )
				m_headLookController.SetEnableControllerAndLerpWeight(p_b);	
			else
				m_eyeLookController.SetEnableControllerAndLerpEyeLookTarget(p_b);
		}
		
		public void LookAtFingerDrag ( bool p_fingerPos, bool p_bIsHead )
 		{
			if( p_bIsHead )
				m_bIsHeadLookActive = p_fingerPos;
			else
				m_bIsEyeLookActive = p_fingerPos;
 		}
		
		public void LookAtObject(string p_tag, bool p_bIsHead )
		{
			// reset lerp speed
			m_lerpSpeed = 0.0f;
			
			if( p_tag.Length < 1 )
			{
				if( p_bIsHead )
				{
					StatsManager.Instance.headLook = "";
					m_headLookAtObject = null;
					m_headLookController.SetEnableControllerAndLerpWeight(false);
					this.DisableLookAt(p_bIsHead);
				}
				else
				{
					StatsManager.Instance.eyeLook = "";
					m_eyeLookAtObject = null;
					m_eyeLookController.SetEnableControllerAndLerpEyeLookTarget(false);
					this.DisableLookAt(p_bIsHead);
				}
				return;
			}
			
			if( p_bIsHead )
				m_headLookAtObject = GameObject.FindGameObjectWithTag(p_tag); 
			else
				m_eyeLookAtObject  = GameObject.FindGameObjectWithTag(p_tag); 
			
			if( !m_headLookAtObject )
			{
				m_headLookAtObject = null;
				m_headLookController.SetEnableControllerAndLerpWeight(false);
			}
			else
			{
				StatsManager.Instance.headLook = p_tag;
				m_headLookController.SetEnableControllerAndLerpWeight(true);
			}
			
			if( !m_eyeLookAtObject )
			{
				m_eyeLookAtObject = null;
				m_eyeLookController.SetEnableControllerAndLerpEyeLookTarget(false);
			}
			else
			{
				StatsManager.Instance.eyeLook = p_tag;
				m_eyeLookController.SetEnableControllerAndLerpEyeLookTarget(true);
			}
		}
		
		public void LookAtPos ( Vector3 p_pos, bool p_bIsHead )
		{
			if( p_bIsHead )
				m_headLookController.HeadLookTransform.position = p_pos;
			else
				m_eyeLookController.EyeLookTransform.position = p_pos;
		}
		
		public void DisableLookAt ( bool p_bIsHead )
		{
			if( p_bIsHead )
				m_bIsHeadLookActive = false;
			else
				m_bIsEyeLookActive = false;
			
			//this.LookAtObject("", p_bIsHead);
		}
 		
//		public void DisableLookAt ( Controller p_controller, bool p_bIsOn )
//		{
//			if( p_controller == Controller.Head )
//				m_bIsHeadLookActive = p_bIsOn;
//			else if( p_controller == Controller.Eye )
//				m_bIsEyeLookActive = p_bIsOn;
//		}
		
		public void OpenMouth ( float p_wideness )
		{
			m_petAnimController.SetMouthWideness(p_wideness);
		}
		
		public void BucketSplash()
		{
			OutBucket[] buckets = GameObject.FindObjectsOfType(typeof(OutBucket)) as OutBucket[];
			foreach( OutBucket bucket in buckets )
			{
				bucket.SplashOnBucket();
			}
		}
		
		public void BucketBlast(float p_time)
		{
			OutBucket[] buckets = GameObject.FindObjectsOfType(typeof(OutBucket)) as OutBucket[];
			foreach( OutBucket bucket in buckets )
			{
				bucket.BlastOnBucket(p_time);
			}
		}
		
		public void SetPettingDelegate ( PettingMain p_delegate )
		{
			m_delegate = p_delegate;
		}
	}
}
