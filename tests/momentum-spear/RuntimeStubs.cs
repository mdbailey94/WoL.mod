// Explicit simulation only. These types do not load Unity or execute the game.
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace UnityEngine
{
    public class Object
    {
        private static int next; private readonly int id = ++next;
        public bool Destroyed;
        public int GetInstanceID() { return id; }
        public static readonly List<GameObject> Created = new List<GameObject>();
        public static Enemy[] Enemies = new Enemy[0]; public static int Queries;
        public static T[] FindObjectsOfType<T>() { Queries++; if (typeof(T) != typeof(Enemy)) throw new Exception("Unexpected target type"); return (T[])(object)Enemies; }
        public static void Destroy(Object obj)
        {
            if (obj == null) return;
            obj.Destroyed = true;
            GameObject go = obj as GameObject;
            if (!ReferenceEquals(go, null)) { go.activeInHierarchy = false; foreach(var component in go.Components)component.Destroyed=true; foreach (Transform child in go.transform.Children) Destroy(child.gameObject); }
        }
        public static bool operator ==(Object a, Object b) { bool na = ReferenceEquals(a, null) || a.Destroyed, nb = ReferenceEquals(b, null) || b.Destroyed; return na || nb ? na == nb : ReferenceEquals(a, b); }
        public static bool operator !=(Object a, Object b) { return !(a == b); }
        public override bool Equals(object value) { return ReferenceEquals(this, value); }
        public override int GetHashCode() { return id; }
    }
    public class Component : Object { public T[] GetComponentsInChildren<T>(bool includeInactive) where T:class { return gameObject.GetComponentsInChildren<T>(); } public GameObject gameObject; public Transform transform { get { return gameObject.transform; } } }
    public class MonoBehaviour : Component { public bool enabled = true; public bool isActiveAndEnabled { get { return enabled && gameObject.activeInHierarchy; } } public MonoBehaviour() { gameObject = new GameObject(GetType().Name); } }
    public class GameObject : Object
    {
        public string name; public bool activeInHierarchy = true; public readonly Transform transform; public readonly List<Component> Components = new List<Component>();
        public GameObject(string name = "object") { this.name = name; transform = new Transform { gameObject = this }; Object.Created.Add(this); }
        public T[] GetComponentsInChildren<T>() where T:class {var result=new List<T>();foreach(var c in Components)if(c is T)result.Add(c as T);foreach(var child in transform.Children)if(!child.gameObject.Destroyed)result.AddRange(child.gameObject.GetComponentsInChildren<T>());return result.ToArray();}
        public T AddComponent<T>() where T : Component, new() { T c = new T(); c.gameObject = this; Components.Add(c); return c; }
    }
    public class Transform { public Vector3 position; public Vector3 localScale = new Vector3(1,1,1); public readonly List<Transform> Children = new List<Transform>(); public GameObject gameObject; public void SetParent(Transform parent, bool local) { parent.Children.Add(this); } }
    public struct Vector2 { public static Vector2 zero {get{return new Vector2();}} public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } public static implicit operator Vector2(Vector3 v) { return new Vector2(v.x, v.y); } public static Vector2 operator -(Vector2 a, Vector2 b) { return new Vector2(a.x - b.x, a.y - b.y); } }
    public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; } }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; } public static Color clear { get { return new Color(); } } }
    public struct Rect { public float width; public Rect(float x, float y, float w, float h) { width = w; } }
    public static class Mathf { public const float PI = (float)Math.PI; public static float Cos(float a) { return (float)Math.Cos(a); } public static float Sin(float a) { return (float)Math.Sin(a); } public static int Clamp(int x,int a,int b){return Math.Max(a,Math.Min(b,x));} public static float Clamp01(float x){return Clamp(x,0f,1f);} public static float Clamp(float x, float a, float b) { return Math.Max(a, Math.Min(b, x)); } public static bool Approximately(float a, float b) { return Math.Abs(a - b) < 0.00001f; } }
    public enum FilterMode { Point }
    public class Texture2D : Object { public int width; public FilterMode filterMode; public Color[] Pixels; public Texture2D(int w, int h) { width = w; } public void SetPixels(Color[] p) { Pixels = p; } public void Apply() { } }
    public class Sprite : Object { public Rect rect; public float pixelsPerUnit; public static Sprite Create(Texture2D t, Rect r, Vector2 p, float ppu) { return new Sprite { rect = r, pixelsPerUnit = ppu }; } }
    public class Shader : Object { public static bool Available = true; public static Shader Find(string s) { return Available ? new Shader() : null; } }
    public class Material : Object { public Material(Shader s) { } }
    public class LineRenderer : Component
    {
        public static bool Fail; public Material sharedMaterial; public bool useWorldSpace; public int positionCount, sortingOrder, sortingLayerID; public float startWidth, endWidth; public Color startColor, endColor;
        public readonly Dictionary<int, Vector3> Points = new Dictionary<int, Vector3>();
        public void SetPosition(int i, Vector3 p) { if (Fail) throw new Exception("Simulated missing renderer"); if (i < 0 || i >= positionCount) throw new Exception("Invalid line index"); Points[i] = p; }
    }
    public static class Time { public static float time, timeScale = 1f, deltaTime = .016f; public static int frameCount; }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { } public enum LoadSceneMode { Single }
    public static class SceneManager { public static event Action<Scene, LoadSceneMode> sceneLoaded; public static void Load() { if (sceneLoaded != null) sceneLoaded(new Scene(), LoadSceneMode.Single); } }
}
namespace BepInEx.Logging
{
    public class ManualLogSource { public void LogInfo(string s) { } }
}
namespace BepInEx.Configuration
{
    public class ConfigEntry<T> { public T Value; public ConfigEntry(T value) { Value = value; } }
    public class ConfigFile { public readonly Dictionary<string, object> Entries = new Dictionary<string, object>(); public ConfigEntry<T> Bind<T>(string section, string key, T value, string description) { var e = new ConfigEntry<T>(value); Entries[section + "/" + key] = e; return e; } }
}
namespace BepInEx
{
    public class BepInPlugin : Attribute { public BepInPlugin(string id, string name, string version) { } }
    public class BepInDependency : Attribute { public BepInDependency(string id) { } }
    public class TestLogger : Logging.ManualLogSource { public readonly List<string> Warnings = new List<string>(); public void LogWarning(string s) { Warnings.Add(s); } }
    public class BaseUnityPlugin : MonoBehaviour { public readonly Configuration.ConfigFile Config = new Configuration.ConfigFile(); public readonly TestLogger Logger = new TestLogger(); }
}
namespace LegendAPI
{
    public class SkillInfo { public string ID, displayName, description, enhancedDescription; public Sprite icon; public int tier, priceMultiplier; public Type stateType; public SkillStats skillStats; public Func<bool> unlockCondition; }
    public static class Skills { public static SkillInfo Registered; public static void Register(SkillInfo s) { Registered = s; } }
}
public class SkillStats { public string[] ID, elementType, subElementType, targetNames; public int[] damage; public float[] cooldown, knockbackMultiplier, sameAttackImmunityTime; public bool[] shakeCamera; }
public static class IconManager { public static readonly Dictionary<string, Sprite> SkillIcons = new Dictionary<string, Sprite>(); }
public enum VarStatModType { Multiplicative, OverrideWithMods }
public class NumVarStatMod { public string ID; public float Value; public NumVarStatMod(string id, float value, int priority, VarStatModType type) { ID = id; Value = value; } }
public class TestStat
{
    public float BaseValue; public bool FailAdd, FailRemove; public readonly Dictionary<string, NumVarStatMod> Mods = new Dictionary<string, NumVarStatMod>();
    public TestStat(float value) { BaseValue = value; }
    public float CurrentValue { get { float v = BaseValue; foreach (var m in Mods.Values) v *= 1f + m.Value; return v; } }
    public void Modify(NumVarStatMod mod, bool add) { if ((add && FailAdd) || (!add && FailRemove)) throw new Exception("Stat unavailable"); if (add) Mods[mod.ID] = mod; else Mods.Remove(mod.ID); }
}
public class Movement { public Rigidbody2D Body; public void EndMovement(){if(Body!=null)Body.velocity=Vector2.zero;} public TestStat moveSpeedStat = new TestStat(10), dashSpeedStat = new TestStat(20); }
public class AnimSpeedHandler { public TestStat Stat = new TestStat(2); public void Modify(NumVarStatMod m, bool add) { Stat.Modify(m, add); } }
public class Health { public TestStat healthStat = new TestStat(100); }
public class Enemy : MonoBehaviour { public Health health = new Health(); public Movement movement = new Movement(); public AnimSpeedHandler animSpeedHandler = new AnimSpeedHandler(); }
public class Player : MonoBehaviour
{
    public readonly Dictionary<string,SkillState> skillsDict=new Dictionary<string,SkillState>();
    public Player(){movement.Body=rigidbody2D;} public Rigidbody2D rigidbody2D=new Rigidbody2D(); public Transform attackOriginTrans=new Transform(); public float DamageMultiplier=1f; public Health health = new Health(); public bool paused; public string CastAnimStr = "cast"; public Movement movement = new Movement();

    // Mirrors the verified ordering: EndMovement before combo resolution; spawn, combo hook,
    // attack-info initialization and execute event; increment/wrap and destroy on exit.
    public class MeleeAttackState:SkillState
    {
        public bool isBasic,isMeleeSkill,setParent,destroyAttackObjectOnExit,flipObjectForBackhand,alwaysStartForehand,forceMovement;
        public GameObject attackPrefab; public MeleeAttackObject currentAttackObject;
        public int maxComboCount=3,comboCounter=1,currentLevel=1,ExecuteEvents,BaseAttacks; public float EventDamageModifier;
        public float animStartTime,animExecTime,baseCancelThreshold,finalCancelThreshold,baseRunThreshold,finalRunThreshold,baseExitThreshold,finalExitThreshold;
        public float baseAttackScale=1f,penultimateAttackScale=1f,finalAttackScale=1f;
        public Vector3 calcScaleVector=new Vector3(1,1,1); public string atkMoveModStr="MeleeAttackMoveSpeed";public string[] sfxNames;
        public NumVarStatMod attackMoveSpeedMod,finalAttackMoveSpeedMod;public BoolVarStat autoHoldStat=new BoolVarStat();
        public MeleeAttackState(string id,FSM fsm,Player player):base(id,fsm,player){this.fsm=fsm;}
        public override void OnEnter(){base.OnEnter();parent.movement.EndMovement();if(fsm.previousStateName!=fsm.currentStateName)comboCounter=1;currentLevel=comboCounter;parent.movement.moveSpeedStat.Modify(attackMoveSpeedMod,true);}
        public virtual void HandleComboCount(int given){}
        public virtual void ExecuteAttack(){BaseAttacks++;currentAttackObject=new MeleeAttackObject();currentAttackObject.transform.SetParent(parent.attackOriginTrans,false);HandleComboCount(comboCounter);var damage=LegendAPI.Skills.Registered.skillStats.damage;currentAttackObject.attack.BaseDamage=(int)(damage[Math.Min(damage.Length-1,Math.Max(0,currentLevel-1))]*parent.DamageMultiplier);float scale=comboCounter>=maxComboCount?finalAttackScale:comboCounter==maxComboCount-1?penultimateAttackScale:baseAttackScale;currentAttackObject.transform.localScale=new Vector3(calcScaleVector.x*scale,calcScaleVector.y*scale,1);EventDamageModifier=currentAttackObject.attack.damageModifier;ExecuteEvents++;}
        public override void OnExit(){parent.movement.moveSpeedStat.Mods.Remove(atkMoveModStr);comboCounter++;if(comboCounter>maxComboCount)comboCounter=1;if(destroyAttackObjectOnExit&&currentAttackObject!=null)UnityEngine.Object.Destroy(currentAttackObject.gameObject);fsm.previousStateName=fsm.currentStateName;}
    }
    public class SkillState : State<Player>
    {
        public static string staticID;
        public bool IsEmpowered, hasSignatureVariant, applyStopElementStatus; public int BaseEnters, BaseExecutes; public float[] Times;
        public SkillState(string id, FSM fsm, Player player) : base(id,fsm,player) { }
        public virtual string OnEnterAnimStr { get { return parent.CastAnimStr; } }
        protected void SetAnimTimes(params float[] times) { Times = times; }
        public override void OnEnter() { BaseEnters++; } public virtual void ExecuteSkill() { BaseExecutes++; }
    }
}
public interface IState {string name{get;} void OnEnter();void OnExit();}
public class State<T>:IState {public string name{get;private set;}public FSM fsm;public T parent;public State(string name,FSM fsm,T parent){this.name=name;this.fsm=fsm;this.parent=parent;if(fsm!=null)fsm.Register(this);}public virtual void OnEnter(){}public virtual void OnExit(){}}
public class SimRunState:State<Player> {public SimRunState(FSM f,Player p):base("Run",f,p){}public override void OnExit(){parent.movement.EndMovement();}}
public class FSM {
 public string previousStateName="Run",currentStateName="MomentumSpearBasic";public IState currentState;private readonly Dictionary<string,IState> states=new Dictionary<string,IState>();
 public void Register(IState state){states[state.name]=state;}
 public void ChangeState(string target,bool allowSelfTransition=false){
  // Explicit simulated Harmony dispatch, NOT proof of actual runtime patch installation.
  if(Simulation.Patched)WoLMomentumSpear.SpearMomentumCapture.Capture(this,target,allowSelfTransition);
  if(currentState!=null&&currentState.name==target&&!allowSelfTransition)return;
  IState next=states[target];if(currentState!=null)currentState.OnExit();previousStateName=currentStateName;currentState=next;currentStateName=target;currentState.OnEnter();
 }
}
namespace HarmonyLib {
 [AttributeUsage(AttributeTargets.Class)]public class HarmonyPatch:Attribute{public HarmonyPatch(Type t,string n,Type[] args){}}
 [AttributeUsage(AttributeTargets.Method)]public class HarmonyPrefix:Attribute{}
 [AttributeUsage(AttributeTargets.Parameter)]public class HarmonyArgument:Attribute{public HarmonyArgument(int index){}}
 public class Harmony{public Harmony(string id){}public void PatchAll(System.Reflection.Assembly a){Simulation.Patched=true;}public void UnpatchSelf(){Simulation.Patched=false;}}
}

public class Rigidbody2D { public Vector2 velocity; }
public class SpriteRenderer:Component {public bool enabled=true;public int sortingLayerID,sortingOrder;}
public static class Simulation
{
    public static bool Patched;
    public static object Call(object value, string method) { return value.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).Invoke(value, null); }
    public static int Count(object value, string field) { return ((System.Collections.ICollection)value.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value)).Count; }
    public static void Set<T>(WoLMomentumSpear.SpearPlugin p, string key, T value) { ((BepInEx.Configuration.ConfigEntry<T>)p.Config.Entries[key]).Value = value; }
}

public class BoolVarStat {public bool BaseValue;}
public class Attack:Component {public float damageModifier=1f;public int BaseDamage;public int DamageForTarget(){return Math.Max(1,(int)Math.Floor(BaseDamage*(double)damageModifier));}}
public class MeleeAttackObject:MonoBehaviour {public static float PrefabDamageMultiplier=1f;public Attack attack;public MeleeAttackObject(){attack=gameObject.AddComponent<Attack>();attack.damageModifier=PrefabDamageMultiplier;gameObject.AddComponent<SpriteRenderer>();}}
public static class ChaosBundle {public static GameObject Get(string path){return new GameObject(path);}}
