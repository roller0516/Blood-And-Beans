using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
public static class CheckLuigiReuse {
 public static async Task<string> Main(){
  var manager=UIManager.Instance;manager.PopAllPopups();await manager.LoadAsync<UIRoomListScreen>();var screen=manager.PushScreen<UIRoomListScreen>();int selected=-1;screen.Bind(null,null,null,null,i=>selected=i,4);
  var rooms=new[]{new LobbyRoom(1,1,"달빛 카페",6,8),new LobbyRoom(2,2,"유령의 찻잔",3,8),new LobbyRoom(3,3,"밤의 원두",4,8),new LobbyRoom(4,4,"안개 등대",2,8)};
  screen.Render("",rooms,0);var root=(RectTransform)typeof(UIRoomListScreen).GetField("rows",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(screen);
  var first=root.GetComponentsInChildren<UIRoomListRow>(true).Select(x=>x.GetEntityId()).ToArray();screen.Render("",rooms.Take(2).ToArray(),1);var active=root.GetComponentsInChildren<UIRoomListRow>().Length;screen.Render("",rooms,2);var second=root.GetComponentsInChildren<UIRoomListRow>(true).Select(x=>x.GetEntityId()).ToArray();
  root.GetComponentsInChildren<UIRoomListRow>()[2].GetComponent<Button>().onClick.Invoke();
  if(first.Length!=4||active!=2||!first.SequenceEqual(second)||selected!=2)throw new Exception("Room row reuse or callback failed");
  return "Room rows 4 → 2 → 4 retain identical instances; hidden extras return; row 3 invokes local selection index 2. No lobby/network action.";
 }
}
