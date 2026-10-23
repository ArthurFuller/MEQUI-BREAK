using System;using System.IO;using System.Reflection;using System.Linq;using DG.Tweening;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.UI;
public static class CustomizationWaveValidation {
static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
static void Set(object o,string n,object v)=>o.GetType().GetField(n,flags).SetValue(o,v);
static object Get(object o,string n)=>o.GetType().GetField(n,flags).GetValue(o);
static void Call(object o,string n)=>o.GetType().GetMethod(n,flags).Invoke(o,null);
static int checks;static void Check(bool b,string m){if(!b)throw new Exception(m);checks++;}
public static void Run(){try{
EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
DOTween.Init();
var canvas=new GameObject("Canvas",typeof(RectTransform),typeof(Canvas));canvas.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
var content=new GameObject("Content",typeof(RectTransform),typeof(GridLayoutGroup));content.transform.SetParent(canvas.transform,false);var rect=content.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(960,1500);var grid=content.GetComponent<GridLayoutGroup>();grid.cellSize=new Vector2(220,211.14f);grid.spacing=new Vector2(15.6f,16);grid.constraint=GridLayoutGroup.Constraint.FixedColumnCount;grid.constraintCount=4;
Func<string,int,GameObject[]> make=(prefix,count)=>Enumerable.Range(0,count).Select(i=>{var o=new GameObject(prefix+i,typeof(RectTransform),typeof(Image),typeof(Button),typeof(CanvasGroup),typeof(AvatarOptionButton));o.transform.SetParent(content.transform,false);return o;}).ToArray();
var fitter=content.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
var hats=make("Hat",22);var faces=make("Face",12);var colors=make("Color",12);
var controller=new GameObject("Controller").AddComponent<CustomizationController>();Set(controller,"hatOptions",hats);Set(controller,"faceOptions",faces);Set(controller,"colorOptions",colors);Call(controller,"CacheOptionButtons");Call(controller,"CacheOptionWaveTargets");Call(controller,"RefreshVisibleOptions");Canvas.ForceUpdateCanvases();
Action<Action,GameObject[]> validate=(select,items)=>{
select();var seq=(Sequence)Get(controller,"_optionWaveSequence");Check(seq!=null,"Wave missing");
var first=items[0].GetComponent<RectTransform>();var start=first.anchoredPosition;Check(items[0].GetComponent<CanvasGroup>().alpha==0,"First frame visible");
seq.Goto(.08f,false);var moving=first.anchoredPosition;var contentHeight=rect.rect.height;Check(moving.y>start.y&&moving.y<start.y+80,"Movement missing");
LayoutRebuilder.MarkLayoutForRebuild(rect);Canvas.ForceUpdateCanvases();Check(Vector2.Distance(first.anchoredPosition,moving)<.01f,"Grid overwrote the wave during a layout rebuild");
Check(Mathf.Approximately(rect.rect.height,contentHeight),"Content size changed during wave");
seq.Complete(true);Check(fitter.enabled,"Fitter not restored");Check(grid.enabled,"Grid not restored after completion");Check(items.All(o=>Mathf.Approximately(o.GetComponent<CanvasGroup>().alpha,1)),"Invisible slot after completion");
Canvas.ForceUpdateCanvases();Check(Vector2.Distance(first.anchoredPosition,start+Vector2.up*80)<.01f,"Final position drift");};
validate(controller.SelectHat,hats);validate(controller.SelectFace,faces);validate(controller.SelectColor,colors);validate(controller.SelectHat,hats);validate(controller.SelectHat,hats);
for(int i=0;i<15;i++) {controller.SelectHat();((Sequence)Get(controller,"_optionWaveSequence")).Goto(.06f,false);controller.SelectFace();((Sequence)Get(controller,"_optionWaveSequence")).Goto(.09f,false);controller.SelectColor();((Sequence)Get(controller,"_optionWaveSequence")).Goto(.04f,false);}
validate(controller.SelectHat,hats);controller.SelectColor();controller.GetType().GetMethod("KillOptionWave",flags).Invoke(controller,new object[]{true});Check(grid.enabled,"Grid not restored after cancellation");
}catch(Exception e){Debug.LogException(e);File.WriteAllText("wave-result.txt","FAIL: "+e);EditorApplication.Exit(1);return;}
File.WriteAllText("wave-result.txt","PASS: "+checks+" checks; all categories, repeated tab, 45 rapid switches, layout rebuilds, final positions.");EditorApplication.Exit(0);}
}