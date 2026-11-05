using System;
namespace TimeOnScreen.Core;
public sealed record EventReminder
{
    public string Id { get; init; } = Guid.NewGuid().ToString();
    public string Title { get; init; } = "";
    public string Time { get; init; } = "23:00";
    public bool Daily { get; init; } = true;
    public DateTime Date { get; init; } = DateTime.Today;
}
