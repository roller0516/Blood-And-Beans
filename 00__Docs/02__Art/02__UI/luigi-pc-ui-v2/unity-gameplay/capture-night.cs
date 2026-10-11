using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
public static class GalleryNightCapture {
 public static string Main(string destination) {
  if(!EditorApplication.isPlaying || !EditorApplication.isPaused) throw new InvalidOperationException("Paused actual gameplay required.");
  var camera=Camera.main; var director=MatchDirector.Instance;var team=PlayerTeam.Local();
  var player=Unity.Netcode.NetworkManager.Singleton.LocalClient.PlayerObject;
  var bag=player.GetComponent<PlayerInventory>();
  Directory.CreateDirectory(destination);
  var metadata=new {capturedAt=DateTimeOffset.UtcNow.ToString("o"),source="Actual Battle_01 Play Mode, existing Main Camera, UI canvases temporarily disabled for base capture",width=1920,height=1080,scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
   camera=new{name=camera.name,position=new[]{camera.transform.position.x,camera.transform.position.y,camera.transform.position.z},rotation=new[]{camera.transform.eulerAngles.x,camera.transform.eulerAngles.y,camera.transform.eulerAngles.z},fov=camera.fieldOfView,aspect=camera.aspect},
   player=new{position=new[]{player.transform.position.x,player.transform.position.y,player.transform.position.z}},team,day=director.Phase.Day,remaining=director.Phase.Remaining,phase=director.Phase.Current.ToString(),revenue=director.Board.RevenueOf(team),carried=bag.Carried,capacity=bag.Capacity,hasBag=bag.HasBag,bagVisible=bag.BagVisible};
  var canvases=UnityEngine.Object.FindObjectsByType<Canvas>().Where(c=>c.enabled).ToArray();
  var target=camera.targetTexture;var active=RenderTexture.active;var aspect=camera.aspect;
  var rt=new RenderTexture(1920,1080,24);Texture2D texture=null;
  try {foreach(var c in canvases)c.enabled=false;camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
   texture=new Texture2D(1920,1080,TextureFormat.RGBA32,false);texture.ReadPixels(new Rect(0,0,1920,1080),0,0);texture.Apply();File.WriteAllBytes(Path.Combine(destination,"night-base.png"),ImageConversion.EncodeToPNG(texture));
  } finally {camera.targetTexture=target;camera.aspect=aspect;RenderTexture.active=active;foreach(var c in canvases)if(c!=null)c.enabled=true;if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);}
  File.WriteAllText(Path.Combine(destination,"night-capture.json"),Newtonsoft.Json.JsonConvert.SerializeObject(metadata,Newtonsoft.Json.Formatting.Indented));
  return Newtonsoft.Json.JsonConvert.SerializeObject(new{destination,cameraRestored=camera.targetTexture==target,canvasesRestored=canvases.All(c=>c==null||c.enabled),sceneDirty=UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty});
 }
}
