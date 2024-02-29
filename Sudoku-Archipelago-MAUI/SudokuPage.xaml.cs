using Archipelago.MultiClient.Net.BounceFeatures.DeathLink;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.Helpers;
using SudokuSpice.RuleBased;
using Archipelago.MultiClient.Net.Enums;
using System.Globalization;
using System.Numerics;

namespace Sudoku_Archipelago_MAUI;

public partial class SudokuPage : ContentPage
{
    const string PreviouslyHintedLocations = "BkHinted";
    static readonly Random Random = new();

    SudokuCell[,] cells = new SudokuCell[9, 9];

    private ArchipelagoSession session;
    private DeathLinkService deathLinkService;

    private bool DeathlinkOn;
    private int numberOfHints;

    private Timer connectionChecker;

    public SudokuPage(ArchipelagoSession sess, bool enableDeathlink, int numClues)    {
        InitializeComponent();

        session = sess;
        session.MessageLog.OnMessageReceived += MessageLog_OnMessageReceived;
        session.Socket.ErrorReceived += Socket_ErrorReceived;
        session.Socket.SocketClosed += Socket_SocketClosed;

        DeathlinkOn = enableDeathlink;
        numberOfHints = numClues;

        deathLinkService = session.CreateDeathLinkService();
        deathLinkService.OnDeathLinkReceived += deathLinkReceivedHandler;

        if (DeathlinkOn)
            deathLinkService.EnableDeathLink();
        else
            deathLinkService.DisableDeathLink();

        connectionChecker = new Timer(checkConnection, null, TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
        ReconnectButton.IsVisible = false;

        makeGrid();
    }

    private async void checkConnection(object state)
    {
        if (session.Socket.Connected)
            return;

        ReconnectButton.Dispatcher.Dispatch(() =>
        {
            ReconnectButton.IsVisible = true;
        });
        connectionChecker.Change(Timeout.InfiniteTimeSpan, TimeSpan.FromSeconds(60));
    }

    private async void ReconnectButton_Clicked(object sender, EventArgs e)
    {
        ReconnectButton.IsEnabled = false;
        try
        {
            var serverUri = await SecureStorage.Default.GetAsync("serveruri");
            var pName = await SecureStorage.Default.GetAsync("playername");

            var result = session.TryConnectAndLogin("", pName, ItemsHandlingFlags.NoItems,
                tags: new[] { "BK_Sudoku", "TextOnly" }, requestSlotData: true);

            if (result.Successful)
            {
                ReconnectButton.IsVisible = false;
                connectionChecker.Change(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
            }
            else
            {
                string failures = "";
                if (result is LoginFailure lf)
                {
                    failures = string.Join("\n", lf.Errors);
                }
                await DisplayAlert("Error", $"Connect to {serverUri} as {pName} failed:\n" + failures, "OK");
            }
        }
        finally
        {
            ReconnectButton.IsEnabled = true;
        }

    }

    private void Socket_SocketClosed(string reason)
    {
        // ShowMessage("Socket Closed", reason, "cancel", Colors.Black)
        DisplayAlert("Socket Closed", reason, "ok")
            .ContinueWith(a => Navigation.PopAsync());
    }

    private void Socket_ErrorReceived(Exception e, string message)
    {
        DisplayAlert("Error Received", message + "\n" + e, "ok")
            .ContinueWith(a => Navigation.PopAsync());
    }

    private void makeGrid()
    {

        Grid[,] subgrids = new Grid[3, 3];

        foreach (var b in sudokuGrid.Children.OfType<Border>()) {
            var xpos = sudokuGrid.GetColumn(b);
            var ypos = sudokuGrid.GetRow(b);

            if (b.Content != null) {
                var sg = (Grid)b.Content;
               subgrids[xpos, ypos] = sg;
            }

        }

        for (int i = 0; i < 3; i++) {
            for (int j = 0; j < 3; j++) {
                if (subgrids[i, j] == null) {
                    subgrids[i, j] = new Grid()
                    {
                        ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), },
                        RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Star), }
                    };
                    var b = new Border()
                    {
                        Content = subgrids[i, j]
                    };
                    sudokuGrid.Add(b, i, j);
                }
            }
        }

        int found = 0;

        for (int i = 0; i < 9; i++) {
            for (int j = 0; j < 9; j++) {

                bool isSquare(IView v)
                {
                    if (v is SudokuCell c) {
                        if (c.gX == i && c.gY == j) {
                            found++;
                            return true;
                        }
                    }

                    if (v.Parent is Grid sub) {
                        int tx = sub.GetColumn(v);
                        int ty = sub.GetRow(v);

                        if (sub.Parent is Border b) {

                            tx += sudokuGrid.GetColumn(b) * 3;
                            ty += sudokuGrid.GetRow(b) * 3;
                        }
                        else {
                            for (int x = 0; x < 3; x++) {
                                for (int y = 0; y < 3; y++) {
                                    if (subgrids[x, y] == sub) {
                                        tx += x * 3;
                                        ty += y * 3;
                                    }
                                }
                            }
                        }

                        if (tx == i && ty == j) {
                            found++;
                            return true;
                        }
                    }

                    return false;

                }

                var exist = sudokuGrid.Children.OfType<Border>().Select(b => b.Content as Grid).Where(g => g != null).SelectMany(g => g.Children).OfType<SudokuCell>().FirstOrDefault(c => isSquare(c));

                if (exist == null) {
                    cells[i, j] = new SudokuCell()
                    {
                        gX = i,
                        gY = j,
                    };

                    subgrids[i / 3, j / 3].Add(cells[i, j], i % 3, j % 3);
                }
                else {
                    exist.gX = i;
                    exist.gY = j;
                    cells[i, j] = exist;
                }


                // HorizontalTextAlignment="Center" WidthRequest="40" 
                cells[i,j].HorizontalTextAlignment = TextAlignment.Center;
                cells[i,j].WidthRequest = 25;
                cells[i,j].Keyboard = Keyboard.Numeric;

            }
        }

        //DisplayAlert("found", $"like {found} boxes", ":S");

    }

    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        deathLinkService.OnDeathLinkReceived -= deathLinkReceivedHandler;
        session.MessageLog.OnMessageReceived -= MessageLog_OnMessageReceived;
        
        base.OnNavigatedFrom(args);
        deathLinkService.DisableDeathLink();
        deathLinkService = null;
        session = null;


    }

    private void deathLinkReceivedHandler(DeathLink deathLink)
    {
        startNewGame();
        ShowMessage("DeathLink", $"DeathLink recieved from: {deathLink.Source}, reason: {deathLink.Cause}", "X.X", Colors.Red);
    }


    private void LogWriteLine(string text = "", Color color = null )
    {
        color = color ?? Colors.Black;
        LogWriteNoLine(text + "\n", color);
    }

    private void LogWriteNoLine(string text, Color color)
    {
        LogLabel.Dispatcher.DispatchAsync(() =>
        {
            Span sp = new Span();
            sp.Text = text;
            sp.TextColor = color;
            if (LogLabel.FormattedText == null)
                LogLabel.FormattedText = "";

            LogLabel.FormattedText.Spans.Add(sp);
            LogScroll.ScrollToAsync(LogLabel, ScrollToPosition.End, false);
        });
    }

    private async Task ShowMessage(string title, string message, string cancel, Color color)
    {
        LogWriteLine(message, color);
        await DisplayAlert(title, message, cancel);
    }

    private Color ToSystemColor(Archipelago.MultiClient.Net.Models.Color color)
    {
        return new Color(color.R, color.G, color.B);
    }

    private async void MessageLog_OnMessageReceived(LogMessage message)
    {
        switch (message) {
            case HintItemSendLogMessage hintMessage when hintMessage.Sender.Slot == session.ConnectionInfo.Slot:
                foreach (var part in hintMessage.Parts)
                    LogWriteNoLine(part.Text, ToSystemColor(part.Color));
                LogWriteLine();
                break;

            case ItemSendLogMessage itemMessage when itemMessage.Item.Flags == ItemFlags.Advancement
                                                         && itemMessage.Receiver.Slot == session.ConnectionInfo.Slot:
                foreach (var part in itemMessage.Parts)
                    LogWriteNoLine(part.Text, ToSystemColor(part.Color));
                LogWriteLine();
                break;
        }
    }

    private void startNewGame()
    {
        var generator = new StandardPuzzleGenerator();
        var puzzle = generator.Generate(9, numberOfHints, TimeSpan.Zero);

        fillField(puzzle);
        CheckButton.IsEnabled = true;
    }

    void fillField(PuzzleWithPossibleValues puzzle)
    {
        var solver = StandardPuzzles.CreateSolver();
        var solved = solver.Solve(puzzle);

        for (int x = 0; x < 9; x++) {
            for (int y = 0; y < 9; y++) {
                var cell = cells[x, y];
                cell.Value = solved[x, y].Value;

                if (puzzle[x, y].HasValue) {
                    cell.Text = cell.Value.ToString();
                    //cell.TextColor = Colors.Black;
                    cell.IsLocked = true;
                }
                else {
                    cell.Text = "";
                    //cell.TextColor = Colors.;
                    cell.IsLocked = false;
                }
            }
        }
    }

    private async void CheckButton_Clicked(object sender, EventArgs e)
    {
        bool didWin = false;
        try {
            CheckButton.IsEnabled = false;

            bool hasError = false;
            bool isFilled = true;

            foreach (var cell in cells) {
                if (string.IsNullOrEmpty(cell.Text)) {
                    isFilled = false;
                    break;
                }

                if (!string.Equals(cell.Value.ToString(), cell.Text)) {
                    hasError = true;
                }
            }

            if (!isFilled) {
                await ShowMessage("Result", "Not all fields are filled yet", "OK", Colors.Gray);
            }
            else if (hasError) {
                if (deathLinkService != null && DeathlinkOn) {
                    var deathLink = new DeathLink(session.Players.GetPlayerAlias(session.ConnectionInfo.Slot), "Failed to solve a Sudoku");
                    deathLinkService.SendDeathLink(deathLink);
                }

                await ShowMessage("Result", "Wrong inputs", ":(", Colors.Blue);
            }
            else {
                if (session != null && session.Socket.Connected) {
                    CheckButton.IsEnabled = false;
                    didWin = true;
                    
                    var missing = session.Locations.AllMissingLocations;
                    // var alreadyHinted = session.DataStorage[Scope.Slot, PreviouslyHintedLocations].To<long[]>();
                    var alreadyHinted = (await session.DataStorage.GetHintsAsync(session.ConnectionInfo.Slot))
                        ?.Where(h => h.FindingPlayer == session.ConnectionInfo.Slot)?.Select(h => h.LocationId);

                    if (alreadyHinted == null)
                    {
                        await ShowMessage("Failed", "I was unable to find the already hinted locatons.", "Oh", Colors.Red);
                        return;
                    }

                    var availableForHinting = missing.Except(alreadyHinted).ToArray();

                    if (availableForHinting.Any()) {
                        var locationId = availableForHinting[Random.Next(0, availableForHinting.Length)];
                        await session.Locations.ScoutLocationsAsync(true, locationId);
                        
                        await ShowMessage("Result", "Correct, unlocked 1 hint", "Yay", Colors.Blue);
                    }
                    else {
                        await ShowMessage("Result", "Correct, no remaining locations left to hint for", "Sure", Colors.Blue);
                    }
                }
                else {
                    await ShowMessage("Result", "Correct, no hints are unlocked as you are not connected", "Oof", Colors.Blue);
                    ReconnectButton.IsVisible = true;
                }
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error!", "Error during submit: \n" + ex, "ok")
                .ContinueWith(a => Navigation.PopAsync());
        }
        finally {
            CheckButton.IsEnabled = !didWin;
        }
    }

    private void NewButton_Clicked(object sender, EventArgs e)
    {
        startNewGame();
    }

    private async void LogoutButton_Clicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }
}