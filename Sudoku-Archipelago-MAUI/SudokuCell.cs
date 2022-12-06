namespace Sudoku_Archipelago_MAUI
{
    internal class SudokuCell : Entry
    {

        private int value;
        public int Value
        {
            get => value; set
            {
                this.value = value;
                this.Text = value.ToString();
            }
        }

        private bool isLocked;
        public bool IsLocked
        {
            get => isLocked; set
            {
                isLocked = value;
                this.IsEnabled = !value;
            }
        }

        public int gX { get; set; }
        public int gY { get; set; }

        public void Clear()
        {
            this.Text = string.Empty;
            this.IsLocked = false;
        }
    }
}