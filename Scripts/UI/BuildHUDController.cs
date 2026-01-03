using Godot;
using System;

public partial class BuildHUDController : Control
{
    [Export] public NodePath SwingSystemPath;

    private SwingSystem _swingSystem;
    private Control _surveyPanel;
    private Button _surveyDoneBtn;
    private Button _surveyConfirmBtn;
    private OptionButton _terrainPicker;
    private Button _raiseBtn;
    private Button _lowerBtn;
    private HBoxContainer _fillPanel;
    private SpinBox _fillSlider;
    private HBoxContainer _smoothingPanel;
    private SpinBox _smoothingSlider;

    public override void _Ready()
    {
        _swingSystem = GetNodeOrNull<SwingSystem>(SwingSystemPath);
        if (_swingSystem == null) _swingSystem = GetTree().CurrentScene.FindChild("SwingSystem", true, false) as SwingSystem;

        _surveyPanel = this; // Assuming this script is on the BuildHUD root

        _surveyDoneBtn = new Button();
        _surveyDoneBtn.Text = "FINISH SHAPE";
        _surveyDoneBtn.CustomMinimumSize = new Vector2(200, 60);
        _surveyDoneBtn.FocusMode = Control.FocusModeEnum.None;
        AddChild(_surveyDoneBtn);
        _surveyDoneBtn.Pressed += OnSurveyDonePressed;

        _terrainPicker = new OptionButton();
        _terrainPicker.AddItem("Fairway");
        _terrainPicker.AddItem("Rough");
        _terrainPicker.AddItem("Deep Rough");
        _terrainPicker.AddItem("Green");
        _terrainPicker.AddItem("Sand");
        _terrainPicker.AddItem("Water");
        _terrainPicker.Visible = false;
        _terrainPicker.CustomMinimumSize = new Vector2(200, 40);
        AddChild(_terrainPicker);
        _terrainPicker.ItemSelected += (idx) => UpdatePreview();

        _surveyConfirmBtn = new Button();
        _surveyConfirmBtn.Text = "BAKE TERRAIN";
        _surveyConfirmBtn.Visible = false;
        _surveyConfirmBtn.Modulate = Colors.Green;
        _surveyConfirmBtn.CustomMinimumSize = new Vector2(200, 60);
        AddChild(_surveyConfirmBtn);
        _surveyConfirmBtn.Pressed += OnConfirmPressed;

        _raiseBtn = ToolButton("RAISE [↑]", () => _swingSystem?.BuildManager?.ModifyElevation(0.5f));
        _lowerBtn = ToolButton("LOWER [↓]", () => _swingSystem?.BuildManager?.ModifyElevation(-0.5f));

        _fillPanel = new HBoxContainer();
        _fillPanel.Visible = false;
        _fillPanel.Alignment = BoxContainer.AlignmentMode.Center;
        AddChild(_fillPanel);

        _fillSlider = new SpinBox();
        _fillSlider.MinValue = 0;
        _fillSlider.MaxValue = 100;
        _fillSlider.Value = 80;
        _fillSlider.ValueChanged += (val) => _swingSystem?.BuildManager?.SetFillPercentage((float)val);
        _fillPanel.AddChild(new Label { Text = "Fill %: " });
        _fillPanel.AddChild(_fillSlider);

        _smoothingPanel = new HBoxContainer();
        _smoothingPanel.Visible = false;
        _smoothingPanel.Alignment = BoxContainer.AlignmentMode.Center;
        AddChild(_smoothingPanel);

        _smoothingSlider = new SpinBox();
        _smoothingSlider.MinValue = 0;
        _smoothingSlider.MaxValue = 4;
        _smoothingSlider.Value = 0;
        _smoothingSlider.ValueChanged += (val) => { if (_swingSystem?.BuildManager != null) _swingSystem.BuildManager.SmoothingIterations = (int)val; };
        _smoothingPanel.AddChild(new Label { Text = "Smoothing: " });
        _smoothingPanel.AddChild(_smoothingSlider);

        if (_swingSystem?.BuildManager != null)
        {
            _swingSystem.BuildManager.Connect(BuildManager.SignalName.SurveyUpdated, new Callable(this, MethodName.UpdateSurveyButton));
        }

        ResetUI();
    }

    private Button ToolButton(string text, Action action)
    {
        var btn = new Button { Text = text, Visible = false, CustomMinimumSize = new Vector2(200, 40), FocusMode = FocusModeEnum.None };
        btn.Pressed += action;
        AddChild(btn);
        return btn;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationVisibilityChanged)
        {
            if (Visible) ResetUI();
        }
    }

    public void ResetUI()
    {
        UpdateSurveyButton(_swingSystem?.BuildManager?.PointCount ?? 0);
        _terrainPicker.Hide();
        _surveyConfirmBtn.Hide();
        _raiseBtn.Hide();
        _lowerBtn.Hide();
        _fillPanel.Hide();
        _smoothingPanel.Hide();
    }

    private void UpdateSurveyButton(int pointCount)
    {
        if (_swingSystem?.BuildManager == null) return;

        _surveyDoneBtn.Show();
        _surveyDoneBtn.Modulate = Colors.White;

        bool isPicking = _swingSystem.BuildManager.IsPickingTerrain;

        if (isPicking)
        {
            _surveyDoneBtn.Text = "RESET SHAPE";
            _surveyDoneBtn.Modulate = Colors.Salmon;
            _surveyDoneBtn.Disabled = false;
        }
        else if (pointCount < 3)
        {
            _surveyDoneBtn.Text = "PLACE MORE NODES!";
            _surveyDoneBtn.Modulate = new Color(1, 1, 1, 0.5f);
            _surveyDoneBtn.Disabled = true;
        }
        else
        {
            _surveyDoneBtn.Text = "FINISH SHAPE";
            _surveyDoneBtn.Modulate = Colors.White;
            _surveyDoneBtn.Disabled = false;
        }
    }

    private void OnSurveyDonePressed()
    {
        if (_swingSystem?.BuildManager == null) return;

        if (_swingSystem.BuildManager.IsPickingTerrain)
        {
            // This is now a RESET button in this state
            _swingSystem.BuildManager.ClearSurvey();
            ResetUI();
            return;
        }

        // Otherwise it's the FINISH SHAPE button
        _terrainPicker.Show();
        _surveyConfirmBtn.Show();
        _raiseBtn.Show();
        _lowerBtn.Show();
        _smoothingPanel.Show();
        UpdatePreview();
        UpdateSurveyButton(_swingSystem.BuildManager.PointCount);
    }

    private void UpdatePreview()
    {
        if (_swingSystem?.BuildManager == null) return;
        int type = _terrainPicker.Selected;
        _swingSystem.BuildManager.SetPreviewTerrain(type);

        bool isHole = _swingSystem.BuildManager.CurrentElevation < 0;
        _fillPanel.Visible = isHole && (type == 4 || type == 5);
        _smoothingPanel.Show();

        // Reset smoothing slider if manager was reset
        _smoothingSlider.Value = _swingSystem.BuildManager.SmoothingIterations;
    }

    private void OnConfirmPressed()
    {
        if (_swingSystem?.BuildManager == null) return;
        _swingSystem.BuildManager.BakeTerrain(_terrainPicker.Selected);
        _swingSystem.ExitBuildMode();
        ResetUI();
    }
}
