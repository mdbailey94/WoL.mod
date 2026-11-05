$ErrorActionPreference = 'Stop'
$source = Get-Content (Join-Path $PSScriptRoot '../WoLChaosDepths/ChaosDepthsPlugin.cs') -Raw
$reward = $source.Substring($source.IndexOf('        private bool RewardFreshBoss('))
$reward = $reward.Substring(0, $reward.IndexOf('        // Whether a boss other'))
$drop = $source.Substring($source.IndexOf('        private void Drop('))
$drop = $drop.Substring(0, $drop.IndexOf('        // An item reaches'))
$stub = @'
using System;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine {
 public struct Vector3 { public float x; public static Vector3 right => new Vector3 { x=1 }; public static Vector3 operator +(Vector3 a,Vector3 b) => new Vector3{x=a.x+b.x}; public static Vector3 operator *(Vector3 a,float b) => new Vector3{x=a.x*b}; }
 public static class Random { static System.Random rng = new System.Random(1348); public static float value => (float)rng.NextDouble(); public static int Range(int a,int b) => rng.Next(a,b); }
}
public class Transform { public Vector3 position; }
public class Entity { public Transform transform = new Transform(); }
public class Boss : Entity {}
public class MiniBoss : Entity {}
public class Player : Entity { public int playerID; }
public class CharacterSave { public int level = 1; public List<int> finalLegendaryAt = new List<int>(); }
public class Setting<T> { public T Value; public Setting(T v) { Value=v; } }
public static class ProfileSave { public static bool Fresh = true; }
public static class GameController { public static Player[] activePlayers; }
public static class AscensionLink { public static int Level, ItemLevelBonus; public static bool RealmBossDropsLegendary; }
public static class DiveDirector { public static string scene; public static string CurrentScene() => scene; public static bool IsFinalBoss(string s) => s=="FinalBoss"; }
public static class SaveStore { public static void Save(int id,CharacterSave c) {} }
public static class Elites { public static float RarityBoost() => 0; }
public static class Depth { public static int ItemLevelBonus; public static float RarityBoost; }
public static class Layers { public static float RarityBonus; }
public class ItemData { public string rarity, name, unique; public int itemLevel; }
public static class LootTable {
 public static int RarityIndex(string r) => r=="Common"?0:r=="Magic"?1:r=="Legendary"?3:2;
 public static ItemData Roll(int l,int min,float boost,int exact) => new ItemData {itemLevel=l,rarity=(exact>=0?exact:Math.Max(min,2)).ToString()};
 public static string Describe(ItemData i) => i.rarity;
}
public static class Uniques { public static List<string> Defs = new List<string>(); public static void MakeUnique(ItemData i,string d) {} }
public static class Pickups {
 public static bool Physical=true;
 public static List<Tuple<int,ItemData>> items = new List<Tuple<int,ItemData>>();
 public static void Spawn(Player p,ItemData i,Vector3 v) { items.Add(Tuple.Create(p.playerID,i)); }
}
public class RewardHarness {
 public Dictionary<int,CharacterSave> saves = new Dictionary<int,CharacterSave>();
 public Setting<float> floorBossDropChance=new Setting<float>(0), bossDropChance=new Setting<float>(0);
 public Setting<int> floorBossDropRarity=new Setting<int>(1), bossDropRarity=new Setting<int>(2);
 public Setting<bool> realmBossLegendaryAt10=new Setting<bool>(true);
 public bool otherAlive;
 CharacterSave CharacterOf(Player p) { if(!saves.ContainsKey(p.playerID)) saves[p.playerID]=new CharacterSave(); return saves[p.playerID]; }
 bool IsBoss(Entity e) => e is Boss;
 bool OtherBossAlive(Entity e) => otherAlive;
 void Log(string s) {}
 void Collect(Player p,ItemData i,bool left) { Pickups.Spawn(p,i,new Vector3()); }
 public bool Reward(Entity e) => RewardFreshBoss(e);
 public void Ordinary(Player p) => Drop(p,CharacterOf(p),0,new Vector3());
 public static void Require(bool ok,string reason) { if(!ok) throw new Exception(reason); }
 public static string Check() {
  var h=new RewardHarness(); var p0=new Player {playerID=0}; var p1=new Player {playerID=1};
  GameController.activePlayers=new[]{p0,p1}; DiveDirector.scene="Earth";
  h.Reward(new MiniBoss()); Require(Pickups.items.Count==2,"A0 floor boss count");
  Require(Pickups.items[0].Item1==0 && Pickups.items[1].Item1==1,"A0 personal allocation");
  foreach(var x in Pickups.items) Require(x.Item2.rarity=="0","A0 exact Common");
  Pickups.items.Clear(); DiveDirector.scene="FinalBoss"; h.Reward(new Boss());
  Require(Pickups.items.Count==2,"A0 final count"); foreach(var x in Pickups.items) Require(x.Item2.rarity=="1","A0 exact Uncommon");
  Require(h.saves[0].finalLegendaryAt.Count==0,"A0 must not claim A1 legendary");
  Pickups.items.Clear(); AscensionLink.Level=1; h.Reward(new Boss());
  Require(Pickups.items.Count==2,"First A1 guarantees bypass zero chance"); foreach(var x in Pickups.items) Require(x.Item2.rarity=="3","First A1 Legendary");
  Require(h.saves[0].finalLegendaryAt.Contains(1) && h.saves[1].finalLegendaryAt.Contains(1),"Separate save claims");
  Pickups.items.Clear(); h.Reward(new Boss()); Require(Pickups.items.Count==0,"No repeat guarantee");
  AscensionLink.Level=2; h.saves[0].finalLegendaryAt.Add(2); h.Reward(new Boss());
  Require(Pickups.items.Count==1 && Pickups.items[0].Item1==1,"Mixed progress gives missing player their guarantee");
  Pickups.items.Clear(); AscensionLink.Level=3; h.otherAlive=true; h.Reward(new Boss()); Require(Pickups.items.Count==0,"Wait for remaining bosses"); h.otherAlive=false;
  GameController.activePlayers=new[]{p0,null}; AscensionLink.Level=0; h.Reward(new Boss()); Require(Pickups.items.Count==1,"Single player with empty slot");
  Pickups.items.Clear(); GameController.activePlayers=new[]{p0,p1}; h.saves[0].level=4; h.saves[1].level=9;
  for(int i=0;i<10000;i++) h.Ordinary(p0);
  int first=0; foreach(var x in Pickups.items) { if(x.Item1==0) first++; Require(x.Item2.itemLevel==(x.Item1==0?4:9),"Recipient item level"); }
  Require(first>4800 && first<5200,"Random allocation skew");
  return "PASS: actual reward/drop methods: A0 exact rarities, personal guarantees, zero drop chance, repeat and mixed progress, remaining boss, solo, and 10000 random allocations (P1="+first+"). Game objects and loot roll are stubbed; this is not an in-game test.";
 }
'@
Add-Type -TypeDefinition ($stub + "`n" + $reward + "`n" + $drop + "`n}")
[RewardHarness]::Check()
