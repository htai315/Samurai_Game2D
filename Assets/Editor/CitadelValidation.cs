using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class CitadelValidation
{
    private static int smokeFrames;
    static CitadelValidation() { EditorApplication.update += DirectTick; }
    public static void DirectPlay()
    {
        PlayerSettings.companyName="SamuraiRepairValidation";
        PlayerSettings.productName="Citadel-"+Guid.NewGuid().ToString("N");
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(CitadelMapBuilder.ScenePath);
        SessionState.SetBool("Citadel.Direct",true);
        SessionState.SetFloat("Citadel.Deadline",(float)EditorApplication.timeSinceStartup+60);
        EditorApplication.EnterPlaymode();
    }
    private static void DirectTick()
    {
        if(!SessionState.GetBool("Citadel.Direct",false)) return;
        try
        {
            Check(EditorApplication.timeSinceStartup<SessionState.GetFloat("Citadel.Deadline",0),"direct-play timeout");
            if(!EditorApplication.isPlaying || ++smokeFrames<30) return;
            var player=Object.FindFirstObjectByType<PlayerController1>();
            Check(player && !player.IsDead && Mathf.Abs(player.transform.position.x-4)<.1f,"direct scene spawn A");
            Check(Camera.main && PersistentRoot.Instance && LevelRespawnManager.Instance,"direct scene camera/root/respawn");
            Check(Object.FindObjectsByType<PersistentRoot>(FindObjectsSortMode.None).Length==1,"direct scene one persistent root");
            SessionState.SetBool("Citadel.Direct",false);
            Debug.Log("[CitadelValidation] DIRECT PLAY PASSED"); EditorApplication.Exit(0);
        }
        catch(Exception e) { SessionState.SetBool("Citadel.Direct",false); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    private static Action<Exception> finish;
    private static int step, score;
    private static double deadline;
    private static PersistentRoot root;
    private static SaveData saved;
    public static void Begin(Action<Exception> completion)
    {
        finish=completion; step=0; deadline=EditorApplication.timeSinceStartup+90;
        EditorApplication.update+=Tick;
        SceneTransition.TryLoad("LV5-Scene5");
    }
    private static void Tick()
    {
        try
        {
            Check(EditorApplication.timeSinceStartup<deadline,"timeout");
            if(SceneTransition.IsBusy) return;
            var player=Object.FindFirstObjectByType<PlayerController1>();
            if(player) { player.GetComponent<Rigidbody2D>().simulated=false; player.GetComponent<PlayerOutOfBoundsKiller>().enabled=false; }
            foreach(var e in Object.FindObjectsByType<GhostController>(FindObjectsSortMode.None)) e.enabled=false;
            if(step==0)
            {
                root=PersistentRoot.Instance; score=Object.FindFirstObjectByType<GameManager>().Score;
                Object.FindFirstObjectByType<EndItemGate>().SendMessage("OnTriggerEnter2D",player.GetComponent<Collider2D>());
                Check(SceneTransition.IsBusy,"real LV5 gate accepted"); step=1; return;
            }
            if(step==1)
            {
                Check(SceneManager.GetActiveScene().name=="LV6-MoonlitCitadel" && PersistentRoot.Instance==root,"LV5 retains root into LV6");
                Check(Object.FindFirstObjectByType<GameManager>().Score==score,"coins retained at nonterminal gate");
                var level=Object.FindFirstObjectByType<CitadelLevel>();
                Check(level && level.checkpoints.Length==5 && !level.exitTrigger.enabled,"five checkpoints and locked boss exit");
                var routes=Object.FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None).Where(c=>c.name.StartsWith("Route ")).OrderBy(c=>c.bounds.min.x).ToArray();
                Check(routes.Length==30,"30 route segments");
                for(int i=1;i<routes.Length;i++)
                {
                    float gap=routes[i].bounds.min.x-routes[i-1].bounds.max.x;
                    float rise=routes[i].bounds.max.y-routes[i-1].bounds.max.y;
                    Check(gap<=3.01f && rise<=2.01f,"route jump envelope "+i);
                }
                var cp=level.checkpoints[2]; cp.SendMessage("OnTriggerEnter2D",player.GetComponent<Collider2D>());
                Check(level.ActiveCheckpoint==2 && SaveRuntime.IsCollected(cp.SaveId),"beacon activation recorded");
                saved=player.GetComponent<PlayerSaveBridge>().Capture();
                Check(SaveSystem.Save(saved),"new scene save validates");
                SceneTransition.TryLoad("LV6-MoonlitCitadel",()=>SaveRuntime.QueueLoad(SaveSystem.Load()));
                step=2; return;
            }
            if(step==2)
            {
                var level=Object.FindFirstObjectByType<CitadelLevel>();
                Check(level.ActiveCheckpoint==2,"Continue restores checkpoint");
                player.SetDead(true); player.GetComponent<PlayerHealth>().SetCurrentHealth(0);
                LevelRespawnManager.Instance.RespawnPlayer();
                Check(!player.IsDead && Vector2.Distance(player.transform.position,level.checkpoints[2].respawnPoint.position)<.05f,"respawn uses restored checkpoint");
                RunSession.Instance.DoPlayAgain(); step=3; return;
            }
            if(step==3)
            {
                var level=Object.FindFirstObjectByType<CitadelLevel>();
                Check(level.ActiveCheckpoint==-1 && !level.exitTrigger.enabled,"PlayAgain resets beacons/boss gate");
                level.boss.TakeDamage(int.MaxValue); step=4; return;
            }
            if(step==4)
            {
                var level=Object.FindFirstObjectByType<CitadelLevel>();
                if(!level.exitTrigger.enabled) return;
                level.exitTrigger.GetComponent<EndItemGate>().SendMessage("OnTriggerEnter2D",player.GetComponent<Collider2D>());
                Check(SceneTransition.IsBusy,"boss unlocks real exit"); step=5; return;
            }
            Check(SceneManager.GetActiveScene().name=="Ending" && !PersistentRoot.Instance,"LV6 ends and releases root");
            Debug.Log("[CitadelValidation] ALL CHECKS PASSED: LV5->LV6 carry-over, route envelope, checkpoint, Save/Continue, respawn, PlayAgain, boss->Ending");
            Complete(null);
        }
        catch(Exception e) { Complete(e); }
    }
    private static void Check(bool result,string name) { if(!result) throw new Exception("[CitadelValidation] FAILED: "+name); }
    private static void Complete(Exception e) { EditorApplication.update-=Tick; finish(e); }
}
