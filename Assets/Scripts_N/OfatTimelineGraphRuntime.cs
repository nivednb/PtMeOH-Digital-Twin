using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Icon = UITheme.Icon;
using Kind = UITheme.ButtonKind;
using W = UITheme.Weight;

/// <summary>
/// Guided OFAT analysis with two modes.
///
/// Interactive mode locks every reactor slider except the selected factor and records the
/// live response over time. Auto-sweep mode snapshots the current operating inputs, varies
/// exactly one selected factor across its educational range, and evaluates every point with
/// PlantProcessSimulator.Simulate() without mutating the running plant.
///
/// Module-change dots are shown in interactive Points mode. The interactive time axis is
/// paginated one minute per page; automatic sweeps render 13 model-calculated points.
/// </summary>
public sealed class OfatTimelineGraphRuntime : MonoBehaviour, IPointerMoveHandler, IPointerExitHandler
{
    public enum Variable { Free = 0, Temperature = 1, Pressure = 2, H2CO2 = 3, GHSV = 4, FeedFlow = 5 }
    public enum Response { Yield = 0, Efficiency = 1, Methanol = 2 }
    public enum ViewMode { Line = 0, Points = 1 }
    public enum ExperimentMode { Interactive = 0, AutoSweep = 1 }

    private const float SampleInterval = 0.5f;
    private const float PageSeconds = 60f;
    private const float RebuildInterval = 0.2f;
    private const int MaxRecords = 20000;
    private const float HoverRadiusPixels = 16f;
    private const float LegendWidth = 172f;
    private const int TickCount = 5;

    private struct VarMeta
    {
        public string Label;
        public string SliderParam;
        public Color Color;
        public Func<PlantProcessSimulator.ProcessSnapshot, float> ReadValue;
    }
    private struct RespMeta { public string Label; public string Unit; public float Max; public Func<PlantProcessSimulator.ProcessSnapshot, float> Select; }
    private struct Sample { public float T; public float Y; public Variable Var; public float VarValue; }
    private struct Epoch { public float T; public Variable Var; public float VarValue; }
    private struct ChangePoint { public float T; public float Y; public string Module; public string Parameter; public float From; public float To; }
    private struct SweepPoint { public float X; public float Y; }

    private static readonly Dictionary<Variable, VarMeta> Vars = new Dictionary<Variable, VarMeta>
    {
        [Variable.Free]        = new VarMeta { Label = "Free",          SliderParam = null,        Color = UITheme.Hex("64748B"), ReadValue = null },
        [Variable.Temperature] = new VarMeta { Label = "Temperature",   SliderParam = "Temp",      Color = UITheme.Hex("EA580C"), ReadValue = s => s.reactorTemperatureC },
        [Variable.Pressure]    = new VarMeta { Label = "Pressure",      SliderParam = "Pressure",  Color = UITheme.Hex("2563EB"), ReadValue = s => s.reactorPressureBar },
        [Variable.H2CO2]       = new VarMeta { Label = "H₂/CO₂", SliderParam = "H2/CO2", Color = UITheme.Hex("059669"), ReadValue = s => s.h2Co2Ratio },
        [Variable.GHSV]        = new VarMeta { Label = "GHSV",          SliderParam = "GHSV",      Color = UITheme.Hex("7C3AED"), ReadValue = s => s.ghsv },
        [Variable.FeedFlow]    = new VarMeta { Label = "Feed flow",     SliderParam = "Feed flow", Color = UITheme.Hex("CA8A04"), ReadValue = s => s.reactorFeedFlowPercent },
    };

    private static readonly Variable[] VariableOrder = { Variable.Free, Variable.Temperature, Variable.Pressure, Variable.H2CO2, Variable.GHSV, Variable.FeedFlow };

    private static float ReadVarValue(Variable v, PlantProcessSimulator sim)
    {
        if (v == Variable.Free || sim == null || Vars[v].ReadValue == null) return 0f;
        return Vars[v].ReadValue(sim.Current);
    }

    private static string VarUnit(Variable v) => v == Variable.Free ? "" : GraphVisualUtils.GetParameterUnit(Vars[v].SliderParam);

    private RespMeta[] responses;

    private Canvas ownerCanvas;
    private Font labelFont;
    private InteractiveModulePanelRuntime panels;
    private PlantProcessSimulator subscribedSim;

    private Variable currentVar = Variable.Free;
    private Response currentResp = Response.Yield;
    private ViewMode currentMode = ViewMode.Points;
    private ExperimentMode currentExperimentMode = ExperimentMode.Interactive;
    private bool subTabVisible;

    private readonly List<Sample> samples = new List<Sample>();
    private readonly List<Epoch> epochs = new List<Epoch>();
    private readonly List<ChangePoint> changePoints = new List<ChangePoint>();
    private readonly List<SweepPoint> sweepPoints = new List<SweepPoint>();
    private PlantProcessSimulator.ProcessInputs sweepBaselineInputs;
    private float sweepBaselineX;
    private float sweepBaselineY;
    private float sweepMin;
    private float sweepMax;
    private bool hasSweepBaseline;
    private bool sweepNeedsRefresh;

    private float clock;
    private float nextSampleTime;
    private int pageIndex;
    private bool followLive = true;
    private float nextRebuild;
    private bool dirty = true;

    private RectTransform selfRect;
    private RectTransform plotArea;
    private RectTransform linesLayer;
    private RectTransform epochLayer;
    private RectTransform pointsLayer;
    private readonly List<UIGraphLine> linePool = new List<UIGraphLine>();
    private readonly List<Vector2> segBuffer = new List<Vector2>();

    private Text titleText;
    private Text contextText;
    private Text pageLabel;
    private Text yAxisNameLabel;
    private Text[] yTicks;
    private Text[] xTicks;
    private Button[] varButtons;
    private Image[] varDots;
    private Button[] respButtons;
    private Button[] modeButtons;
    private Button[] experimentModeButtons;
    private Button prevPageButton;
    private Button nextPageButton;
    private Button liveButton;
    private Button exportButton;
    private GameObject moduleLegend;
    private RectTransform hoverDot;
    private UIGraphTooltip tooltip;

    private float viewYMax = 100f;
    private float cachedPageStart;
    private float cachedPageEnd;

    private int LatestPage => Mathf.Max(0, Mathf.FloorToInt(clock / PageSeconds));

    public void Initialize(RectTransform container, Canvas canvas, Font font)
    {
        ownerCanvas = canvas;
        labelFont = font;
        panels = FindFirstObjectByType<InteractiveModulePanelRuntime>();

        float designMethanol = PlantProcessSimulator.Instance != null ? PlantProcessSimulator.Instance.DesignMethanolKgH : 1250f;
        responses = new[]
        {
            new RespMeta { Label = "Reactor yield",      Unit = "%",    Max = 100f,           Select = s => s.reactorYieldPercent },
            new RespMeta { Label = "Overall efficiency", Unit = "%",    Max = 100f,           Select = s => s.overallEfficiencyPercent },
            new RespMeta { Label = "Methanol output",    Unit = "kg/h", Max = designMethanol, Select = s => s.methanolProductionKgH },
        };

        RectTransform root = GetComponent<RectTransform>();
        if (root == null) root = gameObject.AddComponent<RectTransform>();
        root.SetParent(container, false);
        UITheme.Fill(root);
        selfRect = root;
        gameObject.AddComponent<UIRaycastTarget>();

        BuildExperimentModeRow(root);
        BuildVariableRow(root);
        BuildResponseRow(root);

        titleText = UITheme.Label("Title", root, "", 15f, W.ExtraBold, UITheme.Ink);
        UITheme.TopBand(titleText.rectTransform, 0f, 124f, LegendWidth + 20f, 20f);
        contextText = UITheme.Label("Context", root, "", 12.5f, W.Medium, UITheme.Subtle);
        UITheme.TopBand(contextText.rectTransform, 0f, 146f, LegendWidth + 20f, 18f);

        GameObject plotObject = new GameObject("Plot Area", typeof(RectTransform));
        plotArea = plotObject.GetComponent<RectTransform>();
        plotArea.SetParent(root, false);
        UITheme.Fill(plotArea, 70f, 178f, LegendWidth + 28f, 64f);

        UIGraphKit.PlotBackground(plotArea);
        UIGraphKit.HorizontalGrid(plotArea, TickCount - 1);
        yTicks = UIGraphKit.YTicks(root, plotArea, TickCount, 12f);
        xTicks = UIGraphKit.XTicks(plotArea, 3, 8f);
        yAxisNameLabel = UIGraphKit.RotatedYTitle(root, plotArea, "");

        linesLayer = new GameObject("Lines", typeof(RectTransform)).GetComponent<RectTransform>();
        linesLayer.SetParent(plotArea, false);
        UITheme.Fill(linesLayer);
        epochLayer = new GameObject("Epochs", typeof(RectTransform)).GetComponent<RectTransform>();
        epochLayer.SetParent(plotArea, false);
        UITheme.Fill(epochLayer);
        pointsLayer = new GameObject("Points", typeof(RectTransform)).GetComponent<RectTransform>();
        pointsLayer.SetParent(plotArea, false);
        UITheme.Fill(pointsLayer);

        BuildPaginationRow(root);
        BuildLegends(root);
        BuildHoverDot();
        tooltip = UIGraphTooltip.Create(root);

        epochs.Add(new Epoch { T = 0f, Var = currentVar, VarValue = 0f });
        RecolorVarButtons();
        RecolorRespButtons();
        RecolorModeButtons();
        RecolorExperimentModeButtons();
        UpdateContext();
        dirty = true;
    }

    // ---- control rows -------------------------------------------------------

    private void BuildExperimentModeRow(RectTransform root)
    {
        RectTransform row = UITheme.NewRect("OFAT Mode Row", root);
        UITheme.TopBand(row, 0f, 0f, 0f, 32f);
        Text lbl = UITheme.Label("Mode Label", row, "OFAT mode", 12.5f, W.ExtraBold, UITheme.Subtle);
        UITheme.TopLeft(lbl.rectTransform, 0f, 0f, 76f, 32f);

        Image segment = UITheme.Panel("Experiment Mode", row, UITheme.Sunken, 16f, true);
        string[] names = { "Interactive", "Auto sweep" };
        experimentModeButtons = new Button[names.Length];
        float x = 3f;
        for (int i = 0; i < names.Length; i++)
        {
            int idx = i;
            Button b = UITheme.MakeButton("Experiment Mode " + i, segment.transform, names[i], Kind.Segment,
                12.5f, null, 13f, false, 16f, W.ExtraBold, 12f);
            float w = UITheme.PreferredWidth(b);
            UITheme.TopLeft((RectTransform)b.transform, x, 3f, w, 26f);
            x += w + 2f;
            b.onClick.AddListener(() => SetExperimentMode((ExperimentMode)idx));
            experimentModeButtons[i] = b;
        }
        UITheme.TopLeft(segment.rectTransform, 82f, 0f, x + 1f, 32f);

        Text note = UITheme.Label("Mode Note", row,
            "Auto sweep snapshots the current operating point and recalculates one factor at a time.",
            11.5f, W.Medium, UITheme.Muted);
        UITheme.TopLeft(note.rectTransform, 300f, 5f, 610f, 22f);
    }

    private void BuildVariableRow(RectTransform root)
    {
        RectTransform row = UITheme.NewRect("Variable Row", root);
        UITheme.TopBand(row, 0f, 40f, 0f, 32f);
        Text lbl = UITheme.Label("Vary Label", row, "Vary one", 12.5f, W.ExtraBold, UITheme.Subtle);
        UITheme.TopLeft(lbl.rectTransform, 0f, 0f, 70f, 32f);

        varButtons = new Button[VariableOrder.Length];
        varDots = new Image[VariableOrder.Length];
        float x = 76f;
        for (int i = 0; i < VariableOrder.Length; i++)
        {
            Variable v = VariableOrder[i];
            Button b = UITheme.MakeButton("Var " + v, row, Vars[v].Label, Kind.Chip, 12.5f, null, 8f, false, 16f, W.Bold, 11f);
            // The chips double as the line-colour legend: each carries its variable's colour.
            Image dot = UITheme.Dot("Swatch", b.transform, 8f, Vars[v].Color);
            dot.transform.SetAsFirstSibling();
            LayoutElement le = dot.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = 8f;
            b.GetComponent<HorizontalLayoutGroup>().spacing = 7f;
            b.Skin().OverrideActive(Vars[v].Color, Color.white);
            float w = UITheme.PreferredWidth(b);
            UITheme.TopLeft((RectTransform)b.transform, x, 0f, w, 32f);
            x += w + 6f;
            b.onClick.AddListener(() => SetVariable(v));
            varButtons[i] = b;
            varDots[i] = dot;
        }

        exportButton = UITheme.MakeButton("Export", row, "Export", Kind.Outline, 13f, Icon.Download, 8f, false, 15f, W.Bold, 12f);
        float ew = UITheme.PreferredWidth(exportButton);
        UITheme.TopRight((RectTransform)exportButton.transform, 0f, 0f, ew, 32f);
        exportButton.onClick.AddListener(ExportNow);
    }

    private void BuildResponseRow(RectTransform root)
    {
        RectTransform row = UITheme.NewRect("Response Row", root);
        UITheme.TopBand(row, 0f, 80f, 0f, 32f);
        Text lbl = UITheme.Label("Show Label", row, "Show", 12.5f, W.ExtraBold, UITheme.Subtle);
        UITheme.TopLeft(lbl.rectTransform, 0f, 0f, 70f, 32f);

        string[] respNames = { "Yield", "Efficiency", "Methanol" };
        respButtons = new Button[respNames.Length];
        float x = 76f;
        for (int i = 0; i < respNames.Length; i++)
        {
            int idx = i;
            Button b = UITheme.MakeButton("Resp " + i, row, respNames[i], Kind.Chip, 12.5f, null, 8f, false, 16f, W.Bold, 12f);
            float w = UITheme.PreferredWidth(b);
            UITheme.TopLeft((RectTransform)b.transform, x, 0f, w, 32f);
            x += w + 6f;
            b.onClick.AddListener(() => SetResponse((Response)idx));
            respButtons[i] = b;
        }

        // Line only / With points segmented control
        Image segment = UITheme.Panel("Mode", row, UITheme.Sunken, 16f, true);
        string[] modeNames = { "Line only", "With points" };
        modeButtons = new Button[modeNames.Length];
        float sx = 3f;
        for (int i = 0; i < modeNames.Length; i++)
        {
            int idx = i;
            Button b = UITheme.MakeButton("Mode " + i, segment.transform, modeNames[i], Kind.Segment, 12.5f, null, 13f, false, 16f, W.ExtraBold, 12f);
            float w = UITheme.PreferredWidth(b);
            UITheme.TopLeft((RectTransform)b.transform, sx, 3f, w, 26f);
            sx += w + 2f;
            b.onClick.AddListener(() => SetMode((ViewMode)idx));
            modeButtons[i] = b;
        }
        UITheme.TopLeft(segment.rectTransform, x + 14f, 0f, sx + 1f, 32f);
    }

    private void BuildPaginationRow(RectTransform root)
    {
        RectTransform row = UITheme.NewRect("Pagination Row", root);
        row.anchorMin = Vector2.zero;
        row.anchorMax = new Vector2(1f, 0f);
        row.pivot = new Vector2(0.5f, 0f);
        row.offsetMin = new Vector2(70f, 0f);
        row.offsetMax = new Vector2(-(LegendWidth + 28f), 30f);

        prevPageButton = UITheme.MakeButton("Prev Page", row, "Previous", Kind.Secondary, 12.5f, Icon.ChevronLeft, 8f, false, 14f, W.Bold, 10f);
        float pw = UITheme.PreferredWidth(prevPageButton);
        UITheme.TopLeft((RectTransform)prevPageButton.transform, 0f, 0f, pw, 30f);
        prevPageButton.onClick.AddListener(PrevPage);

        liveButton = UITheme.MakeButton("Live", row, "Live", Kind.Chip, 12.5f, Icon.Dot, 8f, false, 12f, W.ExtraBold, 11f);
        liveButton.Skin().OverrideActive(UITheme.Accent, Color.white);
        float lw = UITheme.PreferredWidth(liveButton);
        UITheme.TopRight((RectTransform)liveButton.transform, 0f, 0f, lw, 30f);
        liveButton.onClick.AddListener(GoLive);

        nextPageButton = UITheme.MakeButton("Next Page", row, "Next", Kind.Secondary, 12.5f, Icon.ChevronRight, 8f, true, 14f, W.Bold, 10f);
        float nw = UITheme.PreferredWidth(nextPageButton);
        UITheme.TopRight((RectTransform)nextPageButton.transform, lw + 8f, 0f, nw, 30f);
        nextPageButton.onClick.AddListener(NextPage);

        pageLabel = UITheme.Label("Page Label", row, "", 12.5f, W.Bold, UITheme.Muted, TextAnchor.MiddleCenter);
        UITheme.Fill(pageLabel.rectTransform, pw + 8f, 0f, lw + nw + 16f, 0f);
    }

    private void BuildLegends(RectTransform root)
    {
        // The variable chips carry the line colours; the right column explains the points.
        RectTransform legend = UIGraphKit.ModuleLegend(root, "Colour = module changed", false, LegendWidth);
        UITheme.TopRight(legend, 0f, 178f, LegendWidth, legend.sizeDelta.y);
        moduleLegend = legend.gameObject;
    }

    // ---- state changes ----------------------------------------------------

    private void SetExperimentMode(ExperimentMode mode)
    {
        if (mode == currentExperimentMode)
        {
            if (mode == ExperimentMode.AutoSweep)
            {
                sweepNeedsRefresh = true;
                GenerateAutomaticSweep();
            }
            return;
        }

        currentExperimentMode = mode;
        if (currentExperimentMode == ExperimentMode.AutoSweep)
        {
            if (currentVar == Variable.Free) currentVar = Variable.Temperature;
            sweepNeedsRefresh = true;
            GenerateAutomaticSweep();
        }
        else
        {
            sweepPoints.Clear();
            hasSweepBaseline = false;
            epochs.Add(new Epoch { T = clock, Var = currentVar, VarValue = ReadVarValue(currentVar, PlantProcessSimulator.Instance) });
            if (epochs.Count > MaxRecords) epochs.RemoveAt(0);
        }

        ApplyLock();
        RecolorExperimentModeButtons();
        RecolorVarButtons();
        RecolorModeButtons();
        UpdateContext();
        dirty = true;
    }

    private void SetVariable(Variable v)
    {
        if (currentExperimentMode == ExperimentMode.AutoSweep && v == Variable.Free)
            v = Variable.Temperature;

        if (v == currentVar)
        {
            if (currentExperimentMode == ExperimentMode.AutoSweep)
            {
                sweepNeedsRefresh = true;
                GenerateAutomaticSweep();
            }
            return;
        }

        currentVar = v;
        if (currentExperimentMode == ExperimentMode.AutoSweep)
        {
            sweepNeedsRefresh = true;
            GenerateAutomaticSweep();
        }
        else
        {
            epochs.Add(new Epoch { T = clock, Var = v, VarValue = ReadVarValue(v, PlantProcessSimulator.Instance) });
            if (epochs.Count > MaxRecords) epochs.RemoveAt(0);
        }

        ApplyLock();
        RecolorVarButtons();
        UpdateContext();
        dirty = true;
    }

    private void SetResponse(Response r)
    {
        if (r == currentResp) return;
        currentResp = r;
        if (currentExperimentMode == ExperimentMode.AutoSweep)
        {
            sweepNeedsRefresh = true;
            GenerateAutomaticSweep();
        }
        RecolorRespButtons();
        UpdateContext();
        dirty = true;
    }

    /// <summary>
    /// Generates a model-based OFAT curve from the actual current operating inputs.
    /// The live plant is never stepped through the sweep: CurrentInputs is copied, exactly
    /// one selected input is changed for each point, and the same pure
    /// PlantProcessSimulator.Simulate() calculation used by the application evaluates it.
    /// This keeps recycle/mass-balance/process logic identical to the running model while
    /// leaving live sliders, storage, animation and timeline state untouched.
    /// </summary>
    private void GenerateAutomaticSweep()
    {
        sweepPoints.Clear();
        PlantProcessSimulator sim = PlantProcessSimulator.Instance;
        if (sim == null || currentVar == Variable.Free || responses == null)
        {
            hasSweepBaseline = false;
            sweepNeedsRefresh = false;
            return;
        }

        if (!GetSweepRange(currentVar, out sweepMin, out sweepMax))
        {
            hasSweepBaseline = false;
            sweepNeedsRefresh = false;
            return;
        }

        sweepBaselineInputs = sim.CurrentInputs;
        sweepBaselineX = ReadInputValue(sweepBaselineInputs, currentVar);

        PlantProcessSimulator.ProcessSnapshot baselineResult = sim.Simulate(sweepBaselineInputs);
        sweepBaselineY = responses[(int)currentResp].Select(baselineResult);
        hasSweepBaseline = true;

        const int intervals = 12; // 13 calculated model points, including both range limits.
        for (int i = 0; i <= intervals; i++)
        {
            float x = Mathf.Lerp(sweepMin, sweepMax, i / (float)intervals);
            PlantProcessSimulator.ProcessInputs hypothetical = sweepBaselineInputs;
            SetInputValue(ref hypothetical, currentVar, x);
            PlantProcessSimulator.ProcessSnapshot result = sim.Simulate(hypothetical);
            sweepPoints.Add(new SweepPoint
            {
                X = x,
                Y = responses[(int)currentResp].Select(result)
            });
        }

        sweepNeedsRefresh = false;
        dirty = true;
    }

    private static bool GetSweepRange(Variable v, out float min, out float max)
    {
        switch (v)
        {
            case Variable.Temperature: min = 180f; max = 300f; return true;
            case Variable.Pressure:    min = 40f;  max = 100f; return true;
            case Variable.H2CO2:       min = 1f;   max = 6f; return true;
            case Variable.GHSV:        min = 1000f; max = 20000f; return true;
            case Variable.FeedFlow:    min = 20f;  max = 130f; return true;
            default:                   min = 0f;    max = 0f; return false;
        }
    }

    private static float ReadInputValue(PlantProcessSimulator.ProcessInputs input, Variable v)
    {
        return v switch
        {
            Variable.Temperature => input.temperature,
            Variable.Pressure => input.pressure,
            Variable.H2CO2 => input.ratio,
            Variable.GHSV => input.ghsv,
            Variable.FeedFlow => input.reactorFeedFlow,
            _ => 0f
        };
    }

    private static void SetInputValue(ref PlantProcessSimulator.ProcessInputs input, Variable v, float value)
    {
        switch (v)
        {
            case Variable.Temperature: input.temperature = value; break;
            case Variable.Pressure: input.pressure = value; break;
            case Variable.H2CO2: input.ratio = value; break;
            case Variable.GHSV: input.ghsv = value; break;
            case Variable.FeedFlow: input.reactorFeedFlow = value; break;
        }
    }

    private void SetMode(ViewMode m)
    {
        currentMode = m;
        RecolorModeButtons();
        dirty = true;
    }

    /// <summary>Called by the dashboard when the OFAT sub-tab is shown / hidden. The reactor
    /// slider lock is only in force while this sub-tab is actually visible.</summary>
    public void SetSubTabVisible(bool visible)
    {
        subTabVisible = visible;
        ApplyLock();
        if (visible) dirty = true;
    }

    private void ApplyLock()
    {
        if (panels == null) panels = FindFirstObjectByType<InteractiveModulePanelRuntime>();
        if (panels == null) return;
        if (subTabVisible && currentExperimentMode == ExperimentMode.Interactive && currentVar != Variable.Free)
            panels.SetReactorVariableLock(Vars[currentVar].SliderParam);
        else
            panels.ClearReactorVariableLock();
    }

    private void PrevPage()
    {
        pageIndex = Mathf.Max(0, pageIndex - 1);
        followLive = false;
        dirty = true;
    }

    private void NextPage()
    {
        pageIndex = Mathf.Min(LatestPage, pageIndex + 1);
        followLive = pageIndex >= LatestPage;
        dirty = true;
    }

    private void GoLive()
    {
        followLive = true;
        pageIndex = LatestPage;
        dirty = true;
    }

    // ---- lifecycle ------------------------------------------------------

    private void OnDisable()
    {
        if (panels != null) panels.ClearReactorVariableLock();
    }

    private void OnDestroy()
    {
        if (subscribedSim != null)
        {
            subscribedSim.ResetRequested -= HandleReset;
            subscribedSim.ManualChangeCommitted -= HandleChange;
        }
        if (panels != null) panels.ClearReactorVariableLock();
    }

    private void HandleReset()
    {
        samples.Clear();
        changePoints.Clear();
        epochs.Clear();
        clock = 0f;
        nextSampleTime = 0f;
        pageIndex = 0;
        followLive = true;
        if (currentExperimentMode == ExperimentMode.AutoSweep)
        {
            if (currentVar == Variable.Free) currentVar = Variable.Temperature;
        }
        else
        {
            currentVar = Variable.Free;
        }
        epochs.Add(new Epoch { T = 0f, Var = currentVar, VarValue = 0f });
        sweepPoints.Clear();
        hasSweepBaseline = false;
        sweepNeedsRefresh = currentExperimentMode == ExperimentMode.AutoSweep;
        ApplyLock();
        RecolorVarButtons();
        UpdateContext();
        dirty = true;
    }

    private void HandleChange(PlantProcessSimulator.ManualChangeInfo info)
    {
        PlantProcessSimulator sim = PlantProcessSimulator.Instance;
        if (sim == null) return;

        if (currentExperimentMode == ExperimentMode.AutoSweep)
        {
            // A committed live control change means the next automatic curve must use a fresh
            // baseline from the real current operating inputs.
            sweepNeedsRefresh = true;
            dirty = true;
            return;
        }

        changePoints.Add(new ChangePoint
        {
            T = clock,
            Y = responses[(int)currentResp].Select(sim.Current),
            Module = info.Module,
            Parameter = info.Parameter,
            From = info.FromValue,
            To = info.ToValue,
        });
        if (changePoints.Count > MaxRecords) changePoints.RemoveAt(0);
        dirty = true;
    }

    private void Update()
    {
        PlantProcessSimulator sim = PlantProcessSimulator.Instance;
        if (sim != subscribedSim)
        {
            if (subscribedSim != null)
            {
                subscribedSim.ResetRequested -= HandleReset;
                subscribedSim.ManualChangeCommitted -= HandleChange;
            }
            subscribedSim = sim;
            if (subscribedSim != null)
            {
                subscribedSim.ResetRequested += HandleReset;
                subscribedSim.ManualChangeCommitted += HandleChange;
            }
        }

        bool running = sim != null && sim.IsRunning;

        if (currentExperimentMode == ExperimentMode.AutoSweep)
        {
            if (sweepNeedsRefresh) GenerateAutomaticSweep();
        }
        else
        {
            if (running) clock += Time.unscaledDeltaTime;

            if (running && clock >= nextSampleTime)
            {
                nextSampleTime = clock + SampleInterval;
                samples.Add(new Sample
                {
                    T = clock,
                    Y = responses[(int)currentResp].Select(sim.Current),
                    Var = currentVar,
                    VarValue = ReadVarValue(currentVar, sim),
                });
                if (samples.Count > MaxRecords) samples.RemoveAt(0);
                dirty = true;
            }

            if (followLive) pageIndex = LatestPage;
        }

        if (dirty || Time.unscaledTime >= nextRebuild)
        {
            nextRebuild = Time.unscaledTime + RebuildInterval;
            Rebuild();
        }
    }

    // ---- rendering ----------------------------------------------------

    private void Rebuild()
    {
        dirty = false;
        if (plotArea == null || responses == null) return;

        for (int i = pointsLayer.childCount - 1; i >= 0; i--) Destroy(pointsLayer.GetChild(i).gameObject);
        for (int i = epochLayer.childCount - 1; i >= 0; i--) Destroy(epochLayer.GetChild(i).gameObject);

        if (currentExperimentMode == ExperimentMode.AutoSweep)
        {
            RebuildAutomaticSweep();
            return;
        }

        SetInteractiveControlsVisible(true);
        RespMeta rm = responses[(int)currentResp];
        int latest = LatestPage;
        pageIndex = Mathf.Clamp(pageIndex, 0, latest);
        float pageStart = pageIndex * PageSeconds;
        float pageEnd = pageStart + PageSeconds;
        cachedPageStart = pageStart;
        cachedPageEnd = pageEnd;
        float slack = SampleInterval * 2f;

        float dataMax = 0f;
        for (int i = 0; i < samples.Count; i++)
        {
            float ti = samples[i].T;
            if (ti < pageStart - slack || ti > pageEnd + slack) continue;
            if (samples[i].Y > dataMax) dataMax = samples[i].Y;
        }
        viewYMax = Mathf.Clamp(dataMax * 1.15f, rm.Max * 0.2f, rm.Max);
        if (viewYMax <= 1f) viewYMax = rm.Max;

        Rect r = plotArea.rect;

        // colour-segmented line: contiguous runs of equal Var
        int poolUsed = 0;
        segBuffer.Clear();
        Variable runVar = Variable.Free;
        bool haveRun = false;
        for (int i = 0; i < samples.Count; i++)
        {
            Sample s = samples[i];
            if (s.T < pageStart - slack) continue;
            if (s.T > pageEnd + slack) break;
            Vector2 p = new Vector2(MapX(s.T, r, pageStart, pageEnd), MapY(s.Y, r));
            if (!haveRun)
            {
                runVar = s.Var;
                haveRun = true;
                segBuffer.Add(p);
            }
            else if (s.Var == runVar)
            {
                segBuffer.Add(p);
            }
            else
            {
                segBuffer.Add(p);
                FlushRun(ref poolUsed, runVar);
                segBuffer.Clear();
                segBuffer.Add(p);
                runVar = s.Var;
            }
        }
        if (haveRun) FlushRun(ref poolUsed, runVar);
        for (int i = poolUsed; i < linePool.Count; i++) linePool[i].ClearPoints();

        // Keep nearby event captions in separate lanes; leave event times and data intact.
        var captionBounds = new List<Rect>();
        var shownCaptions = new HashSet<string>();
        foreach (Epoch e in epochs)
        {
            if (e.T < pageStart || e.T > pageEnd) continue;
            float x = MapX(e.T, r, pageStart, pageEnd);
            Image v = UITheme.Panel("Epoch Line", epochLayer, UITheme.WithAlpha(Vars[e.Var].Color, 0.45f));
            RectTransform vr = v.rectTransform;
            vr.anchorMin = vr.anchorMax = new Vector2(0.5f, 0.5f);
            vr.pivot = new Vector2(0.5f, 0f);
            vr.sizeDelta = new Vector2(1.5f, r.height);
            vr.anchoredPosition = new Vector2(x, r.yMin);

            string epochText = e.Var == Variable.Free
                ? $"Free · {FormatClock(e.T)}"
                : $"{Vars[e.Var].Label} = {GraphVisualUtils.FormatValue(e.VarValue, VarUnit(e.Var))} · {FormatClock(e.T)}";
            if (!shownCaptions.Add(epochText)) continue;
            Text lab = UITheme.Label("Epoch Label", epochLayer, epochText, 11f, W.ExtraBold, Vars[e.Var].Color, TextAnchor.LowerLeft);
            RectTransform lr = lab.rectTransform;
            lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
            lr.pivot = new Vector2(0f, 0f);
            float captionWidth = Mathf.Min(r.width, Mathf.Max(180f, lab.preferredWidth + 4f));
            Rect caption = new Rect(Mathf.Clamp(x + 4f, r.xMin, r.xMax - captionWidth), r.yMax - 15f, captionWidth, 14f);
            while (captionBounds.Exists(other => other.Overlaps(caption))) caption.y -= 18f;
            captionBounds.Add(caption);
            lr.anchoredPosition = caption.position;
            lr.sizeDelta = caption.size;
        }

        // module change points
        if (currentMode == ViewMode.Points)
        {
            for (int i = 0; i < changePoints.Count; i++)
            {
                ChangePoint cp = changePoints[i];
                if (cp.T < pageStart || cp.T > pageEnd) continue;
                Vector2 p = new Vector2(MapX(cp.T, r, pageStart, pageEnd), MapY(cp.Y, r));
                RectTransform dot = UITheme.NewRect("Point", pointsLayer);
                dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
                dot.pivot = new Vector2(0.5f, 0.5f);
                dot.sizeDelta = new Vector2(10f, 10f);
                dot.anchoredPosition = p;
                Image ring = UITheme.Dot("Ring", dot, 14f, Color.white);
                UITheme.Center(ring.rectTransform, 14f, 14f);
                Image fill = UITheme.Dot("Fill", dot, 10f, GraphVisualUtils.GetModuleColor(cp.Module));
                UITheme.Center(fill.rectTransform, 10f, 10f);
            }
        }

        titleText.text = $"OFAT timeline — {rm.Label.ToLowerInvariant()} over time";
        pageLabel.text = $"Page {pageIndex + 1} of {latest + 1}  ·  {FormatClock(pageStart)} – {FormatClock(pageEnd)}";
        xTicks[0].text = FormatClock(pageStart);
        xTicks[1].text = FormatClock(pageStart + PageSeconds * 0.5f);
        xTicks[2].text = FormatClock(pageEnd);
        for (int i = 0; i < TickCount; i++)
            yTicks[i].text = FormatNum(viewYMax * i / (TickCount - 1));
        yAxisNameLabel.text = $"{rm.Label} ({rm.Unit})";
        if (moduleLegend != null) moduleLegend.SetActive(currentMode == ViewMode.Points);
        liveButton.Skin()?.SetActive(followLive);
    }

    private void RebuildAutomaticSweep()
    {
        SetInteractiveControlsVisible(false);
        for (int i = 0; i < linePool.Count; i++) linePool[i].ClearPoints();

        RespMeta rm = responses[(int)currentResp];
        if (sweepPoints.Count == 0 || currentVar == Variable.Free)
        {
            titleText.text = "Model sensitivity (OFAT)";
            contextText.text = "No sweep is available. Select a parameter to recalculate the current model.";
            pageLabel.text = "AUTO SWEEP";
            if (moduleLegend != null) moduleLegend.SetActive(false);
            return;
        }

        Rect r = plotArea.rect;
        float dataMax = 0f;
        for (int i = 0; i < sweepPoints.Count; i++) dataMax = Mathf.Max(dataMax, sweepPoints[i].Y);
        viewYMax = Mathf.Clamp(dataMax * 1.15f, Mathf.Max(1f, rm.Max * 0.2f), Mathf.Max(rm.Max, dataMax * 1.15f));

        segBuffer.Clear();
        for (int i = 0; i < sweepPoints.Count; i++)
        {
            SweepPoint point = sweepPoints[i];
            segBuffer.Add(new Vector2(
                r.xMin + Mathf.InverseLerp(sweepMin, sweepMax, point.X) * r.width,
                MapY(point.Y, r)));
        }

        if (segBuffer.Count >= 2)
        {
            UIGraphLine line = GetPoolLine(0);
            line.color = Vars[currentVar].Color;
            line.SetPoints(segBuffer);
        }

        for (int i = 0; i < sweepPoints.Count; i++)
        {
            SweepPoint point = sweepPoints[i];
            RectTransform dot = UITheme.NewRect("Sweep Point", pointsLayer);
            dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
            dot.pivot = new Vector2(0.5f, 0.5f);
            dot.sizeDelta = new Vector2(9f, 9f);
            dot.anchoredPosition = segBuffer[i];
            Image fill = UITheme.Dot("Fill", dot, 9f, Vars[currentVar].Color);
            UITheme.Center(fill.rectTransform, 9f, 9f);
        }

        if (hasSweepBaseline)
        {
            RectTransform currentDot = UITheme.NewRect("Current Operating Point", pointsLayer);
            currentDot.anchorMin = currentDot.anchorMax = new Vector2(0.5f, 0.5f);
            currentDot.pivot = new Vector2(0.5f, 0.5f);
            currentDot.sizeDelta = new Vector2(15f, 15f);
            currentDot.anchoredPosition = new Vector2(
                r.xMin + Mathf.InverseLerp(sweepMin, sweepMax, sweepBaselineX) * r.width,
                MapY(sweepBaselineY, r));
            Image ring = UITheme.Dot("Current Ring", currentDot, 15f, Color.white);
            UITheme.Center(ring.rectTransform, 15f, 15f);
            Image core = UITheme.Dot("Current Core", currentDot, 9f, UITheme.Ink);
            UITheme.Center(core.rectTransform, 9f, 9f);
        }

        string unit = VarUnit(currentVar);
        titleText.text = $"Model sensitivity (OFAT) — {rm.Label.ToLowerInvariant()} vs {Vars[currentVar].Label}";
        contextText.text =
            $"13 points recalculated with PlantProcessSimulator.Simulate(). Only {Vars[currentVar].Label} changes; every other input is held at the current operating point.";
        pageLabel.text = $"AUTO SWEEP  ·  current baseline {GraphVisualUtils.FormatValue(sweepBaselineX, unit)}";
        xTicks[0].text = GraphVisualUtils.FormatValue(sweepMin, unit);
        xTicks[1].text = GraphVisualUtils.FormatValue((sweepMin + sweepMax) * 0.5f, unit);
        xTicks[2].text = GraphVisualUtils.FormatValue(sweepMax, unit);
        for (int i = 0; i < TickCount; i++)
            yTicks[i].text = FormatNum(viewYMax * i / (TickCount - 1));
        yAxisNameLabel.text = $"{rm.Label} ({rm.Unit})";
        if (moduleLegend != null) moduleLegend.SetActive(false);
    }

    private void SetInteractiveControlsVisible(bool visible)
    {
        if (prevPageButton != null) prevPageButton.gameObject.SetActive(visible);
        if (nextPageButton != null) nextPageButton.gameObject.SetActive(visible);
        if (liveButton != null) liveButton.gameObject.SetActive(visible);
        if (modeButtons != null)
            for (int i = 0; i < modeButtons.Length; i++)
                if (modeButtons[i] != null) modeButtons[i].interactable = visible;
    }

    private void FlushRun(ref int poolUsed, Variable v)
    {
        if (segBuffer.Count < 2) return;
        UIGraphLine line = GetPoolLine(poolUsed);
        line.color = Vars[v].Color;
        line.SetPoints(segBuffer);
        poolUsed++;
    }

    private UIGraphLine GetPoolLine(int index)
    {
        while (linePool.Count <= index)
        {
            GameObject go = new GameObject("Seg " + linePool.Count, typeof(RectTransform));
            go.transform.SetParent(linesLayer, false);
            UITheme.Fill((RectTransform)go.transform);
            UIGraphLine line = go.AddComponent<UIGraphLine>();
            line.Thickness = 2.6f;
            line.raycastTarget = false;
            linePool.Add(line);
        }
        return linePool[index];
    }

    private static float MapX(float t, Rect r, float pageStart, float pageEnd)
        => r.xMin + Mathf.InverseLerp(pageStart, pageEnd, t) * r.width;

    private float MapY(float y, Rect r)
        => r.yMin + Mathf.Clamp01(Mathf.InverseLerp(0f, viewYMax, y)) * r.height;

    // ---- hover ----------------------------------------------------

    public void OnPointerMove(PointerEventData eventData)
    {
        if (currentExperimentMode == ExperimentMode.AutoSweep)
        {
            HandleAutomaticSweepHover(eventData);
            return;
        }

        if (plotArea == null || samples.Count == 0)
        {
            HideTooltip();
            return;
        }
        Camera cam = ownerCanvas != null && ownerCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? ownerCanvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(plotArea, eventData.position, cam, out Vector2 local))
        {
            HideTooltip();
            return;
        }

        Rect r = plotArea.rect;
        if (local.x < r.xMin || local.x > r.xMax || local.y < r.yMin - 8f || local.y > r.yMax + 8f)
        {
            HideTooltip();
            return;
        }
        float frac = Mathf.InverseLerp(r.xMin, r.xMax, local.x);
        float t = Mathf.Lerp(cachedPageStart, cachedPageEnd, frac);

        int nearest = -1;
        float best = float.MaxValue;
        for (int i = 0; i < samples.Count; i++)
        {
            if (samples[i].T < cachedPageStart || samples[i].T > cachedPageEnd) continue;
            float d = Mathf.Abs(samples[i].T - t);
            if (d < best) { best = d; nearest = i; }
        }
        if (nearest < 0)
        {
            HideTooltip();
            return;
        }

        RespMeta rm = responses[(int)currentResp];
        Sample s = samples[nearest];
        string varyingLine = s.Var == Variable.Free
            ? "Varying: none (all reactor sliders free)"
            : $"Varying: {Vars[s.Var].Label} = {GraphVisualUtils.FormatValue(s.VarValue, VarUnit(s.Var))}";
        string text = $"<b>{rm.Label}: {GraphVisualUtils.FormatValue(s.Y, rm.Unit)}</b>\n<color=#94A3B8>t = {FormatClock(s.T)}</color>\n{varyingLine}";

        if (currentMode == ViewMode.Points)
        {
            int cpNearest = -1;
            float cpBest = HoverRadiusPixels;
            for (int i = 0; i < changePoints.Count; i++)
            {
                ChangePoint cp = changePoints[i];
                if (cp.T < cachedPageStart || cp.T > cachedPageEnd) continue;
                Vector2 sp = new Vector2(MapX(cp.T, r, cachedPageStart, cachedPageEnd), MapY(cp.Y, r));
                float d = Vector2.Distance(local, sp);
                if (d < cpBest) { cpBest = d; cpNearest = i; }
            }
            if (cpNearest >= 0)
            {
                ChangePoint cp = changePoints[cpNearest];
                string cu = GraphVisualUtils.GetParameterUnit(cp.Parameter);
                text += $"\n<color=#94A3B8>{UITheme.Pretty(cp.Parameter)} {GraphVisualUtils.FormatValue(cp.From, cu)} → {GraphVisualUtils.FormatValue(cp.To, cu)} · {UIGraphKit.ModuleDisplayName(cp.Module)}</color>";
            }
        }

        if (hoverDot != null)
        {
            hoverDot.anchoredPosition = new Vector2(MapX(s.T, r, cachedPageStart, cachedPageEnd), MapY(s.Y, r));
            hoverDot.gameObject.SetActive(true);
            hoverDot.SetAsLastSibling();
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(selfRect, eventData.position, cam, out Vector2 tl);
        tooltip.Show(text, tl);
    }

    private void HandleAutomaticSweepHover(PointerEventData eventData)
    {
        if (plotArea == null || sweepPoints.Count == 0)
        {
            HideTooltip();
            return;
        }

        Camera cam = ownerCanvas != null && ownerCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? ownerCanvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(plotArea, eventData.position, cam, out Vector2 local))
        {
            HideTooltip();
            return;
        }

        Rect r = plotArea.rect;
        if (local.x < r.xMin || local.x > r.xMax || local.y < r.yMin - 8f || local.y > r.yMax + 8f)
        {
            HideTooltip();
            return;
        }

        float xValue = Mathf.Lerp(sweepMin, sweepMax, Mathf.InverseLerp(r.xMin, r.xMax, local.x));
        int nearest = 0;
        float best = float.MaxValue;
        for (int i = 0; i < sweepPoints.Count; i++)
        {
            float d = Mathf.Abs(sweepPoints[i].X - xValue);
            if (d < best) { best = d; nearest = i; }
        }

        SweepPoint p = sweepPoints[nearest];
        RespMeta rm = responses[(int)currentResp];
        string unit = VarUnit(currentVar);
        string text =
            $"<b>{Vars[currentVar].Label}: {GraphVisualUtils.FormatValue(p.X, unit)}</b>\n" +
            $"{rm.Label}: {GraphVisualUtils.FormatValue(p.Y, rm.Unit)}\n" +
            "<color=#94A3B8>Calculated from the current operating baseline; all other inputs held constant.</color>";

        if (hoverDot != null)
        {
            hoverDot.anchoredPosition = new Vector2(
                r.xMin + Mathf.InverseLerp(sweepMin, sweepMax, p.X) * r.width,
                MapY(p.Y, r));
            hoverDot.gameObject.SetActive(true);
            hoverDot.SetAsLastSibling();
        }
        RectTransformUtility.ScreenPointToLocalPointInRectangle(selfRect, eventData.position, cam, out Vector2 tl);
        tooltip.Show(text, tl);
    }

    public void OnPointerExit(PointerEventData eventData) => HideTooltip();

    private void HideTooltip()
    {
        if (tooltip != null) tooltip.Hide();
        if (hoverDot != null) hoverDot.gameObject.SetActive(false);
    }

    // ---- export ----------------------------------------------------

    private void ExportNow()
    {
        RespMeta rm = responses[(int)currentResp];
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        if (currentExperimentMode == ExperimentMode.AutoSweep)
        {
            if (sweepPoints.Count == 0 || !hasSweepBaseline)
            {
                GraphExportUtil.ShowToast(ownerCanvas, labelFont, "Auto sweep: no calculated points yet.");
                return;
            }

            string baseName = GraphExportUtil.Sanitize($"OFAT_auto_{Vars[currentVar].Label}_{rm.Label}_{stamp}");
            string unit = VarUnit(currentVar);
            var sb = new StringBuilder();
            sb.AppendLine($"# Model sensitivity (OFAT) — {rm.Label} vs {Vars[currentVar].Label}");
            sb.AppendLine($"# exported,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine("# calculation_source,PlantProcessSimulator.Simulate(ProcessInputs)");
            sb.AppendLine("# method,Current operating inputs snapshot; exactly one factor varied; live plant state not mutated");
            sb.AppendLine($"# varied_parameter,{Vars[currentVar].Label}");
            sb.AppendLine($"# range,{sweepMin:0.####},{sweepMax:0.####},{unit}");
            sb.AppendLine("# points,13");
            sb.AppendLine($"# baseline_{Vars[currentVar].Label},{sweepBaselineX:0.####},{unit}");
            sb.AppendLine($"# baseline_response,{sweepBaselineY:0.####},{rm.Unit}");
            sb.AppendLine($"# held_timeline_percent,{sweepBaselineInputs.timeline:0.####}");
            sb.AppendLine($"# held_electrolyzer_power_percent,{sweepBaselineInputs.electrolyzerPower:0.####}");
            sb.AppendLine($"# held_water_feed_percent,{sweepBaselineInputs.waterFeed:0.####}");
            sb.AppendLine($"# held_flue_gas_flow_percent,{sweepBaselineInputs.flueGasFlow:0.####}");
            sb.AppendLine($"# held_amine_flow_percent,{sweepBaselineInputs.amineFlow:0.####}");
            sb.AppendLine($"# held_regenerator_steam_percent,{sweepBaselineInputs.regeneratorSteam:0.####}");
            sb.AppendLine($"# held_regenerator_temperature_C,{sweepBaselineInputs.regenTemp:0.####}");
            sb.AppendLine($"# held_compression_ratio,{sweepBaselineInputs.compressionRatio:0.####}");
            sb.AppendLine($"# held_temperature_C,{sweepBaselineInputs.temperature:0.####}");
            sb.AppendLine($"# held_pressure_bar,{sweepBaselineInputs.pressure:0.####}");
            sb.AppendLine($"# held_H2_CO2_ratio,{sweepBaselineInputs.ratio:0.####}");
            sb.AppendLine($"# held_GHSV_per_h,{sweepBaselineInputs.ghsv:0.####}");
            sb.AppendLine($"# held_reactor_feed_percent,{sweepBaselineInputs.reactorFeedFlow:0.####}");
            sb.AppendLine($"# held_cooling_water_flow_percent,{sweepBaselineInputs.coolingWaterFlow:0.####}");
            sb.AppendLine($"# held_cooling_water_temperature_C,{sweepBaselineInputs.coolingWaterTemperature:0.####}");
            sb.AppendLine($"# held_separator_temperature_C,{sweepBaselineInputs.separatorTemperature:0.####}");
            sb.AppendLine($"# held_recycle_percent,{sweepBaselineInputs.recycleRatio:0.####}");
            sb.AppendLine($"# held_reflux_ratio,{sweepBaselineInputs.refluxRatio:0.####}");
            sb.AppendLine($"# held_distillation_reboiler_temperature_C,{sweepBaselineInputs.distillationReboilerTemp:0.####}");
            sb.AppendLine("#");
            sb.AppendLine($"{GraphExportUtil.Csv(Vars[currentVar].Label)} ({unit}),{GraphExportUtil.Csv(rm.Label)} ({rm.Unit})");
            foreach (SweepPoint p in sweepPoints)
                sb.AppendLine($"{p.X:0.####},{p.Y:0.####}");

            string csvPath = GraphExportUtil.WriteText(baseName, "csv", sb.ToString());
            StartCoroutine(GraphExportUtil.CaptureRegionPng(ownerCanvas, selfRect, baseName, pngPath =>
            {
                string msg = pngPath != null
                    ? $"Exported {baseName}.csv + .png to {GraphExportUtil.ExportDirectory}"
                    : (csvPath != null
                        ? $"Exported {baseName}.csv (PNG failed) to {GraphExportUtil.ExportDirectory}"
                        : "Export failed — see console.");
                GraphExportUtil.ShowToast(ownerCanvas, labelFont, msg);
            }, exportButton != null ? exportButton.gameObject : null));
            return;
        }

        if (samples.Count == 0 && changePoints.Count == 0)
        {
            GraphExportUtil.ShowToast(ownerCanvas, labelFont, "OFAT timeline: nothing recorded yet.");
            return;
        }

        string timelineName = GraphExportUtil.Sanitize($"OFAT_timeline_{rm.Label}_{stamp}");
        var timelineCsv = new StringBuilder();
        timelineCsv.AppendLine($"# OFAT timeline — {rm.Label} over time");
        timelineCsv.AppendLine($"# exported,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        timelineCsv.AppendLine($"# response,{rm.Label} ({rm.Unit})");
        timelineCsv.AppendLine($"# sample_interval_s,{SampleInterval}");
        timelineCsv.AppendLine($"# page_seconds,{PageSeconds}");
        timelineCsv.AppendLine("#");
        timelineCsv.AppendLine("# [variable epochs] time_s,active_variable,variable_value,variable_unit");
        foreach (Epoch e in epochs)
            timelineCsv.AppendLine($"E,{e.T.ToString("0.##")},{Vars[e.Var].Label},{(e.Var == Variable.Free ? "" : e.VarValue.ToString("0.####"))},{VarUnit(e.Var)}");
        timelineCsv.AppendLine("#");
        timelineCsv.AppendLine($"# [samples] time_s,{rm.Label} ({rm.Unit}),active_variable,variable_value,variable_unit");
        foreach (Sample s in samples)
            timelineCsv.AppendLine($"S,{s.T.ToString("0.##")},{s.Y.ToString("0.####")},{Vars[s.Var].Label},{(s.Var == Variable.Free ? "" : s.VarValue.ToString("0.####"))},{VarUnit(s.Var)}");
        timelineCsv.AppendLine("#");
        timelineCsv.AppendLine("# [module change points] time_s,value,module,parameter,from,to,unit");
        foreach (ChangePoint cp in changePoints)
            timelineCsv.AppendLine($"C,{cp.T.ToString("0.##")},{cp.Y.ToString("0.####")},{GraphExportUtil.Csv(cp.Module)},{GraphExportUtil.Csv(cp.Parameter)},{cp.From.ToString("0.###")},{cp.To.ToString("0.###")},{GraphVisualUtils.GetParameterUnit(cp.Parameter)}");

        string timelinePath = GraphExportUtil.WriteText(timelineName, "csv", timelineCsv.ToString());
        StartCoroutine(GraphExportUtil.CaptureRegionPng(ownerCanvas, selfRect, timelineName, pngPath =>
        {
            string msg = pngPath != null
                ? $"Exported {timelineName}.csv + .png to {GraphExportUtil.ExportDirectory}"
                : (timelinePath != null
                    ? $"Exported {timelineName}.csv (PNG failed) to {GraphExportUtil.ExportDirectory}"
                    : "Export failed — see console.");
            GraphExportUtil.ShowToast(ownerCanvas, labelFont, msg);
        }, exportButton != null ? exportButton.gameObject : null));
    }

    // ---- misc -------------------------------------------------------

    private void UpdateContext()
    {
        if (currentExperimentMode == ExperimentMode.AutoSweep)
        {
            if (currentVar == Variable.Free)
            {
                contextText.text = "Select a reactor parameter to calculate a model sensitivity sweep.";
                return;
            }

            contextText.text =
                $"Auto sweep: vary {Vars[currentVar].Label} only. The curve is recalculated from the current live operating inputs using the same process model; the live plant is not changed.";
            return;
        }

        string varLine = currentVar == Variable.Free
            ? "No variable selected — all reactor sliders are free. Pick one to start a controlled run."
            : $"Varying {Vars[currentVar].Label} only — the other four reactor sliders are locked.";
        contextText.text = $"{varLine}  ·  One page = {PageSeconds:0} s";
    }

    private void RecolorVarButtons()
    {
        for (int i = 0; i < varButtons.Length; i++)
        {
            if (varButtons[i] == null) continue;
            bool active = VariableOrder[i] == currentVar;
            varButtons[i].interactable = !(currentExperimentMode == ExperimentMode.AutoSweep && VariableOrder[i] == Variable.Free);
            varButtons[i].Skin()?.SetActive(active);
            if (varDots[i] != null) varDots[i].color = active ? Color.white : Vars[VariableOrder[i]].Color;
        }
    }

    private void RecolorRespButtons()
    {
        for (int i = 0; i < respButtons.Length; i++)
            respButtons[i].Skin()?.SetActive((int)currentResp == i);
    }

    private void RecolorModeButtons()
    {
        for (int i = 0; i < modeButtons.Length; i++)
            modeButtons[i].Skin()?.SetActive((int)currentMode == i);
    }

    private void RecolorExperimentModeButtons()
    {
        if (experimentModeButtons == null) return;
        for (int i = 0; i < experimentModeButtons.Length; i++)
            experimentModeButtons[i].Skin()?.SetActive((int)currentExperimentMode == i);
    }

    private static string FormatClock(float seconds)
    {
        int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
        return $"{total / 60:00}:{total % 60:00}";
    }

    private static string FormatNum(float v)
    {
        float a = Mathf.Abs(v);
        if (a >= 100f) return v.ToString("0");
        if (a >= 10f) return v.ToString("0.#");
        return v.ToString("0.##");
    }

    private void BuildHoverDot()
    {
        RectTransform dot = UITheme.NewRect("Hover Dot", plotArea);
        dot.anchorMin = dot.anchorMax = new Vector2(0.5f, 0.5f);
        dot.pivot = new Vector2(0.5f, 0.5f);
        dot.sizeDelta = new Vector2(16f, 16f);
        Image halo = UITheme.Dot("Halo", dot, 28f, UITheme.WithAlpha(UITheme.Accent, 0.2f));
        UITheme.Center(halo.rectTransform, 28f, 28f);
        Image ring = UITheme.Dot("Ring", dot, 14f, Color.white);
        UITheme.Center(ring.rectTransform, 14f, 14f);
        Image core = UITheme.Dot("Core", dot, 9f, UITheme.Ink);
        UITheme.Center(core.rectTransform, 9f, 9f);
        hoverDot = dot;
        hoverDot.gameObject.SetActive(false);
    }
}
