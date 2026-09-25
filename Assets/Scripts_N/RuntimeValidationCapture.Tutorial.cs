using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

// Synthetic input exercises Unity's input path. Physical device/visual sign-off stays separate.
public sealed partial class RuntimeValidationCapture
{
    private static T Field<T>(object owner, string name) => (T)owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private static Rect ScreenRect(RectTransform rect)
    {
        var corners=new Vector3[4]; rect.GetWorldCorners(corners);
        return Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
    }
    private IEnumerator ValidateTutorial()
    {
        Check(WelcomeScreenRuntime.IsShowing,"Welcome screen first-launch path");
        WelcomeScreenRuntime.Instance?.Begin();yield return new WaitForSecondsRealtime(1f);
        var tour=TutorialRuntime.Instance;
        Check(tour!=null && tour.IsRunning && Field<int>(tour,"index")==0,"Tutorial starts from welcome screen");
        if(tour==null)yield break;
        PlantProcessSimulator.Instance.Pause();
        var inputBefore=JsonUtility.ToJson(PlantProcessSimulator.Instance.CurrentInputs);
        Button next=tour.GetComponentsInChildren<Button>(true).First(b=>b.name=="Next Step");
        Button prev=tour.GetComponentsInChildren<Button>(true).First(b=>b.name=="Previous Step");
        Button skip=tour.GetComponentsInChildren<Button>(true).First(b=>b.name=="Skip Tutorial");
        Check(!prev.interactable,"Tutorial PREVIOUS disabled on first step");
        next.onClick.Invoke(); Check(Field<int>(tour,"index")==1,"Tutorial NEXT");
        prev.onClick.Invoke(); Check(Field<int>(tour,"index")==0,"Tutorial PREVIOUS");
        skip.onClick.Invoke(); yield return new WaitForSecondsRealtime(.4f);
        Check(!tour.IsRunning,"Tutorial SKIP");
        NamedButton("Help")?.onClick.Invoke(); yield return new WaitForSecondsRealtime(.4f);
        Check(NamedButton("Start Tutorial")?.gameObject.activeInHierarchy==true,"HELP offers START TUTORIAL");
        NamedButton("Start Tutorial")?.onClick.Invoke(); yield return new WaitForSecondsRealtime(.5f);
        Check(tour.IsRunning && Field<int>(tour,"index")==0,"HELP tutorial replay");
        Check(!Field<GameObject>(IcodosDashboardRuntime.Instance,"helpPanel").activeSelf,"Replay clears help modal state");
        for(int i=0;i<Field<System.Collections.IList>(tour,"steps").Count;i++)
        {
            Check(Field<int>(tour,"index")==i,"Tutorial step index "+i);
            yield return new WaitForSecondsRealtime(.5f);
            var target=Field<RectTransform>(tour,"currentTarget");
            var step=Field<System.Collections.IList>(tour,"steps")[i];
            string targetName=(string)step.GetType().GetField("TargetName").GetValue(step);
            string stepTitle=(string)step.GetType().GetField("Title").GetValue(step);
            if(!string.IsNullOrEmpty(targetName))Check(target!=null,"Tutorial target resolved "+i);
            var card=Field<RectTransform>(tour,"card");
            Rect bounds=ScreenRect(card);
            Check(bounds.xMin>=-1 && bounds.yMin>=-1 && bounds.xMax<=Screen.width+1 && bounds.yMax<=Screen.height+1,"Tutorial card inside viewport "+i);
            var body=Field<Text>(tour,"cardBody");
            Check(body.preferredHeight<=body.rectTransform.rect.height+1,"Tutorial body fits card "+i);
            if(stepTitle=="The 3D plant and the camera")yield return ValidateTutorialMouse(tour);
            if(stepTitle=="Process map")ValidateProcessLayout();
            if(stepTitle=="Safety and efficiency warnings")
            {
                var arrow=Field<GameObject>(tour,"arrowRoot");
                Check(arrow.activeInHierarchy,"Warning-band arrow visible");
                Check(target!=null && target.name=="Warning Panel","Warning arrow targets actual warning strip");
                var mesh=arrow.GetComponentInChildren<TutorialArrowHead>().canvasRenderer.GetMesh();
                Check(mesh!=null && mesh.vertexCount>=3,"Warning arrowhead emits triangle mesh");
            }
            yield return CaptureEvidence("tutorial-"+i.ToString("D2"));
            next.onClick.Invoke();
        }
        yield return new WaitForSecondsRealtime(.4f);
        Check(!tour.IsRunning,"Tutorial FINISH");
        Check(!Field<bool>(IcodosDashboardRuntime.Instance,"analyticsWindowOpen"),"Tutorial restores closed analytics");
        Check(inputBefore==JsonUtility.ToJson(PlantProcessSimulator.Instance.CurrentInputs),"Tutorial leaves all process inputs unchanged");
        Check(!PlantProcessSimulator.Instance.IsRunning,"Tutorial preserves paused state");
        IcodosDashboardRuntime.Instance.TutorialSetAnalyticsView(true,"ofat");yield return null;
        yield return ValidateAutomaticOfat();
        IcodosDashboardRuntime.Instance.TutorialSetAnalyticsView(false,null);
        PlantProcessSimulator.Instance.Play();
        validationReport.AppendLine("Tutorial input: synthetic Unity Input System mouse/keyboard events, not physical hardware sign-off.");
    }
    private IEnumerator ValidateAutomaticOfat()
    {
        var dashboard=IcodosDashboardRuntime.Instance;
        Check(Field<bool>(dashboard,"analyticsWindowOpen") && FindAnyObjectByType<ExternalAnalyticsWindow>().IsOpen,"Analytics native window opens");
        var graph=FindAnyObjectByType<OfatTimelineGraphRuntime>();
        Check(graph!=null,"Current automatic OFAT component exists");
        if(graph==null)yield break;
        var sim=PlantProcessSimulator.Instance;
        var before=JsonUtility.ToJson(sim.CurrentInputs);
        var sliders=FindObjectsByType<Slider>(FindObjectsInactive.Include);
        var sliderValues=sliders.Select(x=>x.value).ToArray();
        var variables=Field<Button[]>(graph,"varButtons");
        var responses=Field<Button[]>(graph,"respButtons");
        var modes=Field<Button[]>(graph,"experimentModeButtons");
        modes[1].onClick.Invoke();
        for(int variable=1;variable<=5;variable++)
        for(int response=0;response<3;response++)
        {
            variables[variable].onClick.Invoke();responses[response].onClick.Invoke();yield return null;
            var points=Field<System.Collections.IList>(graph,"sweepPoints");
            Check(points.Count==13,"Automatic OFAT 13 points factor="+variable+" response="+response);
            Check(before==JsonUtility.ToJson(Field<PlantProcessSimulator.ProcessInputs>(graph,"sweepBaselineInputs")),"OFAT captures current inputs factor="+variable+" response="+response);
            for(int n=0;n<points.Count;n++)
            {
                var point=points[n];float x=(float)point.GetType().GetField("X").GetValue(point);
                float y=(float)point.GetType().GetField("Y").GetValue(point);
                var hypothetical=sim.CurrentInputs;
                switch(variable){case 1:hypothetical.temperature=x;break;case 2:hypothetical.pressure=x;break;case 3:hypothetical.ratio=x;break;case 4:hypothetical.ghsv=x;break;case 5:hypothetical.reactorFeedFlow=x;break;}
                var expected=sim.Simulate(hypothetical);
                float expectedY=response==0?expected.reactorYieldPercent:response==1?expected.overallEfficiencyPercent:expected.methanolProductionKgH;
                Check(Mathf.Abs(y-expectedY)<.0001f,"OFAT Simulate single-factor oracle factor="+variable+" response="+response+" point="+n);
            }
            Check(before==JsonUtility.ToJson(sim.CurrentInputs),"Automatic OFAT live-state nonmutation factor="+variable+" response="+response);
            Check(sliders.Select((x,n)=>Mathf.Abs(x.value-sliderValues[n])<.0001f).All(x=>x),"Automatic OFAT slider nonmutation factor="+variable+" response="+response);
            if(response==2)yield return CaptureEvidence("ofat-"+variable);
        }
        variables[1].onClick.Invoke();responses[0].onClick.Invoke();
        var oldPoints=Field<System.Collections.IList>(graph,"sweepPoints").Cast<object>().Select(x=>(float)x.GetType().GetField("Y").GetValue(x)).ToArray();
        float oldPressure=sim.CurrentInputs.pressure;
        sim.SetReactorPressure(oldPressure<80?100:40);
        var changed=JsonUtility.ToJson(sim.CurrentInputs);
        variables[1].onClick.Invoke();yield return null;
        var newPoints=Field<System.Collections.IList>(graph,"sweepPoints").Cast<object>().Select(x=>(float)x.GetType().GetField("Y").GetValue(x)).ToArray();
        Check(oldPoints.Zip(newPoints,(x,y)=>Mathf.Abs(x-y)).Any(x=>x>.001f),"Temperature sweep responds to changed held pressure");
        Check(changed==JsonUtility.ToJson(sim.CurrentInputs),"Changed-baseline sweep preserves live inputs");
        sim.SetReactorPressure(oldPressure);
        modes[0].onClick.Invoke();variables[0].onClick.Invoke();responses[0].onClick.Invoke();
        Check(before==JsonUtility.ToJson(sim.CurrentInputs),"OFAT restores original operating point after baseline test");
    }
    private IEnumerator ValidateDaylightIntegration()
    {
        var sim=PlantProcessSimulator.Instance;
        var panels=FindAnyObjectByType<InteractiveModulePanelRuntime>();
        var dashboard=IcodosDashboardRuntime.Instance;
        NamedButton("OVERVIEW")?.onClick.Invoke();
        panels.CloseAllPanels();yield return null;Canvas.ForceUpdateCanvases();
        var tiles=Field<RectTransform[]>(dashboard,"processTiles");
        var original=tiles.Select(ScreenRect).ToArray();
        foreach(string module in new[]{"electrolyzer","absorber","desorber","compressor","reactor","heatexchanger","condenser","separator","distillation","storage","co2tank","h2tank"})
        {
            panels.OpenModule(module,false);yield return null;Canvas.ForceUpdateCanvases();
            var drawer=panels.GetComponentsInChildren<RectTransform>().FirstOrDefault(x=>x.name.EndsWith(" Interactive Panel") && x.gameObject.activeInHierarchy);
            Check(drawer!=null,"Module drawer opens "+module);
            if(drawer!=null)foreach(var tile in tiles)Check(!ScreenRect(drawer).Overlaps(ScreenRect(tile)),"Drawer/KPI no overlap "+module+" / "+tile.name);
            foreach(var tile in tiles)
            foreach(var value in tile.GetComponentsInChildren<UIValueText>())
            {
                float width=value.Value.preferredWidth+value.Unit.preferredWidth+value.GetComponent<HorizontalLayoutGroup>().spacing;
                Check(width<=((RectTransform)value.transform).rect.width+1f,"Compact KPI value/unit fits column "+module+" / "+tile.name+" / "+value.transform.parent.name);
            }
            if(module=="reactor")yield return CaptureEvidence("daylight-reactor-drawer");
            panels.CloseAllPanels();yield return null;Canvas.ForceUpdateCanvases();
            Check(tiles.Select((x,n)=>ScreenRect(x)==original[n]).All(x=>x),"Closing drawer restores KPI layout "+module);
        }
        var graph=FindObjectsByType<OfatTimelineGraphRuntime>(FindObjectsInactive.Include).First();
        dashboard.TutorialSetAnalyticsView(true,"ofat");yield return null;
        graph.SetSubTabVisible(true);
        var modes=Field<Button[]>(graph,"experimentModeButtons");var variables=Field<Button[]>(graph,"varButtons");
        modes[0].onClick.Invoke();panels.OpenModule("reactor",false);
        string[] sliderNames={"Temp Slider","Pressure Slider","H2/CO2 Slider","GHSV Slider","Feed flow Slider"};
        var reactorSliders=sliderNames.Select(n=>FindObjectsByType<Slider>(FindObjectsInactive.Include).First(x=>x.name==n)).ToArray();
        for(int v=1;v<=5;v++)
        {
            variables[v].onClick.Invoke();
            Check(reactorSliders.Select((x,n)=>x.interactable==(n==v-1)).All(x=>x),"Interactive only selected reactor variable adjustable "+v);
        }
        variables[1].onClick.Invoke();
        var responses=Field<Button[]>(graph,"respButtons");
        for(int response=0;response<3;response++)
        {
            responses[response].onClick.Invoke();sim.Play();
            int count=Field<System.Collections.IList>(graph,"samples").Count;
            yield return new WaitForSecondsRealtime(.7f);
            var samples=Field<System.Collections.IList>(graph,"samples");
            Check(samples.Count>count,"Interactive timeline records live response "+response);
        }
        yield return new WaitForSecondsRealtime(.3f);
        var captions=Field<RectTransform>(graph,"epochLayer").GetComponentsInChildren<Text>().Where(x=>x.name=="Epoch Label").ToArray();
        Check(captions.Length>=5,"Interactive timeline retains simultaneous variable captions");
        for(int i=0;i<captions.Length;i++)
        {
            Check(captions[i].preferredWidth<=captions[i].rectTransform.rect.width+1f,"Epoch caption text fits "+i);
            for(int j=i+1;j<captions.Length;j++)Check(!ScreenRect(captions[i].rectTransform).Overlaps(ScreenRect(captions[j].rectTransform)),"Epoch captions do not overlap "+i+" / "+j);
        }
        yield return CaptureEvidence("interactive-ofat-layout");
        variables[0].onClick.Invoke();graph.SetSubTabVisible(false);dashboard.TutorialSetAnalyticsView(false,null);panels.CloseAllPanels();
        NamedButton("REACTOR LAB")?.onClick.Invoke();yield return null;Canvas.ForceUpdateCanvases();
        var reactor=FindObjectsByType<RectTransform>(FindObjectsInactive.Include).First(x=>x.name=="Reactor Lab");
        var description=reactor.Find("Body").GetComponent<Text>();
        var focus=reactor.Find("FOCUS REACTOR").GetComponent<RectTransform>();
        Check(description.preferredHeight<=description.rectTransform.rect.height+1f,"Reactor Lab paragraph fits content height");
        Check(!ScreenRect(description.rectTransform).Overlaps(ScreenRect(focus)),"Reactor Lab paragraph clears Focus reactor button");
        yield return CaptureEvidence("reactor-description-layout");
        NamedButton("OVERVIEW")?.onClick.Invoke();
        NamedButton("Show Streams")?.onClick.Invoke();
        var flows=FindObjectsByType<PipeFlowAnimator>().Where(x=>x.isFlowing).ToArray();
        Check(flows.Length>=35,"Continuous active flow coverage");
        Check(flows.All(x=>x.useSharedProcessClock && Mathf.Abs(x.flowDirection.magnitude-1)<.001f),"Continuous flow shared clock and normalized directions");
        Check(flows.All(x=>x.flowColor==PlantStreamLegend.ColorFor(x.flowKind)),"Flow stream colors match legend");
        var offsets=flows.Select(x=>Field<float>(x,"offset")).ToArray();yield return new WaitForSecondsRealtime(.5f);
        Check(flows.Where((x,n)=>Mathf.Abs(Field<float>(x,"offset")-offsets[n])>.0001f).Any(),"Continuous flow offsets advance");
        var probe=MassFlowProbeRuntime.Instance;Check(probe!=null,"Mass-flow probe exists");
        if(probe!=null)
        {
            probe.SetActive(true);
            typeof(MassFlowProbeRuntime).GetMethod("ShowReadout",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(probe,new object[]{PlantFlowKind.Hydrogen,"validation H2",new Vector2(Screen.width*.45f,Screen.height*.5f)});
            Check(Field<GameObject>(probe,"readout").activeSelf,"Mass-flow probe readout opens");
            Check(Mathf.Abs(PipeStreamState.Evaluate(PlantFlowKind.Hydrogen,sim.Current).MassFlowKgH-sim.Current.h2InputKgH)<.001f,"Mass-flow probe hydrogen uses current simulator");
            Check(Mathf.Abs(PipeStreamState.Evaluate(PlantFlowKind.RecycleGas,sim.Current).MassFlowKgH-sim.Current.recycleGasKgH)<.001f,"Mass-flow probe recycle uses current simulator");
            Check(Mathf.Abs(PipeStreamState.Evaluate(PlantFlowKind.MethanolProduct,sim.Current).MassFlowKgH-sim.Current.methanolProductionKgH)<.001f,"Mass-flow probe product uses current simulator");
            probe.SetActive(false);
        }
        yield return CaptureEvidence("daylight-overview");
    }
    private void ValidateProcessLayout()
    {
        var panel=FindObjectsByType<RectTransform>(FindObjectsInactive.Include).First(t=>t.name=="Guided Process");
        string[] names={"Card Title","Step","Process Title","Explanation","Streams","Previous Step","Next Step"};
        var rects=names.Select(n=>panel.Find(n).GetComponent<RectTransform>()).ToArray();
        for(int i=0;i<rects.Length;i++)
        {
            Check(rects[i].rect.width>0 && rects[i].rect.height>0,"Process Map positive layout "+names[i]);
            for(int j=i+1;j<rects.Length;j++)Check(!ScreenRect(rects[i]).Overlaps(ScreenRect(rects[j])),"Process Map no overlap "+names[i]+" / "+names[j]);
        }
    }
    private IEnumerator ValidateTutorialMouse(TutorialRuntime tour)
    {
        Check(!Field<Image>(tour,"blockerImage").raycastTarget,"Camera step releases spotlight input blocker");
        Vector2 point=new Vector2(Screen.width*.4f,Screen.height*.58f);
        var hits=new List<RaycastResult>();
        EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        Check(hits.Count==0,"Camera tutorial spotlight has no UI raycast obstruction");
        var camera=FindAnyObjectByType<OrbitCameraController>();
        var priorFocusBehavior=InputSystem.settings.backgroundBehavior;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        var mouse=InputSystem.AddDevice<Mouse>(); var keyboard=InputSystem.AddDevice<Keyboard>();
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;yield return null;
        float azimuth=Field<float>(camera,"_azimuth");
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left));yield return new WaitForSecondsRealtime(1.5f);
        azimuth=Field<float>(camera,"_azimuth");
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point+new Vector2(40,10)}.WithButton(MouseButton.Left));yield return null;yield return null;
        Check(Mathf.Abs(Field<float>(camera,"_azimuth")-azimuth)>.01f,"Camera tutorial synthetic mouse drag rotates");
        Vector3 target=Field<Vector3>(camera,"_currentTarget");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftShift));
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point+new Vector2(70,20)}.WithButton(MouseButton.Left));yield return null;yield return null;
        Check(Vector3.Distance(target,Field<Vector3>(camera,"_currentTarget"))>.001f,"Camera tutorial synthetic Shift-drag pans");
        float zoom=camera.useOrthographic ? camera.orthographicSize : camera.distance;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        InputSystem.QueueStateEvent(mouse,new MouseState{position=point,scroll=new Vector2(0,1)});yield return null;yield return null;
        Check(Mathf.Abs((camera.useOrthographic ? camera.orthographicSize : camera.distance)-zoom)>.001f,"Camera tutorial synthetic wheel zooms");
        InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);
        InputSystem.settings.backgroundBehavior=priorFocusBehavior;camera.FocusOverview();
    }
}
