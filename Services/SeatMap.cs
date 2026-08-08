namespace AllFlight.Services;

public class SeatElement
{
    public string Type { get; set; } = "";      // seat, exit_row, lavatory, galley, bassinet, empty
    public string? Designator { get; set; }      // e.g. "1A"
    public decimal? Price { get; set; }           // null = free or not selectable
    public string? Currency { get; set; }
    public bool IsAvailable => Type == "seat" && Designator is not null;
}

public class SeatRow
{
    public List<List<SeatElement>> Sections { get; set; } = new(); // left, middle, right
}

public class SeatMapCabin
{
    public List<SeatRow> Rows { get; set; } = new();
}