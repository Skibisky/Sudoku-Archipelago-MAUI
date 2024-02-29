using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net.Enums;

namespace Sudoku_Archipelago_MAUI;

public partial class MainPage : ContentPage
{
    int count = 0;

    public MainPage()
    {
        InitializeComponent();

    }



    private async void OnConnectClicked(object sender, EventArgs e)
    {

        try {
            ConnectButton.IsEnabled = false;

            var serverUri = ServerAddressEntry.Text;
            var pName = PlayerNameEntry.Text;

            if (string.IsNullOrWhiteSpace(serverUri) || string.IsNullOrWhiteSpace(pName)) {
                await DisplayAlert("Error", "You left it blank", "OK");
            }
            else {

                var session = ArchipelagoSessionFactory.CreateSession(serverUri);
                var result = session.TryConnectAndLogin("", pName, ItemsHandlingFlags.NoItems,
                    tags: new[] { "BK_Sudoku", "TextOnly" }, requestSlotData: true);

                if (result.Successful) {
                    await SecureStorage.Default.SetAsync("serveruri", serverUri);
                    await SecureStorage.Default.SetAsync("playername", pName);

                    var hints = 48;
                    if (difficultyPicker.SelectedIndex == 1) {
                        hints = 35;
                        await SecureStorage.Default.SetAsync("difficulty", "Medium");
                    }
                    else if (difficultyPicker.SelectedIndex == 2) {
                        hints = 24;
                        await SecureStorage.Default.SetAsync("difficulty", "Hard");
                    }
                    else {
                        await SecureStorage.Default.SetAsync("difficulty", "Easy");
                    }

                    await Navigation.PushAsync(new SudokuPage(session, DeathlinkCheck.IsChecked, hints));
                }
                else {
                    string failures = "";
                    if (result is LoginFailure f) {
                        failures = string.Join("\n", f.Errors);
                    }

                    await DisplayAlert("Error", $"Connect to {serverUri} as {pName} failed:\n" + failures, "OK");
                }
            }
        }
        finally {
            ConnectButton.IsEnabled = true;
        }
    }

    private async void ContentPage_Appearing(object sender, EventArgs e)
    {

        var servuri = await SecureStorage.Default.GetAsync("serveruri");
        if (!string.IsNullOrWhiteSpace(servuri)) {
            ServerAddressEntry.Text = servuri;
        }

        var pname = await SecureStorage.Default.GetAsync("playername");
        if (!string.IsNullOrWhiteSpace(pname)) {
            PlayerNameEntry.Text = pname;
        }

        var diff = await SecureStorage.Default.GetAsync("difficulty");
        if (!string.IsNullOrWhiteSpace(diff)) {

            if (diff == "Medium")
                difficultyPicker.SelectedIndex = 1;
            else if (diff == "Hard")
                difficultyPicker.SelectedIndex = 2;
        }


    }

    private void ServerAddressEntry_Completed(object sender, EventArgs e)
    {
        PlayerNameEntry.Focus();
    }
}

