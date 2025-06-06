using Content.Client.UserInterface.Controls;
using Content.Shared._RMC14.CCVar;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client._RMC14.RoundEnd;

[GenerateTypedNameReferences]
public sealed partial class NoEorgPopup : FancyWindow
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private float _remainingTime;
    private bool _initialSkipState;

    public NoEorgPopup()
    {
        IoCManager.InjectDependencies(this);
        RobustXamlLoader.Load(this);

        InitializeUI();
        InitializeEvents();
        ResetTimer();
    }

    private void InitializeUI()
    {
        TitleLabel.Text = Loc.GetString("no-eorg-popup-label");
        MessageLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(Loc.GetString("no-eorg-popup-message")));
        RuleLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(Loc.GetString("no-eorg-popup-rule")));
        RuleTextLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(Loc.GetString("no-eorg-popup-rule-text")));

        _initialSkipState =
            _cfg.GetCVar(RMCCVars.RMCSkipRoundEndNoEorgPopup); // Store the initial CVar value to compare against
        SkipCheckBox.Pressed = _initialSkipState;
        NoEorgCloseButton.Disabled = true;

        UpdateCloseButtonText();
    }

    private void InitializeEvents()
    {
        OnClose += SaveSkipState; // Only change the CVar once the close button is pressed
        NoEorgCloseButton.OnPressed += OnClosePressed;
    }

    private void ResetTimer()
    {
        _remainingTime = _cfg.GetCVar(RMCCVars.RMCRoundEndNoEorgPopupTime); // Set how long to show the popup for
        UpdateCloseButtonText();
    }

    private void SaveSkipState()
    {
        if (SkipCheckBox.Pressed == _initialSkipState)
            return;

        _cfg.SetCVar(RMCCVars.RMCSkipRoundEndNoEorgPopup, SkipCheckBox.Pressed);
        _cfg.SaveToFile();
    }

    private void OnClosePressed(BaseButton.ButtonEventArgs args)
    {
        Close();
    }

    private void UpdateCloseButtonText()
    {
        var isWaiting = _remainingTime > 0f;
        NoEorgCloseButton.Text = isWaiting
            ? Loc.GetString("no-eorg-popup-close-button-wait", ("time", (int)MathF.Ceiling(_remainingTime)))
            : Loc.GetString("no-eorg-popup-close-button");
        NoEorgCloseButton.Disabled = isWaiting;
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (!NoEorgCloseButton.Disabled)
            return;

        _remainingTime = MathF.Max(0f, _remainingTime - args.DeltaSeconds);
        UpdateCloseButtonText();
    }
}
