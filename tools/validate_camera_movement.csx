// Run through Unity Pipeline script eval in an initialized scene, with an isolated save file.
if(string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("NEO_DEMO_SAVE_PATH")))throw new System.Exception("An isolated NEO_DEMO_SAVE_PATH is required");
if(!UnityEngine.Application.isPlaying)throw new System.Exception("Play mode required");
var room=UnityEngine.Object.FindAnyObjectByType<NeoFirstRoom>();var exploration=UnityEngine.Object.FindAnyObjectByType<NeoExplorationScene>();
var board=room!=null?room.board:exploration.board;var control=room!=null?room.controller:exploration.controller;
var flow=UnityEngine.Object.FindAnyObjectByType<DemoBootstrap>();
if(room!=null){flow.StopAllCoroutines();room.Initialize(flow);typeof(DemoBootstrap).GetMethod("Resume",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(flow,null);}
if(control.Active==null||!board.Ready)throw new System.Exception("Scene has not initialized");
control.enabled=false;
var human=control.Active.GetComponent<NeoGridMotor>();var camera=control.mainCamera.GetComponent<UnityEngine.Camera>();
System.Collections.IEnumerator Verify(){
 var checks=new System.Collections.Generic.List<string>();string error=null;
 var start=human.Cell;var center=board.Graph.center;
 var queue=new System.Collections.Generic.Queue<UnityEngine.Vector2Int>();var previous=new System.Collections.Generic.Dictionary<UnityEngine.Vector2Int,UnityEngine.Vector2Int>();queue.Enqueue(start);previous[start]=start;
 var end=start;bool found=false;var dirs=new[]{UnityEngine.Vector2Int.left,UnityEngine.Vector2Int.right,UnityEngine.Vector2Int.up,UnityEngine.Vector2Int.down};
 while(queue.Count>0 && previous.Count<10000){var cell=queue.Dequeue();if(!board.navigationBounds.Contains((UnityEngine.Vector3Int)cell)&&UnityEngine.Vector2.Distance(cell,start)>=8){end=cell;found=true;break;}foreach(var d in dirs){var n=cell+d;if(board.Blocked(n)||previous.ContainsKey(n))continue;previous[n]=cell;queue.Enqueue(n);}}
 if(!found){error="No reachable test destination outside original bounds";}
 var route=new System.Collections.Generic.List<UnityEngine.Vector2Int>();if(found){for(var c=end;c!=start;c=previous[c])route.Add(c);route.Reverse();}
 foreach(var cell in route){if(!human.Step(cell-human.Cell)){error="Manual step rejected at "+cell;break;}float deadline=UnityEngine.Time.realtimeSinceStartup+3;while(human.IsMoving&&UnityEngine.Time.realtimeSinceStartup<deadline)yield return null;if(human.IsMoving){error="Movement timeout";break;}}
 if(error==null)checks.Add("Player crossed original navigation rectangle on painted ground");
 yield return null;yield return null;
 var view=camera.WorldToViewportPoint(human.transform.position);
 if(UnityEngine.Mathf.Abs(view.x-.5f)>.03f||UnityEngine.Mathf.Abs(view.y-.5f)>.03f)error="Player not centered in actual camera: "+view;else checks.Add("Actual camera including PixelPerfectCamera centers player");
 if(board.Graph.center==center)error="Navigation area did not follow player";else checks.Add("ProceduralGridMover moved navigation area with player");
 int mismatch=0;board.Graph.GetNodes(n=>{if(n.Walkable==board.Blocked(NeoGridBoard.Cell((UnityEngine.Vector3)n.position)))mismatch++;});
 if(mismatch!=0)error="Node/terrain mismatch after moving grid: "+mismatch;else checks.Add("All moved graph nodes match ground, walls and rocks");
 bool wallTest=false;foreach(var cell in previous.Keys){foreach(var d in dirs){if(board.walls==null||!board.walls.HasTile((UnityEngine.Vector3Int)(cell+d)))continue;human.Teleport(NeoGridBoard.Center(cell));if(human.Step(d))error="Wall accepted";else checks.Add("Wall still blocks direct movement");wallTest=true;break;}if(wallTest)break;}
 var off=board.TerrainBounds.min-new UnityEngine.Vector3Int(3,3,0);if(!board.StaticBlocked(new UnityEngine.Vector2Int(off.x,off.y)))error="Empty ground accepted";else checks.Add("Empty ground remains blocked");
 human.Teleport(NeoGridBoard.Center(end));yield return null;
 if(room!=null){typeof(NeoFirstRoom).GetMethod("GrantMachop",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(room,new object[]{true});room.machop.RestoreEnergy();control.SelectPartnerByIndex(0);yield return null;yield return null;if(control.mainCamera.target!=room.machop.transform||board.navigationTarget!=room.machop.transform)error="Camera/navigation did not switch with active character";else checks.Add("Control change retargets camera and moving navigation");control.ReturnControlToHuman();}
 control.enabled=true;
 string scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
 System.IO.File.WriteAllText("Temp/"+scene+"-camera-movement-validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{scene,checks,error,passed=error==null,start=start.ToString(),end=end.ToString(),graphCenter=board.Graph.center.ToString(),view=view.ToString()},Newtonsoft.Json.Formatting.Indented));
 UnityEngine.ScreenCapture.CaptureScreenshot("Temp/"+scene+"-camera-follow.png");
}
human.StartCoroutine(Verify());return "Scene integration validation started";


