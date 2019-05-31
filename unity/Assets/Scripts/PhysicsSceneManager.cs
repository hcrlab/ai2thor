﻿using System.Collections.Generic;
using UnityEngine;
using UnityStandardAssets.Characters.FirstPerson;
using System;
using System.Collections;
using UnityEngine.SceneManagement;
using UnityStandardAssets.ImageEffects;

[ExecuteInEditMode]

public class PhysicsSceneManager : MonoBehaviour 
{
	//public Dictionary<SimObjPhysics, List<ReceptacleSpawnPoint>> SceneSpawnPoints =  new Dictionary<SimObjPhysics, List<ReceptacleSpawnPoint>>();

	public List<GameObject> RequiredObjects = new List<GameObject>();

	//get references to the spawned Required objects after spawning them for the first time.
	public List<GameObject> SpawnedObjects = new List<GameObject>();
	public List<SimObjPhysics> PhysObjectsInScene = new List<SimObjPhysics>();

	public List<string> UniqueIDsInScene = new List<string>();

	public List<SimObjPhysics> ReceptaclesInScene = new List<SimObjPhysics>();

	public GameObject HideAndSeek;

    //public List<SimObjPhysics> LookAtThisList = new List<SimObjPhysics>();

	private bool m_Started = false;
	private Vector3 gizmopos;
	private Vector3 gizmoscale;
	private Quaternion gizmoquaternion;
	private GameObject _anchor;
	private GameObject _target;
	private GameObject _receptacle;
	private SimObjPhysics _receptaclePhysics;
	private int _gridsize;

	private void OnEnable()
	{
		//clear this on start so that the CheckForDuplicates function doesn't check pre-existing lists
		SetupScene();

		if(GameObject.Find("HideAndSeek"))
		HideAndSeek = GameObject.Find("HideAndSeek");

		if(!GameObject.Find("Objects"))
		{
			GameObject c = new GameObject("Objects");
			Debug.Log(c.transform.name + " was missing and is now added");
		}

		//on enable, set the ssao on the camera according to the current quality setting. Disable on lower quality for performance
		//need to adjust this value if the number of Quality Settings change
		//right now only Very High and Ultra will have ssao on by default.
		if(QualitySettings.GetQualityLevel() < 5)
		{
			GameObject.Find("FirstPersonCharacter").
			GetComponent<ScreenSpaceAmbientOcclusion>().enabled = false;
		}

		else
		{
			GameObject.Find("FirstPersonCharacter").
			GetComponent<ScreenSpaceAmbientOcclusion>().enabled = true;
		}
		UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
	}

	public void SetupScene()
	{
		UniqueIDsInScene.Clear();
		ReceptaclesInScene.Clear();
		PhysObjectsInScene.Clear();
		GatherSimObjPhysInScene();
		GatherAllReceptaclesInScene();
	}

	// clear table
    // for now, assume we start from default FloorPlan1
    // later, can find all objects in contact with the table and clear them off
	void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		int z = -3;
		string[] objectsOnTable =
		{
			"Apple1",
			"Bowl_1",
			"bread_1",
			"ButterKnife_1",
			"CreditCard_4",
			"PaperClutter1",
			"Tomato_1"
		};
		List<string> objectsOnTableList = new List<string>(objectsOnTable);
		foreach (string obj in objectsOnTableList)
		{
			GameObject moving = GameObject.Find(obj);
			moving.transform.Translate(0, 0, z);
		}
	}
	
	// Use this for initialization
	void Start ()
	{
		_receptacle = GameObject.Find("CounterTop_Physics");
		_anchor = GameObject.Find("Bowl_1");
		_target = GameObject.Find("Apple1");
		_gridsize = 20; // (_gridsize + 1 )^2 grid points
		GenerateLayouts(_receptacle, _anchor, _target, _gridsize);
	}
	
	// Update is called once per frame
	void Update () 
	{
//		if (Input.GetKeyUp(KeyCode.Space))
//			GenerateLayouts(_receptacle, _anchor, _target, _gridsize);
	}

	void onDisable()
	{
		Debug.Log("OnDisable");
		UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
	}
	public bool ToggleHideAndSeek(bool hide)
	{
		if(HideAndSeek)
		{
			if (HideAndSeek.activeSelf != hide) {
				HideAndSeek.SetActive(hide);
				SetupScene();
			}
			return true;
		}

		else
		{
			#if UNITY_EDITOR
			Debug.Log("Hide and Seek object reference not set!");
			#endif

			return false;
		}


	}

    public void GatherSimObjPhysInScene()
	{
		//PhysObjectsInScene.Clear();
		PhysObjectsInScene = new List<SimObjPhysics>();

		PhysObjectsInScene.AddRange(FindObjectsOfType<SimObjPhysics>());
		PhysObjectsInScene.Sort((x, y) => (x.Type.ToString().CompareTo(y.Type.ToString())));

		foreach(SimObjPhysics o in PhysObjectsInScene)
		{
			Generate_UniqueID(o);

			///debug in editor, make sure no two object share ids for some reason
			#if UNITY_EDITOR
			if (CheckForDuplicateUniqueIDs(o))
			{
				
				Debug.Log("Yo there are duplicate UniqueIDs! Check" + o.UniqueID);
				
			}


			else
			#endif
				UniqueIDsInScene.Add(o.UniqueID);

		}
		
		PhysicsRemoteFPSAgentController fpsController = GameObject.Find("FPSController").GetComponent<PhysicsRemoteFPSAgentController>();
		if (fpsController.imageSynthesis != null) {
			fpsController.imageSynthesis.OnSceneChange();
		}
	}

	public void GatherAllReceptaclesInScene()
	{
		foreach(SimObjPhysics sop in PhysObjectsInScene)
		{
			if(sop.DoesThisObjectHaveThisSecondaryProperty(SimObjSecondaryProperty.Receptacle))
			{
				ReceptaclesInScene.Add(sop);

				foreach (GameObject go in sop.ReceptacleTriggerBoxes)
				{
					if (go == null) {
						Debug.LogWarning(sop.gameObject + " has non-empty receptacle trigger boxes but contains a null value.");
						continue;
					}
					Contains c = go.GetComponent<Contains>();
					if (c == null) {
						Debug.LogWarning(sop.gameObject + " is missing a contains script on one of its receptacle boxes.");
						continue;
					}
					if(go.GetComponent<Contains>().myParent == null) {
						go.GetComponent<Contains>().myParent = sop.transform.gameObject;
					}
				}
			}
		}
		ReceptaclesInScene.Sort((r0, r1) => (r0.gameObject.GetInstanceID().CompareTo(r1.gameObject.GetInstanceID())));
	}
    
	private void Generate_UniqueID(SimObjPhysics o)
    {
		//check if this object require's it's parent simObj's UniqueID as a prefix
		if(ReceptacleRestrictions.UseParentUniqueIDasPrefix.Contains(o.Type))
		{
			SimObjPhysics parent = o.transform.parent.GetComponent<SimObjPhysics>();
			if (parent == null) {
				Debug.LogWarning("Object " + o + " requires a SimObjPhysics " +
				"parent to create its unique ID but none exists. Using 'None' instead.");
				o.UniqueID = "None|" + o.Type.ToString();
				return;
			}

			if(parent.UniqueID == null)
			{
				Vector3 ppos = parent.transform.position;
				string xpPos = (ppos.x >= 0 ? "+" : "") + ppos.x.ToString("00.00");
				string ypPos = (ppos.y >= 0 ? "+" : "") + ppos.y.ToString("00.00");
				string zpPos = (ppos.z >= 0 ? "+" : "") + ppos.z.ToString("00.00");
				parent.UniqueID = parent.Type.ToString() + "|" + xpPos + "|" + ypPos + "|" + zpPos;
			}

			o.UniqueID = parent.UniqueID + "|" + o.Type.ToString();
			return;

		}

        Vector3 pos = o.transform.position;
        string xPos = (pos.x >= 0 ? "+" : "") + pos.x.ToString("00.00");
        string yPos = (pos.y >= 0 ? "+" : "") + pos.y.ToString("00.00");
        string zPos = (pos.z >= 0 ? "+" : "") + pos.z.ToString("00.00");
        o.UniqueID = o.Type.ToString() + "|" + xPos + "|" + yPos + "|" + zPos;
    }
    
	private bool CheckForDuplicateUniqueIDs(SimObjPhysics sop)
	{
		if (UniqueIDsInScene.Contains(sop.UniqueID))
			return true;

		else
			return false;
	}

	public void AddToObjectsInScene(SimObjPhysics sop)
	{
		PhysObjectsInScene.Add(sop);
	}

	public void AddToIDsInScene(SimObjPhysics sop)
	{
		UniqueIDsInScene.Add(sop.uniqueID);
	}

	//use action.randomseed for seed, use action.forceVisible for if objects shoudld ONLY spawn outside and not inside anything
	//set forceVisible to true for if you want objects to only spawn in immediately visible receptacles.
	public bool RandomSpawnRequiredSceneObjects(ServerAction action)
	{
		
		if(RandomSpawnRequiredSceneObjects(action.randomSeed, action.forceVisible, action.maxNumRepeats, action.placeStationary))
		{
			return true;
		}
		
		else
		return false;
	}

	//if no values passed in, default to system random based on ticks
	public void RandomSpawnRequiredSceneObjects()
	{
		RandomSpawnRequiredSceneObjects(System.Environment.TickCount, false, 50, false);
	}

	//place each object in the array of objects that should appear in this scene randomly in valid receptacles
	//a seed of 0 is the default positions placed by hand(?)
	public bool RandomSpawnRequiredSceneObjects(
		int seed, 
		bool SpawnOnlyOutside,
		int maxcount,
		bool StaticPlacement
	) {
		#if UNITY_EDITOR
		var Masterwatch = System.Diagnostics.Stopwatch.StartNew();
		#endif

		if(RequiredObjects.Count == 0)
		{
			#if UNITY_EDITOR
			Debug.Log("No objects in Required Objects array, please add them in editor");
			#endif
			
			return false;
		}

		UnityEngine.Random.InitState(seed);

		List<SimObjType> TypesOfObjectsPrefabIsAllowedToSpawnIn = new List<SimObjType>();
		Dictionary<SimObjType, List<SimObjPhysics>> AllowedToSpawnInAndExistsInScene = new Dictionary<SimObjType, List<SimObjPhysics>>();

		int HowManyCouldntSpawn = RequiredObjects.Count;

		// GameObject topLevelObject = GameObject.Find("Objects");
		// PhysicsRemoteFPSAgentController controller = GameObject.FindObjectsOfType<PhysicsRemoteFPSAgentController>()[0];
		
		// foreach (GameObject go in SpawnedObjects) {
		// 	go.SetActive(true);
		// 	SimObjPhysics sop = go.GetComponent<SimObjPhysics>();
		// 	sop.transform.parent = topLevelObject.transform;
		// 	sop.transform.position = new Vector3(0.0f, controller.sceneBounds.min.y - 10f, 0.0f);
		// 	go.GetComponent<Rigidbody>().isKinematic = true;
		// }

		//if we already spawned objects, lets just move them around
		if(SpawnedObjects.Count > 0)
		{
			HowManyCouldntSpawn = SpawnedObjects.Count;

			//for each object in RequiredObjects, start a list of what objects it's allowed 
			//to spawn in by checking the PlacementRestrictions dictionary
			foreach(GameObject go in SpawnedObjects)
			{
				AllowedToSpawnInAndExistsInScene = new Dictionary<SimObjType, List<SimObjPhysics>>();

				SimObjType goObjType = go.GetComponent<SimObjPhysics>().ObjType;

				bool typefoundindictionary = ReceptacleRestrictions.PlacementRestrictions.ContainsKey(goObjType);
				if(typefoundindictionary)
				{
					TypesOfObjectsPrefabIsAllowedToSpawnIn = new List<SimObjType>(ReceptacleRestrictions.PlacementRestrictions[goObjType]);

					//remove from list if receptacle isn't in this scene
					//compare to receptacles that exist in scene, get the ones that are the same
					
					foreach(SimObjPhysics sop in ReceptaclesInScene)
					{
						// don't random spawn in objects that are pickupable to prevent Egg spawning in Plate with the plate spawned in Cabinet....
						bool allowed = false;
						if (sop.PrimaryProperty != SimObjPrimaryProperty.CanPickup) { 
							if(SpawnOnlyOutside)
							{
								if(ReceptacleRestrictions.SpawnOnlyOutsideReceptacles.Contains(sop.ObjType) && TypesOfObjectsPrefabIsAllowedToSpawnIn.Contains(sop.ObjType))
								{
									allowed = true;
								}
							}
							else if(TypesOfObjectsPrefabIsAllowedToSpawnIn.Contains(sop.ObjType))
							{
								allowed = true;
							}
						}
						if (allowed) {
							if (!AllowedToSpawnInAndExistsInScene.ContainsKey(sop.ObjType)) {
								AllowedToSpawnInAndExistsInScene[sop.ObjType] = new List<SimObjPhysics>();
							}
							AllowedToSpawnInAndExistsInScene[sop.ObjType].Add(sop);
						}
					}
				}

				//not found indictionary!
				else
				{
					#if UNITY_EDITOR
					Debug.Log(go.name +"'s Type is not in the ReceptacleRestrictions dictionary!");
					#endif
					break;
				}

				// Now we have an updated list of receptacles in the scene that are also in the list
				// of valid receptacles for this given game object "go" that we are currently checking this loop
				if(AllowedToSpawnInAndExistsInScene.Count > 0)
				{
					//SimObjPhysics targetReceptacle;
					InstantiatePrefabTest spawner = gameObject.GetComponent<InstantiatePrefabTest>();
					List<ReceptacleSpawnPoint> targetReceptacleSpawnPoints;
			
					//each sop here is a valid receptacle
					bool spawned = false;
					foreach(SimObjPhysics sop in ShuffleSimObjPhysicsDictList(AllowedToSpawnInAndExistsInScene))
					{
						//targetReceptacle = sop;

						//check if the target Receptacle is an ObjectSpecificReceptacle
						//if so, if this game object is compatible with the ObjectSpecific restrictions, place it!
						//this is specifically for things like spawning a mug inside a coffee maker
						if(sop.DoesThisObjectHaveThisSecondaryProperty(SimObjSecondaryProperty.ObjectSpecificReceptacle))
						{
							ObjectSpecificReceptacle osr = sop.GetComponent<ObjectSpecificReceptacle>();

							if(osr.HasSpecificType(go.GetComponent<SimObjPhysics>().ObjType))
							{
								//in the random spawn function, we need this additional check because there isn't a chance for
								//the physics update loop to fully update osr.isFull() correctly, which can cause multiple objects
								//to be placed on the same spot (ie: 2 pots on the same burner)
								if(osr.attachPoint.transform.childCount > 0)
								{
									break;
								}

								//perform additional checks if this is a Stove Burner! 
								if(sop.GetComponent<SimObjPhysics>().Type == SimObjType.StoveBurner)
								{
									if(StoveTopCheckSpawnArea(go.GetComponent<SimObjPhysics>(), osr.attachPoint.transform.position,
									osr.attachPoint.transform.rotation, false) == true)
									{
										//print("moving object now");
										go.transform.position = osr.attachPoint.position;
										go.transform.SetParent(osr.attachPoint.transform);
										go.transform.localRotation = Quaternion.identity;
										go.GetComponent<Rigidbody>().isKinematic = true;

										HowManyCouldntSpawn--;
										spawned = true;

										// print(go.transform.name + " was spawned in " + sop.transform.name);

										// #if UNITY_EDITOR
										// //Debug.Log(go.name + " succesfully placed in " +sop.UniqueID);
										// #endif

										break;
									}
								}

								//for everything else (coffee maker, toilet paper holder, etc) just place it if there is nothing attached
								else
								{
										go.transform.position = osr.attachPoint.position;
										go.transform.SetParent(osr.attachPoint.transform);
										go.transform.localRotation = Quaternion.identity;
										go.GetComponent<Rigidbody>().isKinematic = true;

										HowManyCouldntSpawn--;
										spawned = true;
										break;
								}
							}
						}

						targetReceptacleSpawnPoints = sop.ReturnMySpawnPoints(false);

						//first shuffle the list so it's raaaandom
						targetReceptacleSpawnPoints.Shuffle();
						
						//try to spawn it, and if it succeeds great! if not uhhh...

						#if UNITY_EDITOR
						// var watch = System.Diagnostics.Stopwatch.StartNew();
						#endif

						if(spawner.PlaceObjectReceptacle(targetReceptacleSpawnPoints, go.GetComponent<SimObjPhysics>(), StaticPlacement, maxcount, 90, true)) //we spawn them stationary so things don't fall off of ledges
						{
							HowManyCouldntSpawn--;
							spawned = true;

							#if UNITY_EDITOR
							// watch.Stop();
							// var y = watch.ElapsedMilliseconds;
						    //print( "SUCCESFULLY placing " + go.transform.name+ " in " + sop.transform.name);
							#endif

							break;
						} 

						#if UNITY_EDITOR
						// watch.Stop();
						// var elapsedMs = watch.ElapsedMilliseconds;
						// print("time for trying, but FAILING, to place " + go.transform.name+ " in " + sop.transform.name + ": " + elapsedMs + " ms");
						#endif
					}
					
					if (!spawned) {
						#if UNITY_EDITOR
						Debug.Log(go.name + " could not be spawned.");
						#endif
						// go.SetActive(false);
					}
				}
			}
		} else {
			throw new NotImplementedException();
		}

		// Debug code to see where every object is spawning
		// string s = "";
		// foreach (GameObject sop in SpawnedObjects) {
		// 	s += sop.name + ": " + sop.transform.parent.gameObject.name + ",\t";
		// }
		// Debug.Log(s);

		///////////////KEEP THIS DEPRECATED STUFF - In case we want to spawn in objects that don't currently exist in the scene, that logic is below////////////////
		// //we have not spawned objects, so instantiate them here first
		// else
		// {
		// 	//for each object in RequiredObjects, start a list of what objects it's allowed 
		// 	//to spawn in by checking the PlacementRestrictions dictionary
		// 	foreach(GameObject go in RequiredObjects)
		// 	{
		// 		TypesOfObjectsPrefabIsAllowedToSpawnIn.Clear();
		// 		AllowedToSpawnInAndExistsInScene.Clear();

		// 		SimObjType goObjType = go.GetComponent<SimObjPhysics>().ObjType;

		// 		bool typefoundindictionary = ReceptacleRestrictions.PlacementRestrictions.ContainsKey(goObjType);
		// 		if(typefoundindictionary)
		// 		{
		// 			TypesOfObjectsPrefabIsAllowedToSpawnIn = new List<SimObjType>(ReceptacleRestrictions.PlacementRestrictions[goObjType]);

		// 			//remove from list if receptacle isn't in this scene
		// 			//compare to receptacles that exist in scene, get the ones that are the same
					
		// 			foreach(SimObjPhysics sop in ReceptaclesInScene)
		// 			{
		// 				if(SpawnOnlyOutside)
		// 				{
		// 					if(ReceptacleRestrictions.SpawnOnlyOutsideReceptacles.Contains(sop.ObjType) && TypesOfObjectsPrefabIsAllowedToSpawnIn.Contains(sop.ObjType))
		// 					{
		// 						AllowedToSpawnInAndExistsInScene.Add(sop);
		// 					}
		// 				}

		// 				else if(TypesOfObjectsPrefabIsAllowedToSpawnIn.Contains(sop.ObjType))
		// 				{
		// 					//updated list of valid receptacles in scene
		// 					AllowedToSpawnInAndExistsInScene.Add(sop);
		// 				}
		// 			}
		// 		}

		// 		else
		// 		{
		// 			#if UNITY_EDITOR
		// 			Debug.Log(go.name +"'s Type is not in the ReceptacleRestrictions dictionary!");
		// 			#endif

		// 			break;
		// 		}

		// 		// // //now we have an updated list of SimObjPhys of receptacles in the scene that are also in the list
		// 		// // //of valid receptacles for this given game object "go" that we are currently checking this loop
		// 		if(AllowedToSpawnInAndExistsInScene.Count > 0)
		// 		{
		// 			SimObjPhysics targetReceptacle;
		// 			InstantiatePrefabTest spawner = gameObject.GetComponent<InstantiatePrefabTest>();
		// 			List<ReceptacleSpawnPoint> targetReceptacleSpawnPoints;

		// 			//RAAANDOM!
		// 			ShuffleSimObjPhysicsList(AllowedToSpawnInAndExistsInScene);
		// 			//bool diditspawn = false;


		// 			GameObject temp = Instantiate(go, new Vector3(0, 100, 0), Quaternion.identity);
		// 			temp.transform.name = go.name;
		// 			//print("create object");
		// 			//GameObject temp = PrefabUtility.InstantiatePrefab(go as GameObject) as GameObject;
		// 			temp.GetComponent<Rigidbody>().isKinematic = true;
		// 			//spawn it waaaay outside of the scene and then we will try and move it in a moment here, hold your horses
		// 			temp.transform.position = new Vector3(0, 100, 0);//GameObject.Find("FPSController").GetComponent<PhysicsRemoteFPSAgentController>().AgentHandLocation();

		// 			foreach(SimObjPhysics sop in AllowedToSpawnInAndExistsInScene)
		// 			{
		// 				targetReceptacle = sop;

		// 				targetReceptacleSpawnPoints = targetReceptacle.ReturnMySpawnPoints(false);

		// 				//first shuffle the list so it's raaaandom
		// 				ShuffleReceptacleSpawnPointList(targetReceptacleSpawnPoints);
						
		// 				//try to spawn it, and if it succeeds great! if not uhhh...

		// 				#if UNITY_EDITOR
		// 				var watch = System.Diagnostics.Stopwatch.StartNew();
		// 				#endif

		// 				if(spawner.PlaceObjectReceptacle(targetReceptacleSpawnPoints, temp.GetComponent<SimObjPhysics>(), true, maxcount, 360, true)) //we spawn them stationary so things don't fall off of ledges
		// 				{
		// 					//Debug.Log(go.name + " succesfully spawned");
		// 					//diditspawn = true;
		// 					HowManyCouldntSpawn--;
		// 					SpawnedObjects.Add(temp);
		// 					break;
		// 				}

		// 				#if UNITY_EDITOR
		// 				watch.Stop();
		// 				var elapsedMs = watch.ElapsedMilliseconds;
		// 				print("time for PlacfeObject: " + elapsedMs);
		// 				#endif

		// 			}
		// 		}
		// 	}			
		// }

		//we can use this to report back any failed spawns if we want that info at some point ?

		#if UNITY_EDITOR
		if(HowManyCouldntSpawn > 0)
		{
			Debug.Log(HowManyCouldntSpawn + " object(s) could not be spawned into the scene!");
		}

		Masterwatch.Stop();
		var elapsed = Masterwatch.ElapsedMilliseconds;
		print("total time: " + elapsed);
		#endif

		//Debug.Log("Iteration through Required Objects finished");
		SetupScene();
		return true;
	}
	
	
	//place each object in the array of objects that should appear in this scene randomly in valid receptacles
	// place anchor and target object on receptacle (counter) according to specified parameters
	//a seed of 0 is the default positions placed by hand(?)
// TODO: Should return a dictionary(?) of attempted layouts and boolean of whether they were successful or not
	public bool GenerateLayouts(
//		int seed, 
		GameObject receptacleObj, // receptacle upon which anchor and target should be placed (i.e. counter)
		GameObject anchor, // anchor object, i.e. object with respect to which target is placed
		GameObject target, // target object
		int gridsize = 9
//		bool SpawnOnlyOutside,
//		int maxcount,
//		bool StaticPlacement
	) {
		#if UNITY_EDITOR
		var Masterwatch = System.Diagnostics.Stopwatch.StartNew();
		#endif

		if(RequiredObjects.Count == 0)
		{
			#if UNITY_EDITOR
			Debug.Log("No objects in Required Objects array, please add them in editor");
			#endif
			
			return false;
		} 
		// testing
		int seed = 0;
		bool SpawnOnlyOutside = true;
		int maxcount = 10;
		bool StaticPlacement = true;
		// end testing

		int degreeIncrement = 360; // do not rotate objects
		SimObjPhysics receptacle = receptacleObj.GetComponent<SimObjPhysics>();
		// step 0: get possible spawn points
		
        List<ReceptacleSpawnPoint> receptacleSpawnPoints = receptacle.ReturnMySpawnPoints(false, gridsize);
        Debug.Log(receptacleSpawnPoints.Count);
//        targetReceptacleSpawnPoints.Shuffle(); // for now, just choose randomly from uniform distribution
        InstantiatePrefabTest spawner = gameObject.GetComponent<InstantiatePrefabTest>();
        StartCoroutine(constructLayouts(receptacleSpawnPoints, spawner, anchor, target, StaticPlacement,
	        maxcount, degreeIncrement, true));
        
		return true;
		
//		bool spawned = spawner.PlaceObjectReceptacle(targetReceptacleSpawnPoints, anchor.GetComponent<SimObjPhysics>(),
//			StaticPlacement, maxcount, degreeIncrement, true);
//        if (!spawned) {
//            #if UNITY_EDITOR
//            Debug.Log(anchor.name + " could not be spawned.");
//            #endif
//        }
//		
//		// step 2 : place target
//		spawned = spawner.PlaceObjectReceptacle(targetReceptacleSpawnPoints, target.GetComponent<SimObjPhysics>(),
//			StaticPlacement, maxcount, degreeIncrement, true);
//        if (!spawned) {
//            #if UNITY_EDITOR
//            Debug.Log(anchor.name + " could not be spawned.");
//            #endif
//        }

//		return true;
		// step 3: return config (?)
		// should be able to delete most/all code below
//		UnityEngine.Random.InitState(seed);
	}

	// TODO: constructLayouts actually tries the layouts and updates dictionary with whether layout successful or not
	public IEnumerator constructLayouts(List<ReceptacleSpawnPoint> receptacleSpawnPoints, InstantiatePrefabTest spawner,
		GameObject anchor, GameObject target, bool PlaceStationary, int maxcount, int degreeIncrement, bool AlwaysPlaceUpright)
	{
        bool anchorSpawned;
        bool targetSpawned;
        Quaternion rotation;
        
		// radius of anchor
		Vector3 anchorSize = anchor.GetComponent<SimObjPhysics>().BoundingBox.GetComponent<BoxCollider>().size;
		float r_a = anchorSize[0]/2; // radius_anchor
		
		// height of anchor
		float h_a = anchorSize[1]; // height_anchor
		
		// radius of target
		Vector3 targetSize = target.GetComponent<SimObjPhysics>().BoundingBox.GetComponent<BoxCollider>().size;
		float r_t = targetSize[0]/2; // radius_target
		
		// construct list of distances
		List<float> distances = new List<float> {0, r_a + r_t, r_a + 2*r_t, r_a + 4*r_t, r_a + 8*r_t};
		
		// construct list of heights
		List<float> heights = new List<float> {0, h_a, 2*h_a};
		
		// construct list of angles
		List<int> angles = new List<int> {0, 45, 90, 135, 180, 225, 270, 315};
		
        List<ReceptacleSpawnPoint> pointList = new List<ReceptacleSpawnPoint>(1);
        List<ReceptacleSpawnPoint> targetPointList = new List<ReceptacleSpawnPoint>(1);
        ReceptacleSpawnPoint firstRSP = receptacleSpawnPoints[0];
        pointList.Add(firstRSP);
        targetPointList.Add(new ReceptacleSpawnPoint(Vector3.zero, firstRSP.ReceptacleBox, firstRSP.Script, firstRSP.ParentSimObjPhys));
		foreach (ReceptacleSpawnPoint point in receptacleSpawnPoints)
		{
			pointList[0] = point;
			anchorSpawned = spawner.PlaceObjectReceptacle(pointList, anchor.GetComponent<SimObjPhysics>(),
                PlaceStationary, maxcount, degreeIncrement, AlwaysPlaceUpright, true);
			// if spawn successful, try placing target
			if (anchorSpawned)
			{
				foreach (int angle in angles)
				{
					foreach (float distance in distances)
					{
						// ignoring height for now
						rotation = Quaternion.AngleAxis(angle, Vector3.up);
						targetPointList[0].Point = point.Point + rotation * Vector3.left * distance;
                        targetSpawned = spawner.PlaceObjectReceptacle(targetPointList, target.GetComponent<SimObjPhysics>(),
                            PlaceStationary, maxcount, degreeIncrement, AlwaysPlaceUpright, true);
                        if (!targetSpawned)
                        {
	                        break;
                        }
                        yield return null; // put this after break, so frames where target doesn't spawn aren't rendered
					}
				}
			}
//            if (!anchorSpawned) {
//                #if UNITY_EDITOR
//                Debug.Log(anchor.name + " could not be spawned.");
//                #endif
//            }
		}
	}


	//a variation of the CheckSpawnArea logic from InstantiatePrefabTest.cs, but filter out things specifically for stove tops
	//which are unique due to being placed close together, which can cause objects placed on them to overlap in super weird ways oh
	//my god it took like 2 days to figure this out it should have been so simple
	public bool StoveTopCheckSpawnArea(SimObjPhysics simObj, Vector3 position, Quaternion rotation, bool spawningInHand)
	{
		//print("stove check");
		int layermask;

		//first do a check to see if the area is clear

        //if spawning in the agent's hand, ignore collisions with the Agent
		if(spawningInHand)
		{
			layermask = 1 << 8;
		}

        //oh we are spawning it somehwere in the environment, we do need to make sure not to spawn inside the agent or the environment
		else
		{
			layermask = (1 << 8) | (1 << 10);
		}

        //simObj.transform.Find("Colliders").gameObject.SetActive(false);
        Collider[] objcols;
        //make sure ALL colliders of the simobj are turned off for this check - can't just turn off the Colliders child object because of objects like
        //laptops which have multiple sets of colliders, with one part moving...
        objcols = simObj.transform.GetComponentsInChildren<Collider>();

        foreach (Collider col in objcols)
        {
            if(col.gameObject.name != "BoundingBox")
            col.enabled = false;
        }

 		//keep track of both starting position and rotation to reset the object after performing the check!
        Vector3 originalPos = simObj.transform.position;
        Quaternion originalRot = simObj.transform.rotation;

		//let's move the simObj to the position we are trying, and then change it's rotation to the rotation we are trying
        simObj.transform.position = position;
        simObj.transform.rotation = rotation;

        //now let's get the BoundingBox of the simObj as reference cause we need it to create the overlapbox
        GameObject bb = simObj.BoundingBox.transform.gameObject;
        BoxCollider bbcol = bb.GetComponent<BoxCollider>();

        #if UNITY_EDITOR
		m_Started = true;     
        gizmopos = bb.transform.TransformPoint(bbcol.center); 
        //gizmopos = inst.transform.position;
        gizmoscale = bbcol.size;
        //gizmoscale = simObj.BoundingBox.GetComponent<BoxCollider>().size;
        gizmoquaternion = rotation;
        #endif

        //we need the center of the box collider in world space, we need the box collider size/2, we need the rotation to set the box at, layermask, querytrigger
        Collider[] hitColliders = Physics.OverlapBox(bb.transform.TransformPoint(bbcol.center),
                                                     bbcol.size / 2.0f, simObj.transform.rotation, 
                                                     layermask, QueryTriggerInteraction.Ignore);

		//now check if any of the hit colliders were any object EXCEPT other stove top objects i guess
		bool result= true;

		if(hitColliders.Length > 0)
		{
			foreach(Collider col in hitColliders)
			{
				//if we hit some structure object like a stove top or countertop mesh, ignore it since we are snapping this to a specific position right here
				if(!col.GetComponentInParent<SimObjPhysics>())
				break;

				//if any sim object is hit that is not a stove burner, then ABORT
				if(col.GetComponentInParent<SimObjPhysics>().Type != SimObjType.StoveBurner)
				{
					result = false;
					simObj.transform.position = originalPos;
					simObj.transform.rotation = originalRot;

					foreach (Collider yes in objcols)
					{
						if(yes.gameObject.name != "BoundingBox")
						yes.enabled = true;
					}

					return result;
				}
			}
		}
         
		//nothing hit in colliders, so we are good to spawn.
		foreach (Collider col in objcols)
		{
			if(col.gameObject.name != "BoundingBox")
			col.enabled = true;
		}
		
		simObj.transform.position = originalPos;
		simObj.transform.rotation = originalRot;
		return result;//we are good to spawn, return true
	}

	public List<SimObjPhysics> ShuffleSimObjPhysicsDictList(Dictionary<SimObjType, List<SimObjPhysics>> dict)
	{
		List<SimObjType> types = new List<SimObjType>();
		Dictionary<SimObjType, int> indDict = new Dictionary<SimObjType, int>();
		foreach (KeyValuePair<SimObjType, List<SimObjPhysics>> pair in dict) {
			types.Add(pair.Key);
			indDict[pair.Key] = pair.Value.Count - 1;
		}
		types.Sort();
		types.Shuffle();
		foreach (SimObjType t in types) {
			dict[t].Shuffle();
		}

		bool changed = true;
		List<SimObjPhysics> shuffledSopList = new List<SimObjPhysics>();
		while (changed) {
			changed = false;
			foreach (SimObjType type in types) {
				int i = indDict[type];
				if (i >= 0) {
					changed = true;
					shuffledSopList.Add(dict[type][i]);
					indDict[type]--;
				}
			}
		}
		return shuffledSopList;
	}

#if UNITY_EDITOR
	void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        if (m_Started)
        {
            Matrix4x4 cubeTransform = Matrix4x4.TRS(gizmopos, gizmoquaternion, gizmoscale);
            Matrix4x4 oldGizmosMatrix = Gizmos.matrix;

            Gizmos.matrix *= cubeTransform;

            Gizmos.DrawWireCube(Vector3.zero, Vector3.one);

            Gizmos.matrix = oldGizmosMatrix;
        }

    }
#endif
}
