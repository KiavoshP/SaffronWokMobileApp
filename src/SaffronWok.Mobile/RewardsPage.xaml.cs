namespace SaffronWok.Mobile;

public sealed partial class RewardsPage : ContentPage
{
    public RewardsPage()
    {
        InitializeComponent();
        if (!DemoData.Enabled)
            return;

        pendingRewards.IsVisible = false;
        sampleBalance.IsVisible = samplePerks.IsVisible = true;
        balance.Text = $"{DemoData.RewardPoints} pts";
        goal.Text = $"{DemoData.RewardPoints} / {DemoData.RewardGoal} pts toward a Free Zesty Jade Combo";
        progress.Progress = (double)DemoData.RewardPoints / DemoData.RewardGoal;
        BindableLayout.SetItemsSource(perkList, DemoData.Perks);
    }
}
