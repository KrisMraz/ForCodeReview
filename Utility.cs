//#define DEBUG_GUI_UTILS

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using DragonCatcher.CarePlay.TouchPlay;

public static class Utility  
{
	/*
	 * +KJ 02/13/13 this is used to call a certain function after the animation is done
	 * 
	 * */
	public delegate void AnimationCallBack ();
	/*
	public static void AnimationDone( UIAnimation p_ani, AnimationCallBack p_callback)
	{
    	p_ani.onComplete = () => p_callback();
	}	
	*/
	
	public static bool ReachabilityCheck ()
	{
		/*
		if( iPhoneSettings.internetReachability  == iPhoneNetworkReachability.NotReachable ) 
            return false;
       	else
           return true;
            //*/
		
		return true;
	}
	
	public static Texture2D ConvertBytesToTexture2d ( byte[] p_imageBytes )
	{
		Texture2D tex = new Texture2D(1,1);
				  tex.LoadImage( p_imageBytes );
		return tex;
	}
	
	// +KJ:04102013 random utility
	public static int seed = 0;
	public static System.Random m_randomWithSeed = new System.Random(0);
	public static void SetSeed ( int p_seed )
	{
		seed = p_seed;
		m_randomWithSeed = new System.Random(p_seed);
	}
	
	public static int RANDOM_INT_SEED ( int p_max )
	{
		if ( p_max < 0 ) {
			D.Warn("Utility::RANDOM_INT Warning!! max must be > 0!");
			p_max = int.MaxValue;
		}
		
		return m_randomWithSeed.Next(p_max);
	}
	
	public static int RANDOM_INT_SEED ( int p_min, int p_max )
	{
		if ( p_min > p_max ) {
			D.Warn("Utility::RANDOM_INT Warning!! min must always be < the max value");
			return -1;
		}
		
		return m_randomWithSeed.Next(p_min, p_max);
	}
	
	private static System.Random m_randomizer = new System.Random();
	public static int RANDOM_INT(int p_max)
	{
		if ( p_max < 0 ) {
			D.Warn("Utility::RANDOM_INT Warning!! max must be > 0!");
			p_max = int.MaxValue;
		}
		
		return m_randomizer.Next(p_max);
	}
	
	public static int RANDOM_INT(int p_min, int p_max)
	{
		if ( p_min > p_max ) {
			D.Warn("Utility::RANDOM_INT Warning!! min must always be < the max value");
			return -1;
		}
		
		return m_randomizer.Next(p_min, p_max);
	}
	
	public static int RANDOM_IN_TWO_INT ( int p_a, int p_b )
	{
		int ran = Utility.RANDOM_INT(1,10);
		if ( ran < 5 ) { return p_a; }
		
		return p_b;
	}
	
	public static float RANDOM_FLOAT ( float p_min, float p_max )
	{
//		double range = (double) p_max - (double) p_max;
//		double sample = m_randomizer.NextDouble();
//		double scaled = (sample * range) + p_max;
//		float f = (float) scaled;
//		return f;
		
		return UnityEngine.Random.Range(p_min, p_max);
	}
	
	/** +KJ:04102013 
	 * Float|Int unboxing utility 
	 **/
	public static float ParseToFloat ( object p_val )
	{
		if ( p_val == null ) {
			Debug.LogWarning("Warning! p_val is null.. returning -1.0f");
			return -1.0f;
		}
		
		if( p_val is Double )	{ return (float)(double)p_val; }
		else 					{ return float.Parse(p_val.ToString()); }
	}
	
	public static List<T> PrimitiveArrayListToList<T>( ArrayList p_array ) where T:struct
	{
		List<T> list = new List<T>( p_array.Count );
		
		for ( int i = 0; i < p_array.Count; i++ ) {
			T _object = (T)Convert.ChangeType(p_array[i], typeof(T));
			list.Add(_object);
		}
	
		return list;
	}
	
	public static List<T> ArrayListToList<T> ( ArrayList p_array ) 
	{
		List<T> list = new List<T>( p_array.Count );
		
		foreach ( object obj in p_array ) { 
			T o = (T)Convert.ChangeType( obj, typeof(T) );
			list.Add( o ); 
		}
		
		return list;
	}
	
	public static ArrayList ListToArraylist<T> ( List<T> p_array )
	{
		ArrayList arr = new ArrayList();
	    
		for ( int counter = 0; counter < p_array.Count; counter++ ) {
			arr.Add((object)p_array[counter]);
		}
		
		return arr;
	}
	
	public static Dictionary<K,V> HashToDictionary<K,V> ( Hashtable p_hash )
	{
		Dictionary<K,V> dict = new Dictionary<K, V>();
		
		foreach ( DictionaryEntry entry in p_hash ) {
			K k = (K)entry.Key;
			V v = (V)Convert.ChangeType( entry.Value, typeof(V) );
		  	dict.Add( k, v );
		}
		
		return dict;
	}
	
	public static Hashtable DictionaryToHash<K,V> ( Dictionary<K,V> p_toConvert ) 
	{
		Hashtable hash = new Hashtable();

		foreach ( KeyValuePair<K,V> p_data in p_toConvert ) {
			hash.Add( p_data.Key, (V)Convert.ChangeType( p_data.Value, typeof(V) ) );
		}
		
		return hash;
	}
	
	public static int GetEpochTimeInSeconds ()
	{	
		var epochStart = new System.DateTime(1970, 1, 1, 8, 0, 0, System.DateTimeKind.Utc);
		int timestamp = (int)(System.DateTime.UtcNow - epochStart).TotalSeconds;
		
		return timestamp;
	}
	
	public static int ParseToInt ( object p_val )
	{
		if ( p_val == null ) {
			Debug.LogWarning("Warning! p_val is null.. returning -1");
			return -1;
		}
		
		if( p_val is Double )	return (int)(double)p_val;
		else 					return int.Parse(p_val.ToString());
	}
	
	public static Vector3 GetProjection ( Vector2 p_value )
	{
		return ( Camera.mainCamera.ScreenToWorldPoint(new Vector3 (p_value.x, p_value.y, Camera.mainCamera.nearClipPlane + 1.25f)));
	}
	
	// +AS:08192013 Gesture Utility
	/// <summary>
	/// Checks two Direction if they're in opposite direction
	/// </summary>
	private static FingerGestures.SwipeDirection SouthEast_NorthWest = ( FingerGestures.SwipeDirection.UpperLeftDiagonal  | FingerGestures.SwipeDirection.LowerRightDiagonal );
	private static FingerGestures.SwipeDirection SouthWest_NorthEast = ( FingerGestures.SwipeDirection.LowerRightDiagonal | FingerGestures.SwipeDirection.UpperLeftDiagonal );
	private static FingerGestures.SwipeDirection LeftDiagonals		 = FingerGestures.SwipeDirection.UpperLeftDiagonal  | FingerGestures.SwipeDirection.LowerLeftDiagonal;
	private static FingerGestures.SwipeDirection RightDiagonals		 = FingerGestures.SwipeDirection.UpperRightDiagonal | FingerGestures.SwipeDirection.LowerRightDiagonal;
	private static FingerGestures.SwipeDirection Dir_Up 			 = FingerGestures.SwipeDirection.Up | FingerGestures.SwipeDirection.UpperDiagonals;
	private static FingerGestures.SwipeDirection Dir_Down			 = FingerGestures.SwipeDirection.Down | FingerGestures.SwipeDirection.LowerDiagonals;
	private static FingerGestures.SwipeDirection Dir_Left			 = FingerGestures.SwipeDirection.Left | ( FingerGestures.SwipeDirection.UpperLeftDiagonal | FingerGestures.SwipeDirection.LowerLeftDiagonal );
	private static FingerGestures.SwipeDirection Dir_Right		  	 = FingerGestures.SwipeDirection.Right | ( FingerGestures.SwipeDirection.UpperRightDiagonal | FingerGestures.SwipeDirection.LowerRightDiagonal );
	
	public static bool IsSameDireaction (
		FingerGestures.SwipeDirection p_newDir,
		FingerGestures.SwipeDirection p_prevDir
	){
		if( p_newDir == p_prevDir )
			return true;
		
		FingerGestures.SwipeDirection groupedDirection = ( p_newDir | p_prevDir );
		
		if( LeftDiagonals == groupedDirection )
			return true;
		
		if( RightDiagonals == groupedDirection )
			return true;
		
		if( RightDiagonals == Dir_Up )
			return true;
		
		if( RightDiagonals == Dir_Down )
			return true;
		
		if( RightDiagonals == Dir_Left )
			return true;
		
		if( RightDiagonals == Dir_Right )
			return true;
		
		return false;
	}
	
	public static bool IsOppositeDirection (
		FingerGestures.SwipeDirection p_newDir,
		FingerGestures.SwipeDirection p_prevDir
	){
		if( p_newDir == p_prevDir )
			return false;
		
		FingerGestures.SwipeDirection groupedDirection = ( p_newDir | p_prevDir );
		
		if( FingerGestures.SwipeDirection.Vertical == groupedDirection )
			return true;
		
		if( FingerGestures.SwipeDirection.Horizontal == groupedDirection )
			return true;
		
		if( FingerGestures.SwipeDirection.Vertical == groupedDirection )
			return true;
		
		if( FingerGestures.SwipeDirection.Vertical == groupedDirection )
			return true;
		
		if( SouthEast_NorthWest == groupedDirection )
			return true;
		
		if( SouthWest_NorthEast == groupedDirection )
			return true;
			
		
		
		return false;
	}
	
	/** +AS:07172013
	 * UI Display Debug
	 **/
	public static bool bEnabledUI = false;
	public static bool bDebugActions = false;
	public static bool bDebugTimedActions = false;
	
	// + LA 073113
	public static bool bIsDebugEnabled = false;
	
	public static string GetiPhoneDocumentsPath () 
    { 
        // Your game has read+write access to /var/mobile/Applications/XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX/Documents 
        // Application.dataPath returns              
        // /var/mobile/Applications/XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX/myappname.app/Data 
        // Strip "/Data" from path 
        string path = Application.dataPath.Substring (0, Application.dataPath.Length - 5); 
        // Strip application name 
        path = path.Substring(0, path.LastIndexOf('/'));  
        return path + "/Documents"; 
    }
	
	// +KJ:09202013 Safe Stop Tween
	public static void StopiTweenByName ( string p_name )
	{
		try{ iTween.StopByName(p_name); }
		catch { /*Debug.Log("Error! Trying to stop a non existing tween. AnimateToCenter ");*/ }
	}
	
	// +KJ:09242013 Shuffle List
	public static void ShuffleList<T> ( List<T> p_lists )
	{
		int count = p_lists.Count;
		
		for (int i = 0; i < count; i++) {
			// Select a random element between i and end of array to swap with.
			int nElements = count - i;
			int n = (Utility.RANDOM_INT (0, count - 1) % nElements) + i;
			
			T objA = p_lists[i];
			T objB = p_lists[n];
			
			p_lists[i] = objB;
			p_lists[n] = objA;
		}
	}
	
	/****************************************************
	 * Debug UI Helpers
	 **/
	private static float m_multiplier	= ( Screen.height > 768 ? 2 : 1 );
	private static Vector2 m_initPos	= new Vector2( 200, 60 );
	private static Vector2 m_initSize	= new Vector2( 95, 60 );
	
	public static bool CreateEnableButton (
		string 	p_label, 
		float 	p_offsetX,
		float 	p_offsetY,
		float 	p_sizeX,
		float 	p_sizeY
	){
#if !DEBUG_GUI_UTILS
		return false;
#else
		return GUI.Button(
			new Rect(
			Screen.width 	- p_offsetX * m_multiplier, 
			Screen.height 	- p_offsetY * m_multiplier, 
			p_sizeX * m_multiplier, 
			p_sizeY * m_multiplier), 
		p_label);
#endif
	}
	
	public static bool CreateEnableButton (
		string 	p_label, 
		Vector2 p_topLeftOffest,
		Vector2 p_size
	){
#if !DEBUG_GUI_UTILS
		return false;
#else
		return Utility.CreateEnableButton(
			p_label,
			p_topLeftOffest.x,
			p_topLeftOffest.y,
			p_size.x,
			p_size.y);
#endif
	}
	
	// +KJ:10072013 Enum Helpers. ( Converts an int/string value/name to Enum )
	public static T EnumFromInt<T> ( int value )
	{
		return (T)((object)value);
	}
	
	public static T EnumFromString<T> ( string value )
	{
		return (T)Enum.Parse(typeof(T),value);
	}
	
	// +KJ:10072013 Load Data Util
	public static T LoadTextData<T> ( string p_path )
	{
		// Load Tutorial Sequence
		TextAsset tdata = (TextAsset)Resources.Load(p_path, typeof(TextAsset));
		
		T t = (T)((object)null);
		
		if( tdata == null ) 
		{
			Debug.LogError("Error! Utility::LoadTextData Path:"+p_path+" \n ");
			return t;
		}
		
		string strTData = tdata.text;
		T tutData = (T)((object)MiniJSON.jsonDecode(strTData));
		
		if( tutData == null ) 
		{
			Debug.LogError("Error! Utility::LoadTextData Path:"+p_path+" \n ");
			return t;
		}
		
		return tutData;
	}
	
	// +KJ:10092013 Disable UIButton util
	public static bool OverAllUIFlag = true;
	public static void SetIsButtonEnabled ( string p_gameObject, bool p_bIsEnabled )
	{
		GameObject button = GameObject.Find(p_gameObject);
		Utility.SetIsButtonEnabled( button, p_bIsEnabled );
	}
	
	public static void SetIsButtonEnabled ( GameObject p_gameObject, bool p_bIsEnabled )
	{
		GameObject button = p_gameObject;
		
		if( button == null )
			return;
		
		UIButton but = button.GetComponent<UIButton>();
		
		if( but == null )
			return;
		
		// temp
		button.GetComponent<Collider>().enabled = p_bIsEnabled;
		
		// update color
		but.UpdateColor( p_bIsEnabled, false);
	}
	
	/* 
	 * +BC 11/04/2013 Setup button for action callback.
	 * 								Mostly used in UI buttons
	*/
	public static void SetupButton(string p_buttonName, Action p_callback)
	{
		GameObject btn = GameObject.Find(p_buttonName) as GameObject;
		if( btn == null )
		{
			D.Error("Error! Cannot find button "+p_buttonName);
			return;
		}
		btn.AddComponent<ButtonUtilityHandlers>();
		btn.GetComponent<ButtonUtilityHandlers>().CustomCall = p_callback;
	}
	
	public static void SetupButton( GameObject p_button, Action p_callback )
	{
		if( p_button == null )
		{
			D.Error("Error! Cannot find button "+p_button.name);
			return;
		}
		p_button.AddComponent<ButtonUtilityHandlers>();
		p_button.GetComponent<ButtonUtilityHandlers>().CustomCall = p_callback;
	}
	
	/*
	public static void DisableComponent<T> ( GameObject p_object ) where T : Component
	{
		if( p_object == null )
			return;
		
		T[] scripts = p_object.GetComponents<T>();
		
		foreach( T script in scripts )
		{
			if( !script.enabled )
				continue;
			
			// disable component
			script.enabled = false;
		}
	}
	//*/
	
	public static int PowerOf2Range ( TutorialController.ETutorialState p_range )
	{
		int range = 0;
		int i = 0;
		
		while ( true ) {
			if( range >= (int)p_range )	break;
			
			if( range == 0 ) range = 1;
				
			range *= 2;
			i++;
		}
		
		return i;
	}
}

/** Button Classes ****************************************************/
public class ButtonUtilityHandlers : MonoBehaviour 
{
	public Action CustomCall;
	
	// +KJ:10022013 Details of the util button class. please do not remove these properties
	[SerializeField]
	private bool ButtonHandler;
	[SerializeField]
	private string Class = "ButtonUtilityHandlers";
	
	// Trigger by UI
	public void OnClick()
	{
		CustomCall();
	}
	
}
